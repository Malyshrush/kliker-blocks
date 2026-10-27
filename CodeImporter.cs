using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed class CodeImportException(int line, string message) : Exception($"Строка {line}: {message}")
{
    public int LineNumber { get; } = line;
    public string Reason { get; } = message;
}

internal sealed record CodeImportResult(List<BlockNode> Nodes, Guid? Entry, int Commands, string? ScriptName = null);

// Historical command parser retained only for internal regression tests.
internal sealed class CodeImporter
{
    private sealed record SourceLine(int Number, string Text);
    private abstract record Statement(int Line);
    private sealed record Command(int Line, string Name, string Arguments) : Statement(Line);
    private sealed record Assignment(int Line, string Name, string? Index, string Operator, string Value) : Statement(Line);
    private sealed record Conditional(int Line, string Condition, List<Statement> Then, List<Statement> Else) : Statement(Line);
    private sealed record Loop(int Line, string Count, int EndLine, List<Statement> Body) : Statement(Line);
    private sealed record ForLoop(int Line, string Name, string? Initial, string Condition, string Step, int EndLine, List<Statement> Body) : Statement(Line);
    private sealed record WhileLoop(int Line, string Condition, int EndLine, List<Statement> Body) : Statement(Line);
    private sealed record SwitchArm(int Line, string Value, List<Statement> Body);
    private sealed record SwitchStatement(int Line, string Expression, List<SwitchArm> Arms, List<Statement> Default) : Statement(Line);
    private sealed record InputGroupStatement(int Line, string MaxMs, int EndLine, List<Statement> Body) : Statement(Line);
    private sealed record Subroutine(int Line, string Name, int EndLine, List<Statement> Body) : Statement(Line);
    private sealed record Placement(BlockNode Node, int Line, int Lane, int Priority, int Sequence);

    private static readonly Regex AssignmentPattern = new(@"^\$([\p{L}_][\p{L}\p{Nd}_]*)(?:\[([^\]]+)\])?\s*(\+=|-=|\*=|/=|=)\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex CallPattern = new(@"^([\p{L}_][\p{L}\p{Nd}_]*)\s*\((.*)\)$", RegexOptions.Compiled);
    private static readonly Regex VariablePattern = new(@"^\$([\p{L}_][\p{L}\p{Nd}_]*)$", RegexOptions.Compiled);
    private static readonly Regex NumberPattern = new(@"^[+-]?\d+(?:[.,]\d+)?$", RegexOptions.Compiled);
    private static readonly Regex ForVariablePattern = new(@"^\$([\p{L}_][\p{L}\p{Nd}_]*)(?:\s*=\s*(.+))?$", RegexOptions.Compiled);
    private static readonly Regex ComparisonPattern = new(@"^\$([\p{L}_][\p{L}\p{Nd}_]*)\s*(>=|<=|==|!=|=|>|<)\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex LogFilePattern = new("^#logfile\\s+\"([^\"]+)\"$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NamePattern = new("^#name\\s+\"([^\"]*)\"$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly List<SourceLine> _lines = [];
    private readonly List<BlockNode> _nodes = [];
    private readonly List<Placement> _placements = [];
    private readonly HashSet<string> _subNames = new(StringComparer.OrdinalIgnoreCase);
    private List<PreprocessedLine> _expandedLines = [];
    private int _index;
    private int _sequence;
    private Point _origin;
    private string? _logFileName;
    private string? _scriptName;

    public static CodeImportResult Convert(string code, Point origin, string? includeDirectory = null)
    {
        var importer = new CodeImporter { _origin = origin };
        try { return importer.ConvertCore(code, includeDirectory ?? AppContext.BaseDirectory); }
        catch (CodeImportException error)
        {
            if (error.LineNumber >= 1 && error.LineNumber <= importer._expandedLines.Count &&
                importer._expandedLines[error.LineNumber - 1] is { SourceFile: not null } included)
                throw new CodeImportException(included.RootLine,
                    $"Во включённом файле «{Path.GetFileName(included.SourceFile)}», строка {included.SourceLine}: {error.Reason}");
            throw;
        }
    }

    private CodeImportResult ConvertCore(string code, string includeDirectory)
    {
        _expandedLines = CodePreprocessor.Process(code, includeDirectory);
        for (var i = 0; i < _expandedLines.Count; i++)
        {
            var text = StripComment(_expandedLines[i].Text).Trim().TrimEnd(';').Trim();
            if (text.Length == 0) continue;
            if (text.StartsWith("#name", StringComparison.OrdinalIgnoreCase))
            {
                var match = NamePattern.Match(text);
                if (!match.Success || !ScriptNameTools.IsValid(match.Groups[1].Value))
                    throw new CodeImportException(i + 1, "#name требует непустое имя в кавычках, до 100 символов.");
                if (_scriptName is not null)
                    throw new CodeImportException(i + 1, "#name указан повторно.");
                _scriptName = match.Groups[1].Value;
                continue;
            }
            if (text.StartsWith("#logfile", StringComparison.OrdinalIgnoreCase))
            {
                var match = LogFilePattern.Match(text);
                if (!match.Success || !ScriptLogWriter.IsValidName(match.Groups[1].Value))
                    throw new CodeImportException(i + 1, "#logfile требует имя файла в кавычках без папки.");
                if (_logFileName is not null)
                    throw new CodeImportException(i + 1, "#logfile указан повторно.");
                _logFileName = match.Groups[1].Value;
                continue;
            }
            _lines.Add(new SourceLine(i + 1, text));
        }
        if (_lines.Count == 0) throw new CodeImportException(1, "Вставьте команды для преобразования.");
        var statements = ParseSequence([], true);
        var subroutines = statements.OfType<Subroutine>().ToList();
        foreach (var sub in subroutines)
            if (!_subNames.Add(sub.Name)) throw new CodeImportException(sub.Line, $"Подпрограмма «{sub.Name}» объявлена повторно.");
        var entry = CompileSequence(statements.Where(item => item is not Subroutine).ToList(), null, 0);
        if (_logFileName is not null)
        {
            var fileLog = Add("LogFile", 0, 0);
            fileLog.Values["Path"] = _logFileName;
            fileLog.Links["Далее"] = entry;
            entry = fileLog.Id;
        }
        var subLane = 4;
        foreach (var sub in subroutines)
        {
            var end = Add("SubReturn", sub.EndLine, subLane + 1);
            var body = CompileSequence(sub.Body, end.Id, subLane + 1);
            var start = Add("SubStart", sub.Line, subLane);
            start.Values["Name"] = sub.Name;
            start.Links["Тело"] = body ?? end.Id;
            subLane += 3;
        }
        if (_nodes.Count > 600) throw new CodeImportException(1, "Слишком много блоков (максимум 600). Разделите код на части.");
        Layout();
        return new CodeImportResult(_nodes, entry, _lines.Count, _scriptName);
    }

    private List<Statement> ParseSequence(HashSet<string> terminators, bool topLevel = false)
    {
        var result = new List<Statement>();
        while (_index < _lines.Count)
        {
            var line = _lines[_index];
            var keyword = line.Text.ToLowerInvariant();
            if (terminators.Contains(keyword) ||
                (terminators.Contains("case") && TryHeader(line.Text, "case", out _))) break;
            if (keyword is "else" or "end_if" or "end_cyc" or "end_sub" or "end_input" or "default" or "end_switch" ||
                TryHeader(line.Text, "case", out _))
                throw new CodeImportException(line.Number, $"Неожиданный оператор «{line.Text}».");
            if (TryHeader(line.Text, "if", out var condition))
            {
                _index++;
                var then = ParseSequence(["else", "end_if"]);
                var otherwise = new List<Statement>();
                if (_index < _lines.Count && _lines[_index].Text.Equals("else", StringComparison.OrdinalIgnoreCase))
                {
                    _index++;
                    otherwise = ParseSequence(["end_if"]);
                }
                RequireTerminator("end_if", line.Number);
                result.Add(new Conditional(line.Number, condition, then, otherwise));
                continue;
            }
            if (TryHeader(line.Text, "repeat", out var repetitions))
            {
                var count = IntegerOrVariable(repetitions, line.Number);
                _index++;
                var body = ParseSequence(["end_cyc"]);
                var endLine = RequireTerminator("end_cyc", line.Number);
                result.Add(new Loop(line.Number, count, endLine, body));
                continue;
            }
            if (TryHeader(line.Text, "for", out var forArguments))
            {
                var parts = SplitArguments(forArguments, line.Number);
                if (parts.Count is < 2 or > 3) throw new CodeImportException(line.Number,
                    "Ожидается for($i[=начало], условие[, шаг]).");
                var variable = ForVariablePattern.Match(parts[0]);
                if (!variable.Success) throw new CodeImportException(line.Number,
                    "Первый аргумент for должен быть $имя или $имя=начало.");
                _index++;
                var body = ParseSequence(["end_cyc"]);
                var endLine = RequireTerminator("end_cyc", line.Number);
                result.Add(new ForLoop(line.Number, variable.Groups[1].Value,
                    variable.Groups[2].Success ? variable.Groups[2].Value : null,
                    parts[1], parts.Count == 3 ? parts[2] : "1", endLine, body));
                continue;
            }
            if (TryHeader(line.Text, "while", out var whileCondition))
            {
                _index++;
                var body = ParseSequence(["end_cyc"]);
                var endLine = RequireTerminator("end_cyc", line.Number);
                result.Add(new WhileLoop(line.Number, whileCondition, endLine, body));
                continue;
            }
            if (TryHeader(line.Text, "switch", out var switchExpression))
            {
                if (string.IsNullOrWhiteSpace(switchExpression)) throw new CodeImportException(line.Number, "switch требует выражение.");
                _index++;
                var arms = new List<SwitchArm>();
                var otherwise = new List<Statement>();
                while (_index < _lines.Count && TryHeader(_lines[_index].Text, "case", out var caseValue))
                {
                    var caseLine = _lines[_index].Number;
                    if (string.IsNullOrWhiteSpace(caseValue)) throw new CodeImportException(caseLine, "case требует значение.");
                    _index++;
                    arms.Add(new SwitchArm(caseLine, caseValue, ParseSequence(["case", "default", "end_switch"])));
                }
                if (_index < _lines.Count && _lines[_index].Text.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    _index++;
                    otherwise = ParseSequence(["end_switch"]);
                }
                RequireTerminator("end_switch", line.Number);
                if (arms.Count == 0) throw new CodeImportException(line.Number, "switch должен содержать хотя бы один case.");
                result.Add(new SwitchStatement(line.Number, switchExpression, arms, otherwise));
                continue;
            }
            if (TryHeader(line.Text, "input_group", out var maxMs))
            {
                var limit = string.IsNullOrWhiteSpace(maxMs) ? "10000" : IntegerOrVariable(maxMs, line.Number);
                _index++;
                var body = ParseSequence(["end_input"]);
                var endLine = RequireTerminator("end_input", line.Number);
                result.Add(new InputGroupStatement(line.Number, limit, endLine, body));
                continue;
            }
            if (TryHeader(line.Text, "sub", out var subName))
            {
                if (!topLevel) throw new CodeImportException(line.Number, "Подпрограмма должна быть объявлена на верхнем уровне кода.");
                var name = Unquote(subName.Trim());
                if (!IsName(name)) throw new CodeImportException(line.Number, "Имя подпрограммы должно содержать буквы, цифры и _.");
                _index++;
                var body = ParseSequence(["end_sub"]);
                var endLine = RequireTerminator("end_sub", line.Number);
                result.Add(new Subroutine(line.Number, name, endLine, body));
                continue;
            }
            var assignment = AssignmentPattern.Match(line.Text);
            if (assignment.Success)
                result.Add(new Assignment(line.Number, assignment.Groups[1].Value,
                    assignment.Groups[2].Success ? assignment.Groups[2].Value.Trim() : null,
                    assignment.Groups[3].Value, assignment.Groups[4].Value.Trim()));
            else
            {
                var command = CallPattern.Match(line.Text);
                if (!command.Success) throw new CodeImportException(line.Number, $"Неподдерживаемый синтаксис: {line.Text}");
                result.Add(new Command(line.Number, command.Groups[1].Value, command.Groups[2].Value));
            }
            _index++;
        }
        return result;
    }

    private int RequireTerminator(string terminator, int openingLine)
    {
        if (_index >= _lines.Count || !_lines[_index].Text.Equals(terminator, StringComparison.OrdinalIgnoreCase))
            throw new CodeImportException(openingLine, $"Не найден {terminator}.");
        return _lines[_index++].Number;
    }

    private Guid? CompileSequence(List<Statement> statements, Guid? next, int lane)
    {
        for (var i = statements.Count - 1; i >= 0; i--)
            next = Compile(statements[i], next, lane);
        return next;
    }

    private Guid? Compile(Statement statement, Guid? next, int lane)
    {
        switch (statement)
        {
            case Command command: return CompileCommand(command, next, lane);
            case Assignment assignment: return CompileAssignment(assignment, next, lane);
            case Conditional conditional:
            {
                var condition = CompileCondition(conditional.Condition, conditional.Line, lane);
                condition.Links["Да"] = CompileSequence(conditional.Then, next, lane + 1) ?? next;
                condition.Links["Нет"] = CompileSequence(conditional.Else, next, lane + 2) ?? next;
                return condition.Id;
            }
            case Loop loop:
            {
                var end = Add("BranchEnd", loop.EndLine, lane + 1, 1);
                var body = CompileSequence(loop.Body, end.Id, lane + 1);
                var repeat = Add("Repeat", loop.Line, lane);
                repeat.Values["Times"] = loop.Count;
                repeat.Links["Тело"] = body ?? end.Id;
                repeat.Links["Далее"] = next;
                return repeat.Id;
            }
            case ForLoop loop:
            {
                var end = Add("BranchEnd", loop.EndLine, lane + 1, 1);
                var increment = Add("MathVariable", loop.EndLine, lane + 1);
                increment.Values["Name"] = loop.Name;
                increment.Values["Operation"] = "Сложить";
                increment.Values["Operand"] = ScalarOrExpression(loop.Step, loop.Line);
                increment.Links["Далее"] = end.Id;
                var body = CompileSequence(loop.Body, increment.Id, lane + 1);
                var test = Add("WhileExpression", loop.Line, lane);
                test.Values["Expression"] = "=" + loop.Condition;
                test.Links["Тело"] = body ?? increment.Id;
                test.Links["Далее"] = next;
                var initialize = Add(loop.Initial is null ? "DefineVariable" : "SetVariable", loop.Line, lane);
                initialize.Values["Name"] = loop.Name;
                initialize.Values["Value"] = loop.Initial is null ? "0" : ScalarOrExpression(loop.Initial, loop.Line);
                initialize.Links["Далее"] = test.Id;
                return initialize.Id;
            }
            case WhileLoop loop:
            {
                var end = Add("BranchEnd", loop.EndLine, lane + 1, 1);
                var body = CompileSequence(loop.Body, end.Id, lane + 1);
                var test = Add("WhileExpression", loop.Line, lane);
                test.Values["Expression"] = "=" + loop.Condition;
                test.Links["Тело"] = body ?? end.Id;
                test.Links["Далее"] = next;
                return test.Id;
            }
            case SwitchStatement select:
            {
                var following = CompileSequence(select.Default, next, lane + 1) ?? next;
                var tempName = "_switch_" + Guid.NewGuid().ToString("N");
                for (var i = select.Arms.Count - 1; i >= 0; i--)
                {
                    var arm = select.Arms[i];
                    var test = Add("IfExpression", arm.Line, lane + i);
                    test.Values["Expression"] = "=$" + tempName + "==(" + arm.Value + ")";
                    test.Links["Да"] = CompileSequence(arm.Body, next, lane + i + 1) ?? next;
                    test.Links["Нет"] = following;
                    following = test.Id;
                }
                var capture = Add("SetVariable", select.Line, lane);
                capture.Values["Name"] = tempName;
                capture.Values["Value"] = ScalarOrExpression(select.Expression, select.Line);
                capture.Links["Далее"] = following;
                return capture.Id;
            }
            case InputGroupStatement group:
            {
                var end = Add("BranchEnd", group.EndLine, lane + 1, 1);
                var body = CompileSequence(group.Body, end.Id, lane + 1);
                var block = Add("InputGroup", group.Line, lane);
                block.Values["MaxMs"] = group.MaxMs;
                block.Links["Тело"] = body ?? end.Id;
                block.Links["Далее"] = next;
                return block.Id;
            }
            case Subroutine: return next;
            default: throw new CodeImportException(statement.Line, "Неизвестная конструкция.");
        }
    }

    private Guid CompileAssignment(Assignment assignment, Guid? next, int lane)
    {
        var name = assignment.Name;
        var expression = assignment.Value;
        var operation = assignment.Operator;
        if (assignment.Index is null && operation == "=" &&
            TryCompileScreenAssignment(assignment, next, lane, out var screenAssignment)) return screenAssignment;
        if (assignment.Index is null && operation == "=" &&
            TryCompileDialogAssignment(assignment, next, lane, out var dialogAssignment)) return dialogAssignment;
        if (assignment.Index is null && operation == "=" &&
            TryCompileDataAssignment(assignment, next, lane, out var dataAssignment)) return dataAssignment;
        if (assignment.Index is not null)
        {
            if (operation != "=") throw new CodeImportException(assignment.Line, "Для массива пока поддерживается только присваивание =.");
            var array = Add("ArraySet", assignment.Line, lane);
            array.Values["Name"] = name;
            array.Values["Index"] = IntegerOrVariable(assignment.Index, assignment.Line);
            array.Values["Value"] = ScalarOrExpression(expression, assignment.Line);
            array.Links["Далее"] = next;
            return array.Id;
        }
        if (operation == "=")
        {
            var arithmetic = Regex.Match(expression,
                @"^\$" + Regex.Escape(name) + @"\s*([+\-*/])\s*(.+)$", RegexOptions.IgnoreCase);
            if (arithmetic.Success)
            {
                operation = arithmetic.Groups[1].Value + "=";
                expression = arithmetic.Groups[2].Value;
            }
        }
        if (operation != "=")
        {
            var math = Add("MathVariable", assignment.Line, lane);
            math.Values["Name"] = name;
            math.Values["Operation"] = operation switch
            {
                "+=" => "Сложить", "-=" => "Вычесть", "*=" => "Умножить", "/=" => "Разделить",
                _ => throw new CodeImportException(assignment.Line, $"Неподдерживаемая операция {operation}")
            };
            math.Values["Operand"] = ScalarOrExpression(expression, assignment.Line);
            math.Links["Далее"] = next;
            return math.Id;
        }
        var set = Add("SetVariable", assignment.Line, lane);
        set.Values["Name"] = name;
        set.Values["Value"] = ScalarOrExpression(expression, assignment.Line);
        set.Links["Далее"] = next;
        return set.Id;
    }

    private bool TryCompileScreenAssignment(Assignment assignment, Guid? next, int lane, out Guid result)
    {
        result = Guid.Empty;
        var call = CallPattern.Match(assignment.Value);
        if (!call.Success) return false;
        var function = call.Groups[1].Value.ToLowerInvariant();
        if (function is not ("pxl" or "pxlcount" or "pxlxor" or "pxlcrc")) return false;
        var args = SplitArguments(call.Groups[2].Value, assignment.Line);
        var expected = function switch { "pxl" => 2, "pxlcount" => 5, _ => 4 };
        if (args.Count != expected) throw new CodeImportException(assignment.Line,
            $"{function} ожидает {expected} аргументов.");
        var kind = function switch
        {
            "pxl" => "BufferPixel", "pxlcount" => "BufferCount", _ => "BufferHash"
        };
        var node = Add(kind, assignment.Line, lane);
        if (function == "pxl")
        {
            node.Values["PixelX"] = IntegerOrVariable(args[0], assignment.Line);
            node.Values["PixelY"] = IntegerOrVariable(args[1], assignment.Line);
        }
        else
        {
            node.Values["Left"] = IntegerOrVariable(args[0], assignment.Line);
            node.Values["Top"] = IntegerOrVariable(args[1], assignment.Line);
            node.Values["Right"] = IntegerOrVariable(args[2], assignment.Line);
            node.Values["Bottom"] = IntegerOrVariable(args[3], assignment.Line);
            if (function == "pxlcount") node.Values["Color"] = ScalarOrExpression(args[4], assignment.Line);
            else node.Values["Algorithm"] = function == "pxlcrc" ? "CRC32" : "XOR";
        }
        node.Values["Result"] = assignment.Name;
        node.Links["Далее"] = next;
        result = node.Id;
        return true;
    }

    private bool TryCompileDataAssignment(Assignment assignment, Guid? next, int lane, out Guid result)
    {
        result = Guid.Empty;
        var call = CallPattern.Match(assignment.Value);
        if (!call.Success) return false;
        var function = call.Groups[1].Value.ToLowerInvariant();
        if (function is not ("fromclip" or "httpget" or "tfread" or "strreadln" or "tfcount" or "iniread")) return false;
        var args = SplitArguments(call.Groups[2].Value, assignment.Line);
        if (function == "iniread")
        {
            if (args.Count is < 2 or > 3)
                throw new CodeImportException(assignment.Line, "INIREAD: iniread(файл,параметр[,секция]).");
            var ini = Add("IniFile", assignment.Line, lane);
            ini.Values["Path"] = ScalarOrExpression(args[0], assignment.Line);
            ini.Values["Key"] = ScalarOrExpression(args[1], assignment.Line);
            if (args.Count == 3) ini.Values["Section"] = ScalarOrExpression(args[2], assignment.Line);
            ini.Values["Action"] = "Прочитать";
            ini.Values["Result"] = assignment.Name;
            ini.Links["Далее"] = next;
            result = ini.Id;
            return true;
        }
        if (function == "httpget")
        {
            if (args.Count is < 1 or > 33)
                throw new CodeImportException(assignment.Line, "HTTPGET требует URL и не более 32 заголовков.");
            var http = Add("HttpGet", assignment.Line, lane);
            http.Values["Url"] = ScalarOrExpression(args[0], assignment.Line);
            http.Values["Headers"] = string.Join('\n', args.Skip(1).Select(item => Scalar(item, assignment.Line)));
            http.Values["Result"] = assignment.Name;
            http.Links["Далее"] = next;
            result = http.Id;
            return true;
        }
        var expected = function switch { "fromclip" => 0, "tfread" or "strreadln" => 2, _ => 1 };
        if (args.Count != expected)
            throw new CodeImportException(assignment.Line, $"{function} ожидает {expected} аргумент(ов).");
        var kind = function switch
        {
            "fromclip" => "ClipboardGet", _ => "TextFile"
        };
        var node = Add(kind, assignment.Line, lane);
        if (function == "fromclip") node.Values["Name"] = assignment.Name;
        else
        {
            node.Values["Path"] = ScalarOrExpression(args[0], assignment.Line);
            node.Values["Action"] = function == "tfcount" ? "Количество строк" : "Прочитать строку";
            if (function is "tfread" or "strreadln") node.Values["Line"] = IntegerOrVariable(args[1], assignment.Line);
            node.Values["Result"] = assignment.Name;
        }
        node.Links["Далее"] = next;
        result = node.Id;
        return true;
    }

    private bool TryCompileDialogAssignment(Assignment assignment, Guid? next, int lane, out Guid result)
    {
        result = Guid.Empty;
        var call = CallPattern.Match(assignment.Value);
        if (!call.Success) return false;
        var function = call.Groups[1].Value.ToLowerInvariant();
        if (function is not ("inputbox" or "dialogbox" or "radiobox")) return false;
        var args = SplitArguments(call.Groups[2].Value, assignment.Line);
        if (function == "radiobox")
        {
            if (args.Count is < 2 or > 101)
                throw new CodeImportException(assignment.Line, "RADIOBOX требует вопрос и 1–100 вариантов.");
            var options = args.Skip(1).Select(item => Scalar(item, assignment.Line)).ToArray();
            if (options.Any(item => item.Contains('\n') || item.Contains('\r')))
                throw new CodeImportException(assignment.Line, "Вариант RADIOBOX не должен содержать перевод строки.");
            var radio = Add("RadioDialog", assignment.Line, lane);
            radio.Values["Message"] = ScalarOrExpression(args[0], assignment.Line);
            radio.Values["Options"] = string.Join('\n', options);
            radio.Values["Result"] = assignment.Name;
            radio.Links["Далее"] = next;
            result = radio.Id;
            return true;
        }
        if (args.Count is < 2 or > 3)
            throw new CodeImportException(assignment.Line, $"{function} ожидает 2 или 3 аргумента.");
        var node = Add(function == "inputbox" ? "InputDialog" : "ChoiceDialog", assignment.Line, lane);
        node.Values["Message"] = ScalarOrExpression(args[0], assignment.Line);
        node.Values["Result"] = assignment.Name;
        if (function == "inputbox")
        {
            node.Values["Default"] = ScalarOrExpression(args[1], assignment.Line);
            if (args.Count == 3) node.Values["DelaySeconds"] = IntegerOrVariable(args[2], assignment.Line);
        }
        else
        {
            node.Values["Buttons"] = IntegerOrVariable(args[1], assignment.Line);
            if (args.Count == 3) node.Values["Icon"] = IntegerOrVariable(args[2], assignment.Line);
            else node.Values["Icon"] = "0";
        }
        node.Links["Далее"] = next;
        result = node.Id;
        return true;
    }

    private BlockNode CompileCondition(string text, int line, int lane)
    {
        if (TryHeader(text.Trim(), "iskeydown", out var key))
        {
            var node = Add("IfKeyDown", line, lane);
            node.Values["Key"] = KeyName(key, line);
            return node;
        }
        var match = ComparisonPattern.Match(text.Trim());
        if (!match.Success || !TryScalar(match.Groups[3].Value, line, out var expected))
        {
            var expression = Add("IfExpression", line, lane);
            expression.Values["Expression"] = "=" + text.Trim();
            return expression;
        }
        var condition = Add("IfVariable", line, lane);
        condition.Values["Name"] = match.Groups[1].Value;
        condition.Values["Comparison"] = match.Groups[2].Value switch
        {
            "==" or "=" => "Равно", "!=" => "Не равно", ">" => "Больше",
            ">=" => "Не меньше", "<" => "Меньше", "<=" => "Не больше", _ => "Равно"
        };
        condition.Values["Value"] = expected;
        return condition;
    }

    private Guid? CompileCommand(Command command, Guid? next, int lane)
    {
        var args = SplitArguments(command.Arguments, command.Line);
        var name = command.Name.ToLowerInvariant();
        if (name == "description") return CompileDescription(args, command, next);
        if (name is "define" or "undefine" or "inc")
        {
            if (args.Count is < 1 or > 2 || name == "undefine" && args.Count != 1)
                throw new CodeImportException(command.Line, $"{command.Name}: ожидается $переменная и необязательное значение для DEFINE/INC.");
            var variable = VariablePattern.Match(args[0].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, $"Первый аргумент {command.Name} должен быть $переменная.");
            var node = Add(name switch
            {
                "define" => "DefineVariable", "undefine" => "UndefineVariable", _ => "MathVariable"
            }, command.Line, lane);
            node.Values["Name"] = variable.Groups[1].Value;
            if (name == "define") node.Values["Value"] = args.Count == 2 ? ScalarOrExpression(args[1], command.Line) : "0";
            if (name == "inc")
            {
                node.Values["Operation"] = "Сложить";
                node.Values["Operand"] = args.Count == 2 ? ScalarOrExpression(args[1], command.Line) : "1";
            }
            node.Links["Далее"] = next;
            return node.Id;
        }
        if (name == "setvar")
        {
            RequireCount(args, 2, command);
            var set = Add("SetVariableByName", command.Line, lane);
            set.Values["Reference"] = ScalarOrExpression(args[0], command.Line);
            set.Values["Value"] = ScalarOrExpression(args[1], command.Line);
            set.Links["Далее"] = next;
            return set.Id;
        }
        if (name == "hintpopup")
        {
            if (args.Count is < 1 or > 2)
                throw new CodeImportException(command.Line, "HINTPOPUP: hintpopup(сообщение[,заголовок]).");
            var hint = Add("HintPopup", command.Line, lane);
            hint.Values["Message"] = ScalarOrExpression(args[0], command.Line);
            if (args.Count == 2) hint.Values["Title"] = ScalarOrExpression(args[1], command.Line);
            hint.Links["Далее"] = next;
            return hint.Id;
        }
        if (name == "toclip")
        {
            RequireCount(args, 1, command);
            var clipboard = Add("ClipboardSet", command.Line, lane);
            clipboard.Values["Text"] = ScalarOrExpression(args[0], command.Line);
            clipboard.Links["Далее"] = next;
            return clipboard.Id;
        }
        if (name == "iniwrite")
        {
            if (args.Count is < 3 or > 4)
                throw new CodeImportException(command.Line, "INIWRITE: iniwrite(файл,параметр,значение[,секция]).");
            var ini = Add("IniFile", command.Line, lane);
            ini.Values["Path"] = ScalarOrExpression(args[0], command.Line);
            ini.Values["Key"] = ScalarOrExpression(args[1], command.Line);
            ini.Values["Value"] = ScalarOrExpression(args[2], command.Line);
            if (args.Count == 4) ini.Values["Section"] = ScalarOrExpression(args[3], command.Line);
            ini.Values["Action"] = "Записать";
            ini.Values["Result"] = "_iniwrite_result";
            ini.Links["Далее"] = next;
            return ini.Id;
        }
        if (name is "tfreadarr" or "tfwritearr")
        {
            RequireCount(args, 2, command);
            var variable = VariablePattern.Match(args[1].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Второй аргумент TFREADARR/TFWRITEARR должен быть $массив.");
            var fileArray = Add("TextFileArray", command.Line, lane);
            fileArray.Values["Path"] = ScalarOrExpression(args[0], command.Line);
            fileArray.Values["Action"] = name == "tfreadarr" ? "Прочитать в массив" : "Записать массив";
            fileArray.Values["Array"] = variable.Groups[1].Value;
            fileArray.Values["Count"] = "_tfarr_count";
            fileArray.Links["Далее"] = next;
            return fileArray.Id;
        }
        if (name is "tfwrite" or "strwriteln" or "tfdelete" or "tfclear")
        {
            if (name == "tfdelete") RequireCount(args, 2, command);
            else if (name == "tfwrite")
            {
                if (args.Count is < 2 or > 3)
                    throw new CodeImportException(command.Line, "TFWRITE: tfwrite(файл,текст[,номер_строки]).");
            }
            else if (name == "strwriteln")
            {
                if (args.Count is < 2 or > 3)
                    throw new CodeImportException(command.Line, "STRWRITELN: strwriteln(файл,текст[,перезапись]).");
            }
            else
            {
                if (args.Count is < 1 or > 2)
                    throw new CodeImportException(command.Line,
                        "TFCLEAR: tfclear(файл[,флаг_удаления]).");
            }
            var file = Add("TextFile", command.Line, lane);
            file.Values["Path"] = ScalarOrExpression(args[0], command.Line);
            file.Values["Action"] = name switch
            {
                "tfdelete" => "Удалить строку", "tfclear" => args.Count == 2 ? "Очистить или удалить файл" : "Очистить",
                "strwriteln" => "Добавить или перезаписать строкой",
                _ => args.Count == 3 ? "Вставить строку" : "Добавить строку"
            };
            if (name == "tfdelete") file.Values["Line"] = IntegerOrVariable(args[1], command.Line);
            if (name == "tfclear" && args.Count == 2)
                file.Values["DeleteFile"] = IntegerOrVariable(args[1], command.Line);
            if (name is "tfwrite" or "strwriteln")
            {
                file.Values["Value"] = ScalarOrExpression(args[1], command.Line);
                if (args.Count == 3)
                {
                    if (name == "tfwrite") file.Values["Line"] = IntegerOrVariable(args[2], command.Line);
                    else file.Values["Rewrite"] = IntegerOrVariable(args[2], command.Line);
                }
            }
            file.Values["Result"] = "_tf_result";
            file.Links["Далее"] = next;
            return file.Id;
        }
        if (name is "arrpush" or "arrpop" or "arrsort")
        {
            RequireCount(args, name == "arrpush" ? 2 : 1, command);
            var variable = VariablePattern.Match(args[0].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Первый аргумент операции с массивом должен быть $массив.");
            var array = Add("ArrayAction", command.Line, lane);
            array.Values["Name"] = variable.Groups[1].Value;
            array.Values["Action"] = name switch
            {
                "arrpush" => "Добавить", "arrpop" => "Извлечь", _ => "Сортировать автоматически"
            };
            if (name == "arrpush") array.Values["Value"] = ScalarOrExpression(args[1], command.Line);
            array.Values["Result"] = "_arrpop_result";
            array.Links["Далее"] = next;
            return array.Id;
        }
        if (name == "strseparate")
        {
            RequireCount(args, 3, command);
            var variable = VariablePattern.Match(args[2].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Третий аргумент STRSEPARATE должен быть $массив.");
            var split = Add("StringSplit", command.Line, lane);
            split.Values["Text"] = ScalarOrExpression(args[0], command.Line);
            split.Values["Separator"] = ScalarOrExpression(args[1], command.Line);
            split.Values["Array"] = variable.Groups[1].Value;
            split.Values["Count"] = "_strseparate_count";
            split.Links["Далее"] = next;
            return split.Id;
        }
        if (name is "getfilelist" or "getdirlist")
        {
            if (args.Count is < 1 or > 2) throw new CodeImportException(command.Line,
                "GETFILELIST/GETDIRLIST: ($массив[,\"путь\\маска\"]).");
            var variable = VariablePattern.Match(args[0].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Первый аргумент списка должен быть $массив.");
            var list = Add("DirectoryList", command.Line, lane);
            list.Values["Array"] = variable.Groups[1].Value;
            list.Values["Pattern"] = args.Count == 2 ? Scalar(args[1], command.Line) : "*";
            list.Values["Kind"] = name == "getdirlist" ? "Папки" : "Файлы";
            list.Links["Далее"] = next;
            return list.Id;
        }
        if (name is "logwrite" or "print")
        {
            var write = Add("Log", command.Line, lane);
            write.Values["Text"] = args.Count switch
            {
                0 => "",
                1 => ScalarOrExpression(args[0], command.Line),
                _ => "=strconcat(" + string.Join(",", args) + ")"
            };
            write.Links["Далее"] = next;
            return write.Id;
        }
        if (name == "logclear")
        {
            RequireCount(args, 0, command);
            var clear = Add("LogClear", command.Line, lane);
            clear.Links["Далее"] = next;
            return clear.Id;
        }
        if (name == "logshow")
        {
            if (args.Count != 1) throw new CodeImportException(command.Line,
                "LOGSHOW пока поддерживает только флаг 0/1; координаты отдельного окна журнала не импортируются.");
            var show = Add("LogShow", command.Line, lane);
            show.Values["Visible"] = IntegerOrVariable(args[0], command.Line);
            show.Links["Далее"] = next;
            return show.Id;
        }
        if (name == "sound")
        {
            RequireCount(args, 1, command);
            var sound = Add("PlaySound", command.Line, lane);
            sound.Values["Path"] = Scalar(args[0], command.Line);
            sound.Links["Далее"] = next;
            return sound.Id;
        }
        if (name == "beep")
        {
            if (args.Count is < 1 or > 2) throw new CodeImportException(command.Line, "BEEP: beep(частота[,длительность]).");
            var beep = Add("Beep", command.Line, lane);
            beep.Values["Frequency"] = IntegerOrVariable(args[0], command.Line);
            if (args.Count == 2) beep.Values["DurationMs"] = IntegerOrVariable(args[1], command.Line);
            beep.Links["Далее"] = next;
            return beep.Id;
        }
        if (name == "getscreen")
        {
            if (args.Count is not (0 or 4)) throw new CodeImportException(command.Line,
                "getscreen() либо getscreen(x1,y1,x2,y2).");
            var buffer = Add("BufferCapture", command.Line, lane);
            if (args.Count == 0) buffer.Values["AreaMode"] = "Весь рабочий стол";
            else
            {
                buffer.Values["Left"] = IntegerOrVariable(args[0], command.Line);
                buffer.Values["Top"] = IntegerOrVariable(args[1], command.Line);
                buffer.Values["Right"] = IntegerOrVariable(args[2], command.Line);
                buffer.Values["Bottom"] = IntegerOrVariable(args[3], command.Line);
            }
            buffer.Links["Далее"] = next;
            return buffer.Id;
        }
        if (name == "pxlreplace")
        {
            RequireCount(args, 6, command);
            var replace = Add("BufferReplace", command.Line, lane);
            replace.Values["Left"] = IntegerOrVariable(args[0], command.Line);
            replace.Values["Top"] = IntegerOrVariable(args[1], command.Line);
            replace.Values["Right"] = IntegerOrVariable(args[2], command.Line);
            replace.Values["Bottom"] = IntegerOrVariable(args[3], command.Line);
            replace.Values["OldColor"] = ScalarOrExpression(args[4], command.Line);
            replace.Values["NewColor"] = ScalarOrExpression(args[5], command.Line);
            replace.Links["Далее"] = next;
            return replace.Id;
        }
        if (name == "scanpxl")
        {
            if (args.Count != 6) throw new CodeImportException(command.Line,
                "SCANPXL пока поддерживает один цвет: scanpxl($массив,x1,y1,x2,y2,цвет).");
            var variable = VariablePattern.Match(args[0].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Первый аргумент SCANPXL должен быть $массив.");
            var scan = Add("BufferScanColor", command.Line, lane);
            scan.Values["ArrayName"] = variable.Groups[1].Value;
            scan.Values["MaxResults"] = "10000";
            scan.Values["Left"] = IntegerOrVariable(args[1], command.Line);
            scan.Values["Top"] = IntegerOrVariable(args[2], command.Line);
            scan.Values["Right"] = IntegerOrVariable(args[3], command.Line);
            scan.Values["Bottom"] = IntegerOrVariable(args[4], command.Line);
            scan.Values["Color"] = ScalarOrExpression(args[5], command.Line);
            scan.Links["Далее"] = next;
            return scan.Id;
        }
        if (name == "scanpicture")
        {
            if (args.Count is < 6 or > 8) throw new CodeImportException(command.Line,
                "SCANPICTURE: scanpicture($массив,x1,y1,x2,y2,файл[,цветФона[,100]]).");
            var variable = VariablePattern.Match(args[0].Trim());
            if (!variable.Success) throw new CodeImportException(command.Line, "Первый аргумент SCANPICTURE должен быть $массив.");
            if (args.Count == 8 && StaticInteger(args[7], command.Line, "currency") != 100)
                throw new CodeImportException(command.Line, "При импорте SCANPICTURE допускается только точность 100%; другие проценты пока не эквивалентны допуску KLIKER.");
            var scan = Add("BufferScanImage", command.Line, lane);
            scan.Values["ArrayName"] = variable.Groups[1].Value;
            scan.Values["MaxResults"] = "10000";
            scan.Values["Left"] = IntegerOrVariable(args[1], command.Line);
            scan.Values["Top"] = IntegerOrVariable(args[2], command.Line);
            scan.Values["Right"] = IntegerOrVariable(args[3], command.Line);
            scan.Values["Bottom"] = IntegerOrVariable(args[4], command.Line);
            scan.Values["Path"] = Scalar(args[5], command.Line);
            if (args.Count >= 7) scan.Values["IgnoreColor"] = ScalarOrExpression(args[6], command.Line);
            scan.Links["Далее"] = next;
            return scan.Id;
        }
        if (name == "wndgetinfo")
        {
            RequireCount(args, 5, command);
            var info = Add("WindowInfo", command.Line, lane);
            info.Values["Handle"] = IntegerOrVariable(args[0], command.Line);
            foreach (var (index, field) in new[] { (1, "XName"), (2, "YName"), (3, "WidthName"), (4, "HeightName") })
            {
                var variable = VariablePattern.Match(args[index].Trim());
                if (!variable.Success) throw new CodeImportException(command.Line, $"Аргумент {index + 1} WNDGETINFO должен быть $переменная.");
                info.Values[field] = variable.Groups[1].Value;
            }
            info.Links["Далее"] = next;
            return info.Id;
        }
        if (name is "wndpos" or "wndsize" or "wndsetinfo" or "wndstate" or "wndbump")
        {
            var expected = name switch { "wndbump" => 1, "wndstate" => 2, "wndpos" or "wndsize" => 3, _ => 5 };
            RequireCount(args, expected, command);
            var action = Add("WindowAction", command.Line, lane);
            action.Values["Handle"] = IntegerOrVariable(args[0], command.Line);
            action.Values["Action"] = name switch
            {
                "wndpos" => "Позиция", "wndsize" => "Размер", "wndsetinfo" => "Геометрия",
                "wndstate" => "Состояние", _ => "Активировать"
            };
            if (name is "wndpos" or "wndsetinfo")
            {
                action.Values["WindowX"] = IntegerOrVariable(args[1], command.Line);
                action.Values["WindowY"] = IntegerOrVariable(args[2], command.Line);
            }
            if (name is "wndsize" or "wndsetinfo")
            {
                var offset = name == "wndsize" ? 1 : 3;
                action.Values["WindowWidth"] = IntegerOrVariable(args[offset], command.Line);
                action.Values["WindowHeight"] = IntegerOrVariable(args[offset + 1], command.Line);
            }
            if (name == "wndstate") action.Values["State"] = IntegerOrVariable(args[1], command.Line);
            action.Links["Далее"] = next;
            return action.Id;
        }
        if (name == "hsettext")
        {
            RequireCount(args, 2, command);
            var setText = Add("ElementSetText", command.Line, lane);
            setText.Values["Handle"] = IntegerOrVariable(args[0], command.Line);
            setText.Values["Text"] = ScalarOrExpression(args[1], command.Line);
            setText.Links["Далее"] = next;
            return setText.Id;
        }
        if (name is "sendmessage" or "postmessage")
        {
            RequireCount(args, 4, command);
            var send = Add("MessageSend", command.Line, lane);
            send.Values["Handle"] = ScalarOrExpression(args[0], command.Line);
            send.Values["Message"] = IntegerOrVariable(args[1], command.Line);
            send.Values["WParam"] = ScalarOrExpression(args[2], command.Line);
            send.Values["LParam"] = ScalarOrExpression(args[3], command.Line);
            send.Values["Mode"] = name == "postmessage" ? "Асинхронно" : "Синхронно";
            send.Links["Далее"] = next;
            return send.Id;
        }
        if (name == "getmessage")
        {
            if (args.Count is < 2 or > 3) throw new CodeImportException(command.Line,
                "GETMESSAGE требует две или три переменные: getmessage($w,$l[,$новое]).");
            var receive = Add("MessageReceive", command.Line, lane);
            for (var i = 0; i < args.Count; i++)
            {
                var variable = VariablePattern.Match(args[i].Trim());
                if (!variable.Success) throw new CodeImportException(command.Line, "Аргументы GETMESSAGE должны быть $переменными.");
                receive.Values[i switch { 0 => "WName", 1 => "LName", _ => "NewName" }] = variable.Groups[1].Value;
            }
            receive.Links["Далее"] = next;
            return receive.Id;
        }
        if (name == "mover")
        {
            RequireCount(args, 2, command);
            var relative = Add("MoveRelative", command.Line, lane);
            relative.Values["DeltaX"] = IntegerOrVariable(args[0], command.Line);
            relative.Values["DeltaY"] = IntegerOrVariable(args[1], command.Line);
            relative.Links["Далее"] = next;
            return relative.Id;
        }
        if (name is "move" or "lclick" or "rclick" or "mclick")
        {
            if (args.Count is not (0 or 2) || (name == "move" && args.Count != 2))
                throw new CodeImportException(command.Line, $"{command.Name} требует координаты X,Y; клик также допускает пустые скобки.");
            BlockNode? click = null;
            if (name != "move")
            {
                click = Add("Click", command.Line, lane, args.Count == 2 ? 1 : 0);
                click.Values["Button"] = name switch { "rclick" => "Правая", "mclick" => "Средняя", _ => "Левая" };
                click.Links["Далее"] = next;
            }
            if (args.Count == 0) return click!.Id;
            var move = Add("MoveCursor", command.Line, lane);
            move.Values["TargetMode"] = "Задать X/Y";
            move.Values["TargetX"] = IntegerOrVariable(args[0], command.Line);
            move.Values["TargetY"] = IntegerOrVariable(args[1], command.Line);
            move.Links["Далее"] = click?.Id ?? next;
            return move.Id;
        }
        if (name is "waitms" or "wait")
        {
            RequireCount(args, 1, command);
            var wait = Add("Wait", command.Line, lane);
            if (name == "waitms") wait.Values["Milliseconds"] = IntegerOrVariable(args[0], command.Line);
            else
            {
                if (!decimal.TryParse(args[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                    seconds < 0 || seconds > 3600)
                    throw new CodeImportException(command.Line, "wait() поддерживает числовые секунды 0–3600; для переменной используйте waitms($имя).");
                wait.Values["Milliseconds"] = decimal.ToInt32(seconds * 1000).ToString(CultureInfo.InvariantCulture);
            }
            wait.Links["Далее"] = next;
            return wait.Id;
        }
        if (name == "print")
        {
            var node = Add("Log", command.Line, lane);
            node.Values["Text"] = string.Concat(args.Select(arg => Scalar(arg, command.Line)));
            node.Links["Далее"] = next;
            return node.Id;
        }
        if (name is "keypress" or "keydown" or "keyup")
        {
            RequireCount(args, 1, command);
            var node = Add(name == "keypress" ? "Key" : "KeyState", command.Line, lane);
            node.Values["Key"] = KeyboardName(args[0], command.Line);
            if (name != "keypress") node.Values["Action"] = name == "keydown" ? "Нажать" : "Отпустить";
            node.Links["Далее"] = next;
            return node.Id;
        }
        if (name == "watchcursor")
        {
            if (args.Count is not (0 or 3)) throw new CodeImportException(command.Line,
                "watchcursor() либо watchcursor(toggle|hold, F9, LMB|RMB|MMB|SHIFT).");
            var node = Add("WatchCursor", command.Line, lane);
            if (args.Count == 3)
            {
                var mode = Unquote(args[0]).ToLowerInvariant();
                node.Values["ActivationMode"] = mode switch
                {
                    "toggle" => "Переключатель", "hold" => "Пока удерживается",
                    _ => throw new CodeImportException(command.Line, "Режим watchcursor: toggle либо hold.")
                };
                node.Values["ActivationKey"] = KeyName(args[1], command.Line);
                var required = KeyName(args[2], command.Line);
                node.Values["RequiredHold"] = required.ToUpperInvariant() switch
                {
                    "LMB" or "LBUTTON" or "ЛКМ" => "Левая кнопка мыши",
                    "RMB" or "RBUTTON" or "ПКМ" => "Правая кнопка мыши",
                    "MMB" or "MBUTTON" or "СКМ" => "Средняя кнопка мыши",
                    _ => "Клавиша"
                };
                if (node.Values["RequiredHold"] == "Клавиша") node.Values["RequiredKey"] = required;
            }
            node.Links["Далее"] = next;
            return node.Id;
        }
        if (name == "call")
        {
            RequireCount(args, 1, command);
            return CallSub(Unquote(args[0]), command.Line, lane, next);
        }
        if (_subNames.Contains(command.Name) && args.Count == 0)
            return CallSub(command.Name, command.Line, lane, next);
        if (name == "stop" && args.Count == 0) return Add("Stop", command.Line, lane).Id;
        throw new CodeImportException(command.Line, $"Команда «{command.Name}» пока не поддерживается импортом. Блоки не добавлены.");
    }

    private Guid? CompileDescription(List<string> args, Command command, Guid? next)
    {
        if (args.Count is not (1 or 7)) throw new CodeImportException(command.Line,
            "description(\"Название\") либо description(\"Название\", x, y, ширина, высота, \"#RRGGBB\", непрозрачность 0–100).");
        var rawTitle = args[0].Trim();
        if (rawTitle.Length < 2 || rawTitle[0] != '"' || rawTitle[^1] != '"')
            throw new CodeImportException(command.Line, "Название description должно быть в двойных кавычках.");
        var title = Unquote(rawTitle);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120)
            throw new CodeImportException(command.Line, "Название description должно содержать 1–120 символов.");
        var x = -20;
        var y = -45;
        var width = 700;
        var height = 360;
        var color = "#A8D9FF";
        var opacity = 35;
        if (args.Count == 7)
        {
            x = StaticInteger(args[1], command.Line, "x");
            y = StaticInteger(args[2], command.Line, "y");
            width = StaticInteger(args[3], command.Line, "ширина");
            height = StaticInteger(args[4], command.Line, "высота");
            color = Unquote(args[5]);
            opacity = StaticInteger(args[6], command.Line, "непрозрачность");
        }
        if (width is < DescriptionGeometry.MinWidth or > DescriptionGeometry.MaxSize ||
            height is < DescriptionGeometry.MinHeight or > DescriptionGeometry.MaxSize ||
            opacity is < 0 or > 100 || !Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            throw new CodeImportException(command.Line,
                "Размер description вне допустимых пределов, цвет не #RRGGBB или непрозрачность не 0–100.");
        var absoluteX = (long)_origin.X + x;
        var absoluteY = (long)_origin.Y + y;
        if (absoluteX is < int.MinValue or > int.MaxValue || absoluteY is < int.MinValue or > int.MaxValue)
            throw new CodeImportException(command.Line, "Координаты description слишком велики.");
        var node = BlockCatalog.Create("Description", (int)absoluteX, (int)absoluteY);
        node.Values["Title"] = title;
        node.Values["Width"] = width.ToString(CultureInfo.InvariantCulture);
        node.Values["Height"] = height.ToString(CultureInfo.InvariantCulture);
        node.Values["FillColor"] = color.ToUpperInvariant();
        node.Values["OpacityPercent"] = opacity.ToString(CultureInfo.InvariantCulture);
        _nodes.Add(node);
        return next;
    }

    private static int StaticInteger(string text, int line, string name) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value
            : throw new CodeImportException(line, $"Параметр {name} в description должен быть постоянным целым числом.");

    private Guid CallSub(string name, int line, int lane, Guid? next)
    {
        if (!_subNames.Contains(name)) throw new CodeImportException(line, $"Подпрограмма «{name}» не определена в этом коде.");
        var node = Add("CallSub", line, lane);
        node.Values["Name"] = name;
        node.Links["Далее"] = next;
        return node.Id;
    }

    private BlockNode Add(string kind, int line, int lane, int priority = 0)
    {
        var node = BlockCatalog.Create(kind, 0, 0);
        _nodes.Add(node);
        _placements.Add(new Placement(node, line, lane, priority, _sequence++));
        return node;
    }

    private void Layout()
    {
        var column = 0;
        foreach (var group in _placements.GroupBy(item => item.Line).OrderBy(group => group.Key))
        {
            foreach (var placement in group.OrderBy(item => item.Priority).ThenBy(item => item.Sequence))
            {
                placement.Node.X = _origin.X + column * 235;
                placement.Node.Y = _origin.Y + placement.Lane * 185;
                column++;
            }
        }
    }

    private static bool TryHeader(string text, string keyword, out string content)
    {
        var match = Regex.Match(text, @"^" + Regex.Escape(keyword) + @"\s*\((.*)\)$", RegexOptions.IgnoreCase);
        content = match.Success ? match.Groups[1].Value.Trim() : "";
        return match.Success;
    }

    private static string IntegerOrVariable(string text, int line)
    {
        var scalar = ScalarOrExpression(text, line);
        if (scalar.StartsWith("${", StringComparison.Ordinal) || int.TryParse(scalar, out _)) return scalar;
        if (scalar.StartsWith('=')) return scalar;
        throw new CodeImportException(line, "Ожидается целое число или числовое выражение.");
    }

    private static bool TryScalar(string text, int line, out string value)
    {
        try { value = Scalar(text, line); return true; }
        catch (CodeImportException) { value = ""; return false; }
    }

    private static string ScalarOrExpression(string text, int line) =>
        TryScalar(text, line, out var value) ? value : "=" + text.Trim();

    private static string Scalar(string text, int line)
    {
        text = text.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') return Unquote(text);
        var variable = VariablePattern.Match(text);
        if (variable.Success) return "${" + variable.Groups[1].Value + "}";
        if (NumberPattern.IsMatch(text)) return text.Replace(',', '.');
        throw new CodeImportException(line, $"Поддерживаются строка, число или $переменная; получено «{text}».");
    }

    private static string KeyName(string text, int line)
    {
        var name = Unquote(text.Trim());
        try { NativeMouse.ParseControlKey(name); }
        catch (ArgumentException) { throw new CodeImportException(line, $"Неизвестная клавиша «{name}»."); }
        return name;
    }

    private static string KeyboardName(string text, int line)
    {
        var name = Unquote(text.Trim());
        try { NativeMouse.ParseKey(name); }
        catch (ArgumentException) { throw new CodeImportException(line, $"Неизвестная клавиша «{name}»."); }
        return name;
    }

    private static string Unquote(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"'
        ? text[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\") : text;

    private static bool IsName(string name) => Regex.IsMatch(name, @"^[\p{L}_][\p{L}\p{Nd}_]*$");

    private static void RequireCount(List<string> args, int count, Command command)
    {
        if (args.Count != count) throw new CodeImportException(command.Line,
            $"{command.Name} ожидает {count} аргумент(ов), получено {args.Count}.");
    }

    private static List<string> SplitArguments(string text, int line)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var result = new List<string>();
        var buffer = new StringBuilder();
        var quoted = false;
        var escaped = false;
        var depth = 0;
        foreach (var character in text)
        {
            if (escaped) { buffer.Append(character); escaped = false; continue; }
            if (character == '\\' && quoted) { buffer.Append(character); escaped = true; continue; }
            if (character == '"') quoted = !quoted;
            if (!quoted && character is '(' or '[') depth++;
            if (!quoted && character is ')' or ']') depth--;
            if (depth < 0) throw new CodeImportException(line, "Лишняя закрывающая скобка.");
            if (character == ',' && !quoted && depth == 0)
            {
                result.Add(buffer.ToString().Trim());
                buffer.Clear();
            }
            else buffer.Append(character);
        }
        if (quoted) throw new CodeImportException(line, "Не закрыта кавычка.");
        if (depth != 0) throw new CodeImportException(line, "Не закрыта скобка.");
        result.Add(buffer.ToString().Trim());
        if (result.Any(item => item.Length == 0)) throw new CodeImportException(line, "Пустой аргумент.");
        return result;
    }

    private static string StripComment(string text)
    {
        var quoted = false;
        var escaped = false;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (escaped) { escaped = false; continue; }
            if (text[i] == '\\' && quoted) { escaped = true; continue; }
            if (text[i] == '"') quoted = !quoted;
            if (!quoted && text[i] == '/' && text[i + 1] == '/') return text[..i];
        }
        return text;
    }
}

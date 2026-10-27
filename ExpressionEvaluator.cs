using System.Globalization;

namespace KlikerBlocks;

// Expressions are opt-in in block fields: a leading '=' keeps older projects unchanged.
internal sealed class ExpressionEvaluator(string source, Func<string, string?, string> variable,
    Func<string, int>? arraySize = null, Func<string, string>? arrayPop = null)
{
    private int _position;
    private int _depth;

    public static string Evaluate(string source, Func<string, string?, string> variable,
        Func<string, int>? arraySize = null, Func<string, string>? arrayPop = null)
    {
        if (source.Length > 4096) throw new InvalidOperationException("Выражение слишком длинное (максимум 4096 символов).");
        if (ConditionalExpressionEvaluator.IsConditional(source))
            return ConditionalExpressionEvaluator.Evaluate(source,
                part => Evaluate(part, variable, arraySize, arrayPop));
        var parser = new ExpressionEvaluator(source, variable, arraySize, arrayPop);
        var value = parser.ParseOr();
        parser.Space();
        if (parser._position != source.Length) throw parser.Error("Лишний текст в конце выражения");
        return Text(value);
    }

    private object ParseOr()
    {
        var left = ParseXor();
        while (TakeWord("or") || Take("||") || Take("|"))
        {
            var right = ParseXor();
            left = Bool(Truthy(left) || Truthy(right));
        }
        return left;
    }

    private object ParseXor()
    {
        var left = ParseAnd();
        while (TakeWord("xor"))
            left = Bool(Truthy(left) ^ Truthy(ParseAnd()));
        return left;
    }

    private object ParseAnd()
    {
        var left = ParseCompare();
        while (TakeWord("and") || Take("&&"))
        {
            var right = ParseCompare();
            left = Bool(Truthy(left) && Truthy(right));
        }
        return left;
    }

    private object ParseCompare()
    {
        var left = ParseAdd();
        while (true)
        {
            string? op = null;
            foreach (var candidate in new[] { ">=", "<=", "==", "!=", ">", "<", "=" })
                if (Take(candidate)) { op = candidate; break; }
            if (op is null) return left;
            var right = ParseAdd();
            var comparison = TryNumber(left, out var a) && TryNumber(right, out var b)
                ? a.CompareTo(b) : string.Compare(Text(left), Text(right), StringComparison.OrdinalIgnoreCase);
            left = Bool(op switch
            {
                "=" or "==" => comparison == 0, "!=" => comparison != 0,
                ">" => comparison > 0, "<" => comparison < 0,
                ">=" => comparison >= 0, "<=" => comparison <= 0, _ => false
            });
        }
    }

    private object ParseAdd()
    {
        var left = ParseMultiply();
        while (true)
        {
            if (Take("+"))
            {
                var right = ParseMultiply();
                left = TryNumber(left, out var a) && TryNumber(right, out var b) ? a + b : Text(left) + Text(right);
            }
            else if (Take("-")) left = Number(left) - Number(ParseMultiply());
            else return left;
        }
    }

    private object ParseMultiply()
    {
        var left = ParseUnary();
        while (true)
        {
            if (Take("*")) left = Number(left) * Number(ParseUnary());
            else if (Take("%"))
            {
                var divisor = Number(ParseUnary());
                if (divisor == 0) throw new DivideByZeroException(UiLanguage.Code == "ru"
                    ? "Деление на ноль в остатке от деления."
                    : "Division by zero in remainder expression.");
                left = Number(left) % divisor;
            }
            else if (Take("/"))
            {
                var divisor = Number(ParseUnary());
                if (divisor == 0) throw new DivideByZeroException("Деление на ноль в выражении.");
                left = Number(left) / divisor;
            }
            else return left;
        }
    }

    private object ParseUnary()
    {
        if (Take("+")) return Number(ParseUnary());
        if (Take("-")) return -Number(ParseUnary());
        if (Take("!")) return Bool(!Truthy(ParseUnary()));
        if (TakeWord("not")) return Bool(!Truthy(ParseUnary()));
        return ParsePower();
    }

    private object ParsePower()
    {
        var left = ParseAtom();
        if (Take("**") || Take("^"))
        {
            var result = Math.Pow((double)Number(left), (double)Number(ParseUnary()));
            if (!double.IsFinite(result) || result > (double)decimal.MaxValue || result < (double)decimal.MinValue)
                throw new InvalidOperationException(UiLanguage.Code == "ru"
                    ? "Степень вышла за допустимый числовой диапазон."
                    : "Power result is outside the supported numeric range.");
            return (decimal)result;
        }
        return left;
    }

    private object ParseAtom()
    {
        Space();
        if (++_depth > 64) throw Error("Слишком глубокое выражение");
        try
        {
            if (Take("("))
            {
                var value = ParseOr();
                Require(")");
                return value;
            }
            if (Take("$"))
            {
                var braced = Take("{");
                var name = Identifier();
                if (braced) Require("}");
                string? index = null;
                if (Take("[")) { index = Text(ParseOr()); Require("]"); }
                return variable(name, index);
            }
            if (_position < source.Length && source[_position] == '"') return Quoted();
            if (_position < source.Length && (char.IsDigit(source[_position]) || source[_position] == '.')) return LiteralNumber();
            var function = Identifier();
            Require("(");
            if (function.Equals("arrsize", StringComparison.OrdinalIgnoreCase) ||
                function.Equals("arrpop", StringComparison.OrdinalIgnoreCase))
            {
                Require("$");
                var braced = Take("{");
                var name = Identifier();
                if (braced) Require("}");
                Require(")");
                return function.Equals("arrsize", StringComparison.OrdinalIgnoreCase)
                    ? arraySize?.Invoke(name) ?? throw Error("ARRSIZE недоступен без хранилища массивов")
                    : arrayPop?.Invoke(name) ?? throw Error("ARRPOP недоступен без хранилища массивов");
            }
            var args = new List<object>();
            if (!Take(")"))
            {
                do { args.Add(ParseOr()); } while (Take(","));
                Require(")");
            }
            return Function(function, args);
        }
        finally { _depth--; }
    }

    private object Function(string name, List<object> args)
    {
        var id = name.ToLowerInvariant();
        void Arity(int count)
        {
            if (args.Count != count) throw Error($"Функция {name} ожидает {count} аргументов");
        }
        switch (id)
        {
            case "rnd": Arity(2); return Random.Shared.NextInt64(checked((long)Number(args[0])), checked((long)Number(args[1])) + 1).ToString(CultureInfo.InvariantCulture);
            case "rndfrom": if (args.Count == 0) throw Error("rndfrom требует аргументы"); return args[Random.Shared.Next(args.Count)];
            case "int": Arity(1); return decimal.Truncate(Number(args[0]));
            case "abs": Arity(1); return Math.Abs(Number(args[0]));
            case "sqrt": Arity(1); return (decimal)Math.Sqrt((double)Number(args[0]));
            case "pow": Arity(2); return (decimal)Math.Pow((double)Number(args[0]), (double)Number(args[1]));
            case "sin": Arity(1); return (decimal)Math.Sin((double)Number(args[0]) * Math.PI / 180);
            case "cos": Arity(1); return (decimal)Math.Cos((double)Number(args[0]) * Math.PI / 180);
            case "arcsin": Arity(1); return (decimal)(Math.Asin((double)Number(args[0])) * 180 / Math.PI);
            case "arccos": Arity(1); return (decimal)(Math.Acos((double)Number(args[0])) * 180 / Math.PI);
            case "round":
                if (args.Count is < 1 or > 2) throw Error("round ожидает 1 или 2 аргумента");
                var precision = args.Count == 2 ? checked((int)Number(args[1])) : 0;
                if (precision is < -28 or > 28) throw Error("Точность ROUND должна быть от -28 до 28");
                if (precision <= 0)
                    return Math.Round(Number(args[0]), -precision, MidpointRounding.AwayFromZero);
                var factor = 1m;
                for (var i = 0; i < precision; i++) factor *= 10m;
                return Math.Round(Number(args[0]) / factor, 0, MidpointRounding.AwayFromZero) * factor;
            case "dist":
                Arity(4);
                var dx = (double)(Number(args[0]) - Number(args[2]));
                var dy = (double)(Number(args[1]) - Number(args[3]));
                return (decimal)Math.Sqrt(dx * dx + dy * dy);
            case "strlen": Arity(1); return Text(args[0]).Length;
            case "strconcat": return string.Concat(args.Select(Text));
            case "strpos":
                if (args.Count is < 2 or > 3) throw Error("strpos ожидает 2 или 3 аргумента");
                var haystack = Text(args[0]);
                var offset = args.Count == 3 ? checked((int)Number(args[2])) : 1;
                if (offset < 1) throw Error("Смещение STRPOS должно быть не меньше 1");
                if (offset > haystack.Length + 1) return 0;
                var found = haystack.IndexOf(Text(args[1]), offset - 1, StringComparison.Ordinal);
                return found < 0 ? 0 : found + 1;
            case "strreplace": Arity(3); return Text(args[0]).Replace(Text(args[1]), Text(args[2]), StringComparison.Ordinal);
            case "strcut":
                Arity(3);
                var input = Text(args[0]);
                var start = checked((int)Number(args[1]));
                var length = checked((int)Number(args[2]));
                if (start < 1 || length < 0) throw Error("STRCUT: позиция должна быть от 1, длина — от 0");
                return input.Substring(Math.Min(start - 1, input.Length),
                    Math.Min(length, Math.Max(0, input.Length - start + 1)));
            case "strcut2":
                Arity(3);
                var sourceText = Text(args[0]);
                var from = checked((int)Number(args[1]));
                var to = checked((int)Number(args[2]));
                if (from < 1 || to < from - 1) throw Error("STRCUT2: неверные границы подстроки");
                return sourceText.Substring(Math.Min(from - 1, sourceText.Length),
                    Math.Min(to - from + 1, Math.Max(0, sourceText.Length - from + 1)));
            case "strfilter":
                Arity(3);
                var mode = checked((int)Number(args[2]));
                if (mode is not (0 or 1)) throw Error("STRFILTER: режим должен быть 0 или 1");
                var selected = Text(args[1]).ToHashSet();
                return new string(Text(args[0]).Where(ch => selected.Contains(ch) == (mode == 1)).ToArray());
            case "strmd5":
                Arity(1);
                return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(Text(args[0]))));
            case "getvar":
                Arity(1);
                return variable(VariableStore.ReferenceName(Text(args[0])), null);
            case "char": Arity(1); return char.ConvertFromUtf32(Math.Clamp((int)Number(args[0]), 0, 0x10ffff));
            case "wndfind":
                if (args.Count is < 1 or > 2) throw Error("wndfind ожидает заголовок и необязательный флаг начала");
                return WindowTools.Find(Text(args[0]), args.Count == 2 && Number(args[1]) != 0).ToInt64();
            case "hget":
                Arity(2);
                return WindowTools.ElementAt(new System.Drawing.Point(checked((int)Number(args[0])),
                    checked((int)Number(args[1])))).ToInt64();
            case "hgetpid":
                Arity(1);
                return WindowTools.ProcessId(new IntPtr(checked((long)Number(args[0]))));
            case "hgettext":
                Arity(1);
                return WindowTools.ElementText(new IntPtr(checked((long)Number(args[0]))));
            default: throw Error($"Неизвестная функция {name}");
        }
    }

    private decimal LiteralNumber()
    {
        var start = _position;
        if (source.AsSpan(_position).StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            _position += 2;
            while (_position < source.Length && Uri.IsHexDigit(source[_position])) _position++;
            return Convert.ToInt64(source[(start + 2).._position], 16);
        }
        while (_position < source.Length && (char.IsDigit(source[_position]) || source[_position] == '.')) _position++;
        if (!decimal.TryParse(source[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            throw Error("Неверное число");
        return number;
    }

    private string Quoted()
    {
        _position++;
        var builder = new System.Text.StringBuilder();
        while (_position < source.Length)
        {
            var ch = source[_position++];
            if (ch == '"') return builder.ToString();
            if (ch == '\\' && _position < source.Length)
            {
                ch = source[_position++];
                ch = ch switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => ch };
            }
            builder.Append(ch);
        }
        throw Error("Не закрыта строка");
    }

    private string Identifier()
    {
        Space();
        var start = _position;
        if (_position < source.Length && (char.IsLetter(source[_position]) || source[_position] == '_')) _position++;
        while (_position < source.Length && (char.IsLetterOrDigit(source[_position]) || source[_position] == '_')) _position++;
        if (start == _position) throw Error("Ожидалось имя переменной или функции");
        return source[start.._position];
    }

    private bool TakeWord(string word)
    {
        Space();
        if (!source.AsSpan(_position).StartsWith(word, StringComparison.OrdinalIgnoreCase)) return false;
        var end = _position + word.Length;
        if (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_')) return false;
        _position = end;
        return true;
    }

    private bool Take(string token)
    {
        Space();
        if (!source.AsSpan(_position).StartsWith(token, StringComparison.Ordinal)) return false;
        _position += token.Length;
        return true;
    }

    private void Require(string token) { if (!Take(token)) throw Error($"Ожидалось «{token}»"); }
    private void Space() { while (_position < source.Length && char.IsWhiteSpace(source[_position])) _position++; }
    private InvalidOperationException Error(string message) => new($"{message} (позиция {_position + 1}): {source}");
    private static string Text(object value) => value switch
    {
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
    private static bool TryNumber(object value, out decimal number) => decimal.TryParse(Text(value),
        NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    private static decimal Number(object value) => TryNumber(value, out var number) ? number
        : throw new InvalidOperationException($"Ожидалось число, получено «{Text(value)}».");
    private static decimal Bool(bool value) => value ? 1m : 0m;
    internal static bool IsTrueValue(string value) =>
        bool.TryParse(value, out var boolean) ? boolean :
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number != 0 : !string.IsNullOrWhiteSpace(value);
    private static bool Truthy(object value) => IsTrueValue(Text(value));
}

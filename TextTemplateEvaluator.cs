using System.Text;

namespace KlikerBlocks;

// A deliberately small template grammar: =[expression]&literal&[expression].
// The expression engine remains the only place that evaluates operators or functions.
internal static class TextTemplateEvaluator
{
    public static string Evaluate(string source, Func<string, string> expression,
        Func<string, string> expandLiteral, Action<string, string> assign)
    {
        if (source.Length > 8192) throw Error("Шаблон слишком длинный (максимум 8192 символов).",
            "Template is too long (maximum 8192 characters).");
        var output = new StringBuilder();
        var assignments = new List<(string Name, string Value)>();
        var position = 0;
        while (position < source.Length)
        {
            if (source[position] == '[')
            {
                var start = ++position;
                var depth = 1;
                var quoted = false;
                while (position < source.Length && depth > 0)
                {
                    var ch = source[position++];
                    if (quoted)
                    {
                        if (ch == '\\' && position < source.Length) position++;
                        else if (ch == '"') quoted = false;
                    }
                    else if (ch == '"') quoted = true;
                    else if (ch == '[') depth++;
                    else if (ch == ']') depth--;
                }
                if (depth != 0) throw Error("Не закрыта скобка ] в шаблоне.",
                    "Missing ] in template.");
                var value = expression(source[start..(position - 1)]);
                var afterBracket = position;
                while (afterBracket < source.Length && source[afterBracket] is ' ' or '\t') afterBracket++;
                if (afterBracket + 1 < source.Length && source[afterBracket] == '=' && source[afterBracket + 1] == '>')
                {
                    position = afterBracket + 2;
                    while (position < source.Length && source[position] is ' ' or '\t') position++;
                    if (position + 2 >= source.Length || source[position++] != '$' || source[position++] != '{')
                        throw Error("После => укажите имя переменной как ${name}.",
                            "After =>, specify a variable as ${name}.");
                    var nameStart = position;
                    while (position < source.Length && source[position] != '}') position++;
                    if (position == source.Length)
                        throw Error("Не закрыта } в имени переменной после =>.",
                            "Missing } in the variable name after =>.");
                    var name = source[nameStart..position++];
                    if (!VariableNameRules.IsAscii(name))
                        throw Error("Имя после =>: только A–Z, a–z, 0–9 и _, начиная с буквы или _.",
                            "After =>, use only A–Z, a–z, 0–9 and _, starting with a letter or _.");
                    assignments.Add((name, value));
                    while (position < source.Length && source[position] is ' ' or '\t') position++;
                }
                output.Append(value);
            }
            else
            {
                var literal = new StringBuilder();
                while (position < source.Length && source[position] != '&')
                {
                    var ch = source[position++];
                    if (ch == '\\' && position < source.Length &&
                        source[position] is '&' or '\\' or '[' or ']')
                        ch = source[position++];
                    literal.Append(ch);
                }
                output.Append(expandLiteral(literal.ToString()));
            }
            if (position >= source.Length) break;
            if (source[position++] != '&') throw Error("Между частями шаблона нужен знак &.",
                "Use & between template parts.");
        }
        foreach (var (name, value) in assignments) assign(name, value);
        return output.ToString();
    }

    private static InvalidOperationException Error(string ru, string en) =>
        new(UiLanguage.Code == "ru" ? ru : en);
}

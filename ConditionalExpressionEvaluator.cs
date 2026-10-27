namespace KlikerBlocks;

// Parse conditional syntax without evaluating inactive branches.
internal static class ConditionalExpressionEvaluator
{
    private readonly record struct Clause(string Condition, string Result);
    private readonly record struct Keyword(int Start, string Name);

    public static bool IsConditional(string source)
    {
        var text = source.AsSpan().TrimStart();
        return text.Length >= 2 && text[..2].Equals("if".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
               (text.Length == 2 || char.IsWhiteSpace(text[2]));
    }

    public static string Evaluate(string source, Func<string, string> evaluate)
    {
        var text = source.Trim();
        var position = 2;
        var first = FindKeyword(text, position);
        if (first is null)
        {
            var conditionOnly = Required(text[position..], "условие после if", "condition after if");
            return ExpressionEvaluator.IsTrueValue(evaluate(conditionOnly)) ? "TRUE" : "FALSE";
        }
        if (first.Value.Name != "then")
            throw Error("После условия нужен then.", "Use then after the condition.");

        var clauses = new List<Clause>();
        var condition = Required(text[position..first.Value.Start], "условие после if", "condition after if");
        position = first.Value.Start + first.Value.Name.Length;
        string? fallback = null;
        while (true)
        {
            var next = FindKeyword(text, position);
            var result = Required(text[position..(next?.Start ?? text.Length)],
                "значение после then", "value after then");
            clauses.Add(new Clause(condition, result));
            if (next is null) break;
            position = next.Value.Start + next.Value.Name.Length;
            if (next.Value.Name == "else")
            {
                if (FindKeyword(text, position) is not null)
                    throw Error("После else не может быть другой ветки.", "No branch can follow else.");
                fallback = Required(text[position..], "значение после else", "value after else");
                break;
            }
            if (next.Value.Name != "else_if")
                throw Error("После значения допустимы только else_if или else.",
                    "Only else_if or else may follow a value.");
            var then = FindKeyword(text, position);
            if (then is null || then.Value.Name != "then")
                throw Error("После else_if нужны условие и then.",
                    "Use a condition and then after else_if.");
            condition = Required(text[position..then.Value.Start],
                "условие после else_if", "condition after else_if");
            position = then.Value.Start + then.Value.Name.Length;
        }

        foreach (var clause in clauses)
            if (ExpressionEvaluator.IsTrueValue(evaluate(clause.Condition)))
                return evaluate(clause.Result);
        return fallback is null ? "FALSE" : evaluate(fallback);
    }

    private static Keyword? FindKeyword(string text, int start)
    {
        var parentheses = 0;
        var brackets = 0;
        var braces = 0;
        var quoted = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '\\' && i + 1 < text.Length) i++;
                else if (ch == '"') quoted = false;
                continue;
            }
            if (ch == '"') { quoted = true; continue; }
            if (ch == '(') { parentheses++; continue; }
            if (ch == ')') { parentheses--; continue; }
            if (ch == '[') { brackets++; continue; }
            if (ch == ']') { brackets--; continue; }
            if (ch == '{') { braces++; continue; }
            if (ch == '}') { braces--; continue; }
            if (parentheses != 0 || brackets != 0 || braces != 0 || !char.IsLetter(ch)) continue;
            var end = i + 1;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            var name = text[i..end];
            if ((name.Equals("then", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("else_if", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("else", StringComparison.OrdinalIgnoreCase)) &&
                (i == 0 || char.IsWhiteSpace(text[i - 1])) &&
                (end == text.Length || char.IsWhiteSpace(text[end])))
                return new Keyword(i, name.ToLowerInvariant());
            i = end - 1;
        }
        return null;
    }

    private static string Required(string text, string ru, string en)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 ? throw Error($"Не указано {ru}.", $"Missing {en}.") : trimmed;
    }

    private static InvalidOperationException Error(string ru, string en) =>
        new(UiLanguage.Code == "ru" ? ru : en);
}

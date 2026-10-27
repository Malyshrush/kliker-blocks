namespace KlikerBlocks;

internal static class ExpressionHelp
{
    internal static string? Section(string title)
    {
        if (UiLanguage.Code == "ru") return null;
        if (title == "Условные выражения и присваивание")
            return "In an evaluated field, write " +
                "=[if (${counter}+2)*3<10 then \"Yes, definitely\" else_if 35>25 then 22 else 33]. " +
                "With counter=1, the result is ‘Yes, definitely’. if takes a condition, then takes a value, " +
                "and any number of else_if condition then value branches may follow. else is optional. " +
                "Quote text values; write numbers or formulas without quotes. Spaces around math operators are optional. " +
                "Only the first matching branch is evaluated. If none matches and there is no else, the result is FALSE.\n\n" +
                "The short form =[if $counter<10] returns TRUE or FALSE. In an If expression block, " +
                "TRUE follows Yes and FALSE follows No. Keywords if, then, else_if and else remain English in every UI language. " +
                "Nested if chains inside one expression are not supported; connect blocks or calculate an intermediate value.\n\n" +
                "To return a result and create or update a variable at once, append => ${xxx} after ]: " +
                "=[if $counter<10 then \"small\" else \"large\"] => ${xxx}. Later blocks can read ${xxx}. " +
                @"Do not put & before =>. Use & after ${xxx} to append text: =[if 1 then 9 else 0] => ${xxx}& units \&. " +
                "The variable name uses A–Z, a–z, 0–9 and _, starting with a letter or _. " +
                "A malformed template makes no assignment.";
        if (title != "Смешанный текст и вычисления") return null;
        return "Fields are evaluated when a block runs. For a single result, use =($counter+2)*3. " +
            @"To mix math and text, use =[(${counter}+2)*3]& units \&. " +
            "When counter=1, the result is ‘9 units &’. Inside [] is an expression; outside, & joins parts and " +
            @"\& prints a literal ampersand. Plain text can use ${counter} for substitution without math." +
            "\n\nExpressions support +, -, *, /, % (remainder), ** or ^ (power), parentheses, " +
            "comparisons >, <, >=, <=, ==, !=. Logical AND is && or and; OR is ||, | or or; " +
            "XOR is xor; NOT is ! or not. Conditions return 1 or 0. Inside [], both $counter and " +
            "${counter} work. Use && for AND inside []; a single & joins parts only outside []." +
            "\n\nIn Choose option, each option line is evaluated separately. Put " +
            @"=[(${counter}+2)*3]& units \& on the first line, then 11 and 22 below. " +
            @"Press Enter between options in properties; graph code represents each break as \n inside one Options value. " +
            @"Windows \r\n is normalized to \n. Graph code writes the template escape \& as \\&. " +
            "Copy the graph on the Choose option help page to try it. Templates are limited to 8192 " +
            "characters; a malformed expression reports an error instead of silently showing a wrong result.";
    }
}

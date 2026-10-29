using System.Globalization;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed class VariableStore
{
    private static readonly Regex NamePattern = new(@"^[\p{L}_][\p{L}\p{Nd}_]*$", RegexOptions.Compiled);
    private static readonly Regex ReferencePattern = new(@"\$\{([\p{L}_][\p{L}\p{Nd}_]*)\}", RegexOptions.Compiled);
    private readonly bool _asciiOnly;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedDictionary<int, string>> _arrays = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _undefined = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly AsyncLocal<List<VariableChange>?> _trace = new();

    internal readonly record struct VariableChange(string Name, string? Value);

    internal sealed class TraceScope(VariableStore store, List<VariableChange>? previous,
        List<VariableChange>? changes) : IDisposable
    {
        public IReadOnlyList<VariableChange> Changes => changes ?? [];
        public void Dispose() => store._trace.Value = previous;
    }

    internal TraceScope BeginTrace(bool enabled)
    {
        var previous = _trace.Value;
        var changes = enabled ? new List<VariableChange>() : null;
        _trace.Value = changes;
        return new TraceScope(this, previous, changes);
    }

    private void Record(string name, string? value) => _trace.Value?.Add(new VariableChange(name, value));

    public VariableStore(bool asciiOnly = false) => _asciiOnly = asciiOnly;

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        lock (_gate) return new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
    }

    internal Dictionary<string, Dictionary<int, string>> SnapshotArrays()
    {
        lock (_gate) return _arrays.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToDictionary(item => item.Key, item => item.Value), StringComparer.OrdinalIgnoreCase);
    }

    internal void Restore(IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, Dictionary<int, string>> arrays)
    {
        foreach (var (name, value) in values) Set(name, value);
        foreach (var (name, items) in arrays)
            foreach (var (index, value) in items) SetArray(name, index, value);
    }

    public static bool HasReference(string text) => text.StartsWith('=') || text.Contains("\n=", StringComparison.Ordinal) ||
        ReferencePattern.IsMatch(text);

    public string Get(string name)
    {
        ValidateName(name);
        if (name.StartsWith('_') && SystemValue(name) is string system) return system;
        lock (_gate)
            return _values.TryGetValue(name, out var value) ? value
                : _undefined.Contains(name) ? "0"
                : throw new InvalidOperationException($"Переменная «{name}» ещё не задана.");
    }

    public string GetArray(string name, int index)
    {
        ValidateName(name);
        if (index < 0) throw new InvalidOperationException("Индекс массива не может быть отрицательным.");
        lock (_gate) return _arrays.TryGetValue(name, out var values) && values.TryGetValue(index, out var value) ? value : "0";
    }

    public void SetArray(string name, int index, string value)
    {
        ValidateName(name);
        if (index is < 0 or > 1000000) throw new InvalidOperationException("Индекс массива должен быть от 0 до 1000000.");
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values)) _arrays[name] = values = [];
            values[index] = value;
            Record($"{name}[{index}]", value);
        }
    }

    public int ArraySize(string name)
    {
        ValidateName(name);
        lock (_gate) return _arrays.TryGetValue(name, out var values) && values.Count > 0 ? values.Keys.Max() + 1 : 0;
    }

    public void ArrayPush(string name, string value)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values)) _arrays[name] = values = [];
            var next = values.Count == 0 ? 0 : values.Keys.Max() + 1;
            if (next > 1000000) throw new InvalidOperationException("Индекс массива не может превышать 1000000.");
            values[next] = value;
            Record($"{name}[{next}]", value);
        }
    }

    public void ReplaceArray(string name, IReadOnlyList<string> items)
    {
        ValidateName(name);
        if (items.Count > 10000) throw new InvalidOperationException("Массив списка ограничен 10000 элементами.");
        var replacement = new SortedDictionary<int, string>();
        for (var i = 0; i < items.Count; i++) replacement[i] = items[i];
        lock (_gate)
        {
            _arrays[name] = replacement;
            Record(name + "[]", $"[{items.Count}] " + string.Join(", ", items.Take(3)));
        }
    }

    public string[] SnapshotArray(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values) || values.Count == 0) return [];
            var size = values.Keys.Max() + 1;
            if (size > 10000) throw new InvalidOperationException("Запись массива в файл ограничена 10000 строками.");
            var snapshot = new string[size];
            for (var i = 0; i < size; i++) snapshot[i] = values.TryGetValue(i, out var value) ? value : "0";
            return snapshot;
        }
    }

    public string ArrayPop(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values) || values.Count == 0) return "0";
            var index = values.Keys.Max();
            var value = values[index];
            values.Remove(index);
            Record($"{name}[{index}]", null);
            return value;
        }
    }

    public void ArraySort(string name, bool numeric)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values)) return;
            var sorted = numeric
                ? values.Values.OrderBy(value => ParseNumber(value, "элемент массива")).ToArray()
                : values.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            values.Clear();
            for (var i = 0; i < sorted.Length; i++) values[i] = sorted[i];
            Record(name + "[]", $"[{sorted.Length}] " + string.Join(", ", sorted.Take(3)));
        }
    }

    public void ArraySortAuto(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_arrays.TryGetValue(name, out var values)) return;
            var numeric = values.Values.All(value => decimal.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out _));
            var sorted = numeric
                ? values.Values.OrderBy(value => decimal.Parse(value, CultureInfo.InvariantCulture)).ToArray()
                : values.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            values.Clear();
            for (var i = 0; i < sorted.Length; i++) values[i] = sorted[i];
            Record(name + "[]", $"[{sorted.Length}] " + string.Join(", ", sorted.Take(3)));
        }
    }

    public void ClearArray(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            _arrays.Remove(name);
            Record(name + "[]", null);
        }
    }

    public void Set(string name, string value)
    {
        ValidateName(name);
        lock (_gate)
        {
            _values[name] = value;
            _undefined.Remove(name);
            Record(name, value);
        }
    }

    public static string ReferenceName(string reference)
    {
        var name = reference.Trim();
        if (name.StartsWith('$')) name = name[1..];
        if (!NamePattern.IsMatch(name))
            throw new InvalidOperationException($"Недопустимое имя переменной «{name}».");
        return name;
    }

    public void SetExistingByReference(string reference, string value)
    {
        var name = ReferenceName(reference);
        ValidateName(name);
        lock (_gate)
        {
            if (!_values.ContainsKey(name))
                throw new InvalidOperationException($"SETVAR: переменная «{name}» ещё не создана.");
            _values[name] = value;
            Record(name, value);
        }
    }

    public void Define(string name, string value)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (_values.TryAdd(name, value)) Record(name, value);
            _undefined.Remove(name);
        }
    }

    public void Undefine(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            _values.Remove(name);
            _arrays.Remove(name);
            _undefined.Add(name);
            Record(name, null);
        }
    }

    public string Expand(string text) => ReferencePattern.Replace(text, match => Get(match.Groups[1].Value));

    public string Resolve(string text) => text.StartsWith("=[", StringComparison.Ordinal)
        ? TextTemplateEvaluator.Evaluate(text[1..], Evaluate, Expand, Set)
        : text.StartsWith('=') ? Evaluate(text[1..]) : Expand(text);

    public string ResolveLines(string text) => string.Join('\n', text.Split('\n').Select(Resolve));

    public string Evaluate(string expression) => ExpressionEvaluator.Evaluate(expression, (name, index) =>
        index is null ? Get(name) : GetArray(name, ArrayIndex(index)), ArraySize, ArrayPop);

    private static int ArrayIndex(string value)
    {
        var number = ParseNumber(value, "индекс массива");
        if (number != decimal.Truncate(number) || number < 0 || number > 1000000)
            throw new InvalidOperationException("Индекс массива должен быть целым числом от 0 до 1000000.");
        return (int)number;
    }

    private static string? SystemValue(string name)
    {
        var now = DateTime.Now;
        var position = System.Windows.Forms.Cursor.Position;
        return name.ToLowerInvariant() switch
        {
            "_xmouse" => position.X.ToString(CultureInfo.InvariantCulture),
            "_ymouse" => position.Y.ToString(CultureInfo.InvariantCulture),
            "_time_h" => now.Hour.ToString(CultureInfo.InvariantCulture),
            "_time_m" => now.Minute.ToString(CultureInfo.InvariantCulture),
            "_time_s" => now.Second.ToString(CultureInfo.InvariantCulture),
            "_date_y" => now.Year.ToString(CultureInfo.InvariantCulture),
            "_date_m" => now.Month.ToString(CultureInfo.InvariantCulture),
            "_date_d" => now.Day.ToString(CultureInfo.InvariantCulture),
            "_time_str" => now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            "_date_str" => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "_time_t" => DateTimeOffset.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            "_ms" => Environment.TickCount64.ToString(CultureInfo.InvariantCulture),
            "_xmin" => System.Windows.Forms.SystemInformation.VirtualScreen.Left.ToString(CultureInfo.InvariantCulture),
            "_ymin" => System.Windows.Forms.SystemInformation.VirtualScreen.Top.ToString(CultureInfo.InvariantCulture),
            "_xmax" => (System.Windows.Forms.SystemInformation.VirtualScreen.Right - 1).ToString(CultureInfo.InvariantCulture),
            "_ymax" => (System.Windows.Forms.SystemInformation.VirtualScreen.Bottom - 1).ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    public string Calculate(string name, string operation, string operandText)
    {
        ValidateName(name);
        lock (_gate)
        {
            var left = _values.TryGetValue(name, out var current) ? ParseNumber(current, name) : 0m;
            var right = ParseNumber(operandText, "операнд");
            var result = operation switch
            {
                "Сложить" => left + right,
                "Вычесть" => left - right,
                "Умножить" => left * right,
                "Разделить" when right != 0 => left / right,
                "Разделить" => throw new DivideByZeroException($"Деление переменной «{name}» на ноль."),
                _ => throw new InvalidOperationException($"Неизвестная операция с переменной: {operation}")
            };
            var value = result.ToString(CultureInfo.InvariantCulture);
            _values[name] = value;
            Record(name, value);
            return value;
        }
    }

    public bool Compare(string name, string comparison, string expected)
    {
        var actual = Get(name);
        if (comparison == "Содержит") return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
        if (comparison is "Равно" or "Не равно")
        {
            var equal = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            return comparison == "Равно" ? equal : !equal;
        }
        var left = ParseNumber(actual, name);
        var right = ParseNumber(expected, "значение сравнения");
        return comparison switch
        {
            "Больше" => left > right,
            "Не меньше" => left >= right,
            "Меньше" => left < right,
            "Не больше" => left <= right,
            _ => throw new InvalidOperationException($"Неизвестное сравнение переменной: {comparison}")
        };
    }

    private static decimal ParseNumber(string text, string label)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)) return number;
        throw new InvalidOperationException($"«{label}» должен содержать число, получено: «{text}».");
    }

    private void ValidateName(string name)
    {
        if (_asciiOnly ? !VariableNameRules.IsAscii(name) : !NamePattern.IsMatch(name))
            throw new InvalidOperationException($"Недопустимое имя переменной «{name}». Используйте только A–Z, a–z, 0–9 и _, начиная с буквы или _.");
    }
}

using System.Windows.Automation;

namespace KlikerBlocks;

internal sealed record UiElementSelector(string WindowTitle, string AutomationId, string Name, string ControlType)
{
    internal bool HasElementKey => !string.IsNullOrWhiteSpace(AutomationId) ||
        !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(ControlType);
}

internal sealed record UiElementDescription(string WindowTitle, string AutomationId, string Name,
    string ControlType);

internal static class UiAutomationTools
{
    internal static UiElementDescription Inspect(Point position)
    {
        var element = AutomationElement.FromPoint(new System.Windows.Point(position.X, position.Y));
        if (element is null) throw new InvalidOperationException("Элемент под указателем не найден.");
        var window = element;
        var walker = TreeWalker.ControlViewWalker;
        for (var i = 0; i < 30; i++)
        {
            if (window.Current.ControlType == ControlType.Window) break;
            var parent = walker.GetParent(window);
            if (parent is null || parent == AutomationElement.RootElement) break;
            window = parent;
        }
        return new UiElementDescription(window.Current.Name, element.Current.AutomationId,
            element.Current.Name, element.Current.ControlType.ProgrammaticName.Replace("ControlType.", ""));
    }

    internal static AutomationElement? Find(UiElementSelector selector)
    {
        if (!selector.HasElementKey)
            throw new InvalidOperationException("Укажите ID, имя или тип элемента Windows.");
        var handle = string.IsNullOrWhiteSpace(selector.WindowTitle)
            ? WindowTools.ForegroundHandle() : WindowTools.Find(selector.WindowTitle);
        if (handle == IntPtr.Zero) return null;
        var root = AutomationElement.FromHandle(handle);
        var conditions = new List<Condition>();
        if (!string.IsNullOrWhiteSpace(selector.AutomationId))
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, selector.AutomationId));
        if (!string.IsNullOrWhiteSpace(selector.Name))
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, selector.Name));
        if (!string.IsNullOrWhiteSpace(selector.ControlType))
        {
            var type = KnownType(selector.ControlType);
            if (type is null) throw new InvalidOperationException("Неизвестный тип элемента Windows.");
            conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        }
        var condition = conditions.Count == 1 ? conditions[0] : new AndCondition(conditions.ToArray());
        return root.FindFirst(TreeScope.Subtree, condition);
    }

    private static ControlType? KnownType(string text) => text.Trim().ToLowerInvariant() switch
    {
        "button" => ControlType.Button,
        "edit" => ControlType.Edit,
        "checkbox" => ControlType.CheckBox,
        "combobox" => ControlType.ComboBox,
        "listitem" => ControlType.ListItem,
        "menuitem" => ControlType.MenuItem,
        "text" => ControlType.Text,
        "window" => ControlType.Window,
        "pane" => ControlType.Pane,
        "treeitem" => ControlType.TreeItem,
        "hyperlink" => ControlType.Hyperlink,
        "dataitem" => ControlType.DataItem,
        "tabitem" => ControlType.TabItem,
        "radiobutton" => ControlType.RadioButton,
        _ => null
    };

    internal static string Act(AutomationElement element, string action, string value)
    {
        switch (action)
        {
            case "Нажать":
                if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
                    throw new InvalidOperationException("Элемент не поддерживает нажатие через UI Automation.");
                ((InvokePattern)invoke).Invoke(); return "1";
            case "Ввести текст":
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var setter))
                    throw new InvalidOperationException("Элемент не поддерживает ввод текста через UI Automation.");
                ((ValuePattern)setter).SetValue(value); return value;
            case "Переключить":
                if (!element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
                    throw new InvalidOperationException("Элемент не поддерживает переключение через UI Automation.");
                ((TogglePattern)toggle).Toggle(); return "1";
            case "Выбрать":
                if (!element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var select))
                    throw new InvalidOperationException("Элемент не поддерживает выбор через UI Automation.");
                ((SelectionItemPattern)select).Select(); return "1";
            case "Прочитать текст":
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var getter))
                    return ((ValuePattern)getter).Current.Value;
                return element.Current.Name;
            default: throw new InvalidOperationException("Неизвестное действие UI Automation.");
        }
    }
}

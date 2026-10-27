namespace KlikerBlocks;

internal static class MacroBuilder
{
    public static (Guid? First, int NodesAdded) AddToProject(ProjectModel project, MacroRecording recording)
    {
        if (recording.Events.Count == 0) return (null, 0);
        var startCount = project.Nodes.Count;
        BlockNode? previous = null;
        string previousOutput = "Далее";
        Guid? first = null;
        var sequence = 0;

        BlockNode Append(string kind)
        {
            var column = sequence % 5;
            var row = sequence / 5;
            var node = BlockCatalog.Create(kind, 110 + column * 235, 330 + row * 140);
            sequence++;
            project.Nodes.Add(node);
            if (previous != null) previous.Links[previousOutput] = node.Id;
            first ??= node.Id;
            previous = node;
            previousOutput = "Далее";
            return node;
        }

        foreach (var entry in recording.Events)
        {
            if (entry.DelayMs > 0)
                Append("Wait").Values["Milliseconds"] = Math.Clamp(entry.DelayMs, 0, 3_600_000).ToString();
            if (recording.Mode == "Клики и образы" && entry.Kind == "Click")
            {
                if (!string.IsNullOrWhiteSpace(entry.CapturePath))
                {
                    var image = Append("FindImage");
                    image.Values["Path"] = entry.CapturePath;
                    var desktop = SystemInformation.VirtualScreen;
                    var padding = Math.Max(0, recording.SearchPadding);
                    image.Values["Left"] = Math.Max(desktop.Left, entry.CaptureX - padding).ToString();
                    image.Values["Top"] = Math.Max(desktop.Top, entry.CaptureY - padding).ToString();
                    image.Values["Right"] = Math.Min(desktop.Right, entry.CaptureX + entry.CaptureWidth + padding).ToString();
                    image.Values["Bottom"] = Math.Min(desktop.Bottom, entry.CaptureY + entry.CaptureHeight + padding).ToString();
                    previousOutput = "Найден";
                }
                var move = Append("MoveCursor");
                move.Values["TargetMode"] = "Задать X/Y";
                move.Values["TargetX"] = entry.X.ToString();
                move.Values["TargetY"] = entry.Y.ToString();
                var click = Append("Click");
                click.Values["Button"] = entry.Button;
                continue;
            }

            switch (entry.Kind)
            {
                case "MouseMove":
                    Move(entry);
                    break;
                case "MouseDown":
                case "MouseUp":
                    Move(entry);
                    var button = Append("MouseButton");
                    button.Values["Button"] = entry.Button;
                    button.Values["Action"] = entry.Kind == "MouseDown" ? "Нажать" : "Отпустить";
                    break;
                case "Wheel":
                    Move(entry);
                    var wheel = Append("Wheel");
                    wheel.Values["Direction"] = entry.WheelSteps >= 0 ? "Вверх" : "Вниз";
                    wheel.Values["Steps"] = Math.Max(1, Math.Abs(entry.WheelSteps)).ToString();
                    break;
                case "KeyDown":
                case "KeyUp":
                    var key = Append("VirtualKey");
                    key.Values["VirtualKeyCode"] = entry.Key;
                    key.Values["Action"] = entry.Kind == "KeyDown" ? "Нажать" : "Отпустить";
                    break;
            }
        }
        return (first, project.Nodes.Count - startCount);

        void Move(MacroEvent entry)
        {
            var move = Append("MoveCursor");
            move.Values["TargetMode"] = "Задать X/Y";
            move.Values["TargetX"] = entry.X.ToString();
            move.Values["TargetY"] = entry.Y.ToString();
        }
    }
}

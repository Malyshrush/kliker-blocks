namespace KlikerBlocks;

// Opt-in Windows integration smoke. It opens and closes only windows bearing
// unique image names created by this run; normal --self-test remains headless.
internal static class ShowcaseColorSmoke
{
    public static void Run()
    {
        NativeMouse.EnableDpiAwareness();
        ApplicationConfiguration.Initialize();
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(900, 0), Size = new Size(260, 90),
            Text = "KLIKER Paint smoke test", TopMost = true
        };
        Exception? failure = null;
        host.Shown += async (_, _) =>
        {
            using var session = new ShowcaseSession(host, Console.WriteLine);
            try
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await session.OpenColorWindowsAsync(stop.Token);
                if (session.ColorWindows.Count != 4 ||
                    session.ColorWindows.Select(item => item.Window).Distinct().Count() != 4)
                    throw new InvalidOperationException("Paint did not create four distinct demo windows.");
                host.Activate();
                await Task.Delay(200, stop.Token);
                var values = new VariableStore(asciiOnly: true);
                values.Set("demo_color", "blue");
                await session.FindSelectedColorAsync(values, stop.Token);
                await Task.Delay(600, stop.Token);
                var blue = session.ColorWindows.Single(item => item.Color == "blue");
                if (WindowTools.GetWindow(blue.Window)?.Title.Contains(blue.Title,
                    StringComparison.OrdinalIgnoreCase) != true)
                    throw new InvalidOperationException("Selected blue Paint window disappeared.");
                if (session.ColorWindows.Where(item => item.Color != "blue").Any(item =>
                    WindowTools.GetWindow(item.Window)?.Title.Contains(item.Title,
                        StringComparison.OrdinalIgnoreCase) == true))
                    throw new InvalidOperationException("A nonselected demo Paint window remained open.");
                Console.WriteLine("Paint color-window smoke passed.");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                try { session.CloseDemoColorWindows(); }
                catch (Exception error) { failure ??= error; }
                host.Close();
            }
        };
        Application.Run(host);
        if (failure is not null) throw failure;
    }
}

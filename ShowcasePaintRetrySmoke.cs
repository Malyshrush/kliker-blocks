namespace KlikerBlocks;

// Opt-in live test: uses one new blank Paint window owned by this run.
internal static class ShowcasePaintRetrySmoke
{
    internal static void Run()
    {
        NativeMouse.EnableDpiAwareness();
        ApplicationConfiguration.Initialize();
        using var host = new Form { Text = "KLIKER Paint retry smoke", StartPosition = FormStartPosition.Manual,
            Location = new Point(0, 0), Size = new Size(200, 80) };
        Exception? failure = null;
        host.Shown += async (_, _) =>
        {
            using var session = new ShowcaseSession(host, Console.WriteLine);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try
            {
                var values = new VariableStore(asciiOnly: true);
                values.Set("demo_choice", "star");
                await session.ExecuteAsync("DrawChoice", values, stop.Token);
                values.Set("demo_choice", "heart");
                await session.ExecuteAsync("DrawChoice", values, stop.Token);
                Console.WriteLine("Paint retry smoke passed: star, Undo, heart.");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                try { await session.ClosePaintForSmokeAsync(stop.Token); }
                catch (Exception error) { failure ??= error; }
                host.Close();
            }
        };
        Application.Run(host);
        if (failure is not null) throw failure;
    }
}

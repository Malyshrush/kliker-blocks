using System.Diagnostics;

namespace KlikerBlocks;

// Opt-in integration check: opens one uniquely named Notepad file, types and verifies
// it, intentionally corrupts one character, then verifies whole-document recovery.
internal static class ShowcaseNotepadSmoke
{
    internal static void Run(bool withExistingDocument = false)
    {
        NativeMouse.EnableDpiAwareness();
        ApplicationConfiguration.Initialize();
        using var host = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(900, 0), Size = new Size(260, 90),
            Text = "Kliker Blocks Notepad smoke test"
        };
        Exception? failure = null;
        host.Shown += async (_, _) =>
        {
            using var session = new ShowcaseSession(host, Console.WriteLine);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            string? priorFile = null;
            string? priorTitle = null;
            try
            {
                if (withExistingDocument)
                {
                    Directory.CreateDirectory(ShowcaseState.DemoDirectory);
                    priorTitle = "Kliker-Blocks-prior-note-" + Guid.NewGuid().ToString("N")[..8];
                    priorFile = Path.Combine(ShowcaseState.DemoDirectory, priorTitle + ".txt");
                    File.WriteAllText(priorFile, "USER DOCUMENT — DO NOT CHANGE");
                    var info = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
                    info.ArgumentList.Add(priorFile);
                    _ = Process.Start(info) ?? throw new InvalidOperationException("Notepad did not start.");
                    for (var i = 0; i < 45 && !WindowTools.ListOpenWindows().Any(window =>
                             window.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase) &&
                             window.Title.Contains(priorTitle, StringComparison.OrdinalIgnoreCase)); i++)
                        await Task.Delay(100, stop.Token);
                    if (!WindowTools.ListOpenWindows().Any(window => window.Title.Contains(priorTitle,
                            StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Pre-existing Notepad document did not appear.");
                }
                var values = new VariableStore(asciiOnly: true);
                values.Set("demo_name", "Тест");
                await session.ExecuteAsync("OpenNotepad", values, stop.Token);
                await session.VerifyNotepadRecoverySmokeAsync(stop.Token);
                VerifyPromptTakesFocus(host, stop.Token);
                if (priorFile is not null && File.ReadAllText(priorFile) != "USER DOCUMENT — DO NOT CHANGE")
                    throw new InvalidOperationException("The pre-existing Notepad file was modified.");
                Console.WriteLine("Notepad typing and recovery smoke passed.");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                try { await session.CloseNotepadForSmokeAsync(stop.Token); }
                catch (Exception error) { failure ??= error; }
                if (priorTitle is not null)
                {
                    try { await CloseOwnPriorTabAsync(priorTitle, stop.Token); }
                    catch (Exception error) { failure ??= error; }
                }
                host.Close();
            }
        };
        Application.Run(host);
        if (failure is not null) throw failure;
    }

    private static void VerifyPromptTakesFocus(Form owner, CancellationToken cancel)
    {
        var notepad = WindowTools.ListOpenWindows().FirstOrDefault(window =>
            window.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase) &&
            window.Title.Contains("showcase-note-", StringComparison.OrdinalIgnoreCase));
        if (notepad is null) throw new InvalidOperationException("Demo Notepad window was not found for dialog focus check.");
        WindowTools.Activate(notepad.Handle);
        if (!WindowTools.IsForegroundHandle(notepad.Handle))
            throw new InvalidOperationException("Notepad did not become foreground before the dialog focus check.");
        bool focused = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                form.Text == UiLanguage.T("Kliker Blocks — ввод"));
            focused = dialog is not null && WindowTools.IsForegroundHandle(dialog.Handle) &&
                dialog.ActiveControl is TextBox;
            if (dialog is not null) { dialog.DialogResult = DialogResult.OK; dialog.Close(); }
        };
        timer.Start();
        _ = DialogTools.Input(owner, "Focus check", "test", 0, cancel, onlyOk: true);
        if (!focused) throw new InvalidOperationException("Input dialog did not receive keyboard focus after Notepad.");
        Console.WriteLine("Input dialog took keyboard focus from Notepad.");

        WindowTools.Activate(notepad.Handle);
        focused = false;
        using var radioTimer = new System.Windows.Forms.Timer { Interval = 250 };
        radioTimer.Tick += (_, _) =>
        {
            radioTimer.Stop();
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                form.Text == UiLanguage.T("Kliker Blocks — выбор"));
            focused = dialog is not null && WindowTools.IsForegroundHandle(dialog.Handle) &&
                dialog.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<RadioButton>())
                    .Any(radio => radio.Focused);
            if (dialog is not null) { dialog.DialogResult = DialogResult.OK; dialog.Close(); }
        };
        radioTimer.Start();
        _ = DialogTools.Radio(owner, "Choose a color", ["Red", "Blue"], cancel, onlyOk: true);
        if (!focused) throw new InvalidOperationException("Choice list did not receive keyboard focus after Notepad.");
        Console.WriteLine("Choice list took keyboard focus from Notepad.");
    }

    private static async Task CloseOwnPriorTabAsync(string title, CancellationToken cancel)
    {
        var target = WindowTools.ListOpenWindows().FirstOrDefault(window =>
            window.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase) &&
            window.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        if (target is null) return;
        using var lease = await DesktopInputCoordinator.AcquireAsync(cancel, Console.WriteLine);
        WindowTools.Activate(target.Handle);
        await Task.Delay(130, cancel);
        if (WindowTools.GetWindow(target.Handle)?.Title.Contains(title, StringComparison.OrdinalIgnoreCase) != true ||
            !WindowTools.IsForegroundHandle(target.Handle)) return;
        NativeMouse.KeyDown(0x11);
        try { NativeMouse.KeyDown(0x57); NativeMouse.KeyUp(0x57); }
        finally { NativeMouse.KeyUp(0x11); }
    }
}

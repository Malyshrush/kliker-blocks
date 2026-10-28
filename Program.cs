namespace KlikerBlocks;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            SelfTest.Run();
            return;
        }
        if (args.Contains("--scheduler-smoke"))
        {
            UiLanguage.Configure("en");
            var scenario = Path.Combine(Path.GetTempPath(), "KlikerBlocks-schedule-smoke-" + Guid.NewGuid().ToString("N") + ".klb");
            File.WriteAllText(scenario, System.Text.Json.JsonSerializer.Serialize(ProjectModel.CreateSample()));
            var schedule = new ScenarioSchedule { ProjectPath = scenario,
                StartLocal = DateTime.Now.AddDays(1), Repeat = "Once" };
            try
            {
                ScenarioScheduling.Register(schedule, Application.ExecutablePath);
                Console.WriteLine("Scheduled task registered: " + ScenarioScheduling.TaskName(schedule.Id));
            }
            finally
            {
                try { ScenarioScheduling.Delete(schedule.Id); }
                finally { File.Delete(scenario); }
            }
            Console.WriteLine("Scheduled task removed.");
            return;
        }
        if (args.Contains("--help-preview"))
        {
            ApplicationConfiguration.Initialize();
            UiLanguage.Configure("ru");
            Application.Run(new HelpForm("ChoiceDialog"));
            return;
        }
        if (args.Contains("--automation-preview"))
        {
            ApplicationConfiguration.Initialize();
            UiLanguage.Configure("ru");
            Application.Run(new ScenarioAutomationForm(new AppSettings()));
            return;
        }
        if (args.Contains("--support-preview"))
        {
            ApplicationConfiguration.Initialize();
            UiLanguage.Configure("ru");
            Application.Run(new SupportForm());
            return;
        }
        if (args.Contains("--language-preview") || args.Contains("--ui-preview"))
        {
            NativeMouse.EnableDpiAwareness();
            ApplicationConfiguration.Initialize();
            UiLanguage.Configure("ru");
            if (args.Contains("--language-preview")) Application.Run(new FirstRunLanguageForm());
            else Application.Run(new MainForm(uiPreview: true));
            return;
        }
        if (args.Contains("--beep-smoke"))
        {
            AudioTools.Beep(523, 30);
            Console.WriteLine("System beep smoke passed.");
            return;
        }
        if (args.Contains("--showcase-color-smoke"))
        {
            ShowcaseColorSmoke.Run();
            return;
        }
        if (args.Contains("--showcase-paint-retry-smoke"))
        {
            ShowcasePaintRetrySmoke.Run();
            return;
        }
        if (args.Contains("--showcase-notepad-smoke"))
        {
            ShowcaseNotepadSmoke.Run();
            return;
        }
        if (args.Contains("--showcase-notepad-existing-smoke"))
        {
            ShowcaseNotepadSmoke.Run(withExistingDocument: true);
            return;
        }
        if (args.Contains("--localization-audit"))
        {
            SelfTest.AuditLocalization();
            return;
        }
        if (args.Length == 2 && args[0] == "--export-help-en")
        {
            var titles = new[] { "Общие сведения", "Панель, пауза и трей", "Запуск и остановка",
                "Автозапуск, расписание и обновления", "Выделение тонких связей",
                "Демонстрация Что я умею", "Окна и точность демонстрации", "Визуальные блоки и связи",
                "Текстовый код Kliker Blocks", "Переменные и массивы", "Журнал и запись в файл",
                "Снимки и образы", "Запись макросов", "Слежение за курсором",
                "Индикатор сценария", "Профили и назначения мыши", "Перевод текста и языковые модели",
                "Установка модели Argos", "Возможности компьютера и голоса", "Коды клавиш VK (1–255)" };
            var export = new
            {
                overview = EnglishHelp.Overview,
                sections = titles.Where(title => EnglishHelp.Section(title) is not null)
                    .ToDictionary(title => title, title => EnglishHelp.Section(title)!),
                blocks = BlockCatalog.All.ToDictionary(spec => spec.Kind,
                    spec => EnglishHelp.Block(spec.Kind) ?? "")
            };
            File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(export));
            return;
        }
        if (args.Contains("--speech-smoke"))
        {
            Console.WriteLine("Windows voices: " + string.Join(", ", WindowsSpeech.InstalledVoices()
                .Select(voice => voice.Name + " [" + voice.Language + "]")));
            var russianWave = WindowsSpeech.RenderModernWaveAsync("Привет мой друг",
                "", 0, 100, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("Russian wave bytes: " + russianWave.Length +
                ", header: " + Convert.ToHexString(russianWave.AsSpan(0, Math.Min(12, russianWave.Length))));
            WindowsSpeech.SpeakAsync("KLIKER speech test", "", 0, 0, CancellationToken.None)
                .GetAwaiter().GetResult();
            var longWave = WindowsSpeech.RenderModernWaveAsync(
                string.Concat(Enumerable.Repeat("KLIKER speech cancellation test. ", 12)),
                "", -5, 0, CancellationToken.None).GetAwaiter().GetResult();
            using var stop = new CancellationTokenSource();
            var longSpeech = WindowsSpeech.PlayWaveAsync(longWave, stop.Token);
            Thread.Sleep(200);
            stop.Cancel();
            try
            {
                longSpeech.GetAwaiter().GetResult();
                throw new InvalidOperationException("Speech cancellation did not stop playback.");
            }
            catch (OperationCanceledException) { }
            Console.WriteLine("Speech test passed (volume 0).");
            return;
        }
        if (args.Contains("--speech-audible-smoke"))
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            WindowsSpeech.SpeakAsync("Привет, это проверка KLIKER", "", 0, 100,
                CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("Russian speech played for " + timer.ElapsedMilliseconds + " ms.");
            return;
        }

        NativeMouse.EnableDpiAwareness();
        ApplicationConfiguration.Initialize();
        Application.Idle += (_, _) => UiLanguage.ApplyToOpenForms();
        LanguageSession? session = null;
        var capabilitiesInitialized = false;
        var launchHandled = false;
        while (true)
        {
            var settings = AppSettings.Load();
            if (!settings.InitialLanguageChosen)
            {
                using var choice = new FirstRunLanguageForm();
                if (choice.ShowDialog() != DialogResult.OK || choice.SelectedLanguageCode is null) return;
                settings.UiLanguageCode = choice.SelectedLanguageCode;
                settings.InitialLanguageChosen = true;
                try { settings.Save(); }
                catch (Exception error)
                {
                    MessageBox.Show("Could not save the language choice / Не удалось сохранить выбор языка:\n" +
                        error.Message, choice.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                continue; // Recreate the application UI from the saved language before showing any graph.
            }
            UiLanguage.Configure(settings.UiLanguageCode);
            var launch = launchHandled ? null : ScenarioScheduling.Resolve(args, settings);
            if (!launchHandled && args.Contains("--scheduled-run") && launch is null) return;
            if (!capabilitiesInitialized)
            {
                ComputerCapabilities.Initialize();
                capabilitiesInitialized = true;
            }
            using var form = new MainForm(session, launch: launch);
            Application.Run(form);
            launchHandled = true;
            session = form.PendingLanguageSession;
            if (session is null) break;
        }
    }
}

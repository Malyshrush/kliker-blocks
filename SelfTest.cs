using System.Drawing;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Reflection;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class SelfTest
{
    public static void AuditLocalization()
    {
        var terms = BlockCatalog.All.SelectMany(spec => new[] { spec.Title, spec.Hint }
            .Concat(spec.Outputs)
            .Concat(spec.Fields.SelectMany(field => new[] { field.Label, field.Default }.Concat(field.Options ?? []))))
            .Where(value => value.Any(ch => ch is >= '\u0400' and <= '\u04FF'))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
        {
            UiLanguage.Configure(code);
            var missing = terms.Where(value => !UiLanguage.Has(value)).ToArray();
            Console.WriteLine($"{code}: {missing.Length} of {terms.Length} block UI strings missing");
            foreach (var value in missing) Console.WriteLine("  " + value);
        }
        UiLanguage.Configure("ru");
    }

    public static void Run()
    {
        UiLanguage.Configure("ru");
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        var expectedTitle = AppIdentity.TitleFor(assemblyVersion);
        Assert(MainForm.AppTitle == expectedTitle && AppIdentity.Title == expectedTitle &&
            AppIdentity.TitleFor(new Version(2, 3)) == "Kliker Blocks v2.3" &&
            AppIdentity.TitleFor(new Version(2, 3, 4)) == "Kliker Blocks v2.3.4",
            "main-window title follows the running assembly version");
        Assert(AutostartSettings.CommandFor(@"C:\Program Files\Kliker Blocks\Kliker.Blocks.exe") ==
            "\"C:\\Program Files\\Kliker Blocks\\Kliker.Blocks.exe\"",
            "autostart command quotes executable paths containing spaces");
        using (var languageChoice = new FirstRunLanguageForm())
        {
            languageChoice.Choose("ru");
            Assert(languageChoice.SelectedLanguageCode == "ru" && languageChoice.DialogResult == DialogResult.OK,
                "first-run language chooser accepts Russian");
        }
        using (var languageChoice = new FirstRunLanguageForm())
        {
            languageChoice.Choose("en");
            Assert(languageChoice.SelectedLanguageCode == "en" && languageChoice.DialogResult == DialogResult.OK,
                "first-run language chooser accepts English");
        }
        using (var languageChoice = new FirstRunLanguageForm())
        {
            languageChoice.Opacity = 0;
            languageChoice.StartPosition = FormStartPosition.Manual;
            languageChoice.Location = new Point(-10000, -10000);
            languageChoice.Show();
            var choices = languageChoice.Controls.OfType<TableLayoutPanel>().Single()
                .Controls.OfType<TableLayoutPanel>().Single();
            Assert(choices.Controls.OfType<Button>().Count() == 2 &&
                choices.Controls.OfType<Button>().All(button => button.Height >= 48),
                "first-run language buttons have enough height for their labels");
            languageChoice.Close();
        }
        TestMouseMappings();
        TestDialogWindowOptions();
        TestSupportLinks();
        TestLanguagePacksAndNames();
        TestDisplayedBranding();
        TestInstalledProgramCatalog();
        TestHelpGraphExamples();
        TestEditorInsertionAndMove();
        TestGraphAppearanceAndRouting();
        TestNewAutomation();
        TestUiAutomationLive();
        TestEnglishMainWindow();
        Console.WriteLine("UI and editor tests passed.");
        TestEnglishHelpCoverage();
        TestOtherHelpCoverage();
        TestSpeakBlock();
        TestSystemBeep();
        TestComputerCapabilities();
        TestArgosPackageAndHelpChunks();
        TestBundledTranslation().GetAwaiter().GetResult();
        Console.WriteLine("Language and speech tests passed.");
        TestJournalTracing();
        Console.WriteLine("Journal tests passed.");
        Task.Run(TestTranslation).GetAwaiter().GetResult();
        Console.WriteLine("Translation tests passed.");
        TestShowcaseGraph();
        Console.WriteLine("Showcase graph tests passed.");
        TestScenarioPlayback();
        Console.WriteLine("Scenario tests passed.");
        using (var statusIcon = TrayStatusIcon.Create(SystemIcons.Application, Color.LimeGreen))
            Assert(statusIcon.Width == 16 && statusIcon.Height == 16, "tray status icon has valid size");
        Assert(HelpForm.MatchesSearch("Найти изображение FindImage Tolerance", ["изображение", "tolerance"]) &&
            !HelpForm.MatchesSearch("Найти изображение FindImage", ["изображение", "window"]),
            "sectioned help searches block descriptions and field names case-insensitively");
        var center = new Point(960, 540);
        var moved = new Point(980, 550);
        Assert(CursorMath.IsOutside(moved, center, 3), "displacement");
        Assert(!CursorMath.IsOutside(new Point(961, 541), center, 3), "threshold");
        Assert(CursorMath.Target("Центр монитора", moved, center, center, 0, 0, 0, 0) == center, "center target");
        Assert(CursorMath.Target("Задать X/Y", moved, center, center, 30, 40, 0, 0) == new Point(30, 40), "absolute target");
        Assert(CursorMath.Target("От смещённой точки", moved, center, center, 0, 0, -20, -10) == center, "relative target");
        Assert(CursorMath.Target("От опоры", moved, center, center, 0, 0, 100, -50) == new Point(1060, 490), "anchor offset");
        Assert(IndicatorConditionMode.Resolve(IndicatorConditionMode.Normal, true) == true &&
            IndicatorConditionMode.Resolve(IndicatorConditionMode.Normal, false) == false &&
            IndicatorConditionMode.Resolve(IndicatorConditionMode.Inverted, true) == false &&
            IndicatorConditionMode.Resolve(IndicatorConditionMode.Inverted, false) == true &&
            IndicatorConditionMode.Resolve(IndicatorConditionMode.Ignore, true) is null &&
            IndicatorConditionMode.DefaultFor("IfKeyDown") == IndicatorConditionMode.Normal &&
            IndicatorConditionMode.DefaultFor("IfVariable") == IndicatorConditionMode.Ignore,
            "indicator condition modes map both branches and opt-out");
        var configuredCondition = BlockCatalog.Create("IfKeyDown", 0, 0);
        configuredCondition.Values["IndicatorMode"] = IndicatorConditionMode.Inverted;
        Assert(JsonSerializer.Deserialize<BlockNode>(JsonSerializer.Serialize(configuredCondition))!
            .Value("IndicatorMode") == IndicatorConditionMode.Inverted,
            "indicator mode survives project serialization");
        var project = ProjectModel.CreateSample();
        var copy = JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(project))!;
        Assert(copy.Nodes.Count == 2 && copy.Nodes[0].Link("Далее") == copy.Nodes[1].Id, "project round trip");
        var namedImport = CodeImporter.Convert("#name \"Сценарий 1\"\nprint(\"ready\")", Point.Empty);
        Assert(namedImport.ScriptName == "Сценарий 1" && namedImport.Nodes.Any(n => n.Kind == "Log"),
            "#name imports script title without dropping commands");
        project.ScriptName = namedImport.ScriptName!;
        Assert(JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(project))!.ScriptName == "Сценарий 1",
            "script name survives project serialization");
        var graphTextProject = ProjectModel.CreateSample();
        var graphLog = BlockCatalog.Create("Log", 600, -50);
        graphLog.Values["Text"] = "Первая строка\n\"вторая\"";
        graphTextProject.Nodes.Add(graphLog);
        graphTextProject.Nodes[1].Links["Далее"] = graphLog.Id;
        graphLog.Links["Далее"] = graphTextProject.Nodes[1].Id;
        var graphCode = GraphCodeCodec.Serialize(graphTextProject);
        var graphParsed = GraphCodeCodec.Parse(graphCode);
        Assert(GraphCodeCodec.Serialize(new ProjectModel { Nodes = graphParsed }) == graphCode &&
            graphParsed[2].Value("Text") == graphLog.Value("Text") &&
            graphParsed[2].Link("Далее") == graphParsed[1].Id,
            "visual graph ↔ KLIKER code round trip preserves values, positions, IDs and cycles");
        var handWrittenGraph = GraphCodeCodec.Parse("block begin Start 0 0\n link \"Далее\" -> work\nend\nblock work Log 200 0\n value \"Text\" = \"ручной код\"\nend");
        Assert(handWrittenGraph.Count == 2 && handWrittenGraph[0].Link("Далее") == handWrittenGraph[1].Id &&
            handWrittenGraph[1].Value("Text") == "ручной код", "hand-written KLIKER code creates connected blocks");
        try
        {
            GraphCodeCodec.Parse("block begin Start 0 0\n link \"Далее\" -> missing\nend");
            throw new Exception("Self-test failed: unknown graph target was accepted");
        }
        catch (GraphCodeException error) { Assert(error.LineNumber == 2, "invalid code reports link line"); }
        var exampleDirectory = Path.Combine(AppContext.BaseDirectory, "Examples");
        if (Directory.Exists(exampleDirectory))
        {
            foreach (var example in Directory.GetFiles(exampleDirectory, "*.klb"))
            {
                var exampleProject = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(example))!;
                var textGraph = GraphCodeCodec.Serialize(exampleProject);
                Assert(GraphCodeCodec.Serialize(new ProjectModel { Nodes = GraphCodeCodec.Parse(textGraph) }) == textGraph,
                    "text graph round trip for example " + Path.GetFileName(example));
            }
        }
        try
        {
            CodeImporter.Convert("#name \"one\"\n#name \"two\"\nprint(1)", Point.Empty);
            throw new Exception("Self-test failed: duplicate #name was accepted");
        }
        catch (CodeImportException error) { Assert(error.LineNumber == 2, "duplicate #name rejects"); }
        Assert(!copy.WindowScope.Enabled && WindowScopeGuard.Validate(copy.WindowScope) is null,
            "window binding is optional and disabled in existing projects");
        var pauseStart = BlockCatalog.Create("Start", 0, 0);
        var pauseLog = BlockCatalog.Create("Log", 200, 0);
        pauseLog.Values["Text"] = "continued";
        pauseStart.Links["Далее"] = pauseLog.Id;
        var pauseMessages = new ConcurrentQueue<string>();
        var pauseRunner = new Runner(new ProjectModel { Nodes = [pauseStart, pauseLog] },
            pauseMessages.Enqueue, (_, _) => { });
        pauseRunner.SetPaused(true);
        var pausedTask = pauseRunner.RunAsync(CancellationToken.None);
        Assert(!pausedTask.Wait(80) && !pauseMessages.Contains("continued"),
            "paused scenario does not advance to the next block");
        pauseRunner.SetPaused(false);
        Assert(pausedTask.Wait(2000) && pauseMessages.Contains("continued"),
            "scenario resumes after pause");
        using (var pauseCancel = new CancellationTokenSource())
        {
            var cancelRunner = new Runner(new ProjectModel { Nodes = [pauseStart, pauseLog] },
                _ => { }, (_, _) => { });
            cancelRunner.SetPaused(true);
            var cancelTask = cancelRunner.RunAsync(pauseCancel.Token);
            pauseCancel.Cancel();
            try
            {
                cancelTask.GetAwaiter().GetResult();
                throw new Exception("Self-test failed: stop did not cancel paused scenario");
            }
            catch (OperationCanceledException) { }
        }
        var scopeSettings = new WindowScopeSettings { Enabled = true, MatchMode = "Процесс", ProcessName = "WINWORD" };
        Assert(WindowScopeGuard.Validate(scopeSettings) is null &&
            WindowScopeGuard.Validate(new WindowScopeSettings { Enabled = true, MatchMode = "Процесс" }) is not null,
            "enabled window binding requires a target");
        using (var targetAppears = new ManualResetEventSlim(false))
        using (var scopeCancel = new CancellationTokenSource())
        {
            var activated = false;
            var scopeEvents = new ConcurrentQueue<string>();
            var guard = new WindowScopeGuard(scopeSettings, scopeEvents.Enqueue,
                _ => targetAppears.IsSet ? new IntPtr(123) : IntPtr.Zero,
                _ => activated, _ => activated = true);
            // Keep the polling task off the WinForms synchronization context while this STA test waits.
            var ready = Task.Run(() => guard.EnsureReadyAsync(scopeCancel.Token));
            Assert(!ready.Wait(50), "bound scenario waits while target window is absent");
            targetAppears.Set();
            Assert(ready.Wait(3000) && activated && scopeEvents.Any(message => message.Contains("ожидает")),
                "bound scenario activates opened target and resumes");
        }
        var focusRequested = false;
        var passiveGuard = new WindowScopeGuard(scopeSettings, _ => { },
            _ => new IntPtr(321), _ => focusRequested, _ => focusRequested = true);
        passiveGuard.WaitForOpenAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!focusRequested && passiveGuard.TryFocusAsync(CancellationToken.None).GetAwaiter().GetResult() && focusRequested,
            "window presence check does not steal focus before the input turn");
        var savedScope = new ProjectModel { WindowScope = scopeSettings };
        Assert(JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(savedScope))!.WindowScope.ProcessName == "WINWORD",
            "window binding persists with project");
        using (var targetWindow = new Form { Text = "KLIKER scope test " + Guid.NewGuid().ToString("N"),
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-1800, -1800) })
        {
            targetWindow.Show();
            Application.DoEvents();
            var byTitle = new WindowScopeSettings { Enabled = true, MatchMode = "Заголовок",
                TitleContains = targetWindow.Text };
            Assert(WindowTools.FindTarget(byTitle) == targetWindow.Handle,
                "window can be resolved by title");
            var candidate = WindowTools.GetWindow(targetWindow.Handle)!;
            var byHandle = new WindowScopeSettings { Enabled = true, MatchMode = "HWND",
                WindowHandle = targetWindow.Handle.ToInt64(), ProcessId = candidate.ProcessId };
            Assert(WindowTools.FindTarget(byHandle) == targetWindow.Handle,
                "window can be resolved by HWND and process identity");
            targetWindow.Close();
        }
        project.UseCustomIndicatorPosition = true;
        project.IndicatorX = 30;
        project.IndicatorY = 40;
        var otherInstanceProject = project.Copy();
        otherInstanceProject.IndicatorX = 310;
        project.IndicatorAppearance.Width = 36;
        project.IndicatorAppearance.Height = 36;
        project.IndicatorAppearance.ShowTitle = false;
        project.IndicatorAppearance.ShowDetail = false;
        otherInstanceProject.IndicatorAppearance.Width = 245;
        otherInstanceProject.IndicatorAppearance.Symbol = "Квадрат";
        otherInstanceProject.IndicatorToggleHotkey = "Ctrl+Shift+O";
        Assert(project.IndicatorX == 30 && otherInstanceProject.IndicatorX == 310 &&
            otherInstanceProject.Copy().IndicatorY == 40, "independent per-project indicator coordinates");
        Assert(project.IndicatorAppearance.Width == 36 && otherInstanceProject.IndicatorAppearance.Width == 245 &&
            project.IndicatorAppearance.Symbol == "Круг" && otherInstanceProject.IndicatorAppearance.Symbol == "Квадрат" &&
            otherInstanceProject.Copy().IndicatorToggleHotkey == "Ctrl+Shift+O", "independent serialized indicator appearance and hotkey");
        using (var firstIndicator = new ScenarioIndicatorForm())
        using (var secondIndicator = new ScenarioIndicatorForm())
        {
            firstIndicator.SetAppearance(project.IndicatorAppearance);
            secondIndicator.SetAppearance(otherInstanceProject.IndicatorAppearance);
            firstIndicator.SetPlacement(project);
            secondIndicator.SetPlacement(otherInstanceProject);
            Assert(firstIndicator.Location == new Point(30, 40) && firstIndicator.Size == new Size(36, 36) &&
                secondIndicator.Location == new Point(310, 40) && secondIndicator.Size == new Size(245, 62),
                "two independent indicator window positions and sizes");
        }
        var imageAppearance = new IndicatorAppearance { Symbol = "Изображение", ImagePath = "C:\\removed-image.png" };
        using (var appearanceDialog = new IndicatorAppearanceForm(imageAppearance, "Ctrl+Shift+I", null))
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var field in new[] { "_runningVisual", "_activeVisual", "_inactiveVisual" })
                ((IndicatorVisualChoice)typeof(IndicatorAppearanceForm).GetField(field, flags)!.GetValue(appearanceDialog)!).ImagePath = "";
            typeof(IndicatorAppearanceForm).GetMethod("SaveValues", flags)!.Invoke(appearanceDialog, null);
            Assert(appearanceDialog.DialogResult == DialogResult.OK && appearanceDialog.Appearance.ImagePath == "" &&
                appearanceDialog.Appearance.Symbol == "Круг" && appearanceDialog.Appearance.ActiveImagePath == "",
                "clearing former images can be applied");
            var layout = appearanceDialog.Controls.OfType<TableLayoutPanel>().Single();
            Assert(layout.RowStyles[1].SizeType == SizeType.Absolute &&
                layout.GetControlFromPosition(0, 1) is FlowLayoutPanel footer &&
                footer.Controls.OfType<Button>().Any(button => button.Text == "Применить"),
                "appearance buttons stay outside scrolling tabs");
        }
        var shapes = new[] { "Прямоугольник", "Квадрат", "Круг", "Овал", "Скруглённый прямоугольник",
            "Скруглённый квадрат", "Капсула", "Ромб", "Звезда", "Шестиугольник" };
        Assert(shapes.All(ScenarioIndicatorForm.WindowShapes.Contains), "all requested window shapes available");
        Assert(new IndicatorAppearance { Width = 220, Height = 70, WindowShape = "Круг" }.EffectiveSize() == new Size(70, 70),
            "circle window has equal sides");
        var dominantAppearance = new IndicatorAppearance { Width = 60, Height = 60, WindowShape = "Круг",
            Symbol = "Круг", SymbolSize = 100, BackgroundColor = "#FFFFFF", RunningColor = "#FF0000",
            ShowTitle = false, ShowDetail = false };
        Assert(dominantAppearance.SymbolDominates() && dominantAppearance.EffectiveSize() == new Size(100, 100) &&
            dominantAppearance.EffectiveWindowShape() == "Круг", "oversized symbol defines window size and shape");
        dominantAppearance.Symbol = "Ромб";
        Assert(dominantAppearance.EffectiveWindowShape() == "Ромб",
            "oversized diamond determines window shape even when configured window is round");
        dominantAppearance.Symbol = "Круг";
        using (var dominant = new ScenarioIndicatorForm())
        {
            dominant.SetAppearance(dominantAppearance);
            using var bitmap = dominant.RenderBitmap();
            Assert(bitmap.Width == 100 && bitmap.Height == 100 && bitmap.GetPixel(50, 50).R > 200 &&
                bitmap.GetPixel(50, 50).G < 70 && bitmap.GetPixel(50, 10).G < 70,
                "oversized symbol fills window without white background ring");
        }
        using (var translucent = new ScenarioIndicatorForm())
        {
            translucent.SetAppearance(new IndicatorAppearance { Width = 100, Height = 100,
                WindowShape = "Круг", SymbolSize = 20, BackgroundOpacityPercent = 0,
                RunningOpacityPercent = 50, RunningColor = "#FF0000", ShowTitle = false, ShowDetail = false });
            using var bitmap = translucent.RenderBitmap();
            Assert(bitmap.GetPixel(15, 50).A == 0 && bitmap.GetPixel(50, 50).A is >= 120 and <= 130,
                "background can be fully transparent while state symbol is half transparent");
            translucent.Location = new Point(-1800, -1800);
            translucent.Show();
            Application.DoEvents();
            translucent.Hide();
        }
        using (var stateOpacities = new ScenarioIndicatorForm())
        {
            stateOpacities.SetAppearance(new IndicatorAppearance { Width = 100, Height = 100,
                WindowShape = "Круг", SymbolSize = 20, BackgroundOpacityPercent = 0,
                RunningOpacityPercent = 100, ActiveOpacityPercent = 0, InactiveOpacityPercent = 75,
                ShowTitle = false, ShowDetail = false });
            stateOpacities.SetConditions(true, "");
            using var activeBitmap = stateOpacities.RenderBitmap();
            Assert(activeBitmap.GetPixel(50, 50).A == 0, "active state can be fully transparent");
            stateOpacities.SetConditions(false, "");
            using var waitingBitmap = stateOpacities.RenderBitmap();
            Assert(waitingBitmap.GetPixel(50, 50).A is >= 185 and <= 195,
                "waiting state has independent 75 percent opacity");
        }
        var opacityAppearance = new IndicatorAppearance { BackgroundOpacityPercent = 0,
            RunningOpacityPercent = 25, ActiveOpacityPercent = 50, InactiveOpacityPercent = 75 };
        var opacityCopy = JsonSerializer.Deserialize<IndicatorAppearance>(JsonSerializer.Serialize(opacityAppearance))!;
        Assert(opacityCopy.BackgroundOpacityPercent == 0 && opacityCopy.RunningOpacityPercent == 25 &&
            opacityCopy.ActiveOpacityPercent == 50 && opacityCopy.InactiveOpacityPercent == 75,
            "background and state opacities persist in project");
        var visualSettings = new IndicatorAppearance { WindowShape = "Звезда", BackgroundMode = "Картинка",
            BackgroundImagePath = "background.png", ActiveMode = "Картинка", ActiveImagePath = "active.png",
            ShowRunningTitle = false, ShowActiveTitle = true, ShowInactiveTitle = false, ShowDetail = false };
        var visualCopy = JsonSerializer.Deserialize<IndicatorAppearance>(JsonSerializer.Serialize(visualSettings))!;
        Assert(visualCopy.WindowShape == "Звезда" && visualCopy.BackgroundImagePath == "background.png" &&
            visualCopy.ActiveImagePath == "active.png" && !visualCopy.ShowRunningTitle && !visualCopy.ShowDetail,
            "window shape, independent images and text visibility serialize");
        using (var shaped = new ScenarioIndicatorForm())
        {
            shaped.SetAppearance(new IndicatorAppearance { Width = 100, Height = 100, WindowShape = "Круг" });
            Assert(shaped.Region is not null && !shaped.Region.IsVisible(0, 0) && shaped.Region.IsVisible(50, 50),
                "window corners are clipped rather than filled");
            shaped.SetWatch(new CursorWatchState(true, false, "Переключатель", "F9", "Левая кнопка мыши", center));
            Assert(shaped.DisplayState == 1, "F9 armed state is shown active before LMB");
            shaped.SetWatch(new CursorWatchState(true, true, "Переключатель", "F9", "Левая кнопка мыши", center));
            Assert(shaped.DisplayState == 1, "LMB does not change armed indicator");
            shaped.SetWatch(new CursorWatchState(false, false, "Переключатель", "F9", "Левая кнопка мыши", center));
            Assert(shaped.DisplayState == 2, "second F9 press shows inactive");
            shaped.SetConditions(false, "SHIFT + LMB: ожидание");
            Assert(shaped.DisplayState == 2, "key conditions waiting shows inactive");
            shaped.SetConditions(true, "SHIFT + LMB: нажаты");
            Assert(shaped.DisplayState == 1, "all key conditions satisfied shows active");
        }
        foreach (var windowShape in ScenarioIndicatorForm.WindowShapes)
        {
            using var shaped = new ScenarioIndicatorForm();
            shaped.SetAppearance(new IndicatorAppearance { Width = 120, Height = 90, WindowShape = windowShape });
            Assert(shaped.Region is not null && shaped.Region.IsVisible(shaped.Width / 2, shaped.Height / 2),
                "renderable window region: " + windowShape);
        }
        using (var visualChoice = new IndicatorVisualChoice("Проверка", "Картинка", "#FF0000", ""))
        {
            visualChoice.CreateControl();
            visualChoice.PerformLayout();
            var browse = (Button)typeof(IndicatorVisualChoice).GetField("_browse", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visualChoice)!;
            Assert(browse.Width >= 90 && browse.Height >= 25 && browse.Right <= browse.Parent!.ClientSize.Width,
                "image Browse button is fully inside its row");
            visualChoice.ValidateImage(null); // An empty image path is optional even in image mode.
        }
        var probeImage = Path.Combine(Path.GetTempPath(), "KLIKER-indicator-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.SetPixel(0, 0, Color.Aqua);
                bitmap.Save(probeImage, System.Drawing.Imaging.ImageFormat.Png);
            }
            using var imageIndicator = new ScenarioIndicatorForm();
            imageIndicator.SetAppearance(new IndicatorAppearance { BackgroundMode = "Картинка",
                BackgroundImagePath = probeImage, ActiveMode = "Картинка", ActiveImagePath = probeImage });
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert(typeof(ScenarioIndicatorForm).GetField("_backgroundImage", flags)!.GetValue(imageIndicator) is Bitmap &&
                typeof(ScenarioIndicatorForm).GetField("_activeImage", flags)!.GetValue(imageIndicator) is Bitmap,
                "background and active-state images load independently");
        }
        finally { if (File.Exists(probeImage)) File.Delete(probeImage); }
        var start = BlockCatalog.Create("Start", 0, 0);
        var parallel = BlockCatalog.Create("Parallel", 0, 0);
        var one = BlockCatalog.Create("Log", 0, 0);
        var two = BlockCatalog.Create("Log", 0, 0);
        var endOne = BlockCatalog.Create("BranchEnd", 0, 0);
        var endTwo = BlockCatalog.Create("BranchEnd", 0, 0);
        var finish = BlockCatalog.Create("Stop", 0, 0);
        one.Values["Text"] = "A";
        two.Values["Text"] = "B";
        start.Links["Далее"] = parallel.Id;
        parallel.Links["Ветка A"] = one.Id;
        parallel.Links["Ветка B"] = two.Id;
        parallel.Links["Далее"] = finish.Id;
        one.Links["Далее"] = endOne.Id;
        two.Links["Далее"] = endTwo.Id;
        var events = new List<string>();
        var graph = new ProjectModel { Nodes = [start, parallel, one, two, endOne, endTwo, finish] };
        new Runner(graph, events.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(events.Count(message => message is "A" or "B") == 2 && events.Contains("A") && events.Contains("B"), "parallel branches");
        Assert(ScreenTools.ParseColor("#FF0000").ToArgb() == Color.Red.ToArgb(), "HTML color");
        Assert(ScreenTools.ParseColor("255").ToArgb() == Color.Red.ToArgb(), "legacy COLORREF");
        Assert(ScreenTools.Matches(Color.FromArgb(101, 99, 100), Color.FromArgb(100, 100, 100), 1), "color tolerance");
        Assert(!ScreenTools.Matches(Color.FromArgb(102, 99, 100), Color.FromArgb(100, 100, 100), 1), "color rejection");
        var hotkey = HotkeyParser.Parse("Ctrl+Alt+Q");
        Assert(hotkey.Key == 'Q' && (hotkey.Modifiers & 3) == 3, "hotkey parser");
        Assert(HotkeyParser.Parse("F8").Key == 0x77, "single key hotkey");
        Assert(VirtualKeyCatalog.Describe(13) == "Enter" && VirtualKeyCatalog.Describe(65).Contains('A') &&
            VirtualKeyCatalog.Describe(118) == "F7", "VK help table");
        Assert(VirtualKeyCatalog.AllCodes().Count() == 255 &&
            VirtualKeyCatalog.InputName(120, false) == "F9" &&
            VirtualKeyCatalog.InputName(1, true) == "LMB" &&
            VirtualKeyCatalog.InputName(49, true) == "D1" &&
            NativeMouse.ParseKey("120") == 120 && NativeMouse.ParseKey("1") == 49 &&
            NativeMouse.ParseControlKey("1") == 1 && NativeMouse.ParseControlKey("D1") == 49,
            "key capture names and numeric VK values");
        Assert(NativeMouse.ParseControlKey("LMB") == 1 && NativeMouse.ParseControlKey("F9") == 120,
            "mouse and keyboard control key aliases");
        var toggleGate = new CursorActivationGate("Переключатель", false);
        Assert(!toggleGate.Update(false, true) && toggleGate.Update(true, false) == false &&
            toggleGate.Update(true, true) && toggleGate.Update(false, true) &&
            !toggleGate.Update(true, true), "toggle and secondary hold gate");
        Assert(!toggleGate.Armed, "toggle reports its final off state");
        Assert(new CursorActivationGate("Переключатель", false).Update(false, true, true),
            "brief F9 tap between polls activates toggle");
        var holdGate = new CursorActivationGate("Пока удерживается", false);
        Assert(!holdGate.Update(false, true) && !holdGate.Update(true, false) &&
            holdGate.Update(true, true) && !holdGate.Update(false, true), "hold activation gate");
        var initiallyHeld = new CursorActivationGate("Переключатель", true);
        Assert(!initiallyHeld.Update(true, true) && !initiallyHeld.Update(false, true) &&
            initiallyHeld.Update(true, true), "toggle requires a new key press");
        Assert(new CursorActivationGate("Сразу", false).Update(false, true), "default cursor watch activation");
        var indicatorState = new CursorWatchState(true, false, "Переключатель", "F9",
            "Левая кнопка мыши", Point.Empty);
        Assert(indicatorState.Detail == "F9: включено" &&
            (indicatorState with { Armed = false }).Detail == "F9: выключено" &&
            (indicatorState with { Active = true }).Detail == "F9: включено", "indicator state descriptions");
        var watchProject = ProjectModel.CreateSample();
        Assert(watchProject.Nodes[1].Value("ActivationMode") == "Переключатель" &&
            watchProject.Nodes[1].Value("RequiredHold") == "Левая кнопка мыши", "safe default watch settings");
        watchProject.Nodes[1].Values["DurationMs"] = "0";
        watchProject.Nodes[1].Values["ThresholdPx"] = "999999";
        using var f9 = new ManualResetEventSlim(false);
        using var lmb = new ManualResetEventSlim(false);
        using var stopWatch = new CancellationTokenSource();
        var watchEvents = new ConcurrentQueue<CursorWatchState>();
        var watchFinished = false;
        var watchTask = new Runner(watchProject, _ => { }, (_, _) => { },
            watchStatus: (_, state) => { if (state is null) watchFinished = true; else watchEvents.Enqueue(state); },
            keyState: key => (key switch { 120 => f9.IsSet, 1 => lmb.IsSet, _ => false }, false))
            .RunAsync(stopWatch.Token);
        try
        {
            Assert(SpinWait.SpinUntil(() => watchEvents.TryPeek(out var state) && !state.Armed && !state.Active, 2000),
                "watch starts inactive before F9");
            f9.Set();
            Assert(SpinWait.SpinUntil(() => watchEvents.ToArray().LastOrDefault() is { Armed: true, Active: false }, 2000),
                "F9 arms watch without LMB");
            lmb.Set();
            Assert(SpinWait.SpinUntil(() => watchEvents.ToArray().LastOrDefault() is { Active: true }, 2000),
                "LMB activates armed watch");
            lmb.Reset();
            Assert(SpinWait.SpinUntil(() => watchEvents.ToArray().LastOrDefault() is { Armed: true, Active: false }, 2000),
                "releasing LMB deactivates watch");
        }
        finally
        {
            stopWatch.Cancel();
            try { watchTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
        }
        Assert(watchFinished, "watch indicator receives finished state");
        var suppliedCycle = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Examples", "Cyclic_key_condition.klb")))!;
        Assert(suppliedCycle.RunMode == "Повторять до остановки" && suppliedCycle.Nodes.Count == 3 &&
            suppliedCycle.Nodes.Single(node => node.Kind == "IfKeyDown").Value("Key") == "F8",
            "cyclic sample project loads");
        var suppliedRelative = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Examples", "Relative_hold_F10.klb")))!;
        Assert(suppliedRelative.RunMode == "Повторять до остановки" &&
            suppliedRelative.Nodes.Single(node => node.Kind == "MoveRelative").Value("DeltaY") == "2" &&
            suppliedRelative.Nodes.Single(node => node.Kind == "IfKeyDown").Value("Key") == "F10",
            "relative movement sample project loads without executing mouse input");
        foreach (var file in new[] { "Relative_delayed_once.klb", "Relative_smooth.klb",
            "Relative_return.klb", "Relative_trajectory.klb", "Desktop_cursor_return.klb" })
        {
            var example = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Examples", file)))!;
            Assert(example.Nodes.Count > 1 && example.Nodes.Count(node => node.Kind == "Start") == 1 &&
                example.Nodes.Select(node => node.Id).Distinct().Count() == example.Nodes.Count &&
                example.Nodes.All(node => node.Links.Values.All(target => target is null ||
                    example.Nodes.Any(candidate => candidate.Id == target))),
                "movement example loads with valid links: " + file);
        }
        var relativeStart = BlockCatalog.Create("Start", 0, 0);
        var relativeAction = BlockCatalog.Create("MoveRelative", 220, 0);
        relativeStart.Links["Далее"] = relativeAction.Id;
        relativeAction.Values["DeltaX"] = "-3";
        relativeAction.Values["DeltaY"] = "2";
        var relativeCalls = new List<Point>();
        new Runner(new ProjectModel { Nodes = [relativeStart, relativeAction] }, _ => { }, (_, _) => { },
            relativeMove: (dx, dy) => relativeCalls.Add(new Point(dx, dy)))
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(relativeCalls.SequenceEqual([new Point(-3, 2)]),
            "relative block sends signed deltas to input backend exactly once");
        var parallelInputCount = 0;
        var overlappingInput = 0;
        var concurrentProject = new ProjectModel { Nodes = [relativeStart, relativeAction] };
        Task RunConcurrent() => new Runner(concurrentProject, _ => { }, (_, _) => { },
            relativeMove: (_, _) =>
            {
                if (Interlocked.Increment(ref parallelInputCount) != 1) Interlocked.Increment(ref overlappingInput);
                Thread.Sleep(100);
                Interlocked.Decrement(ref parallelInputCount);
            }).RunAsync(CancellationToken.None);
        Task.WhenAll(RunConcurrent(), RunConcurrent()).GetAwaiter().GetResult();
        Assert(overlappingInput == 0, "separate runners serialize automatic input actions");
        using (var firstTurn = DesktopInputCoordinator.AcquireAsync(CancellationToken.None).GetAwaiter().GetResult())
        {
            using var waitingCancel = new CancellationTokenSource();
            var waitingTurn = DesktopInputCoordinator.AcquireAsync(waitingCancel.Token);
            Assert(!waitingTurn.Wait(120), "second scenario waits while desktop input is occupied");
            waitingCancel.Cancel();
            try { waitingTurn.GetAwaiter().GetResult(); throw new Exception("Cancelled turn was granted."); }
            catch (OperationCanceledException) { }
        }
        var groupStart = BlockCatalog.Create("Start", 0, 0);
        var inputGroup = BlockCatalog.Create("InputGroup", 220, 0);
        var insideGroup = BlockCatalog.Create("Log", 440, 0);
        var groupEnd = BlockCatalog.Create("BranchEnd", 660, 0);
        var afterGroup = BlockCatalog.Create("Log", 440, 150);
        insideGroup.Values["Text"] = "inside group";
        afterGroup.Values["Text"] = "after group";
        groupStart.Links["Далее"] = inputGroup.Id;
        inputGroup.Links["Тело"] = insideGroup.Id;
        inputGroup.Links["Далее"] = afterGroup.Id;
        insideGroup.Links["Далее"] = groupEnd.Id;
        var groupEvents = new ConcurrentQueue<string>();
        using (var occupied = DesktopInputCoordinator.AcquireAsync(CancellationToken.None).GetAwaiter().GetResult())
        {
            var groupedRun = new Runner(new ProjectModel { Nodes = [groupStart, inputGroup, insideGroup, groupEnd, afterGroup] },
                groupEvents.Enqueue, (_, _) => { }).RunAsync(CancellationToken.None);
            Assert(!groupedRun.Wait(120) && !groupEvents.Contains("inside group"),
                "input group waits for another scenario to finish");
            occupied.Dispose();
            Assert(groupedRun.Wait(2000) && groupEvents.ToArray().TakeLast(2).SequenceEqual(["inside group", "after group"]),
                "input group runs atomically and continues after automatic release");
        }
        using (var finalTurnTimeout = new CancellationTokenSource(2000))
        using (DesktopInputCoordinator.AcquireAsync(finalTurnTimeout.Token).GetAwaiter().GetResult()) { }
        var timeoutStart = BlockCatalog.Create("Start", 0, 0);
        var timeoutGroup = BlockCatalog.Create("InputGroup", 200, 0);
        var groupTimeoutWait = BlockCatalog.Create("Wait", 400, 0);
        timeoutGroup.Values["MaxMs"] = "100";
        groupTimeoutWait.Values["Milliseconds"] = "500";
        timeoutStart.Links["Далее"] = timeoutGroup.Id;
        timeoutGroup.Links["Тело"] = groupTimeoutWait.Id;
        try
        {
            new Runner(new ProjectModel { Nodes = [timeoutStart, timeoutGroup, groupTimeoutWait] }, _ => { }, (_, _) => { })
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            throw new Exception("Input group timeout was ignored.");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("превысила")) { }
        using (var afterTimeout = new CancellationTokenSource(2000))
        using (DesktopInputCoordinator.AcquireAsync(afterTimeout.Token).GetAwaiter().GetResult()) { }
        var importedGroup = CodeImporter.Convert("input_group(1500)\nprint(\"atomic\")\nend_input\nprint(\"next\")", Point.Empty);
        var importedBlock = importedGroup.Nodes.Single(node => node.Kind == "InputGroup");
        Assert(importedBlock.Value("MaxMs") == "1500" && importedBlock.Link("Тело") is not null &&
            importedBlock.Link("Далее") is not null && importedGroup.Nodes.Count(node => node.Kind == "BranchEnd") == 1,
            "code import creates an atomic input group with both outgoing branches");
        var split = RelativeMotionPlan.Split(7, -5, 4);
        Assert(split.Count == 4 && split.Sum(part => part.X) == 7 && split.Sum(part => part.Y) == -5 &&
            split.All(part => Math.Abs(part.X) <= 2 && Math.Abs(part.Y) <= 2),
            "smooth relative motion preserves signed total without a large jump");
        var pressGate = new KeyPressGate();
        Assert(!pressGate.Update(false) && pressGate.Update(true) && !pressGate.Update(true) &&
            !pressGate.Update(false) && pressGate.Update(true) && !pressGate.Update(true),
            "new-press condition fires once per key press");
        relativeAction.Values["DelayMs"] = "0";
        relativeAction.Values["DurationMs"] = "4";
        relativeAction.Values["Steps"] = "4";
        relativeCalls.Clear();
        new Runner(new ProjectModel { Nodes = [relativeStart, relativeAction] }, _ => { }, (_, _) => { },
            relativeMove: (dx, dy) => relativeCalls.Add(new Point(dx, dy)))
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(relativeCalls.Sum(part => part.X) == -3 && relativeCalls.Sum(part => part.Y) == 2 &&
            relativeCalls.All(part => Math.Abs(part.X) <= 1 && Math.Abs(part.Y) <= 1),
            "timed relative block emits bounded steps that sum to the requested movement");
        var nativeInputType = typeof(NativeMouse).GetNestedType("NativeInput", BindingFlags.NonPublic)!;
        Assert(System.Runtime.InteropServices.Marshal.SizeOf(nativeInputType) == (IntPtr.Size == 8 ? 40 : 28),
            "SendInput structure matches Windows INPUT size");
        var cyclicStart = BlockCatalog.Create("Start", 0, 0);
        var cyclicCondition = BlockCatalog.Create("IfKeyDown", 220, 0);
        var cyclicLog = BlockCatalog.Create("Log", 440, 0);
        cyclicCondition.Values["Key"] = "A";
        cyclicLog.Values["Text"] = "cyclic-tick";
        cyclicStart.Links["Далее"] = cyclicCondition.Id;
        cyclicCondition.Links["Да"] = cyclicLog.Id;
        var cyclicProject = new ProjectModel { RunMode = "Повторять до остановки", CycleDelayMs = 10,
            Nodes = [cyclicStart, cyclicCondition, cyclicLog] };
        using var aKey = new ManualResetEventSlim(false);
        using var cyclicStop = new CancellationTokenSource();
        var cyclicEvents = new ConcurrentQueue<string>();
        var cyclicTask = new Runner(cyclicProject, cyclicEvents.Enqueue, (_, _) => { },
            keyState: key => (key == 65 && aKey.IsSet, false)).RunAsync(cyclicStop.Token);
        try
        {
            Thread.Sleep(60);
            Assert(!cyclicEvents.Contains("cyclic-tick") && !cyclicTask.IsCompleted,
                "cyclic scenario waits for condition without ending");
            aKey.Set();
            Assert(SpinWait.SpinUntil(() => cyclicEvents.Count(message => message == "cyclic-tick") >= 3, 2000),
                "cyclic scenario revisits key condition");
            aKey.Reset();
            Thread.Sleep(70);
            var afterRelease = cyclicEvents.Count(message => message == "cyclic-tick");
            Thread.Sleep(70);
            Assert(cyclicEvents.Count(message => message == "cyclic-tick") == afterRelease,
                "held-key condition stops taking Yes branch after release");
        }
        finally
        {
            cyclicStop.Cancel();
            try { cyclicTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        var combinedProject = cyclicProject.Copy();
        var secondCondition = BlockCatalog.Create("IfKeyDown", 440, 0);
        secondCondition.Values["Key"] = "B";
        Assert(secondCondition.Value("IndicatorMode", IndicatorConditionMode.Normal) == IndicatorConditionMode.Normal,
            "new key condition activates indicator by default");
        combinedProject.Nodes.Single(node => node.Kind == "IfKeyDown").Links["Да"] = secondCondition.Id;
        secondCondition.Links["Да"] = combinedProject.Nodes.Single(node => node.Kind == "Log").Id;
        combinedProject.Nodes.Add(secondCondition);
        using var firstHeld = new ManualResetEventSlim(false);
        using var secondHeld = new ManualResetEventSlim(false);
        using var combinedStop = new CancellationTokenSource();
        var conditionEvents = new ConcurrentQueue<(Guid Id, bool Selected)>();
        var combinedTask = new Runner(combinedProject, _ => { }, (_, _) => { },
            keyState: key => (key switch { 65 => firstHeld.IsSet, 66 => secondHeld.IsSet, _ => false }, false),
            conditionStatus: (id, selected) => conditionEvents.Enqueue((id, selected)))
            .RunAsync(combinedStop.Token);
        try
        {
            var firstId = combinedProject.Nodes.Single(node => node.Kind == "IfKeyDown" && node.Value("Key") == "A").Id;
            Assert(SpinWait.SpinUntil(() => conditionEvents.Contains((firstId, false)), 2000),
                "first key condition reports waiting");
            firstHeld.Set();
            Assert(SpinWait.SpinUntil(() => conditionEvents.Contains((firstId, true)) &&
                conditionEvents.Contains((secondCondition.Id, false)), 2000),
                "serial key conditions report partial activation");
            secondHeld.Set();
            Assert(SpinWait.SpinUntil(() => conditionEvents.Contains((secondCondition.Id, true)), 2000),
                "serial key conditions report full activation");
            firstHeld.Reset();
            Assert(SpinWait.SpinUntil(() => conditionEvents.Count(state => state == (firstId, false)) >= 2, 2000),
                "release reports deactivation");
        }
        finally
        {
            combinedStop.Cancel();
            try { combinedTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        var invertedProject = cyclicProject.Copy();
        invertedProject.Nodes.Single(node => node.Kind == "IfKeyDown").Values["IndicatorMode"] = IndicatorConditionMode.Inverted;
        using var invertedHeld = new ManualResetEventSlim(false);
        using var invertedStop = new CancellationTokenSource();
        var invertedEvents = new ConcurrentQueue<bool>();
        var invertedTask = new Runner(invertedProject, _ => { }, (_, _) => { },
            keyState: key => (key == 65 && invertedHeld.IsSet, false),
            conditionStatus: (_, active) => invertedEvents.Enqueue(active)).RunAsync(invertedStop.Token);
        try
        {
            Assert(SpinWait.SpinUntil(() => invertedEvents.Contains(true), 2000),
                "inverted condition activates indicator on No");
            invertedHeld.Set();
            Assert(SpinWait.SpinUntil(() => invertedEvents.Contains(false), 2000),
                "inverted condition deactivates indicator on Yes");
        }
        finally
        {
            invertedStop.Cancel();
            try { invertedTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        var variableIndicatorStart = BlockCatalog.Create("Start", 0, 0);
        var variableIndicatorSet = BlockCatalog.Create("SetVariable", 200, 0);
        var variableIndicatorCheck = BlockCatalog.Create("IfVariable", 400, 0);
        variableIndicatorSet.Values["Name"] = "ready";
        variableIndicatorSet.Values["Value"] = "1";
        variableIndicatorCheck.Values["Name"] = "ready";
        variableIndicatorCheck.Values["Value"] = "1";
        variableIndicatorCheck.Values["IndicatorMode"] = IndicatorConditionMode.Normal;
        variableIndicatorStart.Links["Далее"] = variableIndicatorSet.Id;
        variableIndicatorSet.Links["Далее"] = variableIndicatorCheck.Id;
        var variableIndicatorEvents = new List<bool>();
        new Runner(new ProjectModel { Nodes = [variableIndicatorStart, variableIndicatorSet, variableIndicatorCheck] },
            _ => { }, (_, _) => { }, conditionStatus: (_, active) => variableIndicatorEvents.Add(active))
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(variableIndicatorEvents.SequenceEqual([true]), "non-key condition can control indicator");
        var toggleProject = cyclicProject.Copy();
        toggleProject.Nodes.Single(node => node.Kind == "IfKeyDown").Values["Mode"] = "Переключатель";
        using var toggleStop = new CancellationTokenSource();
        var toggleEvents = new ConcurrentQueue<string>();
        var toggleTask = new Runner(toggleProject, toggleEvents.Enqueue, (_, _) => { },
            keyState: key => (key == 65 && aKey.IsSet, false)).RunAsync(toggleStop.Token);
        try
        {
            aKey.Set();
            Assert(SpinWait.SpinUntil(() => toggleEvents.Count(message => message == "cyclic-tick") >= 2, 2000),
                "condition toggle arms on first key press");
            aKey.Reset();
            var beforeReleasedTicks = toggleEvents.Count(message => message == "cyclic-tick");
            Assert(SpinWait.SpinUntil(() => toggleEvents.Count(message => message == "cyclic-tick") >= beforeReleasedTicks + 2, 2000),
                "condition toggle remains armed after key release");
            Thread.Sleep(50);
            aKey.Set();
            Thread.Sleep(50);
            aKey.Reset();
            Thread.Sleep(70);
            var afterSecondPress = toggleEvents.Count(message => message == "cyclic-tick");
            Thread.Sleep(70);
            Assert(toggleEvents.Count(message => message == "cyclic-tick") == afterSecondPress,
                "second key press disarms condition toggle");
        }
        finally
        {
            toggleStop.Cancel();
            try { toggleTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        var feedbackStart = BlockCatalog.Create("Start", 0, 0);
        var feedbackA = BlockCatalog.Create("Log", 220, 0);
        var feedbackB = BlockCatalog.Create("Log", 440, 0);
        feedbackStart.Links["Далее"] = feedbackA.Id;
        feedbackA.Values["Text"] = "feedback-tick";
        feedbackA.Links["Далее"] = feedbackB.Id;
        feedbackB.Links["Далее"] = feedbackA.Id;
        var feedbackProject = new ProjectModel { CycleDelayMs = 10, Nodes = [feedbackStart, feedbackA, feedbackB] };
        using var feedbackStop = new CancellationTokenSource();
        var feedbackEvents = new ConcurrentQueue<string>();
        var feedbackTask = new Runner(feedbackProject, feedbackEvents.Enqueue, (_, _) => { }).RunAsync(feedbackStop.Token);
        try
        {
            Assert(SpinWait.SpinUntil(() => feedbackEvents.Count(message => message == "feedback-tick") >= 3, 2000),
                "explicit back-link repeats even in one-pass mode");
            Assert(!feedbackTask.IsCompleted, "explicit feedback remains running until stopped");
        }
        finally
        {
            feedbackStop.Cancel();
            try { feedbackTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        var repeatingStopStart = BlockCatalog.Create("Start", 0, 0);
        var repeatingStopBlock = BlockCatalog.Create("Stop", 220, 0);
        repeatingStopStart.Links["Далее"] = repeatingStopBlock.Id;
        var repeatingStopProject = new ProjectModel { RunMode = "Повторять до остановки",
            Nodes = [repeatingStopStart, repeatingStopBlock] };
        Assert(new Runner(repeatingStopProject, _ => { }, (_, _) => { }).RunAsync(CancellationToken.None)
            .Wait(TimeSpan.FromSeconds(2)), "Stop block terminates repeat mode");
        var cyclicWatchProject = ProjectModel.CreateSample();
        cyclicWatchProject.RunMode = "Повторять до остановки";
        cyclicWatchProject.CycleDelayMs = 10;
        var extraCondition = BlockCatalog.Create("IfKeyDown", 240, 180);
        extraCondition.Values["Key"] = "A";
        cyclicWatchProject.Nodes[0].Links["Далее"] = extraCondition.Id;
        extraCondition.Links["Да"] = cyclicWatchProject.Nodes[1].Id;
        cyclicWatchProject.Nodes.Add(extraCondition);
        cyclicWatchProject.Nodes[1].Values["ThresholdPx"] = "999999";
        using var cyclicWatchStop = new CancellationTokenSource();
        using var watchF9 = new ManualResetEventSlim(false);
        var cyclicWatchEvents = new ConcurrentQueue<CursorWatchState?>();
        var cyclicWatchTask = new Runner(cyclicWatchProject, _ => { }, (_, _) => { },
            watchStatus: (_, state) => cyclicWatchEvents.Enqueue(state),
            keyState: key => (key switch { 65 => aKey.IsSet, 120 => watchF9.IsSet, _ => false }, false))
            .RunAsync(cyclicWatchStop.Token);
        try
        {
            aKey.Set();
            Assert(SpinWait.SpinUntil(() => cyclicWatchEvents.Any(state => state is { Armed: false }), 2000),
                "cyclic zero-duration watch yields after each poll");
            watchF9.Set();
            Assert(SpinWait.SpinUntil(() => cyclicWatchEvents.Any(state => state is { Armed: true, Active: false }), 2000),
                "cyclic watch preserves F9 toggle without LMB");
            aKey.Reset();
            Assert(SpinWait.SpinUntil(() => cyclicWatchEvents.Any(state => state is null), 2000),
                "watch indicator clears when upstream key condition turns off");
            Assert(!cyclicWatchTask.IsCompleted, "cyclic watch scenario keeps checking its upstream condition");
        }
        finally
        {
            cyclicWatchStop.Cancel();
            try { cyclicWatchTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }
        foreach (var (file, mode) in new[] { ("F9_toggle_LMB.klb", "Переключатель"), ("F9_hold_LMB.klb", "Пока удерживается") })
        {
            var example = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Examples", file)))!;
            Assert(example.Nodes.Count == 2 && example.Nodes[0].Link("Далее") == example.Nodes[1].Id &&
                example.Nodes[1].Value("ActivationMode") == mode &&
                example.Nodes[1].Value("RequiredHold") == "Левая кнопка мыши", "cursor activation example " + file);
        }
        foreach (var (file, key, x, width, indicatorKey) in new[] {
            ("Dual_A_F9.klb", "F9", 30, 36, "Ctrl+Shift+I"),
            ("Dual_B_F8.klb", "F8", 300, 245, "Ctrl+Shift+O") })
        {
            var example = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Examples", file)))!;
            Assert(example.ShowIndicator && example.UseCustomIndicatorPosition && example.IndicatorX == x &&
                example.IndicatorY == 18 && example.Nodes[1].Value("ActivationKey") == key &&
                example.Nodes[1].Value("RequiredHold") == "Левая кнопка мыши" &&
                example.IndicatorAppearance.Width == width && example.IndicatorToggleHotkey == indicatorKey,
                "dual instance example " + file);
        }
        var imported = CodeImporter.Convert("""
            #name "Проверка"
            $counter = 0
            for($i=0, $i<3)
              $counter = $counter + 1
              if($counter >= 2)
                print("n=", $counter)
              else
                print("small")
              end_if
            end_cyc
            wait(0.001)
            print("end")
            sub(hello)
              print("sub")
            end_sub
            hello()
            """, new Point(250, 300));
        Assert(imported.Entry != null && imported.Nodes.Any(node => node.Kind == "WhileExpression") &&
            imported.Nodes.Any(node => node.Kind == "IfVariable") &&
            imported.Nodes.Any(node => node.Kind == "SubStart") &&
            imported.Nodes.Min(node => node.X) == 250, "legacy code import layout and block kinds");
        var importedStart = BlockCatalog.Create("Start", 0, 0);
        importedStart.Links["Далее"] = imported.Entry;
        var importedEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [importedStart, .. imported.Nodes] }, importedEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(importedEvents.Count(message => message == "small") == 1 &&
            importedEvents.Contains("n=2") && importedEvents.Contains("n=3") &&
            importedEvents.IndexOf("end") < importedEvents.IndexOf("sub"),
            "imported conditions, loop, variables and subroutine execution");
        var counterImport = CodeImporter.Convert("$count = $count + 1\nprint($count)", Point.Empty);
        var counterStart = BlockCatalog.Create("Start", 0, 0);
        counterStart.Links["Далее"] = counterImport.Entry;
        var counterLog = new List<string>();
        new Runner(new ProjectModel { Nodes = [counterStart, .. counterImport.Nodes] }, counterLog.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(counterLog.Contains("1"), "legacy-style uninitialized counter starts at zero");
        var clicks = CodeImporter.Convert("lclick(100, 200)\nwaitms(20)\nwatchcursor(toggle, F9, LMB)", Point.Empty);
        var relativeImport = CodeImporter.Convert("mover(0, 2)", Point.Empty);
        Assert(relativeImport.Nodes.Single().Kind == "MoveRelative" &&
            relativeImport.Nodes.Single().Value("DeltaY") == "2", "legacy MOVER maps to relative input");
        var descriptionImport = CodeImporter.Convert("""
            description("Этап", -20, -45, 750, 180, "#ABCDEF", 40)
            print("ok")
            """, new Point(250, 300));
        var importedDescription = descriptionImport.Nodes.Single(node => node.Kind == "Description");
        Assert(descriptionImport.Entry == descriptionImport.Nodes.Single(node => node.Kind == "Log").Id &&
            importedDescription.X == 230 && importedDescription.Y == 255 &&
            DescriptionGeometry.Bounds(importedDescription).Size == new Size(750, 180) &&
            DescriptionGeometry.Opacity(importedDescription) == 40 &&
            importedDescription.Value("FillColor") == "#ABCDEF" && importedDescription.Links.Count == 0,
            "description import creates a non-executable editable background area");
        var descriptionOnly = CodeImporter.Convert("description(\"Только область\")", new Point(250, 300));
        Assert(descriptionOnly.Entry == null && descriptionOnly.Nodes.Single().Kind == "Description",
            "description-only code import has no executable entry");
        try
        {
            CodeImporter.Convert("description(\"Ошибка\", 0, 0, 700, 260, \"#AABBCC\", 101)", Point.Empty);
            throw new Exception("Self-test failed: invalid description opacity was accepted");
        }
        catch (CodeImportException error) { Assert(error.LineNumber == 1, "description import validates opacity"); }
        var visualExample = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Examples", "Description_groups.klb")))!;
        Assert(visualExample.Nodes.Count(node => node.Kind == "Description") == 2 &&
            JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(visualExample))!.Nodes.Count == visualExample.Nodes.Count,
            "description example loads and survives project serialization");
        var copiedAnnotations = BlockClipboard.Copy(visualExample.Nodes.Where(node => node.Kind is "Description" or "Log"));
        Assert(copiedAnnotations.Count == 4 && copiedAnnotations.Count(node => node.Kind == "Description") == 2,
            "description can be copied with ordinary blocks");
        var selectionProject = new ProjectModel { Nodes =
            [BlockCatalog.Create("Description", 100, 100), BlockCatalog.Create("Log", 200, 200)] };
        using (var selectionCanvas = new GraphCanvas { Project = selectionProject })
        {
            selectionCanvas.Size = new Size(1000, 500);
            selectionCanvas.SelectArea(new Rectangle(190, 190, 230, 100));
            Assert(selectionCanvas.SelectedNodes.Count == 1 && selectionCanvas.SelectedNodes[0].Kind == "Log",
                "partial Shift selection ignores the background description");
            selectionCanvas.SelectArea(new Rectangle(90, 90, 730, 290));
            Assert(selectionCanvas.SelectedNodes.Count == 2,
                "Shift selection includes description only when fully enclosed");
            using var preview = new Bitmap(1000, 500);
            selectionCanvas.DrawToBitmap(preview, new Rectangle(0, 0, 1000, 500));
            Assert(preview.GetPixel(300, 270).ToArgb() == BlockCatalog.Get("Log").Color.ToArgb(),
                "ordinary blocks render opaquely in front of description areas");
        }
        Assert(clicks.Nodes.Count(node => node.Kind == "MoveCursor") == 1 &&
            clicks.Nodes.Count(node => node.Kind == "Click") == 1 &&
            clicks.Nodes.Single(node => node.Kind == "WatchCursor").Value("RequiredHold") == "Левая кнопка мыши",
            "imported click, wait and gated cursor watch");
        try
        {
            CodeImporter.Convert("waitms(5)\nrnd(1, 10)", Point.Empty);
            throw new Exception("Self-test failed: unsupported import was accepted");
        }
        catch (CodeImportException error) { Assert(error.LineNumber == 2, "unsupported import line diagnostic"); }
        Assert(ExternalTools.ResolvePath("test.ahk", "C:\\PROJECT\\KLIKER") == "C:\\PROJECT\\KLIKER\\test.ahk", "relative path");
        foreach (var spec in BlockCatalog.All)
            Assert(!AppHelp.For(spec.Kind).Contains("пока не добавлено"), "help for " + spec.Kind);
        var recordingProject = ProjectModel.CreateSample();
        var recording = new MacroRecording
        {
            Name = "Тест", Mode = "Клики и образы", Events =
            [
                new MacroEvent { Kind = "Click", DelayMs = 25, X = 200, Y = 300, Button = "Правая",
                    CapturePath = "test.png", CaptureX = 180, CaptureY = 280, CaptureWidth = 40, CaptureHeight = 40 }
            ]
        };
        var (first, added) = MacroBuilder.AddToProject(recordingProject, recording);
        Assert(first != null && added == 4, "click macro blocks");
        var imageBlock = recordingProject.Nodes.First(n => n.Kind == "FindImage");
        Assert(imageBlock.Link("Найден") != null && imageBlock.Link("Нет") == null, "image recovery branch");
        var fullProject = ProjectModel.CreateSample();
        var full = new MacroRecording { Mode = "Все действия ввода", Events =
            [new MacroEvent { Kind = "KeyDown", Key = "65" }, new MacroEvent { Kind = "KeyUp", Key = "65" }] };
        Assert(MacroBuilder.AddToProject(fullProject, full).NodesAdded == 2, "full macro key events");
        Assert(JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(recordingProject))!.Nodes.Count == recordingProject.Nodes.Count,
            "macro project round trip");
        var subStart = BlockCatalog.Create("Start", 0, 0);
        var callOne = BlockCatalog.Create("CallSub", 0, 0);
        var callTwo = BlockCatalog.Create("CallSub", 0, 0);
        var subDefinition = BlockCatalog.Create("SubStart", 0, 0);
        var subLog = BlockCatalog.Create("Log", 0, 0);
        var subReturn = BlockCatalog.Create("SubReturn", 0, 0);
        subDefinition.Values["Name"] = "Тест";
        callOne.Values["Name"] = "тест";
        callTwo.Values["Name"] = "ТЕСТ";
        subStart.Links["Далее"] = callOne.Id;
        callOne.Links["Далее"] = callTwo.Id;
        subDefinition.Links["Тело"] = subLog.Id;
        subLog.Links["Далее"] = subReturn.Id;
        subLog.Values["Text"] = "sub";
        var subEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [subStart, callOne, callTwo, subDefinition, subLog, subReturn] },
            subEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(subEvents.Count(message => message == "sub") == 2, "reusable subroutine");
        var nestedStart = BlockCatalog.Create("Start", 0, 0);
        var nestedCall = BlockCatalog.Create("CallSub", 0, 0);
        var nestedDefinition = BlockCatalog.Create("SubStart", 0, 0);
        var nestedRepeat = BlockCatalog.Create("Repeat", 0, 0);
        var nestedReturn = BlockCatalog.Create("SubReturn", 0, 0);
        var unreachable = BlockCatalog.Create("Log", 0, 0);
        nestedStart.Links["Далее"] = nestedCall.Id;
        nestedDefinition.Links["Тело"] = nestedRepeat.Id;
        nestedRepeat.Links["Тело"] = nestedReturn.Id;
        nestedRepeat.Links["Далее"] = unreachable.Id;
        nestedRepeat.Values["Times"] = "3";
        var nestedEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [nestedStart, nestedCall, nestedDefinition, nestedRepeat, nestedReturn, unreachable] },
            nestedEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(!nestedEvents.Contains("unreachable") && nestedEvents.Count(x => x.Contains("тело 1/3")) == 1,
            "END_SUB returns out of nested repeat");
        var copiedStart = BlockCatalog.Create("Start", 5, 5);
        var copiedA = BlockCatalog.Create("Log", 100, 150);
        var copiedB = BlockCatalog.Create("Wait", 340, 160);
        var outside = BlockCatalog.Create("Stop", 600, 160);
        copiedA.Links["Далее"] = copiedB.Id;
        copiedB.Links["Далее"] = outside.Id;
        copiedA.Values["Text"] = "original";
        var snapshot = BlockClipboard.Copy([copiedStart, copiedA, copiedB]);
        copiedA.Values["Text"] = "changed";
        var pastedProject = new ProjectModel { Nodes = [copiedStart, copiedA, copiedB, outside] };
        var pasted = BlockClipboard.Paste(snapshot, new Point(800, 900), pastedProject);
        Assert(snapshot.Count == 2 && pasted.Count == 2 && pasted[0].X == 800 && pasted[0].Y == 900,
            "block clipboard placement and Start exclusion");
        Assert(pasted[0].Id != copiedA.Id && pasted[0].Link("Далее") == pasted[1].Id &&
            pasted[1].Link("Далее") == null && pasted[0].Value("Text") == "original",
            "block clipboard IDs, internal links, external links and deep copy");
        using (var groupCanvas = new GraphCanvas { Project = pastedProject })
        {
            groupCanvas.SelectArea(new Rectangle(95, 145, 450, 120));
            Assert(groupCanvas.SelectedNodes.Count == 2, "marquee selects intersecting blocks");
            var originalPositions = groupCanvas.SelectedNodes.ToDictionary(node => node.Id, node => new Point(node.X, node.Y));
            groupCanvas.MoveSelectedFrom(originalPositions, 40, -20);
            Assert(copiedA.X == 140 && copiedA.Y == 130 && copiedB.X == 380 && copiedB.Y == 140,
                "group drag moves every selected block together");
            groupCanvas.Select(null);
            Assert(groupCanvas.SelectedNodes.Count == 0, "blank click resets group selection");
        }
        var variables = new VariableStore();
        variables.Set("Счётчик", "1");
        Assert(variables.Expand("Значение ${счётчик}") == "Значение 1", "variable substitution");
        Assert(variables.Calculate("Счётчик", "Сложить", "2") == "3" &&
            variables.Compare("счётчик", "Не меньше", "3"), "variable arithmetic and comparison");
        variables.Set("временная", "5");
        variables.SetArray("временная", 0, "элемент");
        variables.Undefine("временная");
        Assert(variables.Get("временная") == "0" && variables.ArraySize("временная") == 0,
            "UNDEFINE removes scalar and array, leaving legacy zero readback");
        variables.Define("временная", "9");
        Assert(variables.Get("временная") == "9", "DEFINE may initialize after UNDEFINE");
        variables.Set("ссылка", "$временная");
        Assert(variables.Evaluate("getvar($ссылка)") == "9", "GETVAR resolves a variable name held in a string");
        variables.SetExistingByReference("$временная", "12");
        Assert(variables.Get("временная") == "12", "SETVAR updates an existing variable by string name");
        try
        {
            variables.SetExistingByReference("$неизвестная", "1");
            throw new Exception("Self-test failed: SETVAR created a missing variable");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("SETVAR:")) { }
        Assert(variables.Evaluate("($Счётчик + 2) * 3") == "15" &&
            variables.Evaluate("$Счётчик >= 3 and $Счётчик < 5") == "1" &&
            variables.Evaluate("0 and (1 + 2) == 3") == "0" &&
            variables.Evaluate("strconcat(\"a\", \"b\")") == "ab" &&
            variables.Evaluate("round(sqrt(9) + abs(-2), 0)") == "5", "numeric, logical and string expressions");
        var templateVariables = new VariableStore(asciiOnly: true);
        templateVariables.Set("counter", "1");
        Assert(templateVariables.Resolve(@"=[(${counter}+2)*3]& па \&") == "9 па &" &&
            templateVariables.Resolve("=[2**3]& power") == "8 power" &&
            templateVariables.Resolve("=[2^3]&[10%3]") == "81" &&
            templateVariables.Resolve("=[$counter>0 && $counter<2]") == "1" &&
            templateVariables.Resolve("=[$counter>2 or $counter==1]") == "1" &&
            templateVariables.ResolveLines("=[($counter+2)*3]& па\\&\n11\n22") == "9 па&\n11\n22" &&
            templateVariables.ResolveLines("11\n=[($counter+2)*3]& па \\&") == "11\n9 па &" &&
            VariableStore.HasReference("11\n=[$counter+2]"),
            "mixed expression templates evaluate each radio option, math, logic and escaped ampersand");
        Assert(templateVariables.Resolve("=[if (${counter}+2)*3<10 then \"Правда, точно есть\" else_if 35>25 then 22 else 33]") == "Правда, точно есть" &&
            templateVariables.Resolve("=[if 0 then 11 else_if 35>25 then 22 else 33]") == "22" &&
            templateVariables.Resolve("=[if 0 then 11 else_if 0 then 22 else 33]") == "33" &&
            templateVariables.Resolve("=[if 1 then (2+3)*4 else 0]") == "20" &&
            templateVariables.Resolve("=[if 1]") == "TRUE" &&
            templateVariables.Resolve("=[if 0]") == "FALSE" &&
            templateVariables.Resolve("=[if 0 then 11]") == "FALSE" &&
            templateVariables.Resolve("=[if 0 then 1/0 else \"safe then else_if\"]") == "safe then else_if" &&
            ExpressionEvaluator.IsTrueValue("TRUE") && !ExpressionEvaluator.IsTrueValue("FALSE"),
            "conditional expressions choose one branch, skip inactive values and return true/false");
        Assert(templateVariables.Resolve("=[if 1 then (2+3)*4 else 1/0] => ${xxx}") == "20" &&
            templateVariables.Get("xxx") == "20" &&
            templateVariables.Resolve("=[if 0 then \"нет\" else \"да\"]=>${xxx}") == "да" &&
            templateVariables.Get("xxx") == "да" &&
            templateVariables.Resolve(@"=[2*3]=>${calculated}& units \&") == "6 units &" &&
            templateVariables.Get("calculated") == "6" &&
            templateVariables.Resolve("=[if 0] => ${flag}") == "FALSE" &&
            templateVariables.Get("flag") == "FALSE",
            "template assignment creates and updates an ASCII variable and returns its value");
        try
        {
            templateVariables.Resolve("=[1] => ${неверно}");
            throw new Exception("Self-test failed: non-ASCII assignment name was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("A–Z", StringComparison.Ordinal)) { }
        try
        {
            templateVariables.Resolve("=[1] => ${uncommitted}&[2+");
            throw new Exception("Self-test failed: malformed template was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains(']')) { }
        try
        {
            templateVariables.Get("uncommitted");
            throw new Exception("Self-test failed: malformed template assigned a variable");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("uncommitted", StringComparison.Ordinal)) { }
        try
        {
            templateVariables.Resolve("=[if 1 else 2]");
            throw new Exception("Self-test failed: conditional without then was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("then", StringComparison.OrdinalIgnoreCase)) { }
        UiLanguage.Configure("en");
        try
        {
            var conditionalHelp = ExpressionHelp.Section("Условные выражения и присваивание");
            Assert(UiLanguage.T("Условные выражения и присваивание") == "Conditional expressions and assignment" &&
                conditionalHelp is { Length: > 500 } &&
                conditionalHelp.Contains("=> ${xxx}", StringComparison.Ordinal) &&
                !conditionalHelp.Any(ch => ch is >= '\u0400' and <= '\u04ff'),
                "conditional syntax and assignment help are available in English without untranslated Russian");
            try
            {
                templateVariables.Resolve("=[if 1 else 2]");
                throw new Exception("Self-test failed: English conditional error was not raised");
            }
            catch (InvalidOperationException error) when (error.Message == "Use then after the condition.") { }
        }
        finally { UiLanguage.Configure("ru"); }
        try
        {
            templateVariables.Resolve("=[1+2");
            throw new Exception("Self-test failed: unclosed template expression was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains(']')) { }
        Assert(variables.Evaluate("round(1236, 1)") == "1240" &&
            variables.Evaluate("round(1236, 2)") == "1200" &&
            variables.Evaluate("round(1.236, -2)") == "1.24" &&
            variables.Evaluate("round(-1.5, 0)") == "-2", "legacy ROUND decimal-place semantics");
        Assert(variables.Evaluate("strpos(\"abcabc\", \"a\")") == "1" &&
            variables.Evaluate("strpos(\"abcabc\", \"a\", 3)") == "4" &&
            variables.Evaluate("strpos(\"abcabc\", \"z\")") == "0" &&
            variables.Evaluate("strcut(\"hello2000\", 6, 2)") == "20" &&
            variables.Evaluate("strcut2(\"hello2000\", 2, 5)") == "ello" &&
            variables.Evaluate("strfilter(\"hello2000\", \"20\", 0)") == "hello" &&
            variables.Evaluate("strfilter(\"hello2000\", \"20\", 1)") == "2000" &&
            variables.Evaluate("strmd5(\"123\")") == "202CB962AC59075B964B07152D234B70",
            "legacy string functions and one-based positions");
        variables.SetArray("данные", 0, "20");
        variables.SetArray("данные", 1, "3");
        Assert(variables.Evaluate("$данные[1] + 2") == "5" && variables.ArraySize("данные") == 2,
            "array indexing in expressions");
        variables.ArraySort("данные", true);
        Assert(variables.GetArray("данные", 0) == "3" && variables.ArrayPop("данные") == "20" &&
            variables.ArraySize("данные") == 1, "array sort and pop");
        Assert(variables.Evaluate("arrsize($данные)") == "1" &&
            variables.Evaluate("arrpop($данные)") == "3" &&
            variables.Evaluate("arrsize($данные)") == "0",
            "ARRSIZE and ARRPOP expressions use array name without scalar lookup");
        variables.ReplaceArray("данные", ["один", "", "три"]);
        Assert(variables.GetArray("данные", 0) == "один" && variables.GetArray("данные", 1) == "" &&
            variables.GetArray("данные", 2) == "три" && variables.ArraySize("данные") == 3 &&
            variables.SnapshotArray("данные").SequenceEqual(["один", "", "три"]),
            "atomic array replacement preserves empty string entries");
        variables.ClearArray("данные");
        variables.SetArray("данные", 2, "конец");
        Assert(variables.SnapshotArray("данные").SequenceEqual(["0", "0", "конец"]),
            "sparse arrays snapshot missing indexes as zero");
        variables.ClearArray("данные");
        variables.ArrayPush("данные", "10");
        variables.ArrayPush("данные", "2");
        variables.ArraySortAuto("данные");
        Assert(variables.GetArray("данные", 0) == "2" && variables.GetArray("данные", 1) == "10",
            "automatic array sort compares numeric values numerically");
        Assert(variables.Evaluate("$_date_y") == DateTime.Now.Year.ToString(), "read-only date variable");
        variables.Set("_user", "своё");
        Assert(variables.Evaluate("$_user") == "своё", "user variable names may start with underscore");
        var expressionStart = BlockCatalog.Create("Start", 0, 0);
        var expressionSet = BlockCatalog.Create("SetVariable", 0, 0);
        var expressionWhile = BlockCatalog.Create("WhileExpression", 0, 0);
        var expressionIncrement = BlockCatalog.Create("MathVariable", 0, 0);
        var expressionEnd = BlockCatalog.Create("BranchEnd", 0, 0);
        var expressionIf = BlockCatalog.Create("IfExpression", 0, 0);
        var expressionLog = BlockCatalog.Create("Log", 0, 0);
        expressionSet.Values["Name"] = "i";
        expressionSet.Values["Value"] = "=1+1";
        expressionWhile.Values["Expression"] = "=$i<5";
        expressionIncrement.Values["Name"] = "i";
        expressionIncrement.Values["Operand"] = "1";
        expressionIf.Values["Expression"] = "=$i==5";
        expressionLog.Values["Text"] = "готово ${i}";
        expressionStart.Links["Далее"] = expressionSet.Id;
        expressionSet.Links["Далее"] = expressionWhile.Id;
        expressionWhile.Links["Тело"] = expressionIncrement.Id;
        expressionIncrement.Links["Далее"] = expressionEnd.Id;
        expressionWhile.Links["Далее"] = expressionIf.Id;
        expressionIf.Links["Да"] = expressionLog.Id;
        var expressionEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [expressionStart, expressionSet, expressionWhile,
            expressionIncrement, expressionEnd, expressionIf, expressionLog] }, expressionEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(expressionEvents.Contains("готово 5"), "while expression reevaluates variables after every body");
        var expressionImport = CodeImporter.Convert("""
            $i = 0
            $values[0] = rnd(1, 1)
            while($i < 3)
              $i = $i + 1
            end_cyc
            if($i == 3 and $values[0] == 1)
              print("import-ok")
            end_if
            """, Point.Empty);
        Assert(expressionImport.Nodes.Any(item => item.Kind == "ArraySet") &&
            expressionImport.Nodes.Any(item => item.Kind == "WhileExpression") &&
            expressionImport.Nodes.Any(item => item.Kind == "IfExpression"),
            "expression, array and while imports create blocks");
        var expressionImportStart = BlockCatalog.Create("Start", 0, 0);
        expressionImportStart.Links["Далее"] = expressionImport.Entry;
        var importEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [expressionImportStart, .. expressionImport.Nodes] },
            importEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(importEvents.Contains("import-ok"), "imported array, expression and while execute");
        var selectionImport = CodeImporter.Convert("""
            $i = 1
            $step = 2
            for($i, $i < 6, $step)
              print($i)
            end_cyc
            switch($i)
            case(6)
              print("wrong")
            case(7)
              print("seven")
            default
              print("other")
            end_switch
            """, Point.Empty);
        var selectionStart = BlockCatalog.Create("Start", 0, 0);
        selectionStart.Links["Далее"] = selectionImport.Entry;
        var selectionEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [selectionStart, .. selectionImport.Nodes] },
            selectionEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(selectionEvents.Contains("1") && selectionEvents.Contains("3") && selectionEvents.Contains("5") &&
            selectionEvents.Contains("seven") && !selectionEvents.Contains("wrong") && !selectionEvents.Contains("other"),
            "for condition and step, switch first matching case");
        var resumedFor = CodeImporter.Convert("""
            $i = 4
            for($i, $i < 6)
              print($i)
            end_cyc
            switch(9)
            case(1)
              print("wrong")
            default
              print("fallback")
            end_switch
            """, Point.Empty);
        var resumedStart = BlockCatalog.Create("Start", 0, 0);
        resumedStart.Links["Далее"] = resumedFor.Entry;
        var resumedEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [resumedStart, .. resumedFor.Nodes] },
            resumedEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(resumedEvents.Contains("4") && resumedEvents.Contains("5") && !resumedEvents.Contains("0") &&
            resumedEvents.Contains("fallback"), "for without initializer preserves variable and switch default works");
        var screenColors = new[] { 0x0000ff, 0x00ff00, 0x0000ff, 0x112233, 0x0000ff, 0x00ff00 };
        Assert(ScreenTools.ParseColor("0x0000FF").ToArgb() == Color.Red.ToArgb(),
            "hexadecimal legacy COLORREF parses correctly");
        var screenBuffer = new ScreenAnalysisBuffer(new Rectangle(10, 20, 3, 2), screenColors);
        screenColors[0] = 0;
        Assert(screenBuffer.Pixel(new Point(10, 20)) == 0x0000ff &&
            screenBuffer.Count(new Rectangle(10, 20, 3, 2), Color.Red, 0, CancellationToken.None) == 3 &&
            screenBuffer.Find(new Rectangle(10, 20, 3, 2), Color.Lime, 0, CancellationToken.None) == new Point(11, 20),
            "snapshot stores stable pixels, counts and finds screen coordinates");
        var originalCrc = screenBuffer.Hash(new Rectangle(10, 20, 3, 2), true, CancellationToken.None);
        var originalXor = screenBuffer.Hash(new Rectangle(10, 20, 3, 2), false, CancellationToken.None);
        Assert(screenBuffer.Replace(new Rectangle(10, 20, 3, 2), Color.Red, Color.Blue, CancellationToken.None) == 3 &&
            screenBuffer.Count(new Rectangle(10, 20, 3, 2), Color.Red, 0, CancellationToken.None) == 0 &&
            screenBuffer.Count(new Rectangle(10, 20, 3, 2), Color.Blue, 0, CancellationToken.None) == 3 &&
            screenBuffer.Hash(new Rectangle(10, 20, 3, 2), true, CancellationToken.None) != originalCrc &&
            screenBuffer.Hash(new Rectangle(10, 20, 3, 2), false, CancellationToken.None) != originalXor,
            "snapshot replacement changes pixel hashes");
        var bufferFile = Path.Combine(Path.GetTempPath(), "KLIKER-screen-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            screenBuffer.SavePng(bufferFile);
            using var savedBuffer = new Bitmap(bufferFile);
            Assert(savedBuffer.Size == new Size(3, 2) && savedBuffer.GetPixel(0, 0).ToArgb() == Color.Blue.ToArgb(),
                "snapshot saves modified RGB pixels");
        }
        finally { if (File.Exists(bufferFile)) File.Delete(bufferFile); }
        var screenImport = CodeImporter.Convert("""
            getscreen(10,20,13,22)
            $color = pxl(10,20)
            $count = pxlcount(10,20,13,22,255)
            $xor = pxlxor(10,20,13,22)
            $crc = pxlcrc(10,20,13,22)
            pxlreplace(10,20,13,22,-1,65280)
            """, Point.Empty);
        Assert(screenImport.Nodes.Any(item => item.Kind == "BufferCapture") &&
            screenImport.Nodes.Any(item => item.Kind == "BufferPixel" && item.Value("Result") == "color") &&
            screenImport.Nodes.Any(item => item.Kind == "BufferCount" && item.Value("Result") == "count") &&
            screenImport.Nodes.Count(item => item.Kind == "BufferHash") == 2 &&
            screenImport.Nodes.Any(item => item.Kind == "BufferReplace" && item.Value("OldColor") == "-1"),
            "legacy screen buffer commands import as blocks");
        var bufferStart = BlockCatalog.Create("Start", 0, 0);
        var bufferPixel = BlockCatalog.Create("BufferPixel", 0, 0);
        var bufferCount = BlockCatalog.Create("BufferCount", 0, 0);
        var bufferFind = BlockCatalog.Create("BufferFindColor", 0, 0);
        var bufferLog = BlockCatalog.Create("Log", 0, 0);
        bufferPixel.Values["PixelX"] = "10";
        bufferPixel.Values["PixelY"] = "20";
        bufferPixel.Values["Result"] = "pixel";
        bufferCount.Values["Left"] = "10"; bufferCount.Values["Top"] = "20";
        bufferCount.Values["Right"] = "13"; bufferCount.Values["Bottom"] = "22";
        bufferCount.Values["Color"] = "#FF0000"; bufferCount.Values["Result"] = "count";
        bufferFind.Values["Left"] = "10"; bufferFind.Values["Top"] = "20";
        bufferFind.Values["Right"] = "13"; bufferFind.Values["Bottom"] = "22";
        bufferFind.Values["Color"] = "#00FF00";
        bufferLog.Values["Text"] = "${_return1},${_return2}|${pixel}|${count}";
        bufferStart.Links["Далее"] = bufferPixel.Id;
        bufferPixel.Links["Далее"] = bufferCount.Id;
        bufferCount.Links["Далее"] = bufferFind.Id;
        bufferFind.Links["Найден"] = bufferLog.Id;
        var bufferEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [bufferStart, bufferPixel, bufferCount, bufferFind, bufferLog] },
            bufferEvents.Add, (_, _) => { }, initialScreenBuffer: new ScreenAnalysisBuffer(
                new Rectangle(10, 20, 3, 2), new[] { 0x0000ff, 0x00ff00, 0x0000ff, 0x112233, 0x0000ff, 0x00ff00 }))
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(bufferEvents.Contains("11,20|255|3"), "screen buffer blocks set results and found coordinates");
        var scanBounds = new Rectangle(10, 20, 4, 2);
        var scanColors = new[] { 0x0000ff, 0x00ff00, 0x0000ff, 0x00ff00, 0, 0, 0, 0 };
        var scanBuffer = new ScreenAnalysisBuffer(scanBounds, scanColors);
        Assert(scanBuffer.ScanColor(scanBounds, Color.Red, 0, 10, CancellationToken.None)
            .SequenceEqual(new[] { new Point(10, 20), new Point(12, 20) }) &&
            scanBuffer.ScanColor(scanBounds, Color.Red, 0, 1, CancellationToken.None)
            .SequenceEqual(new[] { new Point(10, 20) }), "snapshot scans color coordinates and respects result limit");
        var scanTemplate = Path.Combine(Path.GetTempPath(), "KLIKER-template-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var bitmap = new Bitmap(2, 1))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.SetPixel(1, 0, Color.FromArgb(0, 0, 0, 0));
                bitmap.Save(scanTemplate);
            }
            Assert(scanBuffer.ScanImage(scanBounds, scanTemplate, 0, null, 10, CancellationToken.None)
                .SequenceEqual(new[] { new Point(10, 20), new Point(12, 20) }),
                "snapshot image scan handles transparent template pixels and overlaps");
            using (var bitmap = new Bitmap(2, 1))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.SetPixel(1, 0, Color.Blue);
                bitmap.Save(scanTemplate);
            }
            Assert(scanBuffer.ScanImage(scanBounds, scanTemplate, 0, Color.Blue, 10, CancellationToken.None)
                .SequenceEqual(new[] { new Point(10, 20), new Point(12, 20) }) &&
                scanBuffer.ScanImage(scanBounds, scanTemplate, 0, null, 10, CancellationToken.None).Count == 0,
                "snapshot image scan ignores selected background only when requested");
            var scanStart = BlockCatalog.Create("Start", 0, 0);
            var scanNode = BlockCatalog.Create("BufferScanImage", 0, 0);
            var scanLog = BlockCatalog.Create("Log", 0, 0);
            scanNode.Values["Left"] = "10"; scanNode.Values["Top"] = "20";
            scanNode.Values["Right"] = "14"; scanNode.Values["Bottom"] = "22";
            scanNode.Values["Path"] = scanTemplate;
            scanNode.Values["IgnoreColor"] = "#0000FF";
            scanNode.Values["ArrayName"] = "точки";
            scanNode.Values["CountName"] = "найдено";
            scanLog.Values["Text"] = "=$найдено";
            var firstXLog = BlockCatalog.Create("Log", 0, 0);
            var lastYLog = BlockCatalog.Create("Log", 0, 0);
            firstXLog.Values["Text"] = "=$точки[0]";
            lastYLog.Values["Text"] = "=$точки[3]";
            scanStart.Links["Далее"] = scanNode.Id;
            scanNode.Links["Далее"] = scanLog.Id;
            scanLog.Links["Далее"] = firstXLog.Id;
            firstXLog.Links["Далее"] = lastYLog.Id;
            var scanEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [scanStart, scanNode, scanLog, firstXLog, lastYLog] }, scanEvents.Add, (_, _) => { },
                initialScreenBuffer: scanBuffer).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(scanEvents.Contains("2") && scanEvents.Contains("10") && scanEvents.Contains("20"),
                "image scan block stores count and interleaved coordinates");
        }
        finally { if (File.Exists(scanTemplate)) File.Delete(scanTemplate); }
        var scanImport = CodeImporter.Convert("getscreen(10,20,14,22)\nscanpxl($points,10,20,14,22,255)\nscanpicture($images,10,20,14,22,\"a.png\")", Point.Empty);
        Assert(scanImport.Nodes.Any(item => item.Kind == "BufferScanColor" && item.Value("ArrayName") == "points") &&
            scanImport.Nodes.Any(item => item.Kind == "BufferScanImage" && item.Value("ArrayName") == "images"),
            "legacy scan commands import as blocks");
        using (var testWindow = new Form { Text = "KLIKER-self-test-" + Guid.NewGuid().ToString("N"),
            StartPosition = FormStartPosition.Manual })
        {
            var handle = testWindow.Handle;
            WindowTools.Move(handle, 83, 97, 321, 232);
            Assert(WindowTools.Bounds(handle) == new Rectangle(83, 97, 321, 232),
                "window geometry reads own hidden test window");
            WindowTools.Position(handle, 100, 120);
            WindowTools.Size(handle, 345, 256);
            Assert(WindowTools.Bounds(handle) == new Rectangle(100, 120, 345, 256),
                "window position and size preserve the other dimensions");
            Assert(ExpressionEvaluator.Evaluate("wndfind(\"KLIKER-no-such-window-" + Guid.NewGuid().ToString("N") + "\")",
                (_, _) => "0") == "0", "wndfind expression returns zero for missing title");
            var windowStart = BlockCatalog.Create("Start", 0, 0);
            var windowInfo = BlockCatalog.Create("WindowInfo", 0, 0);
            var windowLog = BlockCatalog.Create("Log", 0, 0);
            windowInfo.Values["Handle"] = handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
            windowInfo.Values["XName"] = "xw"; windowInfo.Values["YName"] = "yw";
            windowInfo.Values["WidthName"] = "ww"; windowInfo.Values["HeightName"] = "hw";
            windowLog.Values["Text"] = "${xw},${yw},${ww},${hw}";
            windowStart.Links["Далее"] = windowInfo.Id;
            windowInfo.Links["Далее"] = windowLog.Id;
            var windowEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [windowStart, windowInfo, windowLog] }, windowEvents.Add,
                (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(windowEvents.Contains("100,120,345,256"), "window info block stores geometry in variables");
        }
        var windowImport = CodeImporter.Convert("$h = wndfind(\"Блокнот\")\nwndgetinfo($h,$x,$y,$w,$hh)\nwndpos($h,10,20)\nwndsize($h,300,400)\nwndsetinfo($h,$x,$y,$w,$hh)\nwndstate($h,0)\nwndbump($h)", Point.Empty);
        Assert(windowImport.Nodes.Any(item => item.Kind == "WindowInfo") &&
            windowImport.Nodes.Count(item => item.Kind == "WindowAction") == 5,
            "legacy window commands import as blocks");
        using (var elementHost = new Form { StartPosition = FormStartPosition.Manual,
            Location = new Point(Screen.PrimaryScreen!.WorkingArea.Left +
                Screen.PrimaryScreen.WorkingArea.Width / 2 - 130,
                Screen.PrimaryScreen.WorkingArea.Top + Screen.PrimaryScreen.WorkingArea.Height / 2 - 65),
            Size = new Size(260, 130), ShowInTaskbar = false, TopMost = true })
        using (var element = new TextBox { Left = 12, Top = 12, Width = 160, Text = "первый" })
        {
            elementHost.Controls.Add(element);
            elementHost.Show();
            Application.DoEvents();
            WindowTools.RaiseWithoutActivating(elementHost.Handle);
            Application.DoEvents();
            var handle = element.Handle;
            var screenPoint = element.PointToScreen(new Point(4, 4));
            var hit = WindowTools.ElementAt(screenPoint);
            Assert(WindowTools.ProcessId(handle) == Environment.ProcessId,
                "element PID belongs to the test process");
            if (WindowTools.ProcessId(hit) == Environment.ProcessId)
                Assert(hit == handle, "element lookup uses screen point and child HWND");
            else Console.WriteLine("Screen-point hit test skipped: another process overlays the test form.");
            Assert(WindowTools.ElementText(handle) == "первый", "element text reads standard control");
            WindowTools.SetElementText(handle, "второй");
            Assert(WindowTools.ElementText(handle) == "второй" && element.Text == "второй" &&
                ExpressionEvaluator.Evaluate($"hgetpid({handle.ToInt64()})", (_, _) => "0") ==
                Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "element set text and expression PID operate on own control");
            elementHost.Hide();
        }
        var elementImport = CodeImporter.Convert("$h = hget(40,50)\n$pid = hgetpid($h)\n$text = hgettext($h)\nhsettext($h,\"new\")", Point.Empty);
        Assert(elementImport.Nodes.Any(item => item.Kind == "ElementSetText") &&
            elementImport.Nodes.Count(item => item.Kind == "SetVariable") == 3,
            "legacy element functions and hsettext import as blocks");
        using (var messageWindow = new TestMessageWindow())
        {
            var handle = messageWindow.Handle;
            Assert(WindowTools.SendNumericMessage(handle, 1024, 123, 321, false) == 77,
                "SENDMESSAGE returns receiver result");
            var received = messageWindow.Inbox.Read(0);
            Assert(received.IsNew && received.WParam == 123 && received.LParam == 321,
                "WM_USER inbox stores synchronous message parameters");
            Assert(WindowTools.SendNumericMessage(handle, 1024, 456, 654, true) == 1,
                "POSTMESSAGE queues a numeric message");
            Application.DoEvents();
            received = messageWindow.Inbox.Read(received.Version);
            Assert(received.IsNew && received.WParam == 456 && received.LParam == 654,
                "WM_USER inbox receives posted message");
            var msgStart = BlockCatalog.Create("Start", 0, 0);
            var msgFirst = BlockCatalog.Create("MessageReceive", 0, 0);
            var msgSecond = BlockCatalog.Create("MessageReceive", 0, 0);
            var msgLog = BlockCatalog.Create("Log", 0, 0);
            msgFirst.Values["WName"] = "w"; msgFirst.Values["LName"] = "l"; msgFirst.Values["NewName"] = "first";
            msgSecond.Values["WName"] = "w"; msgSecond.Values["LName"] = "l"; msgSecond.Values["NewName"] = "second";
            msgLog.Values["Text"] = "${_hwnd_self}|${w}|${l}|${first}|${second}";
            msgStart.Links["Далее"] = msgFirst.Id;
            msgFirst.Links["Далее"] = msgSecond.Id;
            msgSecond.Links["Далее"] = msgLog.Id;
            var msgEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [msgStart, msgFirst, msgSecond, msgLog] }, msgEvents.Add,
                (_, _) => { }, messageInbox: messageWindow.Inbox, selfHandle: handle)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(msgEvents.Contains($"{handle.ToInt64()}|456|654|1|0"),
                "GETMESSAGE blocks report latest message and clear new flag after first read");
            var sendStart = BlockCatalog.Create("Start", 0, 0);
            var sendNode = BlockCatalog.Create("MessageSend", 0, 0);
            sendNode.Values["Handle"] = "${_hwnd_self}";
            sendNode.Values["Message"] = "1024";
            sendNode.Values["WParam"] = "808";
            sendNode.Values["LParam"] = "909";
            sendStart.Links["Далее"] = sendNode.Id;
            new Runner(new ProjectModel { Nodes = [sendStart, sendNode] }, _ => { }, (_, _) => { },
                messageInbox: messageWindow.Inbox, selfHandle: handle)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Application.DoEvents();
            var sent = messageWindow.Inbox.Read(received.Version);
            Assert(sent.IsNew && sent.WParam == 808 && sent.LParam == 909,
                "message send block addresses its own instance through _hwnd_self");
        }
        var messageImport = CodeImporter.Convert("postmessage($_hwnd_self,1024,1,2)\ngetmessage($w,$l,$fresh)\nsendmessage($_hwnd_self,1024,3,4)", Point.Empty);
        Assert(messageImport.Nodes.Count(item => item.Kind == "MessageSend") == 2 &&
            messageImport.Nodes.Any(item => item.Kind == "MessageReceive" && item.Value("NewName") == "fresh"),
            "legacy numeric message commands import as blocks");
        var signalImport = CodeImporter.Convert("print(\"X=\",3)\nlogclear()\nlogshow(0)\nsound(\"test.wav\")\nbeep(440,12)", Point.Empty);
        Assert(signalImport.Nodes.Any(item => item.Kind == "LogClear") &&
            signalImport.Nodes.Any(item => item.Kind == "LogShow") &&
            signalImport.Nodes.Any(item => item.Kind == "PlaySound") &&
            signalImport.Nodes.Any(item => item.Kind == "Beep"),
            "legacy log and sound commands import as blocks");
        var signalStart = BlockCatalog.Create("Start", 0, 0);
        signalStart.Links["Далее"] = signalImport.Entry;
        var signalEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [signalStart, .. signalImport.Nodes] }, signalEvents.Add, (_, _) => { },
            clearLog: () => signalEvents.Add("clear"), showLog: visible => signalEvents.Add(visible ? "show" : "hide"),
            playSound: path => signalEvents.Add("sound:" + Path.GetFileName(path)),
            beep: (frequency, duration, _) => { signalEvents.Add($"beep:{frequency}:{duration}"); return Task.CompletedTask; })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(signalEvents.Contains("X=3") && signalEvents.Contains("clear") && signalEvents.Contains("hide") &&
            signalEvents.Contains("sound:test.wav") && signalEvents.Contains("beep:440:12"),
            "log and sound blocks execute and pass parameters");
        try
        {
            CodeImporter.Convert("logshow(1,20,30)", Point.Empty);
            throw new Exception("Self-test failed: unsupported LOGSHOW coordinates were imported");
        }
        catch (CodeImportException) { }
        try
        {
            AudioTools.PlayWave("not-a-wave.mp3");
            throw new Exception("Self-test failed: non-WAV audio was accepted");
        }
        catch (InvalidOperationException) { }
        var dialogImport = CodeImporter.Convert("$answer = dialogbox(\"Continue?\",4,2)\n$text = inputbox(\"Name?\",\"guest\",5)\nprint($answer,\":\",$text)", Point.Empty);
        foreach (var buttonSet in ChoiceDialogCatalog.Sets)
        {
            Assert(ChoiceDialogCatalog.ButtonsFor(buttonSet.Code).SequenceEqual(buttonSet.Buttons),
                "dialog set " + buttonSet.Code);
            foreach (var button in buttonSet.Buttons)
                Assert(ChoiceDialogCatalog.ResultCode(button) is >= 1 and <= 7,
                    "dialog result " + button);
        }
        Assert(ChoiceDialogCatalog.ResultCode("Да") == (int)DialogResult.Yes &&
            ChoiceDialogCatalog.ResultCode("Да") == 6 &&
            ChoiceDialogCatalog.ResultCode("Нет") == 7 &&
            ChoiceDialogCatalog.ButtonsFor(4).SequenceEqual(["Да", "Нет"]),
            "Yes/No set code 4 differs from result codes 6/7");
        Assert(dialogImport.Nodes.Any(item => item.Kind == "ChoiceDialog" && item.Value("Result") == "answer") &&
            dialogImport.Nodes.Any(item => item.Kind == "InputDialog" && item.Value("Result") == "text"),
            "legacy dialogs import into result blocks");
        var dialogStart = BlockCatalog.Create("Start", 0, 0);
        dialogStart.Links["Далее"] = dialogImport.Entry;
        var dialogEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [dialogStart, .. dialogImport.Nodes] }, dialogEvents.Add, (_, _) => { },
            inputDialog: (message, defaultValue, timeout, _, _) =>
            {
                dialogEvents.Add($"input:{message}:{defaultValue}:{timeout}");
                return Task.FromResult("Лена");
            },
            choiceDialog: (message, buttons, icon, _, _) =>
            {
                dialogEvents.Add($"choice:{message}:{buttons}:{icon}");
                return Task.FromResult(6);
            }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(dialogEvents.Contains("choice:Continue?:4:2") && dialogEvents.Contains("input:Name?:guest:5") &&
            dialogEvents.Contains("6:Лена"), "dialog blocks store results for subsequent expressions");
        using (var owner = new Form())
        using (var dialogStop = new CancellationTokenSource(150))
        {
            _ = owner.Handle;
            try
            {
                DialogTools.Input(owner, "Cancellation test", "default", 0, dialogStop.Token);
                throw new Exception("Self-test failed: input dialog ignored cancellation");
            }
            catch (OperationCanceledException) { }
        }
        var radioImport = CodeImporter.Convert("$pick = radiobox(\"Выбор\",\"Красный\",\"Зелёный\",\"Синий\")\nhintpopup(\"Готово\",\"Проверка\")\nprint($pick)", Point.Empty);
        Assert(radioImport.Nodes.Any(item => item.Kind == "RadioDialog" &&
            item.Value("Options") == "Красный\nЗелёный\nСиний") &&
            radioImport.Nodes.Any(item => item.Kind == "HintPopup"),
            "legacy RADIOBOX and HINTPOPUP import as blocks");
        var radioStart = BlockCatalog.Create("Start", 0, 0);
        radioStart.Links["Далее"] = radioImport.Entry;
        var radioEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [radioStart, .. radioImport.Nodes] }, radioEvents.Add, (_, _) => { },
            radioDialog: (message, options, _, _) =>
            {
                radioEvents.Add($"radio:{message}:{string.Join('|', options)}");
                return Task.FromResult(2);
            },
            hintPopup: (message, title) => radioEvents.Add($"hint:{title}:{message}"))
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(radioEvents.Contains("radio:Выбор:Красный|Зелёный|Синий") &&
            radioEvents.Contains("hint:Проверка:Готово") && radioEvents.Contains("2"),
            "radio selection result and notification flow through scenario");
        using (var owner = new Form())
        using (var radioStop = new CancellationTokenSource(150))
        {
            _ = owner.Handle;
            try
            {
                DialogTools.Radio(owner, "Cancellation test", ["one", "two"], radioStop.Token);
                throw new Exception("Self-test failed: radio dialog ignored cancellation");
            }
            catch (OperationCanceledException) { }
        }
        var arrayImport = CodeImporter.Convert("arrpush($arr,10)\narrpush($arr,2)\narrsort($arr)\nprint($arr[0],\":\",arrsize($arr),\":\",arrpop($arr),\":\",arrsize($arr))", Point.Empty);
        var arrayStart = BlockCatalog.Create("Start", 0, 0);
        arrayStart.Links["Далее"] = arrayImport.Entry;
        var arrayEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [arrayStart, .. arrayImport.Nodes] }, arrayEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(arrayEvents.Contains("2:2:10:1") &&
            arrayImport.Nodes.Count(item => item.Kind == "ArrayAction") == 3,
            "ARRPUSH/ARRSORT import and ARRSIZE/ARRPOP expression evaluate in order");
        var defineImport = CodeImporter.Convert("define($n,5)\ndefine($n,99)\ninc($n,2)\nprint($n)\nundefine($n)\nprint($n)\ndefine($n,9)\ninc($n)\nprint($n)", Point.Empty);
        var defineStart = BlockCatalog.Create("Start", 0, 0);
        defineStart.Links["Далее"] = defineImport.Entry;
        var defineEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [defineStart, .. defineImport.Nodes] }, defineEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(defineEvents.Contains("7") && defineEvents.Contains("0") && defineEvents.Contains("10") &&
            defineImport.Nodes.Any(item => item.Kind == "UndefineVariable"),
            "DEFINE/INC/UNDEFINE import and execute in sequence");
        var dynamicImport = CodeImporter.Convert("$target=10\n$ref=\"$target\"\n$before=getvar($ref)\nsetvar($ref,$before*3)\nprint($target)", Point.Empty);
        var dynamicStart = BlockCatalog.Create("Start", 0, 0);
        dynamicStart.Links["Далее"] = dynamicImport.Entry;
        var dynamicEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [dynamicStart, .. dynamicImport.Nodes] }, dynamicEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(dynamicEvents.Contains("30") && dynamicImport.Nodes.Any(item => item.Kind == "SetVariableByName"),
            "GETVAR expression and SETVAR command update a dynamically named variable");
        var splitImport = CodeImporter.Convert("$line = \"one,,three\"\nstrseparate($line,\",\",$parts)\nprint($parts[0],\":\",$parts[1],\":\",$parts[2],\":\",arrsize($parts),\":\",$_strseparate_count)", Point.Empty);
        var splitStart = BlockCatalog.Create("Start", 0, 0);
        splitStart.Links["Далее"] = splitImport.Entry;
        var splitEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [splitStart, .. splitImport.Nodes] }, splitEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(splitEvents.Contains("one::three:3:3") && splitImport.Nodes.Any(item => item.Kind == "StringSplit"),
            "STRSEPARATE import splits text into array including empty parts");
        var dataImport = CodeImporter.Convert("$clip = fromclip()\ntoclip($clip)\n$web = httpget(\"https://example.com\")\n$line = tfread(\"sample.txt\",2)\n$count = tfcount(\"sample.txt\")\ntfdelete(\"sample.txt\",1)\ntfclear(\"sample.txt\",0)", Point.Empty);
        Assert(dataImport.Nodes.Any(item => item.Kind == "ClipboardGet" && item.Value("Name") == "clip") &&
            dataImport.Nodes.Any(item => item.Kind == "ClipboardSet" && item.Value("Text") == "${clip}") &&
            dataImport.Nodes.Any(item => item.Kind == "HttpGet" && item.Value("Result") == "web") &&
            dataImport.Nodes.Count(item => item.Kind == "TextFile") == 4,
            "legacy clipboard, HTTP and text-file data commands import to blocks");
        var httpImport = CodeImporter.Convert("$web = httpget(\"https://example.com\",\"X-Test: 1\",\"Accept: text/plain\")", Point.Empty);
        Assert(httpImport.Nodes.Single(item => item.Kind == "HttpGet").Value("Headers") ==
            "X-Test: 1\nAccept: text/plain", "HTTPGET imports multiple request headers");
        Assert(DataTools.ParseHttpHeaders("X-Test: 1\nAccept: text/plain").Count == 2,
            "HTTP headers parse one per line");
        try
        {
            DataTools.ParseHttpHeaders("Broken header");
            throw new Exception("Self-test failed: malformed HTTP header was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("заголовок")) { }
        using (var httpRequest = DataTools.CreateHttpRequest("https://example.com/", "X-Kliker-Test: yes"))
            Assert(httpRequest.Method == HttpMethod.Get &&
                httpRequest.Headers.TryGetValues("X-Kliker-Test", out var headerValues) &&
                headerValues.Single() == "yes",
                "HTTPGET builds request with custom header without external network");
        Assert(CodeImporter.Convert("tfclear(\"sample.txt\",1)", Point.Empty).Nodes
            .Any(item => item.Kind == "TextFile" && item.Value("Action") == "Очистить или удалить файл" &&
                item.Value("DeleteFile") == "1"), "TFCLEAR delete flag imports");
        var manualSwitchStart = BlockCatalog.Create("Start", 0, 0);
        var manualSwitch = BlockCatalog.Create("SwitchValue", 0, 0);
        var manualSwitchHit = BlockCatalog.Create("Log", 0, 0);
        var manualSwitchMiss = BlockCatalog.Create("Log", 0, 0);
        manualSwitch.Values["Expression"] = "=2+1";
        manualSwitch.Values["Case1"] = "1";
        manualSwitch.Values["Case2"] = "=1+2";
        manualSwitchHit.Values["Text"] = "selected";
        manualSwitchMiss.Values["Text"] = "missed";
        manualSwitchStart.Links["Далее"] = manualSwitch.Id;
        manualSwitch.Links["Случай 2"] = manualSwitchHit.Id;
        manualSwitch.Links["Иначе"] = manualSwitchMiss.Id;
        var manualEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [manualSwitchStart, manualSwitch, manualSwitchHit, manualSwitchMiss] },
            manualEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(manualEvents.Contains("selected") && !manualEvents.Contains("missed"), "manual switch selects first numeric match");
        var dataFolder = Path.Combine(Path.GetTempPath(), "KLIKER-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataFolder);
        try
        {
            var childCode = Path.Combine(dataFolder, "child.cms");
            var nestedCode = Path.Combine(dataFolder, "nested.cms");
            var legacyCms = Path.Combine(dataFolder, "legacy.cms");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            File.WriteAllBytes(legacyCms, System.Text.Encoding.GetEncoding(1251).GetBytes(
                "#define MSG:\"Привет\"\n#include \"legacy-child.cms\"\nprint(MSG)"));
            File.WriteAllBytes(Path.Combine(dataFolder, "legacy-child.cms"),
                System.Text.Encoding.GetEncoding(1251).GetBytes("print(MSG)"));
            var legacyText = CmsFileReader.Read(legacyCms);
            var legacyImport = CodeImporter.Convert(legacyText, Point.Empty, dataFolder);
            var legacyStart = BlockCatalog.Create("Start", 0, 0);
            legacyStart.Links["Далее"] = legacyImport.Entry;
            var legacyEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [legacyStart, .. legacyImport.Nodes] },
                legacyEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(legacyEvents.Count(message => message == "Привет") == 2,
                "CP1251 CMS and CP1251 #include decode and execute correctly");
            var unicodeCms = Path.Combine(dataFolder, "unicode.cms");
            File.WriteAllText(unicodeCms, "print(\"Юникод\")", new System.Text.UnicodeEncoding(false, true));
            Assert(CmsFileReader.Read(unicodeCms).Contains("Юникод"), "UTF-16 BOM CMS decodes correctly");
            var invalidCms = Path.Combine(dataFolder, "binary.cms");
            File.WriteAllBytes(invalidCms, [0, 1, 2]);
            try
            {
                CmsFileReader.Read(invalidCms);
                throw new Exception("Self-test failed: binary CMS was accepted");
            }
            catch (InvalidDataException) { }
            var logImport = CodeImporter.Convert("#logfile \"script.log\"\nprint(\"один\")\nlogwrite(\"два\")",
                Point.Empty, dataFolder);
            Assert(logImport.Entry == logImport.Nodes.Single(node => node.Kind == "LogFile").Id,
                "#logfile configures output before script commands");
            var logStart = BlockCatalog.Create("Start", 0, 0);
            logStart.Links["Далее"] = logImport.Entry;
            var logProject = new ProjectModel { Nodes = [logStart, .. logImport.Nodes] };
            new Runner(logProject, _ => { }, (_, _) => { }, baseDirectory: dataFolder)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            var scriptLog = Path.Combine(dataFolder, "script.log");
            Assert(File.ReadAllLines(scriptLog).SequenceEqual(["один", "два"]),
                "#logfile duplicates only PRINT/LOGWRITE output to file");
            new Runner(logProject, _ => { }, (_, _) => { }, baseDirectory: dataFolder)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(File.ReadAllLines(scriptLog).SequenceEqual(["один", "два", "один", "два"]),
                "#logfile appends on a later run");
            try
            {
                CodeImporter.Convert("#logfile \"../outside.log\"\nprint(1)", Point.Empty, dataFolder);
                throw new Exception("Self-test failed: #logfile accepted parent directory");
            }
            catch (CodeImportException error) { Assert(error.LineNumber == 1, "#logfile rejects external path"); }
            try
            {
                CodeImporter.Convert("#logfile \"a.log\"\n#logfile \"b.log\"\nprint(1)", Point.Empty, dataFolder);
                throw new Exception("Self-test failed: duplicate #logfile was accepted");
            }
            catch (CodeImportException error) { Assert(error.LineNumber == 2, "duplicate #logfile rejects"); }
            File.WriteAllText(childCode, "print(MSG)\n#include \"nested.cms\"");
            File.WriteAllText(nestedCode, "print(MSG)");
            var includedImport = CodeImporter.Convert("#define MSG:\"ready\"\n#include \"child.cms\"\nprint(MSG)",
                Point.Empty, dataFolder);
            var includedStart = BlockCatalog.Create("Start", 0, 0);
            includedStart.Links["Далее"] = includedImport.Entry;
            var includedEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [includedStart, .. includedImport.Nodes] },
                includedEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(includedEvents.Count(message => message == "ready") == 3,
                "#define expands in root and nested #include files before block import");
            try
            {
                CodeImporter.Convert("#include \"../outside.cms\"", Point.Empty, dataFolder);
                throw new Exception("Self-test failed: #include escaped project directory");
            }
            catch (CodeImportException error) { Assert(error.LineNumber == 1 && error.Message.Contains("папки проекта"),
                "#include rejects paths outside project directory"); }
            File.WriteAllText(childCode, "#include \"child.cms\"");
            try
            {
                CodeImporter.Convert("#include \"child.cms\"", Point.Empty, dataFolder);
                throw new Exception("Self-test failed: cyclic #include was accepted");
            }
            catch (CodeImportException error) { Assert(error.LineNumber == 1 && error.Message.Contains("цикл #include"),
                "cyclic #include rejects at root directive"); }
            File.WriteAllText(childCode, "unsupported_command()");
            try
            {
                CodeImporter.Convert("#include \"child.cms\"", Point.Empty, dataFolder);
                throw new Exception("Self-test failed: invalid included command was accepted");
            }
            catch (CodeImportException error) { Assert(error.LineNumber == 1 &&
                error.Message.Contains("child.cms") && error.Message.Contains("строка 1"),
                "included command error identifies source file and line"); }
            var dataFile = Path.Combine(dataFolder, "sample.txt");
            Assert(DataTools.TextFile(dataFile, "Добавить строку", 1, "первый") == "1" &&
                DataTools.TextFile(dataFile, "Добавить строку", 1, "второй") == "2" &&
                DataTools.TextFile(dataFile, "Прочитать строку", 2, "") == "второй",
                "text file append and read");
            var clearFile = Path.Combine(dataFolder, "clear.kltest");
            File.WriteAllText(clearFile, "temporary");
            var clearImport = CodeImporter.Convert("$mode=0\ntfclear(\"clear.kltest\",$mode)\n$mode=1\ntfclear(\"clear.kltest\",$mode)", Point.Empty);
            var clearStart = BlockCatalog.Create("Start", 0, 0);
            clearStart.Links["Далее"] = clearImport.Entry;
            new Runner(new ProjectModel { Nodes = [clearStart, .. clearImport.Nodes] }, _ => { }, (_, _) => { },
                baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(!File.Exists(clearFile), "TFCLEAR runtime flag clears then deletes temporary file");
            File.WriteAllText(clearFile, "temporary");
            var invalidClear = CodeImporter.Convert("tfclear(\"clear.kltest\",2)", Point.Empty);
            var invalidStart = BlockCatalog.Create("Start", 0, 0);
            invalidStart.Links["Далее"] = invalidClear.Entry;
            try
            {
                new Runner(new ProjectModel { Nodes = [invalidStart, .. invalidClear.Nodes] }, _ => { }, (_, _) => { },
                    baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                throw new Exception("Self-test failed: invalid TFCLEAR flag was accepted");
            }
            catch (InvalidOperationException error)
            {
                Assert(error.Message.Contains("флаг удаления") && File.ReadAllText(clearFile) == "temporary",
                    "invalid TFCLEAR flag does not change file");
            }
            Assert(DataTools.TextFile(clearFile, "Удалить файл", 1, "") == "0" && !File.Exists(clearFile),
                "manual delete-file operation removes specified file");
            var insertFile = Path.Combine(dataFolder, "insert.kltest");
            var writeImport = CodeImporter.Convert("tfwrite(\"insert.kltest\",\"первый\")\ntfwrite(\"insert.kltest\",\"третий\")\ntfwrite(\"insert.kltest\",\"второй\",2)\n$a = tfread(\"insert.kltest\",1)\n$b = tfread(\"insert.kltest\",2)\n$c = tfread(\"insert.kltest\",3)\nprint($a,\":\",$b,\":\",$c)", Point.Empty);
            var writeStart = BlockCatalog.Create("Start", 0, 0);
            writeStart.Links["Далее"] = writeImport.Entry;
            var writeEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [writeStart, .. writeImport.Nodes] }, writeEvents.Add,
                (_, _) => { }, baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(writeEvents.Contains("первый:второй:третий") &&
                File.ReadAllLines(insertFile).SequenceEqual(["первый", "второй", "третий"]),
                "TFWRITE append and positional insertion preserve following lines");
            var stringFile = Path.Combine(dataFolder, "string.kltest");
            var stringFileImport = CodeImporter.Convert("strwriteln(\"string.kltest\",\"first\")\nstrwriteln(\"string.kltest\",\"second\")\n$before=strreadln(\"string.kltest\",2)\n$rewrite=1\nstrwriteln(\"string.kltest\",\"replacement\",$rewrite)\n$after=strreadln(\"string.kltest\",1)\n$count=tfcount(\"string.kltest\")\nprint($before,\":\",$after,\":\",$count)", Point.Empty);
            var stringFileStart = BlockCatalog.Create("Start", 0, 0);
            stringFileStart.Links["Далее"] = stringFileImport.Entry;
            var stringFileEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [stringFileStart, .. stringFileImport.Nodes] },
                stringFileEvents.Add, (_, _) => { }, baseDirectory: dataFolder)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(stringFileEvents.Contains("second:replacement:1") &&
                File.ReadAllLines(stringFile).SequenceEqual(["replacement"]),
                "STRREADLN and STRWRITELN append/rewrite import use the runtime flag");
            var arrayFile = Path.Combine(dataFolder, "array.kltest");
            var arrayFileImport = CodeImporter.Convert("$arr[0]=\"one\"\n$arr[1]=\"\"\n$arr[2]=\"three\"\ntfwritearr(\"array.kltest\",$arr)\narrpush($arr,\"old\")\ntfreadarr(\"array.kltest\",$arr)\nprint($arr[0],\":\",$arr[1],\":\",$arr[2],\":\",arrsize($arr),\":\",$_tfarr_count)", Point.Empty);
            var arrayFileStart = BlockCatalog.Create("Start", 0, 0);
            arrayFileStart.Links["Далее"] = arrayFileImport.Entry;
            var arrayFileEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [arrayFileStart, .. arrayFileImport.Nodes] }, arrayFileEvents.Add,
                (_, _) => { }, baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(arrayFileEvents.Contains("one::three:3:3") &&
                File.ReadAllLines(arrayFile).SequenceEqual(["one", "", "three"]) &&
                arrayFileImport.Nodes.Count(item => item.Kind == "TextFileArray") == 2,
                "TFWRITEARR and TFREADARR round trip every array element and replace old data");
            Assert(DataTools.TextFile(dataFile, "Удалить строку", 1, "") == "1" &&
                DataTools.TextFile(dataFile, "Прочитать строку", 1, "") == "второй",
                "text file line delete");
            var fileReadImport = CodeImporter.Convert("$row = tfread(\"sample.txt\",1)\n$count = tfcount(\"sample.txt\")\nprint($row,\":\",$count)\ntfdelete(\"sample.txt\",1)\n$count = tfcount(\"sample.txt\")\nprint($count)", Point.Empty);
            var fileReadStart = BlockCatalog.Create("Start", 0, 0);
            fileReadStart.Links["Далее"] = fileReadImport.Entry;
            var fileReadEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [fileReadStart, .. fileReadImport.Nodes] }, fileReadEvents.Add,
                (_, _) => { }, baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(fileReadEvents.Contains("второй:1") && fileReadEvents.Contains("0"),
                "TFREAD/TFCOUNT/TFDELETE import executes against temporary file");
            DataTools.TextFile(dataFile, "Добавить строку", 1, "второй");
            var iniFile = Path.Combine(dataFolder, "settings.ini");
            DataTools.Ini(iniFile, "main", "value", "A");
            DataTools.Ini(iniFile, "main", "value", "B");
            Assert(DataTools.Ini(iniFile, "main", "value", null) == "B", "INI key round trip");
            var iniImport = CodeImporter.Convert("iniwrite(\"import.ini\",\"key\",\"alpha\")\niniwrite(\"import.ini\",\"key\",\"beta\",\"custom\")\n$a = iniread(\"import.ini\",\"key\")\n$b = iniread(\"import.ini\",\"key\",\"custom\")\nprint($a,\":\",$b)", Point.Empty);
            var iniStart = BlockCatalog.Create("Start", 0, 0);
            iniStart.Links["Далее"] = iniImport.Entry;
            var iniEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [iniStart, .. iniImport.Nodes] }, iniEvents.Add,
                (_, _) => { }, baseDirectory: dataFolder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(iniEvents.Contains("alpha:beta") &&
                iniImport.Nodes.Count(item => item.Kind == "IniFile") == 4 &&
                DataTools.Ini(Path.Combine(dataFolder, "import.ini"), "default", "key", null) == "alpha" &&
                DataTools.Ini(Path.Combine(dataFolder, "import.ini"), "custom", "key", null) == "beta",
                "INIREAD and INIWRITE import default and explicit sections");
            Assert(DataTools.ListFiles(dataFolder, "*.txt", false).SequenceEqual(["sample.txt"]),
                "file list mask");
            Directory.CreateDirectory(Path.Combine(dataFolder, "subdir"));
            var patternRoot = dataFolder.Replace('\\', '/');
            var fileImport = CodeImporter.Convert($"getfilelist($files,\"{patternRoot}/*.txt\")\ngetdirlist($dirs,\"{patternRoot}/*\")\nprint(arrsize($files),\":\",$files[0],\":\",arrsize($dirs))", Point.Empty);
            var fileStart = BlockCatalog.Create("Start", 0, 0);
            fileStart.Links["Далее"] = fileImport.Entry;
            var fileEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [fileStart, .. fileImport.Nodes] }, fileEvents.Add, (_, _) => { })
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(fileEvents.Contains("1:sample.txt:1") &&
                fileImport.Nodes.Count(item => item.Kind == "DirectoryList") == 2,
                "GETFILELIST and GETDIRLIST import mask and populate arrays");
        }
        finally { Directory.Delete(dataFolder, true); }
        var variableStart = BlockCatalog.Create("Start", 0, 0);
        var set = BlockCatalog.Create("SetVariable", 0, 0);
        var math = BlockCatalog.Create("MathVariable", 0, 0);
        var test = BlockCatalog.Create("IfVariable", 0, 0);
        var output = BlockCatalog.Create("Log", 0, 0);
        set.Values["Name"] = "n"; set.Values["Value"] = "9";
        math.Values["Name"] = "n"; math.Values["Operand"] = "1";
        test.Values["Name"] = "n"; test.Values["Comparison"] = "Равно"; test.Values["Value"] = "10";
        output.Values["Text"] = "n=${n}";
        variableStart.Links["Далее"] = set.Id;
        set.Links["Далее"] = math.Id;
        math.Links["Далее"] = test.Id;
        test.Links["Да"] = output.Id;
        var variableEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [variableStart, set, math, test, output] }, variableEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(variableEvents.Contains("n=10"), "variable blocks and runtime substitution");
        var repeatStart = BlockCatalog.Create("Start", 0, 0);
        var repeat = BlockCatalog.Create("Repeat", 0, 0);
        var body = BlockCatalog.Create("Log", 0, 0);
        var after = BlockCatalog.Create("Log", 0, 0);
        repeat.Values["Times"] = "10";
        body.Values["Text"] = "body";
        after.Values["Text"] = "after";
        repeatStart.Links["Далее"] = repeat.Id;
        repeat.Links["Тело"] = body.Id;
        repeat.Links["Далее"] = after.Id;
        var repeatEvents = new List<string>();
        new Runner(new ProjectModel { Nodes = [repeatStart, repeat, body, after] }, repeatEvents.Add, (_, _) => { })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(repeatEvents.Count(x => x == "body") == 10 && repeatEvents.Count(x => x == "after") == 1 &&
            repeatEvents.FindLastIndex(x => x == "body") < repeatEvents.FindIndex(x => x == "after"),
            "Repeat runs body ten times before Далее");
        using var canvas = new GraphCanvas();
        canvas.SetWorldSize(2200, 1500);
        canvas.Zoom = 4f;
        Assert(canvas.Zoom > 2.5f && canvas.Size == new Size(2200, 1500), "unbounded canvas zoom without size cap");
        canvas.ResetView();
        Assert(canvas.Zoom == 1f, "reset canvas view");
        var waitStart = BlockCatalog.Create("Start", 0, 0);
        var longWait = BlockCatalog.Create("Wait", 0, 0);
        waitStart.Links["Далее"] = longWait.Id;
        longWait.Values["Milliseconds"] = "10000";
        using var stop = new CancellationTokenSource(30);
        try
        {
            new Runner(new ProjectModel { Nodes = [waitStart, longWait] }, _ => { }, (_, _) => { })
                .RunAsync(stop.Token).GetAwaiter().GetResult();
            throw new Exception("Self-test failed: cancellation was ignored");
        }
        catch (OperationCanceledException) { }
        using (var settingsWindow = new MouseMappingSettingsForm(new MouseMappingSettings
        {
            Profiles = [new MouseMappingProfile { Name = "Тест", Layers = [new MouseMappingLayer
                { Bindings = [new MouseMappingBinding()] }] }]
        }))
            Assert(settingsWindow.Result.Profiles.Count == 1,
                "mouse mapping settings window loads profiles and bindings");
        using (var journalSettings = new JournalSettingsForm(true, "my-journal.txt", null))
            Assert(journalSettings.WriteToFile && journalSettings.FileName == "my-journal.txt",
                "journal settings dialog reflects saved project settings");
        TestCompactControlLocalization();
        TestShowcaseButtonLayout();
        TestSequentialWindowLoops();
        Console.WriteLine("Self-test passed");
    }

    private static void TestGraphAppearanceAndRouting()
    {
        var start = BlockCatalog.Create("Start", 40, 40);
        var middle = BlockCatalog.Create("Log", 320, 60);
        var target = BlockCatalog.Create("Log", 620, 80);
        var original = BlockAppearanceManager.Resolve(middle).Copy();
        try
        {
            BlockAppearanceManager.Reload(new AppSettings());
            var input = GraphLinkRouter.InputPoint(middle);
            var output = GraphLinkRouter.OutputPoint(start, 0);
            Assert(input.X <= middle.X + 20 && input.Y <= middle.Y + 20 &&
                output.X >= start.X + BlockCatalog.Width - 20 &&
                output.Y >= start.Y + BlockCatalog.Height(start) - 20,
                "default ports sit on upper-left/lower-right block outlines");
            using (var outline = BlockGeometry.Outline(BlockCatalog.Bounds(middle), "Rounded"))
                Assert(outline.IsOutlineVisible(input, new Pen(Color.Black, 5)),
                    "default input lies on rounded outline");
            foreach (var shape in new[] { "Rectangle", "Square", "Circle", "Diamond" })
            {
                middle.Appearance = new BlockAppearance { Shape = shape, InputAngle = 270,
                    OutputAngles = new Dictionary<string, int> { ["Далее"] = 90 },
                    FillColor = "#123456", FillOpacityPercent = 55,
                    BorderColor = "#ABCDEF", BorderOpacityPercent = 75, BorderWidth = 2.5m };
                using var path = BlockGeometry.Outline(BlockCatalog.Bounds(middle), shape);
                using var pen = new Pen(Color.Black, 5);
                Assert(path.IsOutlineVisible(GraphLinkRouter.InputPoint(middle), pen) &&
                    path.IsOutlineVisible(GraphLinkRouter.OutputPoint(middle, 0), pen),
                    $"{shape} ports remain on contour");
            }
            middle.Appearance = new BlockAppearance { Shape = "Circle", InputAngle = 135,
                OutputAngles = new Dictionary<string, int> { ["Далее"] = 315 },
                InputPort = new PortAppearance { Shape = "Diamond", Size = 22,
                    FillColor = "#123456", FillOpacityPercent = 65 },
                OutputPorts = new Dictionary<string, PortAppearance> { ["Далее"] = new()
                    { Shape = "Triangle", Size = 19, FillColor = "#654321" } } };
            start.LinkStyles["Далее"] = new LinkAppearance { End = "Diamond", Pattern = "Dash",
                Color = "#12AB34", OpacityPercent = 64, Thickness = 3.5m,
                EndSize = 24, ExitDistance = 30, EntryDistance = 20 };
            start.Links["Далее"] = target.Id;
            var nodes = new[] { start, middle, target };
            var route = GraphLinkRouter.Route(start, 0, target, nodes, start.LinkStyles["Далее"]);
            Assert(route.Length >= 3 && route[0] == GraphLinkRouter.OutputPoint(start, 0) &&
                route[^1] == GraphLinkRouter.InputPoint(target) &&
                Enumerable.Range(1, route.Length - 1).All(i =>
                    !GraphLinkRouter.CrossesInterior(route[i - 1], route[i], BlockCatalog.Bounds(middle))),
                "connection routes around intervening block");
            var project = new ProjectModel { Nodes = [start, middle, target] };
            var code = GraphCodeCodec.Serialize(project);
            var parsed = GraphCodeCodec.Parse(code);
            Assert(parsed[0].LinkStyles["Далее"].End == "Diamond" &&
                parsed[0].LinkStyles["Далее"].EndSize == 24 &&
                parsed[0].LinkStyles["Далее"].ExitDistance == 30 &&
                parsed[1].Appearance?.InputAngle == 135 &&
                parsed[1].Appearance?.InputPort.Size == 22 &&
                parsed[1].Appearance?.OutputPorts["Далее"].Shape == "Triangle" &&
                GraphCodeCodec.Serialize(new ProjectModel { Nodes = parsed }) == code,
                "block and link appearances survive code round trip");
            var straightStyle = new LinkAppearance { ExitDistance = 0, EntryDistance = 0 };
            var direct = GraphLinkRouter.Route(start, 0, target, [start, target], straightStyle);
            Assert(direct.Length == 2 && direct[0] == GraphLinkRouter.OutputPoint(start, 0) &&
                direct[1] == GraphLinkRouter.InputPoint(target),
                "zero bend distances produce a direct connection when unobstructed");
            var backward = GraphLinkRouter.Route(target, 0, start, [start, target], straightStyle);
            Assert(backward.Length > 2,
                "a backward connection avoids running through its own blocks even with zero bend distances");
            using (var descriptionAppearance = new BlockAppearanceForm(BlockCatalog.Create("Description", 0, 0)))
                Assert(descriptionAppearance.Appearance.IsValid(),
                    "appearance editor opens for a description block without connector controls");
            Assert(DataTools.JsonValue("{\"user\":{\"name\":\"Alex\"},\"items\":[{\"id\":7}]}",
                "user.name") == "Alex" && DataTools.JsonValue("{\"items\":[{\"id\":7}]}",
                "items[0].id") == "7", "JSON path resolves nested properties and array indexes");
            var snapshot = BlockClipboard.Copy([middle]);
            Assert(snapshot.Single().Appearance?.Shape == "Circle" &&
                snapshot.Single().Appearance?.InputPort.Size == 22 &&
                !ReferenceEquals(snapshot.Single().Appearance, middle.Appearance),
                "block appearance is copied independently");
            var pasted = BlockClipboard.Paste(snapshot, new Point(900, 900), new ProjectModel());
            Assert(pasted.Single().Appearance?.InputAngle == 135,
                "pasted block keeps custom port position");
            var defaults = new AppSettings { BlockAppearanceDefaults = new Dictionary<string, BlockAppearance>
                { ["Log"] = new() { Shape = "Diamond" } } };
            BlockAppearanceManager.Reload(defaults);
            Assert(BlockAppearanceManager.Resolve(BlockCatalog.Create("Log", 0, 0)).Shape == "Diamond" &&
                BlockAppearanceManager.Resolve(middle).Shape == "Circle",
                "per-type default applies to new blocks and local appearance overrides it");
            UiLanguage.Configure("en");
            foreach (var text in new[] { "Оформление блока…", "Форма блока", "Точка на контуре",
                "Выбрать цвет заливки…", "Оформление связи", "Штриховая", "Стрелка",
                "Форма выбранной точки", "Размер выбранной точки (6–36)",
                "Размер наконечника (4–48)", "Отступ изгиба от входа (0–200)" })
                Assert(UiLanguage.Has(text) && !UiLanguage.T(text).Any(c => c is >= '\u0400' and <= '\u04FF'),
                    $"English graph appearance control: {text}");
            UiLanguage.Configure("ru");
        }
        finally
        {
            BlockAppearanceManager.Reload(AppSettings.Load());
            UiLanguage.Configure("ru");
        }
    }

    private static void TestNewAutomation()
    {
        var semantic = new MacroRecording { Mode = "Клики по элементам Windows", Events =
        [new MacroEvent { Kind = "Click", Button = "Левая", X = 20, Y = 30,
            WindowTitle = "Test window", AutomationId = "SaveButton", ControlType = "Button" }] };
        var project = ProjectModel.CreateSample();
        var built = MacroBuilder.AddToProject(project, semantic);
        Assert(built.NodesAdded == 2 && project.Nodes.Count(node => node.Kind == "UiWait") == 1 &&
            project.Nodes.Count(node => node.Kind == "UiAction") == 1 &&
            project.Nodes.All(node => node.Kind != "Click"),
            "semantic macro converts a captured click into element wait and action");
        var fallback = new MacroRecording { Mode = "Клики по элементам Windows", Events =
            [new MacroEvent { Kind = "Click", Button = "Левая", X = 20, Y = 30 }] };
        var fallbackProject = ProjectModel.CreateSample();
        MacroBuilder.AddToProject(fallbackProject, fallback);
        Assert(fallbackProject.Nodes.Any(node => node.Kind == "Click"),
            "semantic recorder falls back to coordinates when no element was exposed");
        Assert(Runner.ParseModulePairs("child_name=hello\nchild_count=2", "входы").Count == 2,
            "module inputs parse one assignment per line");
        try
        {
            Runner.ParseModulePairs("не_латиница=1", "входы");
            throw new Exception("Self-test failed: module accepted Unicode variable name");
        }
        catch (InvalidOperationException) { }
        var eventFile = Path.Combine(Path.GetTempPath(), "Kliker-event-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var awaited = EventWaitTools.FileAsync(eventFile, 3000, CancellationToken.None);
            File.WriteAllText(eventFile, "changed");
            Assert(awaited.GetAwaiter().GetResult(), "file change event wakes the scenario");
        }
        finally { if (File.Exists(eventFile)) File.Delete(eventFile); }
        var start = BlockCatalog.Create("Start", 0, 0);
        var log = BlockCatalog.Create("Log", 200, 0);
        log.Breakpoint = true;
        start.Links["Далее"] = log.Id;
        var stopped = 0;
        var runner = new Runner(new ProjectModel { Nodes = [start, log] }, _ => { }, (_, _) => { },
            debugState: (id, _, pause) => { if (id == log.Id && pause) Interlocked.Exchange(ref stopped, 1); });
        var execution = Task.Run(() => runner.RunAsync(CancellationToken.None));
        Assert(SpinWait.SpinUntil(() => Volatile.Read(ref stopped) == 1 && runner.IsPauseRequested, 3000) && !execution.IsCompleted,
            "breakpoint pauses before executing the selected block");
        runner.StepDebug();
        execution.GetAwaiter().GetResult();
        var code = GraphCodeCodec.Serialize(new ProjectModel { Nodes = [start, log] });
        Assert(GraphCodeCodec.Parse(code)[1].Breakpoint && code.Contains("breakpoint true"),
            "breakpoint survives graph code round trip");
    }

    private static void TestUiAutomationLive()
    {
        var title = "Kliker UIA smoke " + Guid.NewGuid().ToString("N");
        using var ready = new ManualResetEventSlim();
        Form? target = null;
        Exception? windowError = null;
        var clicked = 0;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { Text = title, Size = new Size(300, 150),
                    StartPosition = FormStartPosition.Manual, Location = new Point(120, 120) };
                var button = new Button { Name = "SmokeButton", Text = "Smoke action",
                    Location = new Point(20, 20), Size = new Size(160, 40) };
                button.Click += (_, _) => Interlocked.Exchange(ref clicked, 1);
                form.Controls.Add(button);
                target = form;
                form.Shown += (_, _) => ready.Set();
                Application.Run(form);
            }
            catch (Exception error) { windowError = error; ready.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert(ready.Wait(5000) && windowError is null, "UI Automation smoke window opened");
            var button = UiAutomationTools.Find(new UiElementSelector(title, "", "Smoke action", "Button"));
            Assert(button is not null && UiAutomationTools.Act(button, "Нажать", "") == "1" &&
                SpinWait.SpinUntil(() => Volatile.Read(ref clicked) == 1, 3000),
                "UI Automation finds and invokes a live Windows control");
        }
        finally
        {
            if (target is { IsDisposed: false, IsHandleCreated: true })
                target.BeginInvoke((Action)(() => target.Close()));
            Assert(thread.Join(5000), "UI Automation smoke window closed");
        }
    }

    private static void TestMouseMappings()
    {
        var press = new MouseMappingBinding { Source = "Кнопка 4", Trigger = "Нажатие", Value = "Ctrl+C" };
        var chord = new MouseMappingBinding { Source = "Кнопка 5", Trigger = "Сочетание",
            ChordSource = "Кнопка 4", Value = "Ctrl+V" };
        var shortPress = new MouseMappingBinding { Source = "ЛКМ", Trigger = "Короткое нажатие", Value = "A" };
        var longPress = new MouseMappingBinding { Source = "ЛКМ", Trigger = "Долгое удержание", DelayMs = 100, Value = "B" };
        var toggle = new MouseMappingBinding { Source = "СКМ", Trigger = "Переключатель", Value = "F8" };
        var repeat = new MouseMappingBinding { Source = "ПКМ", Trigger = "Повтор", DelayMs = 50, Value = "C" };
        var profile = new MouseMappingProfile { Name = "Редактор", ProcessName = "notepad.exe",
            Layers = [new MouseMappingLayer { Name = "Слой 1", Bindings = [press, chord, shortPress, longPress, toggle, repeat] },
                new MouseMappingLayer { Name = "Слой 2" }] };
        var settings = new MouseMappingSettings { Enabled = true,
            Profiles = [profile, new MouseMappingProfile { Name = "Остальное", MatchMode = "Все окна" }] };
        Assert(MouseMappingValidator.Check(settings) is null &&
            JsonSerializer.Deserialize<ProjectModel>(JsonSerializer.Serialize(new ProjectModel { MouseMappings = settings }))!
                .MouseMappings.Profiles[0].Layers[0].Bindings.Count == 6,
            "mouse profiles, layers and bindings validate and survive project serialization");
        var context = new MouseMappingContext(new MouseMappingWindow("notepad", "Notes", "Edit", 10, 20),
            new MouseMappingWindow("explorer", "Desktop", "", 0, 0));
        Assert(MouseProfileMatcher.Select(settings, context) == 0, "process profile has priority over fallback");
        profile.MatchMode = "Под курсором";
        Assert(MouseProfileMatcher.Select(settings, context) == 1, "hovered window profile uses cursor target");
        profile.MatchMode = "Активное окно";
        profile.UseRegion = true; profile.RegionX = 5; profile.RegionY = 15;
        profile.RegionWidth = 20; profile.RegionHeight = 20;
        Assert(MouseProfileMatcher.Select(settings, context) == 0, "profile relative region matches cursor");
        profile.RegionX = 100;
        Assert(MouseProfileMatcher.Select(settings, context) == 1, "profile outside region falls back");
        profile.UseRegion = false;
        var engine = new MouseMappingEngine(settings);
        engine.SetContext(context);
        Assert(engine.ProfileIndex == 0 && engine.LayerIndex == 0, "engine activates first matching profile");
        var down = engine.Handle("Кнопка 4", true, 1000);
        Assert(down.Suppress && down.Commands.Count == 1 && down.Commands[0].Binding.Id == press.Id,
            "mouse button press maps and suppresses original input");
        var chordDown = engine.Handle("Кнопка 5", true, 1010);
        Assert(chordDown.Suppress && chordDown.Commands.Any(command => command.Binding.Id == chord.Id),
            "button chord requires first button held");
        engine.Handle("Кнопка 5", false, 1020);
        Assert(engine.Handle("Кнопка 4", false, 1030).Suppress, "suppressed button release stays suppressed");
        engine.Handle("ЛКМ", true, 1100);
        Assert(engine.Handle("ЛКМ", false, 1150).Commands.Any(command => command.Binding.Id == shortPress.Id),
            "short press fires on release");
        engine.Handle("ЛКМ", true, 1200);
        Assert(engine.Tick(1310).Any(command => command.Binding.Id == longPress.Id) &&
            !engine.Handle("ЛКМ", false, 1320).Commands.Any(command => command.Binding.Id == shortPress.Id),
            "long hold suppresses short press action");
        Assert(engine.Handle("СКМ", true, 1400).Commands.Single().Press &&
            engine.Handle("СКМ", false, 1410).Commands.Count == 0 &&
            !engine.Handle("СКМ", true, 1420).Commands.Single().Press,
            "toggle alternates press and release");
        engine.Handle("СКМ", false, 1430);
        engine.Handle("ПКМ", true, 1500);
        Assert(engine.Tick(1560).Any(command => command.Binding.Id == repeat.Id), "repeat respects interval");
        engine.Handle("ПКМ", false, 1570);
        engine.SwitchLayer("2");
        Assert(engine.LayerIndex == 1 && !engine.Handle("Кнопка 4", true, 1600).Suppress,
            "layer switch changes button mapping");
        Assert(MouseMappingRuntime.ParseSource(0x020A, 120u << 16)?.Source == "Колесо вверх" &&
            MouseMappingRuntime.ParseSource(0x020C, 2u << 16)?.Source == "Кнопка 5" &&
            MouseKeySequence.ParseChord("Ctrl+Alt+F8").SequenceEqual(new byte[] { 17, 18, 119 }),
            "Windows mouse messages and key chords parse correctly");
    }

    private static async Task TestTranslation()
    {
        var handler = new TranslationTestHandler();
        using var client = new HttpClient(handler);
        var result = await TranslationTools.TranslateAsync("Hello, world!", "en", "ru",
            "http://127.0.0.1:5000/translate", 1000, CancellationToken.None, client);
        Assert(result == "Привет, мир!" && handler.Request?.Method == HttpMethod.Post &&
            handler.Request.RequestUri?.AbsolutePath == "/translate" && handler.Body.Contains("\"source\":\"en\"") &&
            handler.Body.Contains("\"target\":\"ru\"") && handler.Body.Contains("Hello, world!"),
            "translation block sends a typed request and reads translatedText without network");
        var detected = await TranslationTools.DetectAsync("Bonjour!", "http://127.0.0.1:5000/translate",
            1000, CancellationToken.None, client);
        var languages = await TranslationTools.GetLanguagesAsync("http://127.0.0.1:5000/translate",
            1000, CancellationToken.None, client);
        Assert(detected.Code == "fr" && detected.Confidence == 91 &&
            languages.Single().Code == "en" && languages.Single().Targets.Contains("ru"),
            "language detection and installed-pair discovery parse service responses");
        Assert(await TranslationTools.TranslateAsync("Уже по-русски", "ru", "ru", "bad-url", 1000,
            CancellationToken.None, client) == "Уже по-русски", "same-language translation needs no service");
        try
        {
            await TranslationTools.TranslateAsync("Text", "xx", "ru", "http://127.0.0.1:5000/translate",
                1000, CancellationToken.None, client);
            throw new Exception("Invalid language was accepted");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("языки")) { }
        Assert(BlockCatalog.All.Any(spec => spec.Kind == "TranslateText"), "translation block is in catalog");
        var start = BlockCatalog.Create("Start", 0, 0);
        var input = BlockCatalog.Create("SetVariable", 100, 0);
        input.Values["Name"] = "source_text";
        input.Values["Value"] = "Hello, world!";
        var translate = BlockCatalog.Create("TranslateText", 200, 0);
        translate.Values["Engine"] = "LibreTranslate";
        translate.Values["Text"] = "${source_text}";
        var log = BlockCatalog.Create("Log", 300, 0);
        log.Values["Text"] = "${translated}";
        start.Links["Далее"] = input.Id;
        input.Links["Далее"] = translate.Id;
        translate.Links["Далее"] = log.Id;
        var events = new List<string>();
        await new Runner(new ProjectModel { Nodes = [start, input, translate, log] }, events.Add, (_, _) => { },
            translate: (text, source, target, _, _, _) =>
                Task.FromResult(text == "Hello, world!" && source == "en" && target == "ru"
                    ? "Привет, мир!" : "Неверные параметры"))
            .RunAsync(CancellationToken.None);
        Assert(events.Contains("Привет, мир!"), "translation block receives resolved variables and stores result");
        var localTranslate = BlockCatalog.Create("TranslateText", 200, 0);
        Assert(localTranslate.Value("Engine") == "LibreTranslate" &&
            BlockCatalog.Create("TranslateArray", 0, 0).Value("Engine") == "LibreTranslate",
            "lightweight translation blocks require an explicit connected service by default");
        localTranslate.Values["Engine"] = "Argos локально";
        localTranslate.Values["Text"] = "Hello, world!";
        localTranslate.LogActions = true;
        start.Links["Далее"] = localTranslate.Id;
        var localEvents = new List<string>();
        await new Runner(new ProjectModel { Nodes = [start, localTranslate] }, localEvents.Add, (_, _) => { },
            translate: (_, _, _, _, _, _) => throw new Exception("unexpected network translation"),
            translateLocal: (text, source, target, _, _) =>
                Task.FromResult(text == "Hello, world!" && source == "en" && target == "ru"
                    ? "Привет, мир!" : "Неверные параметры"))
            .RunAsync(CancellationToken.None);
        Assert(localEvents.Any(entry => entry.Contains("translated=\"Привет, мир!\"")),
            "local translation result appears in checked block journal");
        try
        {
            await ArgosBridge.TranslateAsync("text", "auto", "ru", "python", 1000, CancellationToken.None);
            throw new Exception("Argos accepted auto source");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("конкретные языки")) { }
        var sampleIndex = """
            [{"from_code":"ru","to_code":"en","links":["https://argos-net.com/v1/ru_en.argosmodel"]},
             {"from_code":"en","to_code":"fr","links":["https://argos-net.com/v1/en_fr.argosmodel"]},
             {"from_code":"fr","to_code":"en","links":["https://argos-net.com/v1/fr_en.argosmodel"]},
             {"from_code":"en","to_code":"ru","links":["https://argos-net.com/v1/en_ru.argosmodel"]},
             {"from_code":"ru","to_code":"en","links":["http://untrusted.example/other.argosmodel"]}]
            """;
        var available = OfflineTranslationInstaller.ReadIndex(sampleIndex);
        var plan = OfflineTranslationInstaller.PlanPackages(available, [], "ru", "fr", true);
        Assert(available.Count == 4 && plan.Count == 4 &&
            plan.Select(item => item.Source + item.Target).SequenceEqual(["ruen", "enfr", "fren", "enru"]),
            "on-demand installer plans only trusted HTTPS Argos models, including English pivot and reverse route");
        Assert(OfflineTranslationInstaller.PlanPackages(available,
            [new ArgosModel("ru", "en", "")], "ru", "fr", false).Count == 1,
            "on-demand installer reuses an already installed language leg");
    }

    private sealed class TranslationTestHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var json = request.RequestUri?.AbsolutePath switch
            {
                "/detect" => "[{\"language\":\"fr\",\"confidence\":91}]",
                "/languages" => "[{\"code\":\"en\",\"name\":\"English\",\"targets\":[\"ru\",\"fr\"]}]",
                _ => "{\"translatedText\":\"Привет, мир!\"}"
            };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent(json) };
        }
    }

    private static void TestLanguagePacksAndNames()
    {
        Assert(ProjectModel.CreateSample().VariableNamesAsciiOnly &&
            VariableNameRules.IsAscii("my_value_2") && !VariableNameRules.IsAscii("имя") &&
            !VariableNameRules.IsAscii("2wrong"), "new projects use ASCII-only identifiers");
        var store = new VariableStore(asciiOnly: true);
        store.Set("value_2", "ok");
        try { store.Set("русское_имя", "no"); throw new Exception("Unicode identifier was accepted"); }
        catch (InvalidOperationException error) when (error.Message.Contains("A–Z")) { }
        var legacy = new VariableStore();
        legacy.Set("старое_имя", "ok");
        Assert(legacy.Get("старое_имя") == "ok", "old project identifiers remain readable");
        var newVariable = BlockCatalog.Create("SetVariable", 0, 0);
        var newTranslation = BlockCatalog.Create("TranslateText", 0, 0);
        Assert(VariableNameRules.IsAscii(newVariable.Value("Name")) &&
            VariableNameRules.IsAscii(newTranslation.Value("Result")), "new blocks have ASCII variable defaults");
        Assert(!VariableNameRules.IsIdentifierField("ArraySet", new FieldSpec("Index", "Индекс массива", "0")) &&
            !VariableNameRules.IsIdentifierField("WindowInfo", new FieldSpec("Handle", "HWND (число или переменная)", "0")) &&
            VariableNameRules.IsIdentifierField("ArraySet", new FieldSpec("Name", "Имя массива", "items")),
            "identifier validation distinguishes names from numeric and expression fields");
        Assert(VariableNameRules.FindInvalid(new ProjectModel { Nodes =
            [BlockCatalog.Create("ArraySet", 0, 0), BlockCatalog.Create("WindowInfo", 200, 0)] }).Count == 0,
            "valid numeric array index and HWND are not mistaken for variable names");
        foreach (var spec in BlockCatalog.All)
            Assert(VariableNameRules.FindInvalid(new ProjectModel { Nodes = [BlockCatalog.Create(spec.Kind, 0, 0)] }).Count == 0,
                "new block has valid variable defaults: " + spec.Kind);
        foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
        {
            UiLanguage.Configure(code);
            Assert(UiLanguage.Code == code && UiLanguage.T("Файл") != "Файл" &&
                UiLanguage.T("Перевести текст") != "Перевести текст", "language pack " + code);
            var missingBlockTitles = BlockCatalog.All.Where(spec => !UiLanguage.Has(spec.Title))
                .Select(spec => spec.Kind + ": " + spec.Title).ToArray();
            Assert(missingBlockTitles.Length == 0,
                $"missing {code} block titles: {string.Join(", ", missingBlockTitles)}");
            var missingBlockUi = BlockCatalog.All.SelectMany(spec => new[] { spec.Title, spec.Hint }
                .Concat(spec.Outputs)
                .Concat(spec.Fields.SelectMany(field => new[] { field.Label, field.Default }.Concat(field.Options ?? []))))
                .Where(value => value.Any(ch => ch is >= '\u0400' and <= '\u04FF') && !UiLanguage.Has(value))
                .Distinct(StringComparer.Ordinal).ToArray();
            Assert(missingBlockUi.Length == 0,
                $"missing {code} block labels/options: {string.Join(", ", missingBlockUi)}");
            foreach (var key in new[]
            {
                "визуальные сценарии", "Поиск блока…", "▦ Блоки", "≡ Код Kliker Blocks",
                "Начало сценария", "Возврат при смещении", "Настройте параметры блока ниже.",
                "Готово. Соедините выход блока с входом следующего.",
                "Режим выполнения: один проход…", "Режим выполнения: повторять до остановки…",
                "Записать действия в журнал", "Назначить нажатием…", "свободно", "отключить",
                "управление", "Открыть проект…", "Сохранить проект…", "Выход из Kliker Blocks",
                "▶ Пуск", "⏸ Пауза", "■ Стоп", "▶ Далее", "Состояние: ГОТОВ",
                "Состояние: ВЫПОЛНЯЕТСЯ", "Состояние: ПАУЗА",
                "Привязка к окну: включена", "Привязка к окну: выключена",
                "Компактная панель", "Открыть редактор", "Настройки сценария…",
                "Состояние: готов", "▶ Запустить", "■ Остановить", "▶ Продолжить",
                "готов", "пауза", "выполняется", "Сценарий выполняется",
                "Выбрано блоков: {0}", "Связь", "Удалить связь",
                "Языки интерфейса и модели перевода", "Язык интерфейса (применится сразу)",
                "Python с установленным argostranslate (команда или путь к python.exe)",
                "LibreTranslate: адрес /translate (необязательно для Argos)",
                "Установленные локальные модели Argos", "Языковые пары сервиса LibreTranslate",
                "Проверка локальных моделей…", "Локальные модели не установлены.",
                "Установка модели…", "Проверка сервиса перевода…",
                "Описание сценария", "Как установить модель…",
                "Пакет {0} → {1} не используется блоками Kliker Blocks. Выберите пару ru/en/fr/zh/es/de.",
                "Нет подключения к LibreTranslate ({0}). Это отдельный необязательный сервер; для «Argos локально» он не нужен.",
                "Справка Kliker Blocks", "О программе…", "Поиск: блок, функция, параметр…", "Найдено тем: {0}",
                "Возможности компьютера и голоса…", "Возможности компьютера и голоса",
                "Обновить данные", "Голоса Windows:", "Локальный файл: {0}",
                "Файл журнала: {0}", "Файл сообщений сценария: {0}", "Файл сообщений сценария отключён.",
                "Текст для озвучивания длиннее 16000 символов.",
                "Перевод справки через локальную модель Argos…",
                "{0}  →  {1}\nВыход: {2}\n\nОдин выход соединяется с одним входом. К одному входу могут вести несколько связей.\n\nНажмите Delete для удаления выбранной линии."
            })
                Assert(UiLanguage.Has(key) && UiLanguage.T(key) != key,
                    $"missing {code} interface translation: {key}");
            Assert(UiLanguage.Format("Применено: {0} блоков", 3).Contains("3"),
                "localized format placeholder " + code);
        }
        UiLanguage.Configure("ru");
    }

    private static void TestJournalTracing()
    {
        Assert(JournalSettingsForm.IsValidJournalName("journal.txt", "project.klb") &&
            !JournalSettingsForm.IsValidJournalName("../journal.txt", "project.klb") &&
            !JournalSettingsForm.IsValidJournalName("project.klb", "project.klb") &&
            !JournalSettingsForm.IsValidJournalName("image.png", "project.klb"),
            "journal accepts only safe text filenames");
        var start = BlockCatalog.Create("Start", 0, 0);
        var message = BlockCatalog.Create("Log", 200, 0);
        message.Values["Text"] = "journal-test";
        message.LogActions = true;
        start.Links["Далее"] = message.Id;
        var project = new ProjectModel { Nodes = [start, message], WriteJournalToFile = true,
            JournalFileName = "journal-test.txt", VariableNamesAsciiOnly = true };
        var jsonCopy = project.Copy();
        Assert(jsonCopy.WriteJournalToFile && jsonCopy.JournalFileName == "journal-test.txt" &&
            jsonCopy.Nodes[1].LogActions, "journal settings and per-block flag survive project save");
        var graphCopy = GraphCodeCodec.Parse(GraphCodeCodec.Serialize(project));
        Assert(graphCopy[1].LogActions && !graphCopy[0].LogActions, "trace flag survives code view");
        var clipboardCopy = BlockClipboard.Copy([message]);
        Assert(clipboardCopy[0].LogActions, "trace flag survives block copy");
        var folder = Path.Combine(Path.GetTempPath(), "kliker-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var writer = new ScriptLogWriter(folder);
            writer.Configure(project.JournalFileName);
            Assert(File.Exists(Path.Combine(folder, project.JournalFileName)) &&
                writer.PathOnDisk == Path.Combine(folder, project.JournalFileName),
                "journal is created at configuration and exposes its absolute path");
            var messages = new List<string>();
            void Record(string entry) { messages.Add(entry); writer.Write(entry); }
            new Runner(project, Record, (_, _) => { }, baseDirectory: folder)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(messages.Contains("journal-test") && messages.Any(entry =>
                entry.Contains("[Блок") && entry.Contains("Сообщение") && entry.Contains("выполнен")),
                "checked block adds its action to journal");
            Assert(messages.Any(entry => entry.Contains("параметры") && entry.Contains("journal-test")),
                "checked block logs its resolved input fields");
            Assert(!messages.Any(entry => entry.Contains("[Блок") && entry.Contains("Старт")),
                "unchecked block does not add action trace");
            Assert(File.ReadAllLines(Path.Combine(folder, project.JournalFileName)).SequenceEqual(messages),
                "full journal appends to UTF-8 file");
            var scriptLog = BlockCatalog.Create("LogFile", 200, 0);
            scriptLog.Values["Path"] = "script.log";
            start.Links["Далее"] = scriptLog.Id;
            new Runner(new ProjectModel { Nodes = [start, scriptLog], VariableNamesAsciiOnly = true },
                _ => { }, (_, _) => { }, baseDirectory: folder)
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(File.Exists(Path.Combine(folder, "script.log")),
                "LogFile block creates script.log beside project even without a message block");
            var assign = BlockCatalog.Create("SetVariable", 200, 0);
            assign.Values["Name"] = "choice";
            assign.Values["Value"] = "1";
            var branch = BlockCatalog.Create("IfVariable", 400, 0);
            branch.Values["Name"] = "choice";
            branch.Values["Value"] = "1";
            branch.LogActions = true;
            branch.Links["Да"] = message.Id;
            branch.Links["Нет"] = message.Id;
            start.Links["Далее"] = assign.Id;
            assign.Links["Далее"] = branch.Id;
            assign.LogActions = true;
            var branchEvents = new List<string>();
            new Runner(new ProjectModel { Nodes = [start, assign, branch, message], VariableNamesAsciiOnly = true },
                branchEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(branchEvents.Any(entry => entry.Contains("изменения: choice=\"1\"")),
                "checked assignment logs the value written to a variable");
            Assert(branchEvents.Any(entry => entry.Contains("Если переменная") && entry.Contains("выход «Да»")),
                "trace records actual conditional branch even if both outputs share a target");
            var conditional = BlockCatalog.Create("IfExpression", 400, 100);
            conditional.Values["Expression"] = "=if $choice<0";
            var wrong = BlockCatalog.Create("Log", 600, 0);
            wrong.Values["Text"] = "wrong branch";
            var chosen = BlockCatalog.Create("Log", 600, 100);
            chosen.Values["Text"] = "=[if $choice==1 then \"chosen\" else \"wrong\"] => ${selected_text}";
            var readBack = BlockCatalog.Create("Log", 800, 100);
            readBack.Values["Text"] = "Stored: ${selected_text}";
            start.Links["Далее"] = assign.Id;
            assign.Links["Далее"] = conditional.Id;
            conditional.Links["Да"] = wrong.Id;
            conditional.Links["Нет"] = chosen.Id;
            chosen.Links["Далее"] = readBack.Id;
            var conditionalEvents = new List<string>();
            new Runner(new ProjectModel
                { Nodes = [start, assign, conditional, wrong, chosen, readBack], VariableNamesAsciiOnly = true },
                conditionalEvents.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(conditionalEvents.Any(entry => entry.Contains("Stored: chosen")) &&
                !conditionalEvents.Any(entry => entry.Contains("wrong branch")),
                "runner routes FALSE to No and exposes a template assignment to the next block");
            var broken = BlockCatalog.Create("ArraySet", 200, 0);
            broken.LogActions = true;
            broken.Values["Index"] = "-1";
            start.Links["Далее"] = broken.Id;
            var failures = new List<string>();
            try
            {
                new Runner(new ProjectModel { Nodes = [start, broken], VariableNamesAsciiOnly = true },
                    failures.Add, (_, _) => { }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                throw new Exception("Self-test failed: invalid block should fail");
            }
            catch (InvalidOperationException) { }
            Assert(failures.Any(entry => entry.Contains("[Блок") && entry.Contains("ошибка")),
                "checked block logs failure before propagating it");
            foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
            {
                UiLanguage.Configure(code);
                var sample = BlockTrace.Details(assign, assign,
                    [new VariableStore.VariableChange("choice", "1")]);
                var help = JournalHelp.Section("Журнал и запись в файл");
                Assert(sample.Contains("choice=\"1\"") && help is { Length: > 100 } &&
                    !sample.Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                    !help.Any(ch => ch is >= '\u0400' and <= '\u04ff'),
                    "detailed journal and help are localized for " + code);
            }
            UiLanguage.Configure("ru");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void TestShowcaseGraph()
    {
        UiLanguage.Configure("ru");
        var reusedHandle = new IntPtr(101);
        var ownNote = new WindowCandidate(reusedHandle, "Kliker-Blocks-showcase-note-unique.txt - Notepad", "notepad", 1);
        var otherNote = new WindowCandidate(new IntPtr(102), "Untitled - Notepad", "notepad", 2);
        Assert(ShowcaseSession.FindLaunchedWindow([otherNote, ownNote], new HashSet<IntPtr> { reusedHandle },
                "notepad", "Kliker-Blocks-showcase-note-unique") == ownNote &&
            ShowcaseSession.FindLaunchedWindow([otherNote], new HashSet<IntPtr> { reusedHandle },
                "notepad", "Kliker-Blocks-showcase-note-unique") is null,
            "a new Notepad document can reuse an existing top-level window without targeting another tab");
        var desktop = new Rectangle(0, 0, 1264, 720);
        var noteBounds = ShowcaseLayout.NotepadBounds(desktop);
        var demoBounds = ShowcaseLayout.DemoBounds(desktop, noteBounds, 500);
        var prompt = new Rectangle(ShowcaseLayout.PromptLocation(desktop, 435, 205),
            new Size(435, 205));
        Assert(demoBounds is not null && !noteBounds.IntersectsWith(demoBounds.Value) &&
            !noteBounds.IntersectsWith(prompt) && desktop.Contains(prompt),
            "showcase graph, Notepad and prompt fit without covering each other");
        Assert(ShowcaseSession.SameText("One\r\nTwo\r\n", "One\nTwo") &&
            !ShowcaseSession.SameText("One\nTw", "One\nTwo") &&
            !ShowcaseSession.SameText("Intro\nLong phrase\nLong phrase", "Intro\nLong phrase"),
            "showcase full-document verification tolerates line endings but detects missing and duplicated text");
        Assert(ShowcaseSession.CharacterDelayMs == 32 && ShowcaseSession.LineDelayMs == 144,
            "showcase visible typing is twice as slow as v0.67");
        Assert(ShowcaseSession.ExpectedAfterAppend("Intro\nQuote", "Translation") ==
            "Intro\nQuote\nTranslation" &&
            !ShowcaseSession.SameText("Intro\nQuote\nTransltion",
                ShowcaseSession.ExpectedAfterAppend("Intro\nQuote", "Translation")),
            "every later Notepad addition has a complete expected document for verification");
        Assert(ShowcaseSession.RemainingInput(["Hello", "Мир", "done"], 1, 1) == "ир\r\ndone" &&
            ShowcaseSession.RemainingInput(["Hello"], 0, 0) == "Hello",
            "failed Unicode input pastes only the remaining characters");
        using (var paintWindow = new Bitmap(1000, 680))
        using (var graphics = Graphics.FromImage(paintWindow))
        {
            graphics.Clear(Color.FromArgb(28, 32, 38));
            graphics.FillRectangle(Brushes.White, 155, 200, 700, 440);
            var canvas = PaintShowcaseTools.LocateWhiteCanvas(paintWindow, new Point(40, 60));
            Assert(canvas.Width > 600 && canvas.Height > 350 &&
                PaintShowcaseTools.Strokes("heart", canvas).Single().Length > 150 &&
                PaintShowcaseTools.Strokes("star", canvas).Single().Length > 30 &&
                PaintShowcaseTools.Strokes("house", canvas).Count == 3,
                "showcase detects blank Paint canvas and creates animated mouse paths");
            using var after = (Bitmap)paintWindow.Clone();
            after.SetPixel(500, 340, Color.Red);
            Assert(!PaintShowcaseTools.HasVisibleChange(paintWindow, paintWindow) &&
                !PaintShowcaseTools.HasVisibleChange(paintWindow, after),
                "showcase does not accept a single incidental pixel as a drawing");
            using (var stroke = Graphics.FromImage(after))
                stroke.FillRectangle(Brushes.Red, 440, 310, 80, 20);
            Assert(PaintShowcaseTools.HasVisibleChange(paintWindow, after),
                "showcase recognizes visible Paint strokes");
            graphics.FillRectangle(Brushes.Red, 70, 52, 18, 18);
            Assert(PaintShowcaseTools.FindRedSwatch(paintWindow) is { } swatch &&
                swatch.X is >= 70 and < 88 && swatch.Y is >= 52 and < 70,
                "classic Paint red swatch can be found without fixed UI coordinates");
        }
        using (var classicWindow = new Bitmap(920, 620))
        using (var graphics = Graphics.FromImage(classicWindow))
        {
            graphics.Clear(Color.FromArgb(190, 193, 197));
            graphics.FillRectangle(Brushes.White, 112, 142, 650, 365);
            var canvas = PaintShowcaseTools.LocateWhiteCanvas(classicWindow, new Point(80, 35));
            Assert(canvas.Left == 80 + 112 + 24 && canvas.Top == 35 + 142 + 24 &&
                canvas.Width == 650 - 48 && canvas.Height == 365 - 48,
                "classic Paint canvas found despite different toolbar height");
        }
        Assert(PaintShowcaseTools.KnownPencilName("Pencil (tool)") &&
            PaintShowcaseTools.KnownPencilName("Карандаш") &&
            PaintShowcaseTools.KnownPencilName("Bleistift") &&
            !PaintShowcaseTools.KnownPencilName("Brush"),
            "classic Paint pencil name recognition supports localized tooltips");
        Assert(PaintShowcaseTools.KnownBrushName("Кисти") &&
            PaintShowcaseTools.KnownBrushName("Brushes") &&
            PaintShowcaseTools.ResetKeys(3).SequenceEqual(new byte[] { 0x5A, 0x5A, 0x5A }) &&
            PaintShowcaseTools.ResetKeys(0).Count == 0,
            "retry clears only showcase strokes with Undo and can reselect modern Paint Brushes");
        foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
        {
            UiLanguage.Configure(code);
            Assert(!PaintShowcaseTools.FallbackNotice(true).Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                !PaintShowcaseTools.FallbackNotice(false).Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                ShowcaseHelp68.Section("Окна и точность демонстрации") is { Length: > 100 } help &&
                !help.Any(ch => ch is >= '\u0400' and <= '\u04ff'),
                "Paint fallback and accuracy help are localized for " + code);
        }
        UiLanguage.Configure("ru");
        using (var colorImage = new Bitmap(100, 100))
        using (var graphics = Graphics.FromImage(colorImage))
        {
            graphics.Clear(Color.White);
            graphics.FillRectangle(Brushes.Blue, 25, 25, 50, 50);
            Assert(ShowcaseSession.HasColorPatch(colorImage, Color.Blue) &&
                !ShowcaseSession.HasColorPatch(colorImage, Color.Red),
                "showcase finds a substantial color patch without trusting a toolbar swatch");
        }
        Assert(ShowcaseSession.GuessSourceLanguage("Привет, друг!") == "ru" &&
            ShowcaseSession.GuessSourceLanguage("Hello, friend!") == "en" &&
            ShowcaseSession.GuessSourceLanguage("Bonjour, mon ami!") == "fr" &&
            ShowcaseSession.GuessSourceLanguage("Hola, amigo!") == "es" &&
            ShowcaseSession.GuessSourceLanguage("Hallo, mein Freund!") == "de" &&
            ShowcaseSession.GuessSourceLanguage("你好，朋友！") == "zh",
            "showcase detects all six built-in source languages for representative phrases");
        Assert(ShowcaseText.IsComplete, "showcase has explicit text for every built-in language");
        var imageFolder = Path.Combine(Path.GetTempPath(), "KLIKER-showcase-images-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var shape in new[] { "heart", "house", "star", "arrow" })
            {
                var imagePath = Path.Combine(imageFolder, shape + ".png");
                ShowcaseImageRenderer.Render(shape, imagePath);
                using var image = new Bitmap(imagePath);
                Assert(image.Width == ShowcaseImageRenderer.Width && image.Height == ShowcaseImageRenderer.Height &&
                    image.GetPixel(image.Width / 2, image.Height / 2).ToArgb() != Color.White.ToArgb(),
                    "showcase " + shape + " PNG contains visible artwork");
            }
        }
        finally { if (Directory.Exists(imageFolder)) Directory.Delete(imageFolder, recursive: true); }
        var project = ShowcaseGraph.Create();
        var note = project.Nodes.Single(node => node.Kind == "ShowcaseStep" && node.Value("Action") == "OpenNotepad");
        var greeting = project.Nodes.Single(node => node.Kind == "ShowcaseStep" && node.Value("Action") == "WelcomeSpeech");
        var phrase = project.Nodes.Single(node => node.Kind == "InputDialog" && node.Value("Result") == "demo_phrase");
        Assert(note.X < greeting.X && greeting.X < phrase.X &&
            note.Y == greeting.Y && greeting.Y == phrase.Y,
            "showcase opening nodes have a readable straight sequence");
        Assert(project.Nodes.Count(node => node.Kind == "Start") == 1 &&
            project.Nodes.Any(node => node.Kind == "WhileExpression") &&
            project.Nodes.Any(node => node.Kind == "ChoiceDialog") &&
            project.Nodes.Any(node => node.Kind == "BufferCapture") &&
            project.Nodes.Any(node => node.Kind == "BufferHash") &&
            project.Nodes.Any(node => node.Kind == "BufferSave") &&
            project.Nodes.Any(node => node.Kind == "TextFile") &&
            project.Nodes.Count(node => node.Kind == "BranchEnd") >= 3,
            "showcase graph has drawing feedback loop, screen analysis, image and report");
        var ids = project.Nodes.Select(node => node.Id).ToHashSet();
        Assert(project.Nodes.SelectMany(node => node.Links.Values).All(target => target is Guid id && ids.Contains(id)),
            "showcase graph links resolve to nodes");
        Assert(project.Nodes.Where(node => node.Kind == "ShowcaseStep").Select(node => node.Value("Action"))
            .ToHashSet(StringComparer.Ordinal).IsSupersetOf(new[]
                { "OpenNotepad", "CopyPhrase", "WelcomeSpeech", "IntroduceDrawing", "PrepareShape",
                    "DrawChoice", "PrepareRetry", "IntroduceColors", "OpenColorWindows",
                    "WaitForColorWindows", "FindSelectedColor", "FinalNotepad", "Finish" }),
            "showcase graph contains the new narrative and color search actions");
        Assert(!project.ShowIndicator && project.RunMode == "Один проход" &&
            !project.Nodes.Any(node => node.Kind is "RunFile" or "AutoHotkey"),
            "showcase does not auto-run external commands or loop");
        Assert(ShowcaseQuestionForm.ParseChoice("cœur") == "heart" &&
            ShowcaseQuestionForm.ParseChoice("casa") == "house" &&
            ShowcaseQuestionForm.ParseChoice("звезда") == "star" &&
            ShowcaseQuestionForm.ParseChoice("") == "", "showcase answers are parsed");

        var folder = Path.Combine(Path.GetTempPath(), "KLIKER-showcase-state-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "state.json");
            Assert(ShowcaseState.ReadFirstShownUtc(path) is null, "showcase starts unseen");
            var first = ShowcaseState.MarkFirstShown(path);
            Assert(ShowcaseState.MarkFirstShown(path) == first &&
                ShowcaseState.ReadFirstShownUtc(path) == first,
                "showcase first-run state is persistent and idempotent");
            var firstRun = ShowcaseState.MarkFirstRun(path);
            Assert(ShowcaseState.MarkFirstRun(path) == firstRun &&
                ShowcaseState.ReadFirstShownUtc(path) == first && firstRun >= first,
                "showcase first actual run is recorded separately from first offer");
        }
        finally { Directory.Delete(folder, recursive: true); }

        var start = BlockCatalog.Create("Start", 0, 0);
        var step = BlockCatalog.Create("ShowcaseStep", 250, 0);
        var stop = BlockCatalog.Create("Stop", 500, 0);
        start.Links["Далее"] = step.Id;
        step.Values["Action"] = "Mock";
        step.Links["Далее"] = stop.Id;
        var fired = false;
        var mini = new ProjectModel { VariableNamesAsciiOnly = true, Nodes = [start, step, stop] };
        new Runner(mini, _ => { }, (_, _) => { }, showcaseStep: (action, variables, _) =>
        {
            fired = action == "Mock";
            variables.Set("demo_test", "ok");
            return Task.CompletedTask;
        }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(fired, "showcase step executes through Runner delegate");

        static void Simulate(bool liked, bool retry = false)
        {
            var graph = ShowcaseGraph.Create();
            foreach (var node in graph.Nodes.Where(node =>
                         node.Kind is "BufferCapture" or "BufferHash" or "BufferSave" or "TextFile" or
                             "TextFileArray" or "IniFile" or "DirectoryList" or "ClipboardSet" or
                             "ClipboardGet" or "TranslateArray" or "Beep" or "LogFile"))
            {
                var action = node.Kind;
                node.Kind = "ShowcaseStep";
                node.Values["Action"] = action;
            }
            var executed = new List<string>();
            var feedbackCount = 0;
            var retryCount = 0;
            new Runner(graph, _ => { }, (_, _) => { },
                inputDialog: (_, value, _, _, _) => Task.FromResult(value.Length == 0 ? "Привет, друг." : value),
                radioDialog: (_, options, _, _) => Task.FromResult(options.Count == 4
                    ? retry && retryCount++ == 0 ? 1 : 4 : 2),
                choiceDialog: (_, _, _, _, _) => Task.FromResult(retry && feedbackCount++ == 0
                    ? 7 : liked ? 6 : 7),
                translateLocal: (_, _, _, _, _) => throw new Exception("the showcase must not translate"),
                showcaseStep: (action, variables, _) =>
                {
                    executed.Add(action);
                    if (action == "PrepareShape") variables.Set("demo_choice", "heart");
                    if (action == "PrepareRetry") ShowcaseSession.PrepareRetry(variables);
                    if (action == "PrepareColor") variables.Set("demo_color", "red");
                    if (action == "DrawChoice")
                    {
                        variables.Set("demo_left", "0"); variables.Set("demo_top", "0");
                        variables.Set("demo_right", "100"); variables.Set("demo_bottom", "100");
                    }
                    if (action == "BufferHash") variables.Set("demo_hash", "123");
                    if (action == "DirectoryList") variables.Set("demo_file_count", "2");
                    return Task.CompletedTask;
                }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(executed.Contains("Finish") && executed.Contains("BufferCapture") &&
                executed.Contains("BufferHash") && executed.Contains("BufferSave") &&
                executed.Contains("TextFile") &&
                executed.Contains("OpenColorWindows") && executed.Contains("FindSelectedColor") &&
                executed.Contains("FinalNotepad") &&
                executed.Contains("PrepareRetry") == (!liked || retry) &&
                executed.Count(action => action == "DrawChoice") == (retry ? 2 : 1),
                "showcase retry and feedback branches reach color search, report and finish without translation");
        }
        Simulate(liked: true);
        Simulate(liked: false);
        Simulate(liked: true, retry: true);
        Assert(!project.Nodes.Any(node => node.Kind is "TranslateText" or "TranslateArray" or "DetectLanguage" or "LanguagePair" ||
            node.Kind == "ShowcaseStep" && node.Value("Action") is "CheckModel" or "NoModel" or "AppendTranslation" or "SpeakResult"),
            "version 0.70 showcase has no translation/model steps");

        foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
        {
            UiLanguage.Configure(code);
            var translated = ShowcaseGraph.Create();
            using var preview = new ShowcaseForm();
            using var question = new ShowcaseQuestionForm();
            Assert(!translated.ScriptName.Any(ch => ch is >= '\u0400' and <= '\u04FF') &&
                translated.Nodes.Where(node => node.Kind == "ShowcaseStep")
                    .All(node => !node.Value("Caption").Any(ch => ch is >= '\u0400' and <= '\u04FF')) &&
                !preview.Text.Any(ch => ch is >= '\u0400' and <= '\u04FF') &&
                !question.Text.Any(ch => ch is >= '\u0400' and <= '\u04FF') &&
                UiLanguage.Has("Демонстрация Что я умею") &&
                UiLanguage.Has("Демонстрация выполняется; Esc или «Остановить» прервёт её.") &&
                UiLanguage.Has("Kliker Blocks — ввод") && UiLanguage.Has("Kliker Blocks — выбор"),
                $"showcase graph and controls translated for {code}: " +
                string.Join(", ", translated.Nodes.Where(node => node.Kind == "ShowcaseStep" &&
                    node.Value("Caption").Any(ch => ch is >= '\u0400' and <= '\u04FF'))
                    .Select(node => node.Value("Action") + "=" + node.Value("Caption"))));
            Assert(ShowcaseText.LanguageOptions.Split('\n').Length == UiLanguage.Available.Length &&
                !ShowcaseText.Get("consent").Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                !ShowcaseText.Get("color_wait").Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                !ShowcaseText.HelpSection("Демонстрация Что я умею")!.Any(ch => ch is >= '\u0400' and <= '\u04ff') &&
                ShowcaseText.HelpBlock("ShowcaseStep") == ShowcaseText.HelpSection("Демонстрация Что я умею"),
                $"showcase consent, choices, color prompt and help localized for {code}");
        }
        UiLanguage.Configure("ru");
    }

    private static void TestScenarioPlayback()
    {
        Assert(Runner.SpeedTime(200, 5m) == 40 && Runner.SpeedTime(200, 0.1m) == 2000 &&
            Runner.ParseSpeed("0,1") == 0.1m && Runner.ParseSpeed("5.0") == 5m,
            "scenario playback speed accepts 0.1x–5x and scales waits");
        var folder = Path.Combine(Path.GetTempPath(), "KLIKER-nested-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var childStart = BlockCatalog.Create("Start", 0, 0);
            var childMessage = BlockCatalog.Create("Log", 200, 0);
            childMessage.Values["Text"] = "child-step";
            childStart.Links["Далее"] = childMessage.Id;
            var childResult = BlockCatalog.Create("SetVariable", 400, 0);
            childResult.Values["Name"] = "child_result";
            childResult.Values["Value"] = "${input_value}";
            childMessage.Links["Далее"] = childResult.Id;
            var childFile = Path.Combine(folder, "child.klb");
            File.WriteAllText(childFile, JsonSerializer.Serialize(new ProjectModel
            {
                Nodes = [childStart, childMessage, childResult], PlaybackSpeed = 0.5m
            }));
            var parentStart = BlockCatalog.Create("Start", 0, 0);
            var launch = BlockCatalog.Create("RunScenario", 200, 0);
            launch.Values["Path"] = "child.klb";
            launch.Values["Times"] = "3";
            launch.Values["Speed"] = "5,0";
            launch.Values["Inputs"] = "input_value=hello";
            launch.Values["Outputs"] = "parent_result=child_result";
            parentStart.Links["Далее"] = launch.Id;
            var parentResult = BlockCatalog.Create("Log", 400, 0);
            parentResult.Values["Text"] = "${parent_result}";
            launch.Links["Далее"] = parentResult.Id;
            var messages = new List<string>();
            new Runner(new ProjectModel { Nodes = [parentStart, launch, parentResult] }, messages.Add, (_, _) => { },
                folder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(messages.Count(message => message == "child-step") == 3 &&
                messages.Count(message => message.Contains("Вызов сценария child.klb")) == 3 &&
                messages.Contains("hello"),
                "saved child scenario runs sequentially and exchanges named variables");
            childMessage.Links["Далее"] = childMessage.Id;
            File.WriteAllText(childFile, JsonSerializer.Serialize(new ProjectModel
            {
                Nodes = [childStart, childMessage, childResult]
            }));
            try
            {
                new Runner(new ProjectModel { Nodes = [parentStart, launch, parentResult] }, _ => { }, (_, _) => { },
                    folder).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
                throw new Exception("Self-test failed: cyclic child scenario was not rejected");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("бесконечную обратную связь")) { }
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void TestEnglishHelpCoverage()
    {
        foreach (var spec in BlockCatalog.All)
        {
            var description = EnglishHelp.Block(spec.Kind);
            Assert(!string.IsNullOrWhiteSpace(description) &&
                !description.Any(ch => ch is >= '\u0400' and <= '\u04FF'),
                "offline English description covers " + spec.Kind);
        }
        foreach (var title in new[] { "Панель, пауза и трей", "Запуск и остановка", "Визуальные блоки и связи",
            "Демонстрация Что я умею", "Окна и точность демонстрации",
            "Текстовый код Kliker Blocks", "Переменные и массивы", "Журнал и запись в файл",
            "Снимки и образы", "Запись макросов", "Слежение за курсором",
            "Индикатор сценария", "Профили и назначения мыши", "Перевод текста и языковые модели",
            "Установка модели Argos", "Возможности компьютера и голоса", "Коды клавиш VK (1–255)" })
        {
            var description = EnglishHelp.Section(title);
            Assert(!string.IsNullOrWhiteSpace(description) &&
                !description.Any(ch => ch is >= '\u0400' and <= '\u04FF'),
                "offline English topic covers " + title);
        }
    }

    private static void TestOtherHelpCoverage()
    {
        var topics = new[] { "Панель, пауза и трей", "Запуск и остановка", "Визуальные блоки и связи",
            "Демонстрация Что я умею", "Окна и точность демонстрации",
            "Текстовый код Kliker Blocks", "Переменные и массивы", "Журнал и запись в файл",
            "Снимки и образы", "Запись макросов", "Слежение за курсором",
            "Индикатор сценария", "Профили и назначения мыши", "Перевод текста и языковые модели",
            "Установка модели Argos", "Возможности компьютера и голоса", "Коды клавиш VK (1–255)" };
        foreach (var code in new[] { "fr", "es", "zh" })
        {
            foreach (var spec in BlockCatalog.All)
                Assert(!string.IsNullOrWhiteSpace(LocalizedHelp.Block(spec.Kind, code)) &&
                    !LocalizedHelp.Block(spec.Kind, code)!.Any(ch => ch is >= '\u0400' and <= '\u04FF'),
                    $"offline {code} help covers {spec.Kind}");
            foreach (var title in topics)
                Assert(!string.IsNullOrWhiteSpace(LocalizedHelp.Section(title, code)) &&
                    !LocalizedHelp.Section(title, code)!.Any(ch => ch is >= '\u0400' and <= '\u04FF'),
                    $"offline {code} help covers {title}");
        }
    }

    private static void TestSpeakBlock()
    {
        var start = BlockCatalog.Create("Start", 0, 0);
        var set = BlockCatalog.Create("SetVariable", 200, 0);
        set.Values["Name"] = "message";
        set.Values["Value"] = "Hello from KLIKER";
        var speak = BlockCatalog.Create("SpeakText", 400, 0);
        speak.Values["Text"] = "${message}";
        speak.Values["Rate"] = "2";
        speak.Values["Volume"] = "65";
        start.Links["Далее"] = set.Id;
        set.Links["Далее"] = speak.Id;
        string? heard = null;
        var rate = 0;
        var volume = 0;
        new Runner(new ProjectModel { Nodes = [start, set, speak], VariableNamesAsciiOnly = true },
            _ => { }, (_, _) => { }, speakText: (text, _, speed, loudness, cancel) =>
            {
                cancel.ThrowIfCancellationRequested();
                heard = text; rate = speed; volume = loudness;
                return Task.CompletedTask;
            }).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(heard == "Hello from KLIKER" && rate == 2 && volume == 65,
            "speech block resolves variables and forwards voice controls");
        try { WindowsSpeech.SpeakAsync("text", "", 11, 100, CancellationToken.None)
                .GetAwaiter().GetResult();
            throw new Exception("Self-test failed: speech accepted invalid rate"); }
        catch (ArgumentOutOfRangeException) { }
    }

    private static void TestSupportLinks()
    {
        Assert(WebsiteLinks.Home.StartsWith("https://", StringComparison.Ordinal) &&
            WebsiteLinks.Community.StartsWith("https://", StringComparison.Ordinal),
            "public website and community links use HTTPS");
        Assert(SupportForm.PaymentMethods.Length == 5 &&
            SupportForm.PaymentMethods.All(network => File.Exists(Path.Combine(
                AppContext.BaseDirectory, "Assets", "Support", network.File))),
            "four USDT and one Sberbank QR images are bundled with the application");
        using (var support = new SupportForm())
            Assert(support.Text.Contains("Поддержать", StringComparison.Ordinal),
                "Russian support window is available");
        UiLanguage.Configure("en");
        foreach (var term in new[] { "Поддержать проект", "Поддержать проект Kliker Blocks",
                     "Поддержать Kliker Blocks", "Добровольная поддержка разработки", "Сбербанк",
                     "QR-код недоступен в этой сборке.", "Сайт", "Сайт Kliker Blocks",
                     "Открыть сайт Kliker Blocks ↗" })
            Assert(UiLanguage.Has(term), "support UI has English text: " + term);
        using (var support = new SupportForm())
            Assert(support.Text.Contains("Support", StringComparison.Ordinal),
                "English support window is available");
        UiLanguage.Configure("ru");
    }

    private static void TestDialogWindowOptions()
    {
        foreach (var kind in new[] { "InputDialog", "ChoiceDialog", "RadioDialog" })
        {
            var node = BlockCatalog.Create(kind, 560, 100);
            Assert(node.Value("WindowTitle") == "" && node.Value("WindowX") == "" &&
                node.Value("WindowY") == "" && node.Value("WindowWidth") == "" &&
                node.Value("WindowHeight") == "", kind + " starts with centered default dialog geometry");
            node.Values["WindowTitle"] = "Custom question";
            node.Values["WindowX"] = "80";
            node.Values["WindowY"] = "100";
            node.Values["WindowWidth"] = "560";
            node.Values["WindowHeight"] = kind == "RadioDialog" ? "440" : "240";
            var parsed = GraphCodeCodec.Parse(GraphCodeCodec.Serialize(new ProjectModel { Nodes = [node] })).Single();
            var settings = DialogWindowOptions.From(parsed);
            Assert(settings.Title == "Custom question" && settings.Location == new Point(80, 100) &&
                settings.ClientSize(435, 205, 360, 185).Width == 560 &&
                settings.ClientSize(435, 205, 360, 185).Height == (kind == "RadioDialog" ? 440 : 240),
                kind + " keeps custom title, screen position and size through graph code");
        }
        Assert(new DialogWindowOptions("", "", "", "", "").Location is null &&
            new DialogWindowOptions("", "", "", "", "").ClientSize(435, 205, 360, 185) == new Size(435, 205),
            "blank dialog fields keep centered standard size");
        foreach (var kind in new[] { "InputDialog", "ChoiceDialog", "RadioDialog" })
            foreach (var key in new[] { "WindowX", "WindowY", "WindowWidth", "WindowHeight" })
                Assert(DialogWindowOptions.IsOptionalNumericField(kind, key, ""),
                    kind + " accepts a blank optional geometry field before scenario launch");
        Assert(!DialogWindowOptions.IsOptionalNumericField("MoveWindow", "WindowX", "") &&
            !DialogWindowOptions.IsOptionalNumericField("InputDialog", "WindowX", "abc"),
            "blank exemption is restricted to dialog geometry");
        try
        {
            _ = new DialogWindowOptions("", "80", "", "", "").Location;
            throw new Exception("Self-test failed: one dialog coordinate was accepted");
        }
        catch (InvalidOperationException) { }
        try
        {
            _ = new DialogWindowOptions("", "", "", "300", "").ClientSize(435, 205, 360, 185);
            throw new Exception("Self-test failed: clipped dialog width was accepted");
        }
        catch (InvalidOperationException) { }
        UiLanguage.Configure("en");
        foreach (var term in new[] { "Заголовок окна (пусто = стандартный)",
                     "X окна (пусто = по центру)", "Y окна (пусто = по центру)",
                     "Ширина окна (360–3000; пусто = авто)", "Высота окна (185–3000; пусто = авто)",
                     "Высота окна (180–3000; пусто = авто)", "Высота окна (260–3000; пусто = авто)",
                     "В блоке «{0}» поле «{1}» должно содержать целое число.",
                     "Сайт и сообщество Kliker Blocks…", "Не удалось открыть сайт: {0}",
                     "Для позиции окна укажите целые X и Y вместе либо оставьте оба поля пустыми.",
                     "Размер окна должен быть целым числом от {0} до 3000 или пустым полем." })
            Assert(UiLanguage.Has(term), "dialog geometry has English text: " + term);
        UiLanguage.Configure("ru");

        var start = BlockCatalog.Create("Start", 0, 0);
        var inputBlock = BlockCatalog.Create("InputDialog", 100, 0);
        var choiceBlock = BlockCatalog.Create("ChoiceDialog", 200, 0);
        var radioBlock = BlockCatalog.Create("RadioDialog", 300, 0);
        foreach (var node in new[] { inputBlock, choiceBlock, radioBlock })
        {
            node.Values["WindowTitle"] = "Scenario dialog";
            node.Values["WindowX"] = "80";
            node.Values["WindowY"] = "100";
            node.Values["WindowWidth"] = "560";
            node.Values["WindowHeight"] = "440";
        }
        start.Links["Далее"] = inputBlock.Id;
        inputBlock.Links["Далее"] = choiceBlock.Id;
        choiceBlock.Links["Далее"] = radioBlock.Id;
        var geometryForwarded = 0;
        void CheckForwarded(DialogWindowOptions options)
        {
            if (options.Title == "Scenario dialog" && options.Location == new Point(80, 100) &&
                options.ClientSize(435, 205, 360, 185) == new Size(560, 440)) geometryForwarded++;
        }
        new Runner(new ProjectModel { Nodes = [start, inputBlock, choiceBlock, radioBlock] }, _ => { }, (_, _) => { },
            inputDialog: (_, _, _, options, _) => { CheckForwarded(options); return Task.FromResult("answer"); },
            choiceDialog: (_, _, _, options, _) => { CheckForwarded(options); return Task.FromResult(6); },
            radioDialog: (_, _, options, _) => { CheckForwarded(options); return Task.FromResult(1); })
            .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(geometryForwarded == 3, "runner forwards dialog title and geometry for all three block kinds");

        using var owner = new Form { ShowInTaskbar = false, Opacity = 0,
            StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        owner.Show();
        void InspectDialog(string title, Size size, Action<CancellationToken, DialogWindowOptions> open)
        {
            var checkedLayout = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            timer.Tick += (_, _) =>
            {
                var form = Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == title);
                if (form is null) return;
                checkedLayout = form.StartPosition == FormStartPosition.Manual &&
                    form.ClientSize == size && form.Location == new Point(-2000, -2000) &&
                    form.Controls.OfType<Button>().Any() &&
                    form.Controls.OfType<Button>().All(button => button.Left >= 0 && button.Top >= 0 &&
                        button.Right <= form.ClientSize.Width && button.Bottom <= form.ClientSize.Height);
                form.Close();
            };
            timer.Start();
            try { open(timeout.Token, new DialogWindowOptions(title, "-2000", "-2000",
                size.Width.ToString(), size.Height.ToString())); }
            catch (OperationCanceledException) { }
            timer.Stop();
            Assert(checkedLayout, title + " opens at requested screen position with all buttons inside");
        }
        InspectDialog("Geometry input", new Size(360, 185), (cancel, window) =>
            DialogTools.Input(owner, "Question", "", 0, cancel, options: window));
        InspectDialog("Geometry buttons", new Size(360, 180), (cancel, window) =>
            DialogTools.Choice(owner, "Question", 3, 2, cancel, options: window));
        InspectDialog("Geometry choices", new Size(360, 260), (cancel, window) =>
            DialogTools.Radio(owner, "Question", ["First", "Second"], cancel, window: window));
        void InspectCenteredDialog(string title, Action<CancellationToken, DialogWindowOptions> open)
        {
            var centered = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 30 };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            timer.Tick += (_, _) =>
            {
                var form = Application.OpenForms.Cast<Form>().FirstOrDefault(item => item.Text == title);
                if (form is null) return;
                centered = form.StartPosition == FormStartPosition.CenterScreen &&
                    form.ClientSize.Width >= 360 && form.ClientSize.Height >= 180;
                form.Close();
            };
            timer.Start();
            try { open(timeout.Token, new DialogWindowOptions(title, "", "", "", "")); }
            catch (OperationCanceledException) { }
            timer.Stop();
            Assert(centered, title + " opens centered when all geometry fields are blank");
        }
        InspectCenteredDialog("Centered input", (cancel, window) =>
            DialogTools.Input(owner, "Question", "", 0, cancel, options: window));
        InspectCenteredDialog("Centered buttons", (cancel, window) =>
            DialogTools.Choice(owner, "Question", 4, 2, cancel, options: window));
        InspectCenteredDialog("Centered choices", (cancel, window) =>
            DialogTools.Radio(owner, "Question", ["First", "Second"], cancel, window: window));
        owner.Close();
    }

    private static void TestSystemBeep()
    {
        var tone = AudioTools.CreateToneWave(523, 120);
        Assert(tone.Length == 44 + 96000 * 120 / 1000 * 2 &&
            System.Text.Encoding.ASCII.GetString(tone, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(tone, 8, 4) == "WAVE" &&
            System.Text.Encoding.ASCII.GetString(tone, 36, 4) == "data",
            "normal-audio fallback creates a valid tone with requested duration");
        var method = typeof(AudioTools).GetMethod("NativeBeep", BindingFlags.NonPublic | BindingFlags.Static);
        var import = method?.GetCustomAttribute<DllImportAttribute>();
        Assert(import?.Value == "kernel32.dll" && import.EntryPoint == "Beep",
            "system beep imports the real Windows API export, not the managed method name");
        if (OperatingSystem.IsWindows())
        {
            var module = NativeLibrary.Load("kernel32.dll");
            try { Assert(NativeLibrary.TryGetExport(module, "Beep", out _), "kernel32 exports Beep"); }
            finally { NativeLibrary.Free(module); }
        }
    }

    private static void TestInstalledProgramCatalog()
    {
        var programs = InstalledProgramCatalog.Discover();
        Assert(programs.Count > 0 && programs.All(item =>
                !string.IsNullOrWhiteSpace(item.Name) && Path.IsPathFullyQualified(item.Path) &&
                File.Exists(item.Path)) &&
            programs.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == programs.Count,
            "program picker lists existing paths without duplicates");
        UiLanguage.Configure("en");
        Assert(UiLanguage.T("Выбрать программу или файл") == "Choose a program or file" &&
            UiLanguage.T("Обзор файлов…") == "Browse files…",
            "program picker labels are localized in English");
        UiLanguage.Configure("ru");
    }

    private static void TestHelpGraphExamples()
    {
        foreach (var language in new[] { "ru", "en" })
        {
            UiLanguage.Configure(language);
            foreach (var spec in BlockCatalog.All)
            {
                var example = BlockExampleCatalog.For(spec.Kind);
                var parsed = GraphCodeCodec.Parse(example.Code);
                Assert(parsed.Count > 0 && parsed.Count(node => node.Kind == "Start") == 1 &&
                    parsed.Any(node => node.Kind == spec.Kind) &&
                    parsed.Select(node => node.Id).Distinct().Count() == parsed.Count &&
                    parsed.SelectMany(node => node.Links.Values).Where(id => id is not null)
                        .All(id => parsed.Any(node => node.Id == id)),
                    $"{language} help example for {spec.Kind} is a complete native graph");
                Assert(example.Code.Contains("trace true", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(language == "ru" ? example.NoteRu : example.NoteEn),
                    $"{language} help example for {spec.Kind} explains its trace");
            }
        }
        UiLanguage.Configure("ru");
        var radioExample = BlockExampleCatalog.For("RadioDialog");
        var radioNodes = GraphCodeCodec.Parse(radioExample.Code);
        var radio = radioNodes.Single(node => node.Kind == "RadioDialog");
        var optionLines = radio.Value("Options").Split('\n');
        using (var optionsBox = new TextBox
               { Multiline = true, Text = MultilineFieldText.ForDisplay(radio.Value("Options")) })
        {
            Assert(optionLines.Length == 3 && optionLines[1] == "11" && optionLines[2] == "22" &&
                optionsBox.Lines.SequenceEqual(optionLines) &&
                optionsBox.Text.Contains(Environment.NewLine + "11" + Environment.NewLine + "22", StringComparison.Ordinal),
                "copied RadioDialog help example displays three separate options in Windows properties");
            optionsBox.Text = optionLines[0] + "\r\n11\r\n22";
            radio.Values["Options"] = MultilineFieldText.ForStorage(optionsBox.Text);
        }
        var normalizedCode = GraphCodeCodec.Serialize(new ProjectModel { Nodes = radioNodes });
        var optionCodeLine = normalizedCode.Split('\n').Single(line => line.Contains("value \"Options\"", StringComparison.Ordinal));
        Assert(optionCodeLine.Contains("\\n11\\n22", StringComparison.Ordinal) &&
            !optionCodeLine.Contains("\\r", StringComparison.Ordinal) &&
            GraphCodeCodec.Parse(normalizedCode).Single(node => node.Kind == "RadioDialog")
                .Value("Options") == radio.Value("Options"),
            "graph code uses one JSON-escaped \\n per option and round-trips multiline values");
        UiLanguage.Configure("en");
        Assert(UiLanguage.T("Один вариант на строке. В коде перенос показан как \\n.") ==
            "One option per line. In code, a line break appears as \\n.",
            "multiline option hint is localized in English");
        UiLanguage.Configure("ru");
        foreach (var kind in new[] { "Start", "SetVariable", "DefineVariable", "IfVariable", "IfExpression",
                     "WhileExpression", "SwitchValue", "ArraySet", "ArrayAction", "StringSplit", "Repeat",
                     "Parallel", "InputGroup", "CallSub", "SubStart", "SubReturn", "BranchEnd", "Wait", "Stop", "Log" })
        {
            var graph = new ProjectModel { Nodes = GraphCodeCodec.Parse(BlockExampleCatalog.For(kind).Code),
                VariableNamesAsciiOnly = true };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            new Runner(graph, _ => { }, (_, _) => { }).RunAsync(timeout.Token).GetAwaiter().GetResult();
        }
    }

    private static void TestShowcaseButtonLayout()
    {
        using var showcase = new ShowcaseForm();
        showcase.Opacity = 0;
        showcase.ShowInTaskbar = false;
        showcase.StartPosition = FormStartPosition.Manual;
        showcase.Location = new Point(-10000, -10000);
        showcase.Show();
        Application.DoEvents();
        var root = showcase.Controls.OfType<TableLayoutPanel>().Single();
        var top = root.Controls.OfType<TableLayoutPanel>().First();
        var row = top.Controls.OfType<FlowLayoutPanel>().Single();
        Assert(row.Controls.OfType<Button>().All(button => button.Bottom + button.Margin.Bottom <= row.ClientSize.Height),
            "showcase start/stop/export buttons fit their row without vertical clipping");
    }

    private static void TestDisplayedBranding()
    {
        Assert(AppIdentity.Title.StartsWith("Kliker Blocks v", StringComparison.Ordinal),
            "main window uses the exact product name");
        var texts = new List<string> { AppHelp.Introduction, EnglishHelp.Overview };
        texts.Add(ShowcaseHelp68.Section("Окна и точность демонстрации") ?? "");
        Assert(!ShowcaseHelp68.Section("Окна и точность демонстрации")!.Contains("KLIKER", StringComparison.Ordinal),
            "current showcase help uses the displayed product name");
        texts.AddRange(BlockCatalog.All.SelectMany(spec => new[]
            { AppHelp.For(spec.Kind), EnglishHelp.Block(spec.Kind) ?? "" }));
        foreach (var file in new[] { "en.json", "en-extra.json" })
            texts.Add(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Languages", file)));
        Assert(texts.All(value => !value.Contains("Clickermann", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains("Кликерман", StringComparison.OrdinalIgnoreCase)),
            "current help and shipped language packs contain no third-party product branding");
    }

    private static void TestComputerCapabilities()
    {
        ComputerCapabilities.Initialize();
        Assert(ComputerCapabilities.Current is not null &&
            File.Exists(ComputerCapabilities.SavedFilePath),
            "first-launch capability inventory is saved at an available local path");
        var path = ComputerCapabilities.SavedFilePath;
        var contents = File.ReadAllText(path);
        var copy = JsonSerializer.Deserialize<ComputerCapabilities>(contents);
        Assert(copy is not null && copy.SchemaVersion == 1 &&
            !string.IsNullOrWhiteSpace(copy.WindowsVersion) &&
            !string.IsNullOrWhiteSpace(copy.DotNetVersion) &&
            copy.Voices.Length == WindowsSpeech.InstalledVoices().Count &&
            copy.Displays.Length == Screen.AllScreens.Length,
            "computer capabilities round-trip with voices and displays");
        Assert(!contents.Contains("UserName", StringComparison.OrdinalIgnoreCase) &&
            !contents.Contains("MachineName", StringComparison.OrdinalIgnoreCase),
            "capability inventory excludes user and machine identifiers");
    }

    private static void TestArgosPackageAndHelpChunks()
    {
        var text = new string('а', 9000) + "\n" + new string('б', 2500);
        var chunks = HelpTranslationService.Split(text);
        Assert(chunks.All(chunk => chunk.Length is > 0 and <= 4000) &&
            string.Concat(chunks) == text, "help translation chunks preserve the whole text");
        var file = Path.Combine(Path.GetTempPath(), "kliker-argos-test-" + Guid.NewGuid().ToString("N") + ".argosmodel");
        try
        {
            using (var archive = ZipFile.Open(file, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("ru_en/metadata.json").Open()))
                writer.Write("{\"from_code\":\"ru\",\"to_code\":\"en\",\"from_name\":\"Russian\",\"to_name\":\"English\"}");
            var info = ArgosBridge.ReadPackageInfo(file);
            Assert(info.Source == "ru" && info.Target == "en" &&
                TranslationTools.IsLanguage(info.Source) && TranslationTools.IsLanguage(info.Target),
                "Argos package metadata identifies translation direction");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        var hub = new[] { "ru", "fr", "es", "zh", "de" }
            .SelectMany(code => new[]
            {
                new ArgosModel(code, "en", code + "→en"),
                new ArgosModel("en", code, "en→" + code)
            }).ToArray();
        foreach (var source in new[] { "ru", "en", "fr", "es", "zh", "de" })
            foreach (var target in new[] { "ru", "en", "fr", "es", "zh", "de" })
                Assert(ArgosBridge.CanTranslate(hub, source, target),
                    $"English pivot makes {source}→{target} available");
        Assert(!ArgosBridge.CanTranslate(hub[..^2], "en", "de"),
            "missing destination model is not reported as available");
        Assert(!TranslationTools.IsLanguage("ar"), "unsupported Arabic package is not offered as a KLIKER language pair");
    }

    private static async Task TestBundledTranslation()
    {
        if (!File.Exists(ArgosBridge.BundledPython) || !Directory.Exists(ArgosBridge.BundledModelsDirectory))
            return; // Source-only build: full assets are copied to the published version.
        var models = await ArgosBridge.ListAsync("Встроенный", CancellationToken.None);
        var languages = new[] { "ru", "en", "fr", "es", "zh", "de" };
        foreach (var source in languages)
            foreach (var target in languages)
                Assert(ArgosBridge.CanTranslate(models, source, target),
                    $"bundled model graph covers {source}→{target}");
        var english = await ArgosBridge.TranslateAsync("Привет, мой друг.", "ru", "en",
            "Встроенный", 60000, CancellationToken.None);
        var german = await ArgosBridge.TranslateAsync("Привет, мой друг.", "ru", "de",
            "Встроенный", 60000, CancellationToken.None);
        Assert(!string.IsNullOrWhiteSpace(english) && !string.IsNullOrWhiteSpace(german) &&
            english != german && english != "Привет, мой друг.",
            "bundled Python translates directly and via the English pivot");
    }

    private static void TestSequentialWindowLoops()
    {
        for (var pass = 0; pass < 2; pass++)
        {
            using var window = new Form { ShowInTaskbar = false, Opacity = 0,
                StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
            window.Shown += (_, _) => window.BeginInvoke(window.Close);
            Application.Run(window);
        }
        Assert(true, "Windows message loop can reopen after an in-process language switch");
    }

    private static void TestCompactControlLocalization()
    {
        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        static IEnumerable<ToolStripItem> Items(ToolStripDropDownItem parent)
        {
            foreach (ToolStripItem item in parent.DropDownItems)
            {
                yield return item;
                if (item is ToolStripDropDownItem subMenu)
                    foreach (var nested in Items(subMenu)) yield return nested;
            }
        }

        foreach (var code in UiLanguage.Available.Select(item => item.Code).Where(code => code != "ru"))
        {
            UiLanguage.Configure(code);
            using var compact = new CompactControlForm(() => { }, () => { }, () => { }, () => { },
                () => { }, () => { }, () => { }, () => { }, () => { }, () => { });
            compact.UpdateState("Без имени", true, true, true);
            var controls = Descendants(compact).ToList();
            var captions = controls.Select(control => control.Text).Append(compact.Text)
                .Concat(controls.OfType<MenuStrip>().SelectMany(menu => menu.Items.Cast<ToolStripItem>())
                    .SelectMany(item => item is ToolStripDropDownItem dropDown
                        ? new[] { item }.Concat(Items(dropDown)) : [item]).Select(item => item.Text));
            Assert(!captions.Any(value => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "[А-Яа-яЁё]")),
                "compact panel has no Russian text when locale is " + code);
            using var help = new HelpForm(null);
            var helpControls = Descendants(help).ToList();
            var topicTree = helpControls.OfType<TreeView>().Single();
            var helpBody = helpControls.OfType<RichTextBox>().Single(box => box.BackColor == Color.White);
            var exampleBox = helpControls.OfType<RichTextBox>().Single(box => box.BackColor != Color.White);
            Assert(help.Text != "Справка Kliker Blocks" &&
                topicTree.Nodes[0].Text != "Общие сведения" &&
                !System.Text.RegularExpressions.Regex.IsMatch(helpBody.Text, "[А-Яа-яЁё]"),
                "help overview and navigation are localized for " + code);
            help.Opacity = 0;
            help.ShowInTaskbar = false;
            help.StartPosition = FormStartPosition.Manual;
            help.Location = new Point(-10000, -10000);
            help.Show();
            var startTopic = topicTree.Nodes.Cast<TreeNode>().SelectMany(group => group.Nodes.Cast<TreeNode>())
                .Single(node => node.Text == UiLanguage.T("Старт"));
            topicTree.SelectedNode = startTopic;
            var translatedHelp = helpBody.Text;
            Assert(exampleBox.Text.Contains("# Kliker Blocks GRAPH 1", StringComparison.Ordinal) &&
                helpControls.OfType<Button>().Any(button => button.Visible &&
                    button.Text == (code == "ru" ? "Копировать граф примера" : "Copy example graph")),
                "block help offers a copyable native graph in " + code);
            Assert(translatedHelp.Length > 10 &&
                !System.Text.RegularExpressions.Regex.IsMatch(translatedHelp, "[А-Яа-яЁё]"),
                $"{code} block help opens immediately without Argos: " +
                translatedHelp[..Math.Min(100, translatedHelp.Length)]);
            using var about = new AboutForm();
            var aboutText = string.Join("\n", Descendants(about).OfType<RichTextBox>().Select(box => box.Text));
            Assert(aboutText.Contains(UiLanguage.T("Озвучить текст")) &&
                !System.Text.RegularExpressions.Regex.IsMatch(aboutText, "[А-Яа-яЁё]"),
                "About window lists every block in " + code + ": speech=" +
                aboutText.Contains(UiLanguage.T("Озвучить текст")) + ", Russian=" +
                string.Join(", ", System.Text.RegularExpressions.Regex.Matches(aboutText, "[А-Яа-яЁё][^,\\r\\n]{0,45}")
                    .Select(match => match.Value).Take(8)));
            var history = Descendants(about).OfType<TabControl>().Single().TabPages[1]
                .Controls.OfType<RichTextBox>().Single().Text;
            Assert(history.StartsWith("v1.1", StringComparison.Ordinal) && history.Contains("v1.0", StringComparison.Ordinal) &&
                !history.Contains("v0.", StringComparison.Ordinal),
                "About history begins at 1.0 without development versions");
            about.Opacity = 0;
            about.ShowInTaskbar = false;
            about.StartPosition = FormStartPosition.Manual;
            about.Location = new Point(-10000, -10000);
            about.Show();
            var aboutClient = about.RectangleToScreen(about.ClientRectangle);
            Assert(Descendants(about).OfType<Button>().All(button =>
                aboutClient.Contains(button.RectangleToScreen(button.ClientRectangle))),
                "About window footer buttons fit within its client area in " + code);
            using var capabilities = new ComputerCapabilitiesForm();
            Assert(capabilities.Text == UiLanguage.T("Возможности компьютера и голоса") &&
                Descendants(capabilities).OfType<Button>().All(button =>
                    !System.Text.RegularExpressions.Regex.IsMatch(button.Text, "[А-Яа-яЁё]")),
                "computer and voice inventory controls are localized for " + code);
        }
        UiLanguage.Configure("ru");
    }

    private static void TestEditorInsertionAndMove()
    {
        using var canvas = new GraphCanvas { Size = new Size(1200, 800) };
        var first = BlockCatalog.Create("Log", 100, 100);
        var second = BlockCatalog.Create("Log", 370, 140);
        canvas.Project = new ProjectModel { Nodes = [first, second] };
        Assert(canvas.InsertionWorldPoint is null, "editor starts without an insertion point");
        var mouseDown = typeof(GraphCanvas).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var mouseUp = typeof(GraphCanvas).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Click(MouseButtons button, int x, int y)
        {
            var args = new MouseEventArgs(button, 1, x, y, 0);
            mouseDown.Invoke(canvas, [args]);
            mouseUp.Invoke(canvas, [args]);
        }
        Click(MouseButtons.Left, 700, 500);
        Assert(canvas.InsertionWorldPoint == new Point(700, 500), "editor keeps clicked world point");
        using (var preview = new Bitmap(1200, 800))
        {
            canvas.DrawToBitmap(preview, new Rectangle(0, 0, 1200, 800));
            Assert(preview.GetPixel(700, 500).G > preview.GetPixel(700, 500).R,
                "insertion point is visibly green");
        }
        canvas.Select(first);
        Click(MouseButtons.Left, 800, 550);
        Assert(BlockCatalog.Bounds(first).Contains(new Point(800, 550)),
            "single selected block is centered on click");
        canvas.SelectMany([first, second]);
        var before = new Point(second.X - first.X, second.Y - first.Y);
        Click(MouseButtons.Left, 1100, 700);
        Assert(canvas.InsertionWorldPoint == new Point(1100, 700) &&
            new Point(second.X - first.X, second.Y - first.Y) == before,
            "multi-selection preserves relative spacing");
        Click(MouseButtons.Right, 20, 20);
        Assert(canvas.InsertionWorldPoint is null && canvas.SelectedNodes.Count == 0,
            "right-click reset clears insertion point and selection");
    }

    private static void TestEnglishMainWindow()
    {
        var originalContext = SynchronizationContext.Current;
        UiLanguage.Configure("en");
        Assert(new AppSettings().UiLanguageCode == "en", "fresh install defaults to English");
        using var form = new MainForm();
        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        static IEnumerable<ToolStripItem> Items(ToolStripItem item)
        {
            yield return item;
            if (item is ToolStripDropDownItem dropDown)
                foreach (ToolStripItem child in dropDown.DropDownItems)
                    foreach (var nested in Items(child)) yield return nested;
        }
        var controls = Descendants(form).ToList();
        var captions = controls.Where(control => control is not (TextBoxBase or ListBox or ComboBox))
            .Select(control => control.Text)
            .Concat(form.MainMenuStrip!.Items.Cast<ToolStripItem>().SelectMany(Items).Select(item => item.Text))
            .Where(value => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "[А-Яа-яЁё]"))
            .Take(10).ToArray();
        Assert(captions.Length == 0, "English main window has no Russian captions: " +
            string.Join(" | ", captions));
        Assert(controls.OfType<Button>().Count(button => button.Text == "↗") == 2 &&
            form.MainMenuStrip.Items.Cast<ToolStripItem>().Any(item => item.Text == "Editor") &&
            form.MainMenuStrip.Renderer is KlikerMenuRenderer,
            "journal popout and localized editor menu are available");
        var graph = (GraphCanvas)typeof(MainForm).GetField("_graph", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(form)!;
        var addBlock = typeof(MainForm).GetMethod("AddBlock", BindingFlags.Instance | BindingFlags.NonPublic)!;
        graph.SetInsertionPoint(new Point(700, 450));
        addBlock.Invoke(form, ["Log"]);
        Assert(graph.Project.Nodes.Last().X == 700 && graph.Project.Nodes.Last().Y == 450,
            "new block uses green insertion point");
        graph.SetInsertionPoint(null);
        var count = graph.Project.Nodes.Count;
        addBlock.Invoke(form, ["Log"]);
        Assert(graph.Project.Nodes.Last().X == 120 + (count % 6) * 235 &&
            graph.Project.Nodes.Last().Y == 120 + (count / 6) * 145,
            "new block keeps sequential fallback when the point is cleared");
        typeof(MainForm).GetMethod("ShowEditorWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, null);
        var editorWindow = (Form?)typeof(MainForm).GetField("_editorWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(form);
        Assert(editorWindow is { Visible: true } && graph.Parent != null && graph.Parent != form,
            "editor popout opens a live graph window");
        editorWindow!.Close();
        Assert(graph.Parent != null && editorWindow.IsDisposed, "editor returns to main window after closing popout");
        var journal = (ListBox)typeof(MainForm).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(form)!;
        Assert(journal.DrawMode == DrawMode.OwnerDrawVariable &&
            controls.OfType<CheckBox>().Any(box => box.Text == "Wrap lines"),
            "journal has a localized wrap switch");
        journal.Width = 250;
        journal.Items.Add("2026-10-26 21:00:00  " + string.Join(" ", Enumerable.Repeat("long-value", 35)));
        using (var bitmap = new Bitmap(1, 1))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var measure = typeof(MainForm).GetMethod("MeasureJournalItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var wrapped = new MeasureItemEventArgs(graphics, journal.Items.Count - 1);
            var wrapField = typeof(MainForm).GetField("_journalWrapEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!;
            wrapField.SetValue(form, true);
            measure.Invoke(form, [journal, wrapped]);
            var singleLine = new MeasureItemEventArgs(graphics, journal.Items.Count - 1);
            wrapField.SetValue(form, false);
            measure.Invoke(form, [journal, singleLine]);
            Assert(wrapped.ItemHeight > singleLine.ItemHeight * 2,
                "long journal entry gains multiple visual lines when wrapping is enabled");
        }
        var journalParent = journal.Parent;
        typeof(MainForm).GetMethod("ShowJournalWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, null);
        var journalWindow = (Form?)typeof(MainForm).GetField("_journalWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(form);
        Assert(journalWindow is { Visible: true } && journal.Parent == journalWindow,
            "journal popout opens the live journal");
        Assert(journalWindow!.Controls.OfType<Panel>().SelectMany(panel => panel.Controls.OfType<CheckBox>())
            .Any(box => box.Text == "Wrap lines"), "detached journal exposes the wrap switch");
        journalWindow!.Close();
        Assert(journalWindow.IsDisposed && journal.Parent == journalParent,
            "journal returns after closing popout");
        var englishDemo = ShowcaseGraph.Create();
        Assert(englishDemo.Nodes.Single(node => node.Kind == "InputDialog" && node.Value("Result") == "demo_name")
                .Value("Message").Contains("call", StringComparison.OrdinalIgnoreCase) &&
            ShowcaseText.Get("intro").StartsWith("Hello", StringComparison.Ordinal),
            "English interface chooses English demo graph and narration");
        UiLanguage.Configure("ru");
        Assert(ShowcaseText.Get("intro").StartsWith("Привет", StringComparison.Ordinal),
            "Russian interface chooses Russian demo graph and narration");
        form.Dispose();
        SynchronizationContext.SetSynchronizationContext(originalContext);
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception($"Self-test failed: {name}");
    }

    private sealed class TestMessageWindow : Form
    {
        public WindowsMessageInbox Inbox { get; } = new();

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 1024)
            {
                Inbox.Record(message.WParam.ToInt64(), message.LParam.ToInt64());
                message.Result = new IntPtr(77);
                return;
            }
            base.WndProc(ref message);
        }
    }
}

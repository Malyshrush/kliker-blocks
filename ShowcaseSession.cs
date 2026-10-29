using System.Diagnostics;
using System.Globalization;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed class ShowcaseSession(Form owner, Action<string> log) : IDisposable
{
    internal const int CharacterDelayMs = 80;
    internal const int LineDelayMs = 144;
    private readonly List<Process> _processes = [];
    private ShowcaseQuestionForm? _question;
    private IntPtr _notepad;
    private string _notepadTitle = "";
    private IntPtr _paint;
    private int _paintStrokeCount;
    private string _introText = "";
    private string _notepadDocument = "";
    private readonly List<(string Color, string Title, IntPtr Window)> _colorWindows = [];

    public async Task ExecuteAsync(string action, VariableStore variables, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        switch (action)
        {
            case "OpenNotepad": await OpenNotepadAsync(variables, cancel); break;
            case "WelcomeSpeech": await WelcomeSpeechAsync(variables, cancel); break;
            case "CopyPhrase": await CopyPhraseAsync(variables, cancel); break;
            case "CheckModel": await CheckModelAsync(variables, cancel); break;
            case "NoModel":
                variables.Set("demo_translation", ShowcaseText.Format("missing_model",
                    variables.Get("demo_source"), variables.Get("demo_target")));
                log(UiLanguage.T("Модель перевода не найдена; этот шаг честно пропущен."));
                break;
            case "AppendTranslation": await AppendTranslationAsync(variables, cancel); break;
            case "SpeakResult": await SpeakResultAsync(variables, cancel); break;
            case "AskDrawing": await AskDrawingAsync(variables, cancel); break;
            case "WaitIdle8": await WaitForIdleAsync(8, variables, cancel); break;
            case "WaitIdle10": await WaitForIdleAsync(10, variables, cancel); break;
            case "PaintArrow": await PaintArrowAsync(cancel); break;
            case "NoteReminder": await NoteReminderAsync(cancel); break;
            case "DrawChoice": await DrawChoiceAsync(variables, cancel); break;
            case "IntroduceDrawing": await AppendNotepadAsync(ShowcaseText.Get("drawing_intro"), cancel); break;
            case "PrepareShape": PrepareShape(variables, "demo_shape_selection"); break;
            case "AnnounceDrawing": await AppendNotepadAsync(ShowcaseText.Format("drawing_announce",
                ShowcaseText.Get("shape_" + variables.Get("demo_choice"))), cancel); break;
            case "SpeakFeedbackYes": await SpeakAsync(ShowcaseText.Get("happy"), cancel); break;
            case "SpeakFeedbackNo": await SpeakAsync(ShowcaseText.Get("sorry"), cancel); break;
            case "PrepareRetry": PrepareRetry(variables); break;
            case "IntroduceColors": await AppendNotepadAsync(ShowcaseText.Get("color_intro"), cancel); break;
            case "PrepareColor": await PrepareColorAsync(variables, cancel); break;
            case "OpenColorWindows": await OpenColorWindowsAsync(cancel); break;
            case "WaitForColorWindows": await WaitForColorWindowsAsync(cancel); break;
            case "FindSelectedColor": await FindSelectedColorAsync(variables, cancel); break;
            case "FinalSpeech": await SpeakAsync(ShowcaseText.Get("final_speech"), cancel); break;
            case "FinalNotepad": await AppendNotepadAsync(ShowcaseText.Get("final_note"), cancel); break;
            case "DataChapter":
                log(UiLanguage.T("Глава 2: граф обрабатывает переменные, массивы и файлы в папке демонстрации."));
                break;
            case "DataEnd":
            {
                var summary = UiLanguage.Format("Итог обработки: слов {0}, файлов {1}, счётчик {2}.",
                    variables.Get("demo_word_count"), variables.Get("demo_file_count"), variables.Get("demo_counter"));
                await AppendNotepadAsync(summary, cancel);
                break;
            }
            case "Finish":
                log(UiLanguage.Format("Отчёт демонстрации: {0}",
                    Path.Combine(ShowcaseState.DemoDirectory, "showcase-report.txt")));
                log(UiLanguage.Format("Снимок демонстрации: {0}",
                    Path.Combine(ShowcaseState.DemoDirectory, "showcase-paint.png")));
                break;
            default: throw new InvalidOperationException(UiLanguage.Format("Неизвестный шаг демонстрации: {0}", action));
        }
    }

    private async Task OpenNotepadAsync(VariableStore variables, CancellationToken cancel)
    {
        var name = variables.Get("demo_name").Trim();
        if (name.Length == 0) { name = UiLanguage.T("друг"); variables.Set("demo_name", name); }
        Directory.CreateDirectory(ShowcaseState.DemoDirectory);
        var title = "Kliker-Blocks-showcase-note-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N")[..8];
        _notepadTitle = title;
        var path = Path.Combine(ShowcaseState.DemoDirectory, title + ".txt");
        File.WriteAllText(path, "");
        _notepad = await LaunchAsync("notepad.exe", path, "notepad", title, cancel);
        try
        {
            var area = await OnUiAsync(() => Screen.FromControl(owner).WorkingArea);
            var noteBounds = ShowcaseLayout.NotepadBounds(area);
            WindowTools.Move(_notepad, noteBounds.X, noteBounds.Y, noteBounds.Width, noteBounds.Height);
            if (owner is ShowcaseForm showcase)
                await OnUiAsync(() => { showcase.ArrangeBeside(WindowTools.Bounds(_notepad)); return true; });
        }
        catch (Exception error) { log(UiLanguage.Format("Не удалось разместить Блокнот рядом с вопросом: {0}", error.Message)); }
        var intro = ShowcaseText.Format("intro", name);
        _introText = intro;
        await WithWindowInputAsync(_notepad, async () =>
        {
            await TypeLinesAsync(intro, cancel);
            var copiedIntro = await CopyAllNotepadTextAsync(cancel);
            if (!SameText(copiedIntro, intro))
            {
                log(UiLanguage.T("Блокнот пропустил часть печати; вставляю точный текст через буфер обмена."));
                await ReplaceNotepadTextAsync(intro, cancel);
                copiedIntro = await CopyAllNotepadTextAsync(cancel);
                if (!SameText(copiedIntro, intro))
                    throw new InvalidOperationException(UiLanguage.T("Блокнот не подтвердил точность вводного текста."));
            }
            CtrlPress(0x23); // Ctrl+End after Ctrl+A selection.
            Press(0x0D);
        }, cancel);
        _notepadDocument = intro;
        log(UiLanguage.T("Блокнот открыт; ввод выполнен только в окне демонстрации."));
    }

    private async Task WelcomeSpeechAsync(VariableStore variables, CancellationToken cancel)
    {
        var voice = WindowsSpeech.InstalledVoices().FirstOrDefault(item =>
            item.Language.StartsWith(UiLanguage.Code, StringComparison.OrdinalIgnoreCase));
        if (voice is null)
        {
            log(UiLanguage.T("Подходящий голос Windows не найден; приветствие осталось текстовым."));
            return;
        }
        try
        {
            await WindowsSpeech.SpeakAsync(UiLanguage.Format(
                "Привет, {0}! Посмотрите, как мой граф управляет Блокнотом и рисованием.",
                variables.Get("demo_name")), voice.Name, 0, 85, cancel);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { log(UiLanguage.Format("Озвучивание пропущено: {0}", error.Message)); }
    }

    private async Task CopyPhraseAsync(VariableStore variables, CancellationToken cancel)
    {
        var phrase = variables.Get("demo_phrase").Trim();
        if (phrase.Length == 0) throw new InvalidOperationException(ShowcaseText.Get("invalid_choice"));
        if (phrase.Length > 350) phrase = phrase[..350];
        var quote = ShowcaseText.Format("quoted", phrase);
        var expectedDocument = _introText + "\n" + quote;
        await WithWindowInputAsync(_notepad, async () =>
        {
            CtrlPress(0x23); // Ctrl+End.
            await TypeLinesAsync(quote, cancel);
            await Task.Delay(160, cancel);
            var copied = await CopyAllNotepadTextAsync(cancel);
            if (!SameText(copied, expectedDocument))
            {
                log(UiLanguage.T("Блокнот пропустил часть фразы; исправляю весь документ."));
                await ReplaceNotepadTextAsync(expectedDocument, cancel);
                copied = await CopyAllNotepadTextAsync(cancel);
                if (!SameText(copied, expectedDocument))
                    throw new InvalidOperationException(UiLanguage.T("Блокнот не подтвердил точность текста; демонстрация остановлена."));
            }
        }, cancel);
        _notepadDocument = expectedDocument;
        variables.Set("demo_phrase", phrase);
        log(UiLanguage.T("Весь текст Блокнота проверен через буфер обмена; фраза сохранена для обработки."));
    }

    private async Task CheckModelAsync(VariableStore variables, CancellationToken cancel)
    {
        var selected = variables.Get("demo_language_choice");
        if (!int.TryParse(selected, out var index) || index < 1 || index > UiLanguage.Available.Length)
            throw new InvalidOperationException(ShowcaseText.Get("invalid_choice"));
        var target = UiLanguage.Available[index - 1].Code;
        var source = GuessSourceLanguage(variables.Get("demo_phrase"));
        variables.Set("demo_source", source);
        variables.Set("demo_target", target);
        try
        {
            var models = await ArgosBridge.ListAsync(AppSettings.Load().ArgosPythonPath, cancel);
            variables.Set("demo_model", ArgosBridge.CanTranslate(models, source, target) ? "1" : "0");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            log(UiLanguage.Format("Локальный перевод недоступен: {0}", error.Message));
            variables.Set("demo_model", "0");
        }
    }

    private async Task AppendTranslationAsync(VariableStore variables, CancellationToken cancel)
    {
        var translation = variables.Get("demo_translation");
        var available = variables.Get("demo_model") == "1";
        var line = available ? ShowcaseText.Format("translated",
            ShowcaseText.LanguageName(variables.Get("demo_source")),
            ShowcaseText.TargetLanguageName(variables.Get("demo_target")), translation) : translation;
        await AppendNotepadAsync(line, cancel);
        log(UiLanguage.Format("Результат показан в Блокноте: {0}", line));
    }

    private async Task SpeakResultAsync(VariableStore variables, CancellationToken cancel)
    {
        if (variables.Get("demo_model") != "1") return;
        var target = variables.Get("demo_target");
        var voice = WindowsSpeech.InstalledVoices().FirstOrDefault(item =>
            item.Language.StartsWith(target, StringComparison.OrdinalIgnoreCase));
        if (voice is null)
        {
            log(UiLanguage.T("Голоса для языка перевода нет; текст остаётся на экране."));
            return;
        }
        try { await WindowsSpeech.SpeakAsync(variables.Get("demo_translation"), voice.Name, 0, 90, cancel); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { log(UiLanguage.Format("Озвучивание пропущено: {0}", error.Message)); }
    }

    internal static string GuessSourceLanguage(string text)
    {
        if (text.Any(ch => ch >= '\u4e00' && ch <= '\u9fff')) return "zh";
        if (text.Any(ch => ch >= '\u0400' && ch <= '\u052f')) return "ru";
        var words = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}]+")
            .Select(match => match.Value).ToArray();
        if (text.Contains('ñ') || text.Contains('¿') || text.Contains('¡')) return "es";
        if (text.Contains('ß') || text.Contains('ä') || text.Contains('ö') || text.Contains('ü')) return "de";
        if (text.Contains('ç') || text.Contains('œ') || text.Contains('ê') || text.Contains('è') || text.Contains('à')) return "fr";
        var clues = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["fr"] = ["bonjour", "merci", "vous", "nous", "avec", "pour", "une", "des", "est",
                "je", "suis", "mon", "aime", "très", "être", "salut"],
            ["es"] = ["hola", "gracias", "usted", "nosotros", "con", "para", "una", "los", "está",
                "yo", "soy", "amigo", "buenos", "días", "qué", "cómo"],
            ["de"] = ["hallo", "danke", "sie", "wir", "mit", "für", "eine", "der", "ist",
                "ich", "bin", "mein", "freund", "guten", "tag", "liebe"]
        };
        var scores = clues.ToDictionary(pair => pair.Key,
            pair => words.Count(word => pair.Value.Contains(word, StringComparer.Ordinal)));
        var best = scores.OrderByDescending(pair => pair.Value).First();
        return best.Value > 0 && scores.Values.Count(value => value == best.Value) == 1 ? best.Key : "en";
    }

    private async Task AppendNotepadAsync(string text, CancellationToken cancel)
    {
        var expectedDocument = ExpectedAfterAppend(_notepadDocument, text);
        await WithWindowInputAsync(_notepad, async () =>
        {
            CtrlPress(0x23); // Ctrl+End also clears any Ctrl+A selection used for verification.
            Press(0x0D);
            await TypeLinesAsync(text, cancel);
            var actual = await CopyAllNotepadTextAsync(cancel);
            if (!SameText(actual, expectedDocument))
            {
                log(UiLanguage.T("Блокнот пропустил часть печати; вставляю точный текст через буфер обмена."));
                await ReplaceNotepadTextAsync(expectedDocument, cancel);
                actual = await CopyAllNotepadTextAsync(cancel);
                if (!SameText(actual, expectedDocument))
                    throw new InvalidOperationException(UiLanguage.T("Блокнот не подтвердил точность текста; демонстрация остановлена."));
            }
        }, cancel);
        _notepadDocument = expectedDocument;
        log(text.Replace('\n', ' '));
    }

    internal static string ExpectedAfterAppend(string document, string text) => document + "\n" + text;

    private static void PrepareShape(VariableStore variables, string selectionName)
    {
        var shape = variables.Get(selectionName) switch
        {
            "1" => "star", "2" => "heart", "3" => "house", _ => ""
        };
        if (shape.Length == 0) throw new InvalidOperationException(ShowcaseText.Get("invalid_choice"));
        variables.Set("demo_choice", shape);
    }

    internal static void PrepareRetry(VariableStore variables)
    {
        if (variables.Get("demo_retry_selection") == "4")
        {
            variables.Set("demo_draw_more", "0");
            return;
        }
        PrepareShape(variables, "demo_retry_selection");
        variables.Set("demo_draw_more", "1");
    }

    private async Task SpeakAsync(string text, CancellationToken cancel)
    {
        var voices = WindowsSpeech.InstalledVoices();
        var voice = voices.FirstOrDefault(item => item.Language.StartsWith(UiLanguage.Code,
            StringComparison.OrdinalIgnoreCase));
        if (voice is null) { log(UiLanguage.T("Подходящий голос Windows не найден; приветствие осталось текстовым.")); return; }
        try { await WindowsSpeech.SpeakAsync(text, voice.Name, 0, 90, cancel); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { log(UiLanguage.Format("Озвучивание пропущено: {0}", error.Message)); }
    }

    private async Task PrepareColorAsync(VariableStore variables, CancellationToken cancel)
    {
        var color = variables.Get("demo_color_selection") switch
        {
            "1" => "red", "2" => "blue", "3" => "yellow", "4" => "green", _ => ""
        };
        if (color.Length == 0) throw new InvalidOperationException(ShowcaseText.Get("invalid_choice"));
        variables.Set("demo_color", color);
        await SpeakAsync(ShowcaseText.Format("color_speech", ShowcaseText.Get("color_" + color)), cancel);
    }

    internal IReadOnlyList<(string Color, string Title, IntPtr Window)> ColorWindows => _colorWindows;

    internal async Task OpenColorWindowsAsync(CancellationToken cancel)
    {
        Directory.CreateDirectory(ShowcaseState.DemoDirectory);
        var colors = new (string Key, Color Value)[]
        {
            ("red", Color.Red), ("blue", Color.Blue),
            ("yellow", Color.Yellow), ("green", Color.Lime)
        };
        var area = await OnUiAsync(() => Screen.FromControl(owner).WorkingArea);
        var width = Math.Clamp(area.Width / 4, 300, 430);
        var height = Math.Clamp(area.Height / 3, 270, 390);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        for (var i = 0; i < colors.Length; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var (key, color) = colors[i];
            var title = "Kliker-Blocks-color-" + key + "-" + stamp;
            var path = Path.Combine(ShowcaseState.DemoDirectory, title + ".png");
            using (var square = new Bitmap(100, 100))
            using (var graphics = Graphics.FromImage(square))
            {
                graphics.Clear(color);
                square.Save(path, ImageFormat.Png);
            }
            var window = await LaunchAsync("mspaint.exe", path, "mspaint", title, cancel);
            if (_colorWindows.Any(item => item.Window == window))
                throw new InvalidOperationException("Paint opened the images in tabs, not separate windows.");
            _colorWindows.Add((key, title, window));
            WindowTools.Move(window, area.Left + 20 + i % 2 * (width + 20),
                area.Top + 20 + i / 2 * (height + 20), width, height);
            log(UiLanguage.Format("Рисунок демонстрации: {0}", path));
        }
    }

    private async Task WaitForColorWindowsAsync(CancellationToken cancel)
    {
        if (_colorWindows.Count != 4) throw new InvalidOperationException("Four Paint windows were not opened.");
        await OnUiAsync(() =>
        {
            DialogTools.Acknowledge(owner, ShowcaseText.Get("color_wait"), cancel,
                ShowcaseLayout.PromptLocation(Screen.FromControl(owner).WorkingArea, 470, 190));
            return true;
        });
    }

    internal async Task FindSelectedColorAsync(VariableStore variables, CancellationToken cancel)
    {
        var targetKey = variables.Get("demo_color");
        var color = targetKey switch
        {
            "red" => Color.Red, "blue" => Color.Blue,
            "yellow" => Color.Yellow, "green" => Color.Lime,
            _ => throw new InvalidOperationException(ShowcaseText.Get("invalid_choice"))
        };
        (string Color, string Title, IntPtr Window)? found = null;
        // Modern Paint enforces a large minimum window size. Four windows may
        // overlap even after the visitor rearranges them, so inspect each
        // owned window alone rather than relying on the visible stack order.
        foreach (var item in _colorWindows)
            if (WindowTools.GetWindow(item.Window)?.Title.Contains(item.Title, StringComparison.OrdinalIgnoreCase) == true)
                WindowTools.State(item.Window, restored: false);
        foreach (var item in _colorWindows)
        {
            cancel.ThrowIfCancellationRequested();
            if (WindowTools.GetWindow(item.Window)?.Title.Contains(item.Title, StringComparison.OrdinalIgnoreCase) != true)
                continue;
            WindowTools.State(item.Window, restored: true);
            WindowTools.RaiseWithoutActivating(item.Window);
            await Task.Delay(350, cancel);
            var bounds = Rectangle.Intersect(WindowTools.Bounds(item.Window), SystemInformation.VirtualScreen);
            if (bounds.Width < 100 || bounds.Height < 100) continue;
            using var image = ScreenTools.Capture(bounds);
            if (Environment.GetEnvironmentVariable("KLIKER_SHOWCASE_DIAGNOSTICS") == "1")
            {
                var diagnosticPath = Path.Combine(ShowcaseState.DemoDirectory,
                    "color-diagnostic-" + item.Color + ".png");
                image.Save(diagnosticPath, ImageFormat.Png);
                log(UiLanguage.Format("Снимок демонстрации: {0}", diagnosticPath));
            }
            if (HasColorPatch(image, color) && item.Color == targetKey) found = item;
            WindowTools.State(item.Window, restored: false);
        }
        if (found is null) throw new InvalidOperationException(ShowcaseText.Get("color_not_found"));
        foreach (var item in _colorWindows.Where(item => item.Window != found.Value.Window))
        {
            if (WindowTools.GetWindow(item.Window)?.Title.Contains(item.Title, StringComparison.OrdinalIgnoreCase) == true)
                WindowTools.SendNumericMessage(item.Window, 0x0010, 0, 0, post: true); // WM_CLOSE only our demo image.
        }
        WindowTools.State(found.Value.Window, restored: true);
        var area = await OnUiAsync(() => Screen.FromControl(owner).WorkingArea);
        var target = WindowTools.Bounds(found.Value.Window);
        WindowTools.Move(found.Value.Window, area.Left + (area.Width - target.Width) / 2,
            area.Top + (area.Height - target.Height) / 2, target.Width, target.Height);
        WindowTools.RaiseWithoutActivating(found.Value.Window);
        log(ShowcaseText.Get("color_found"));
    }

    internal void CloseDemoColorWindows()
    {
        foreach (var item in _colorWindows)
            if (WindowTools.GetWindow(item.Window)?.Title.Contains(item.Title, StringComparison.OrdinalIgnoreCase) == true)
                WindowTools.SendNumericMessage(item.Window, 0x0010, 0, 0, post: true);
    }

    internal static bool HasColorPatch(Bitmap image, Color color)
    {
        for (var y = 0; y <= image.Height - 35; y += 5)
            for (var x = 0; x <= image.Width - 35; x += 5)
                if (ScreenTools.Matches(image.GetPixel(x, y), color, 8) &&
                    ScreenTools.Matches(image.GetPixel(x + 34, y), color, 8) &&
                    ScreenTools.Matches(image.GetPixel(x, y + 34), color, 8) &&
                    ScreenTools.Matches(image.GetPixel(x + 34, y + 34), color, 8) &&
                    ScreenTools.Matches(image.GetPixel(x + 17, y + 17), color, 8)) return true;
        return false;
    }

    private async Task AskDrawingAsync(VariableStore variables, CancellationToken cancel)
    {
        _question = await OnUiAsync(() =>
        {
            var dialog = new ShowcaseQuestionForm();
            var area = Screen.FromControl(owner).WorkingArea;
            dialog.Location = new Point(area.Left + 35, area.Top + Math.Max(35, area.Height / 4));
            dialog.Show(owner);
            return dialog;
        });
        using var registration = cancel.Register(() => OnUiFireAndForget(() => _question?.Close()));
        var answer = await _question.Answer.WaitAsync(cancel);
        variables.Set("demo_choice", answer.Length == 0 ? "star" : answer);
        log(answer.Length == 0 ? UiLanguage.T("Рисунок пропущен; выбрана звезда по умолчанию.") :
            UiLanguage.Format("Получен ответ: {0}", answer));
    }

    private async Task WaitForIdleAsync(int seconds, VariableStore variables, CancellationToken cancel)
    {
        while (_question is null && !cancel.IsCancellationRequested) await Task.Delay(100, cancel);
        while (_question is { } question && !question.Answer.IsCompleted &&
               (DateTime.UtcNow - question.LastActivityUtc).TotalSeconds < seconds)
            await Task.Delay(150, cancel);
        variables.Set("demo_idle", _question is { } current && !current.Answer.IsCompleted ? "1" : "0");
    }

    private async Task PaintArrowAsync(CancellationToken cancel)
    {
        if (_question is null || _question.Answer.IsCompleted) return;
        try
        {
            await DrawInPaintAsync("arrow", cancel);
            if (_question.Answer.IsCompleted) return;
            log(UiLanguage.T("Paint нарисовал стрелку мышью в сторону вопроса."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { log(UiLanguage.Format("Напоминание Paint недоступно: {0}", error.Message)); }
        finally { if (_question is { IsDisposed: false }) await OnUiAsync(() =>
            { _question.Activate(); return true; }); }
    }

    private async Task NoteReminderAsync(CancellationToken cancel)
    {
        if (_question is null || _question.Answer.IsCompleted) return;
        try
        {
            var reminderTitle = "Kliker-Blocks-reminder-" + Guid.NewGuid().ToString("N")[..8];
            var path = Path.Combine(ShowcaseState.DemoDirectory, reminderTitle + ".txt");
            Directory.CreateDirectory(ShowcaseState.DemoDirectory);
            File.WriteAllText(path, UiLanguage.T("Я жду вашего ответа. Выберите звезду, сердце или дом в окне Kliker Blocks."));
            await LaunchAsync("notepad.exe", path, "notepad", reminderTitle, cancel);
            log(UiLanguage.T("Открылось второе окно Блокнота с напоминанием."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { log(UiLanguage.Format("Напоминание Блокнота недоступно: {0}", error.Message)); }
        finally { if (_question is { IsDisposed: false }) await OnUiAsync(() =>
            { _question.Activate(); return true; }); }
    }

    private async Task DrawChoiceAsync(VariableStore variables, CancellationToken cancel)
    {
        try
        {
            var choice = variables.Get("demo_choice");
            if (_paint != IntPtr.Zero && _paintStrokeCount > 0) await ClearPaintAsync(cancel);
            await DrawInPaintAsync(choice, cancel);
            log(UiLanguage.Format("Фигура нарисована мышью в Paint: {0}", choice));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            log(UiLanguage.Format("Рисование в Paint недоступно: {0}", error.Message));
            throw;
        }
        Rectangle region;
        try { region = _paint == IntPtr.Zero ? Rectangle.Empty : WindowTools.Bounds(_paint); }
        catch { region = Rectangle.Empty; }
        var screen = await OnUiAsync(() => Screen.FromControl(owner).Bounds);
        if (region.IsEmpty) region = await OnUiAsync(() => owner.Bounds);
        region.Intersect(screen);
        if (region.Width < 20 || region.Height < 20) region = new Rectangle(screen.Left, screen.Top, 80, 80);
        variables.Set("demo_left", region.Left.ToString(CultureInfo.InvariantCulture));
        variables.Set("demo_top", region.Top.ToString(CultureInfo.InvariantCulture));
        variables.Set("demo_right", region.Right.ToString(CultureInfo.InvariantCulture));
        variables.Set("demo_bottom", region.Bottom.ToString(CultureInfo.InvariantCulture));
    }

    private async Task EnsurePaintAsync(CancellationToken cancel)
    {
        if (_paint != IntPtr.Zero) return;
        _paint = await LaunchAsync("mspaint.exe", "", "mspaint", "", cancel);
        if (_question is { IsDisposed: false } question)
        {
            var screen = Screen.FromControl(owner).WorkingArea;
            var questionBounds = await OnUiAsync(() => question.Bounds);
            var width = Math.Min(660, screen.Width - questionBounds.Width - 85);
            if (width >= 420)
                WindowTools.Move(_paint, questionBounds.Right + 12, questionBounds.Top,
                    width, Math.Min(590, screen.Height - 50));
            else WindowTools.Move(_paint, screen.Left + 20, questionBounds.Bottom + 10,
                Math.Max(420, screen.Width - 40), Math.Max(300, screen.Bottom - questionBounds.Bottom - 30));
        }
        else
        {
            var screen = await OnUiAsync(() => Screen.FromControl(owner).WorkingArea);
            var noteBounds = _notepad == IntPtr.Zero ? Rectangle.Empty : WindowTools.Bounds(_notepad);
            var width = Math.Min(980, screen.Width - 35);
            var height = Math.Min(700, screen.Height - 75);
            var x = Math.Max(noteBounds.Right + 14, screen.Right - width - 18);
            if (x + width > screen.Right) x = screen.Right - width - 18;
            WindowTools.Move(_paint, Math.Max(screen.Left + 10, x), screen.Top + 55, width, height);
        }
    }

    private async Task ClearPaintAsync(CancellationToken cancel)
    {
        await WithWindowInputAsync(_paint, async () =>
        {
            // Ctrl+A/Delete switches modern Paint to Selection. Undo only the strokes
            // made by this showcase, leaving the working Brush/Pencil selected.
            foreach (var key in PaintShowcaseTools.ResetKeys(_paintStrokeCount))
            {
                CtrlPress(key);
                await Task.Delay(90, cancel);
            }
            _paintStrokeCount = 0;
            await Task.Delay(180, cancel);
        }, cancel);
    }

    internal async Task ClosePaintForSmokeAsync(CancellationToken cancel)
    {
        if (_paint == IntPtr.Zero) return;
        if (_paintStrokeCount > 0) await ClearPaintAsync(cancel);
        WindowTools.SendNumericMessage(_paint, 0x0010, 0, 0, post: true);
        for (var attempt = 0; attempt < 25 && WindowTools.GetWindow(_paint) is not null; attempt++)
            await Task.Delay(100, cancel);
        if (WindowTools.GetWindow(_paint) is { } remaining)
        {
            // Paint can still display its Save changes prompt after Undo. This
            // live check owns the newly started process and its disposable canvas.
            var owned = _processes.FirstOrDefault(process =>
            {
                try { return !process.HasExited && process.Id == remaining.ProcessId; }
                catch (InvalidOperationException) { return false; }
            });
            if (owned is null)
                throw new InvalidOperationException("The smoke-test Paint window did not close; check it manually.");
            owned.Kill();
            await owned.WaitForExitAsync(cancel);
        }
        _paint = IntPtr.Zero;
    }

    private async Task DrawInPaintAsync(string shape, CancellationToken cancel)
    {
        await EnsurePaintAsync(cancel);
        var bounds = Rectangle.Intersect(WindowTools.Bounds(_paint), SystemInformation.VirtualScreen);
        if (bounds.Width < 400 || bounds.Height < 350)
            throw new InvalidOperationException(UiLanguage.T("Paint: окно слишком мало для демонстрации рисования."));
        WindowTools.Activate(_paint);
        await Task.Delay(250, cancel);
        var tools = PaintShowcaseTools.SelectPencilAndRed(_paint);
        if (!tools.PencilSelected) log(PaintShowcaseTools.FallbackNotice(pencil: true));
        if (!tools.RedSelected) log(PaintShowcaseTools.FallbackNotice(pencil: false));
        await Task.Delay(240, cancel);
        using var windowBefore = ScreenTools.Capture(bounds);
        var canvas = PaintShowcaseTools.LocateWhiteCanvas(windowBefore, bounds.Location);
        using var before = ScreenTools.Capture(canvas);
        var strokes = PaintShowcaseTools.Strokes(shape, canvas);
        await DrawSegmentsAsync(_paint, strokes, cancel);
        await Task.Delay(200, cancel);
        using var after = ScreenTools.Capture(canvas);
        if (!PaintShowcaseTools.HasVisibleChange(before, after))
            throw new InvalidOperationException(UiLanguage.T("Paint не показал штрихи мыши на холсте; рисунок не подтверждён."));
        _paintStrokeCount = strokes.Count;
    }

    private async Task<IntPtr> LaunchAsync(string executable, string file, string processName,
        string uniqueTitle, CancellationToken cancel)
    {
        var priorWindows = WindowTools.ListOpenWindows().Select(window => window.Handle).ToHashSet();
        var info = new ProcessStartInfo(executable) { UseShellExecute = true };
        if (file.Length > 0) info.ArgumentList.Add(file);
        var process = Process.Start(info) ?? throw new InvalidOperationException("Windows did not start " + executable);
        _processes.Add(process);
        for (var attempt = 0; attempt < 45; attempt++)
        {
            cancel.ThrowIfCancellationRequested();
            process.Refresh();
            if (uniqueTitle.Length > 0)
            {
                var matching = FindLaunchedWindow(WindowTools.ListOpenWindows(), priorWindows,
                    processName, uniqueTitle);
                if (matching is not null) return matching.Handle;
            }
            else if (process.MainWindowHandle != IntPtr.Zero &&
                     !priorWindows.Contains(process.MainWindowHandle)) return process.MainWindowHandle;
            await Task.Delay(100, cancel);
        }
        throw new InvalidOperationException(UiLanguage.Format("Окно {0} не появилось за 4,5 секунды.", executable));
    }

    internal static WindowCandidate? FindLaunchedWindow(IEnumerable<WindowCandidate> windows,
        IReadOnlySet<IntPtr> priorWindows, string processName, string uniqueTitle) =>
        windows.FirstOrDefault(item =>
            (item.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase) ||
             (processName == "mspaint" && item.ProcessName.Equals("Paint", StringComparison.OrdinalIgnoreCase))) &&
            item.Title.Contains(uniqueTitle, StringComparison.OrdinalIgnoreCase) &&
            // Modern Notepad reuses an existing top-level window for a newly opened tab.
            // A unique document title proves it is ours even when HWND already existed.
            (uniqueTitle.Length > 0 || !priorWindows.Contains(item.Handle)));

    private async Task WithWindowInputAsync(IntPtr window, Func<Task> action, CancellationToken cancel)
    {
        if (window == IntPtr.Zero) throw new InvalidOperationException(UiLanguage.T("Окно демонстрации не найдено."));
        using var lease = await DesktopInputCoordinator.AcquireAsync(cancel, log);
        WindowTools.Activate(window);
        await Task.Delay(130, cancel);
        if (!WindowTools.IsForegroundHandle(window))
            throw new InvalidOperationException(UiLanguage.T("Окно демонстрации потеряло фокус; ввод отменён."));
        if (window == _notepad) await SelectDemoNotepadTabAsync(cancel);
        await action();
    }

    private bool IsDemoNotepadTabSelected() => _notepad != IntPtr.Zero &&
        WindowTools.IsForegroundHandle(_notepad) &&
        WindowTools.GetWindow(_notepad)?.Title.Contains(_notepadTitle, StringComparison.OrdinalIgnoreCase) == true;

    private async Task SelectDemoNotepadTabAsync(CancellationToken cancel)
    {
        if (IsDemoNotepadTabSelected()) return;
        // Notepad on Windows 11 can put a new document in an existing window. If a
        // reminder or the user selected another tab, return to our unique document.
        for (var attempt = 0; attempt < 32; attempt++)
        {
            cancel.ThrowIfCancellationRequested();
            if (!WindowTools.IsForegroundHandle(_notepad)) break;
            CtrlPress(0x09); // Ctrl+Tab changes tab without touching document text.
            await Task.Delay(70, cancel);
            if (IsDemoNotepadTabSelected()) return;
        }
        throw new InvalidOperationException(UiLanguage.T(
            "Вкладка Блокнота демонстрации не найдена; ввод в другой документ отменён."));
    }

    private void EnsureDemoNotepadTabSelected()
    {
        if (!IsDemoNotepadTabSelected())
            throw new InvalidOperationException(UiLanguage.T(
                "Вкладка Блокнота демонстрации потеряла фокус; ввод в другой документ отменён."));
    }

    private async Task TypeLinesAsync(string value, CancellationToken cancel)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            for (var j = 0; j < lines[i].Length; j++)
            {
                cancel.ThrowIfCancellationRequested();
                EnsureDemoNotepadTabSelected();
                try { NativeMouse.TypeUnicode(lines[i][j].ToString()); }
                catch (InvalidOperationException error)
                {
                    if (!IsDemoNotepadTabSelected())
                        throw new InvalidOperationException(UiLanguage.T("Окно демонстрации потеряло фокус; ввод отменён."), error);
                    log(UiLanguage.Format("Unicode-ввод отклонён Windows; остаток текста вставляется через буфер обмена. Причина: {0}", error.Message));
                    await ClipboardTools.SetTextAsync(RemainingInput(lines, i, j), cancel);
                    EnsureDemoNotepadTabSelected();
                    CtrlPress(0x56);
                    await Task.Delay(230, cancel);
                    return;
                }
                // Modern Notepad may process Unicode input after SendInput returns. Leave enough
                // time between key pairs so a later pair does not overtake the previous one.
                await Task.Delay(CharacterDelayMs, cancel);
            }
            if (i < lines.Length - 1) { EnsureDemoNotepadTabSelected(); Press(0x0D); }
            await Task.Delay(LineDelayMs, cancel);
        }
    }

    private async Task ReplaceNotepadTextAsync(string exactText, CancellationToken cancel)
    {
        await ClipboardTools.SetTextAsync(exactText.Replace("\n", "\r\n"), cancel);
        if (!WindowTools.IsForegroundHandle(_notepad))
        {
            WindowTools.Activate(_notepad);
            await Task.Delay(130, cancel);
        }
        EnsureDemoNotepadTabSelected();
        // Never rely on Ctrl+A remaining selected after an asynchronous clipboard operation.
        CtrlPress(0x41);
        CtrlPress(0x56);
        await Task.Delay(350, cancel);
    }

    internal async Task VerifyNotepadRecoverySmokeAsync(CancellationToken cancel)
    {
        await WithWindowInputAsync(_notepad, async () =>
        {
            NativeMouse.TypeUnicode("X");
            await Task.Delay(120, cancel);
        }, cancel);
        var values = new VariableStore(asciiOnly: true);
        values.Set("demo_phrase", "Проверка точного ввода");
        await CopyPhraseAsync(values, cancel);
    }

    internal async Task CloseNotepadForSmokeAsync(CancellationToken cancel)
    {
        if (_notepad == IntPtr.Zero) return;
        await WithWindowInputAsync(_notepad, async () =>
        {
            CtrlPress(0x53); // Save the uniquely named test file before closing its window.
            await Task.Delay(400, cancel);
            EnsureDemoNotepadTabSelected();
            CtrlPress(0x57); // Ctrl+W closes only our tab in modern Notepad.
        }, cancel);
    }

    internal static string RemainingInput(string[] lines, int line, int character) =>
        lines[line][character..] + (line + 1 < lines.Length ? "\r\n" + string.Join("\r\n", lines.Skip(line + 1)) : "");

    internal static bool SameText(string actual, string expected) =>
        actual.Replace("\r\n", "\n").TrimEnd('\n') == expected.Replace("\r\n", "\n").TrimEnd('\n');

    private async Task<string> CopyAllNotepadTextAsync(CancellationToken cancel)
    {
        var marker = "Kliker-Blocks-COPY-" + Guid.NewGuid().ToString("N");
        await ClipboardTools.SetTextAsync(marker, cancel);
        if (!WindowTools.IsForegroundHandle(_notepad))
        {
            WindowTools.Activate(_notepad);
            await Task.Delay(130, cancel);
        }
        EnsureDemoNotepadTabSelected();
        CtrlPress(0x41);
        CtrlPress(0x43);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(80, cancel);
            var copied = await ClipboardTools.GetTextAsync(cancel);
            if (copied != marker) return copied;
        }
        throw new InvalidOperationException(UiLanguage.T("Блокнот не подтвердил копирование текста."));
    }

    private static void CtrlPress(byte key)
    {
        NativeMouse.KeyDown(0x11);
        try { Press(key); }
        finally { NativeMouse.KeyUp(0x11); }
    }

    private async Task DrawSegmentsAsync(IntPtr window, IEnumerable<Point[]> lines, CancellationToken cancel)
    {
        using var lease = await DesktopInputCoordinator.AcquireAsync(cancel, log);
        WindowTools.Activate(window);
        await Task.Delay(180, cancel);
        if (!WindowTools.IsForegroundHandle(window))
            throw new InvalidOperationException(UiLanguage.T("Окно демонстрации потеряло фокус; ввод отменён."));
        var original = NativeMouse.Position();
        try
        {
            foreach (var points in lines)
            {
                cancel.ThrowIfCancellationRequested();
                if (!WindowTools.IsForegroundHandle(window))
                    throw new InvalidOperationException(UiLanguage.T("Окно демонстрации потеряло фокус; ввод отменён."));
                NativeMouse.MoveInjected(points[0]);
                await Task.Delay(65, cancel);
                NativeMouse.MouseDownInjected();
                try
                {
                    foreach (var point in points.Skip(1))
                    {
                        cancel.ThrowIfCancellationRequested();
                        NativeMouse.MoveInjected(point);
                        await Task.Delay(12, cancel);
                    }
                }
                finally { NativeMouse.MouseUpInjected(); }
            }
        }
        finally { NativeMouse.Move(original); }
    }

    private static void Press(byte key)
    {
        NativeMouse.KeyDown(key);
        NativeMouse.KeyUp(key);
    }

    private Task<T> OnUiAsync<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.BeginInvoke((Action)(() =>
        {
            try { result.TrySetResult(action()); }
            catch (Exception error) { result.TrySetException(error); }
        }));
        return result.Task;
    }

    private void OnUiFireAndForget(Action action)
    {
        if (owner.IsDisposed || !owner.IsHandleCreated) return;
        owner.BeginInvoke(action);
    }

    public void Dispose()
    {
        OnUiFireAndForget(() => { if (_question is { IsDisposed: false }) _question.Close(); });
        foreach (var process in _processes) process.Dispose();
    }
}

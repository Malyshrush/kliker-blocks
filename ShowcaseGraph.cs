namespace KlikerBlocks;

internal static class ShowcaseGraph
{
    public const string ScriptId = "KLIKER_SHOWCASE";

    public static ProjectModel Create()
    {
        var project = new ProjectModel
        {
            ScriptName = UiLanguage.T("Что я умею — интерактивная демонстрация"),
            VariableNamesAsciiOnly = true,
            ShowIndicator = false,
            RunMode = "Один проход"
        };
        BlockNode Add(string kind, int x, int y, params (string Key, string Value)[] values)
        {
            var node = BlockCatalog.Create(kind, x, y);
            foreach (var (key, value) in values) node.Values[key] = value;
            project.Nodes.Add(node);
            return node;
        }
        BlockNode Step(string action, string caption, int x, int y) => Add("ShowcaseStep", x, y,
            ("Action", action), ("Caption", ShowcaseText.Caption(action, caption)));
        static void Link(BlockNode from, string output, BlockNode to) => from.Links[output] = to.Id;

        var start = Add("Start", 100, 110);
        var intro = Add("Log", 350, 110, ("Text", UiLanguage.T("Демонстрация Kliker Blocks началась.")));
        var name = Add("InputDialog", 600, 110,
            ("Message", UiLanguage.T("Как к вам обращаться?")),
            ("Default", UiLanguage.T("друг")), ("DelaySeconds", "0"), ("Result", "demo_name"));
        var note = Step("OpenNotepad", "Открыть Блокнот", 850, 110);
        var greeting = Step("WelcomeSpeech", "Приветствие голосом", 1100, 110);
        var phrase = Add("InputDialog", 1350, 110,
            ("Message", ShowcaseText.Get("enter_text")),
            ("Default", ""),
            ("DelaySeconds", "0"), ("Result", "demo_phrase"));
        var copy = Step("CopyPhrase", "Напечатать и проверить текст", 1600, 110);
        var introduceDrawing = Step("IntroduceDrawing", "Рисование и речь", 1850, 110);
        var shape = Add("RadioDialog", 2100, 110,
            ("Message", ShowcaseText.Get("shape_question")),
            ("Options", ShowcaseText.ShapeOptions), ("Result", "demo_shape_selection"));
        var prepareShape = Step("PrepareShape", "Подготовить рисунок", 2350, 110);
        var drawMore = Add("SetVariable", 2600, 110, ("Name", "demo_draw_more"), ("Value", "1"));
        var drawLoop = Add("WhileExpression", 4350, 110,
            ("Expression", "=$demo_draw_more==1"), ("MaxIterations", "0"));
        var announceDrawing = Step("AnnounceDrawing", "Объявить рисунок", 4600, 350);
        var draw = Step("DrawChoice", "Нарисовать выбранную фигуру", 4850, 350);
        var buffer = Add("BufferCapture", 5100, 350,
            ("Left", "${demo_left}"), ("Top", "${demo_top}"),
            ("Right", "${demo_right}"), ("Bottom", "${demo_bottom}"));
        var hash = Add("BufferHash", 5350, 350,
            ("Left", "${demo_left}"), ("Top", "${demo_top}"),
            ("Right", "${demo_right}"), ("Bottom", "${demo_bottom}"),
            ("Algorithm", "CRC32"), ("Result", "demo_hash"));
        var saveImage = Add("BufferSave", 5600, 350,
            ("Path", Path.Combine(ShowcaseState.DemoDirectory, "showcase-paint.png")));
        var feedback = Add("ChoiceDialog", 5850, 350,
            ("Message", ShowcaseText.Get("drawing_feedback")), ("Buttons", "4"),
            ("Icon", "2"), ("Result", "demo_feedback"));
        var liked = Add("IfVariable", 6100, 350,
            ("Name", "demo_feedback"), ("Comparison", "Равно"), ("Value", "6"));
        var happy = Add("HintPopup", 6350, 240,
            ("Message", ShowcaseText.Get("happy")), ("Title", "Kliker Blocks"));
        var happyVoice = Step("SpeakFeedbackYes", "Озвучить радость", 6600, 240);
        var endYes = Add("SetVariable", 6850, 240, ("Name", "demo_draw_more"), ("Value", "0"));
        var yesEnd = Add("BranchEnd", 7100, 240);
        var sorry = Add("HintPopup", 6350, 490,
            ("Message", ShowcaseText.Get("sorry")), ("Title", "Kliker Blocks"));
        var sorryVoice = Step("SpeakFeedbackNo", "Озвучить сожаление", 6600, 490);
        var retry = Add("RadioDialog", 6850, 490,
            ("Message", ShowcaseText.Get("retry_question")),
            ("Options", ShowcaseText.RetryOptions), ("Result", "demo_retry_selection"));
        var prepareRetry = Step("PrepareRetry", "Повторить или продолжить", 7100, 490);
        var noEnd = Add("BranchEnd", 7350, 490);

        var introduceColors = Step("IntroduceColors", "Поиск цветных картинок", 7600, 110);
        var chooseColor = Add("RadioDialog", 7850, 110,
            ("Message", ShowcaseText.Get("color_question")),
            ("Options", ShowcaseText.ColorOptions), ("Result", "demo_color_selection"));
        var prepareColor = Step("PrepareColor", "Объявить цвет голосом", 8100, 110);
        var openColors = Step("OpenColorWindows", "Открыть четыре цвета", 8350, 110);
        var waitColors = Step("WaitForColorWindows", "Подождать размещения окон", 8600, 110);
        var findColor = Step("FindSelectedColor", "Найти выбранный цвет", 8850, 110);
        var found = Add("HintPopup", 9100, 110,
            ("Message", ShowcaseText.Get("color_found")), ("Title", "Kliker Blocks"));
        var finalVoice = Step("FinalSpeech", "Надеюсь, я впечатлил", 9350, 110);
        var finalNote = Step("FinalNotepad", "Заключение в Блокноте", 9600, 110);
        var report = Add("TextFile", 10750, 540,
            ("Path", Path.Combine(ShowcaseState.DemoDirectory, "showcase-report.txt")),
            ("Action", "Добавить строку"),
            ("Value", "${demo_name} | ${demo_phrase} | CRC32 ${demo_hash} | words ${demo_word_count} | files ${demo_file_count}"),
            ("Result", "demo_report_lines"));
        var finish = Step("Finish", "Показать результат и граф", 11000, 540);
        var stop = Add("Stop", 11250, 540);

        Link(start, "Далее", intro); Link(intro, "Далее", name); Link(name, "Далее", note);
        Link(note, "Далее", greeting); Link(greeting, "Далее", phrase);
        Link(phrase, "Далее", copy); Link(copy, "Далее", introduceDrawing);
        Link(introduceDrawing, "Далее", shape); Link(shape, "Далее", prepareShape);
        Link(prepareShape, "Далее", drawMore); Link(drawMore, "Далее", drawLoop);
        Link(drawLoop, "Тело", announceDrawing); Link(announceDrawing, "Далее", draw);
        Link(draw, "Далее", buffer);
        Link(buffer, "Далее", hash); Link(hash, "Далее", saveImage);
        Link(saveImage, "Далее", feedback); Link(feedback, "Далее", liked);
        Link(liked, "Да", happy); Link(happy, "Далее", happyVoice);
        Link(happyVoice, "Далее", endYes); Link(endYes, "Далее", yesEnd);
        Link(liked, "Нет", sorry); Link(sorry, "Далее", sorryVoice);
        Link(sorryVoice, "Далее", retry); Link(retry, "Далее", prepareRetry);
        Link(prepareRetry, "Далее", noEnd);
        Link(drawLoop, "Далее", introduceColors);
        Link(introduceColors, "Далее", chooseColor); Link(chooseColor, "Далее", prepareColor);
        Link(prepareColor, "Далее", openColors); Link(openColors, "Далее", waitColors);
        Link(waitColors, "Далее", findColor); Link(findColor, "Далее", found);
        Link(finalVoice, "Далее", finalNote);
        // A second chapter uses actual data/control blocks on files in the
        // Showcase directory. It never touches unrelated user documents.
        var chapter = Step("DataChapter", "Данные, условия и циклы", 4250, 540);
        var define = Add("DefineVariable", 4500, 540, ("Name", "demo_counter"), ("Value", "0"));
        var math = Add("MathVariable", 4750, 540, ("Name", "demo_counter"),
            ("Operation", "Сложить"), ("Operand", "1"));
        var byName = Add("SetVariableByName", 5000, 540,
            ("Reference", "$demo_counter"), ("Value", "2"));
        var expression = Add("IfExpression", 5250, 540, ("Expression", "=$demo_counter==2"));
        var split = Add("StringSplit", 5500, 540, ("Text", "${demo_phrase}"),
            ("Separator", " "), ("Array", "demo_words"), ("Count", "demo_words_count"));
        var size = Add("ArrayAction", 5750, 540, ("Name", "demo_words"),
            ("Action", "Размер"), ("Result", "demo_word_count"));
        var wordFile = Path.Combine(ShowcaseState.DemoDirectory, "showcase-words.txt");
        var writeWords = Add("TextFileArray", 6000, 540, ("Path", wordFile),
            ("Action", "Записать массив"), ("Array", "demo_words"), ("Count", "demo_written_words"));
        var readWords = Add("TextFileArray", 6250, 540, ("Path", wordFile),
            ("Action", "Прочитать в массив"), ("Array", "demo_words_copy"),
            ("Count", "demo_read_words"));
        var iniFile = Path.Combine(ShowcaseState.DemoDirectory, "showcase.ini");
        var iniWrite = Add("IniFile", 6500, 540, ("Path", iniFile), ("Section", "visitor"),
            ("Key", "name"), ("Action", "Записать"), ("Value", "${demo_name}"),
            ("Result", "demo_ini_result"));
        var iniRead = Add("IniFile", 6750, 540, ("Path", iniFile), ("Section", "visitor"),
            ("Key", "name"), ("Action", "Прочитать"), ("Result", "demo_ini_name"));
        var listFiles = Add("DirectoryList", 7000, 540,
            ("Path", ShowcaseState.DemoDirectory), ("Mask", "*.txt"),
            ("Array", "demo_files"), ("Count", "demo_file_count"));
        var clipboardSet = Add("ClipboardSet", 7250, 540, ("Text", "${demo_phrase}"));
        var clipboardGet = Add("ClipboardGet", 7500, 540, ("Name", "demo_clipboard"));
        var arrayItem = Add("ArraySet", 7750, 540, ("Name", "demo_samples"),
            ("Index", "0"), ("Value", "${demo_phrase}"));
        var arrayCheck = Add("IfVariable", 8000, 540,
            ("Name", "demo_draw_more"), ("Comparison", "Равно"), ("Value", "0"));
        var switchTarget = Add("SwitchValue", 8500, 540,
            ("Expression", "${demo_choice}"), ("Case1", "star"), ("Case2", "heart"),
            ("Case3", "house"), ("Case4", "arrow"), ("Case5", "none"));
        var repeat = Add("Repeat", 8750, 540, ("Times", "2"));
        var repeatBody = Add("MathVariable", 8750, 770,
            ("Name", "demo_counter"), ("Operation", "Сложить"), ("Operand", "1"));
        var repeatEnd = Add("BranchEnd", 9000, 770);
        var whileLoop = Add("WhileExpression", 9000, 540,
            ("Expression", "=$demo_counter<5"), ("MaxIterations", "10"));
        var whileBody = Add("MathVariable", 9250, 770,
            ("Name", "demo_counter"), ("Operation", "Сложить"), ("Operand", "1"));
        var whileEnd = Add("BranchEnd", 9500, 770);
        var call = Add("CallSub", 9250, 540, ("Name", "demo_summary"));
        var sub = Add("SubStart", 9750, 900, ("Name", "demo_summary"));
        var subLog = Add("Log", 10000, 900,
            ("Text", "Kliker Blocks: ${demo_name}; ${demo_word_count} слов; ${demo_file_count} файлов."));
        var subReturn = Add("SubReturn", 10250, 900);
        var wait = Add("Wait", 9500, 540, ("Milliseconds", "120"));
        var scriptLog = Add("LogFile", 9750, 540, ("Path", "showcase-script.log"));
        var logData = Add("Log", 10000, 730,
            ("Text", "Переменные, массив, INI, список файлов, цикл и подпрограмма проверены."));
        var beep = Add("Beep", 10250, 730, ("Frequency", "523"), ("DurationMs", "120"));
        var endData = Step("DataEnd", "Показать итоги обработки", 10480, 730);

        Link(found, "Далее", chapter); Link(chapter, "Далее", define);
        Link(define, "Далее", math); Link(math, "Далее", byName);
        Link(byName, "Далее", expression);
        Link(expression, "Да", split); Link(expression, "Нет", split);
        Link(split, "Далее", size); Link(size, "Далее", writeWords);
        Link(writeWords, "Далее", readWords); Link(readWords, "Далее", iniWrite);
        Link(iniWrite, "Далее", iniRead); Link(iniRead, "Далее", listFiles);
        Link(listFiles, "Далее", clipboardSet); Link(clipboardSet, "Далее", clipboardGet);
        Link(clipboardGet, "Далее", arrayItem); Link(arrayItem, "Далее", arrayCheck);
        Link(arrayCheck, "Да", switchTarget); Link(arrayCheck, "Нет", switchTarget);
        foreach (var output in BlockCatalog.Get("SwitchValue").Outputs) Link(switchTarget, output, repeat);
        Link(repeat, "Тело", repeatBody); Link(repeatBody, "Далее", repeatEnd);
        Link(repeat, "Далее", whileLoop);
        Link(whileLoop, "Тело", whileBody); Link(whileBody, "Далее", whileEnd);
        Link(whileLoop, "Далее", call);
        Link(sub, "Тело", subLog); Link(subLog, "Далее", subReturn);
        Link(call, "Далее", wait); Link(wait, "Далее", scriptLog);
        Link(scriptLog, "Далее", logData); Link(logData, "Далее", beep);
        Link(beep, "Далее", endData); Link(endData, "Далее", finalVoice);
        Link(finalNote, "Далее", report); Link(report, "Далее", finish);
        Link(finish, "Далее", stop);
        // The showcase doubles as an inspectable execution example: every reached block is traced.
        foreach (var node in project.Nodes) node.LogActions = true;
        foreach (var node in project.Nodes.Where(node => node.X >= 4250 &&
                     node != drawLoop && !new[] { announceDrawing, draw, buffer, hash, saveImage,
                     feedback, liked, happy, happyVoice, endYes, yesEnd, sorry, sorryVoice,
                     retry, prepareRetry, noEnd, introduceColors, chooseColor, prepareColor,
                     openColors, waitColors, findColor, found, finalVoice, finalNote }.Contains(node)))
            node.X += 5700;
        finalVoice.X = 16450; finalVoice.Y = 730;
        finalNote.X = 16700; finalNote.Y = 730;
        report.X = 16950; report.Y = 730;
        finish.X = 17200; finish.Y = 730;
        stop.X = 17450; stop.Y = 730;
        return project;
    }
}

namespace KlikerBlocks;

// Each help example is an independent native graph. The featured node is kept in
// the graph even when other nodes prepare its input or display its result.
internal static class BlockExampleCatalog
{
    internal sealed record Example(string Code, string NoteRu, string NoteEn);

    public static Example For(string kind)
    {
        var nodes = new List<BlockNode>();
        BlockNode Add(string type, int column, int row = 0)
        {
            var node = BlockCatalog.Create(type, 80 + column * 245, 110 + row * 130);
            node.LogActions = true;
            nodes.Add(node);
            return node;
        }
        static void Link(BlockNode from, string output, BlockNode to) => from.Links[output] = to.Id;
        BlockNode Say(string ru, string en, int column, int row = 0)
        {
            var node = Add("Log", column, row);
            node.Values["Text"] = UiLanguage.Code == "ru" ? ru : en;
            return node;
        }

        var start = Add("Start", 0);
        var featured = kind == "Start" ? start : Add(kind, 2);
        var before = start;
        var noteRu = "Запустите один проход и посмотрите журнал выполнения. Выделенный в справке блок записывает свои действия, а соседние блоки показывают результат.";
        var noteEn = "Run once and inspect the execution journal. The featured block records its actions; surrounding blocks prepare input or show the result.";

        if (kind is "MathVariable" or "IfVariable" or "IfExpression" or "WhileExpression" or "SwitchValue" or
            "SetVariableByName" or "UndefineVariable" or "DefineVariable" or "RadioDialog")
        {
            var init = Add("SetVariable", 1);
            init.Values["Name"] = "counter";
            init.Values["Value"] = kind == "WhileExpression" ? "0" : kind == "RadioDialog" ? "1" : "2";
            Link(start, "Далее", init);
            before = init;
        }
        else if (kind == "ClipboardGet")
        {
            var seed = Add("ClipboardSet", 1);
            seed.Values["Text"] = "Kliker Blocks example";
            Link(start, "Далее", seed);
            before = seed;
        }
        else if (kind == "ExtractText")
        {
            var seed = Add("SetVariable", 1);
            seed.Values["Name"] = "source_text";
            seed.Values["Value"] = "Invoice #582";
            Link(start, "Далее", seed);
            before = seed;
        }
        else if (kind is "BufferPixel" or "BufferCount" or "BufferHash" or "BufferReplace" or
            "BufferFindColor" or "BufferFindImage" or "BufferScanColor" or "BufferScanImage" or "BufferSave")
        {
            if (kind is "BufferFindImage" or "BufferScanImage")
            {
                var template = Add("Screenshot", 1, 1);
                template.Values["Path"] = "KlikerBlocks-help-template.png";
                template.Values["Right"] = "100";
                template.Values["Bottom"] = "100";
                Link(start, "Далее", template);
                before = template;
                featured.Values["Path"] = template.Values["Path"];
            }
            var capture = Add("BufferCapture", 1);
            capture.Values["Right"] = "100";
            capture.Values["Bottom"] = "100";
            Link(before, "Далее", capture);
            before = capture;
            featured.Values.TryAdd("Right", "100");
            featured.Values.TryAdd("Bottom", "100");
            if (featured.Values.ContainsKey("Right")) featured.Values["Right"] = "100";
            if (featured.Values.ContainsKey("Bottom")) featured.Values["Bottom"] = "100";
        }
        else if (kind == "MoveFound")
        {
            var finder = Add("FindColor", 1);
            finder.Values["Color"] = "#FFFFFF";
            Link(start, "Далее", finder);
            Link(finder, "Найден", featured);
            Link(finder, "Нет", Say("Белый пиксель не найден", "No white pixel found", 2, 1));
            noteRu += " Курсор переместится, только если на экране найден белый пиксель.";
            noteEn += " The pointer moves only if a white pixel is found on screen.";
        }
        else if (kind is "WindowInfo" or "WindowAction" or "ElementInfo" or "ElementSetText" or "MessageSend")
        {
            var handle = Add("SetVariable", 1);
            handle.Values["Name"] = kind is "ElementInfo" or "ElementSetText" ? "element" : "window";
            handle.Values["Value"] = "${_hwnd_self}";
            Link(start, "Далее", handle);
            before = handle;
        }
        else if (kind == "MoveWindow")
        {
            var info = Add("WindowInfo", 1);
            info.Values["Handle"] = "${_hwnd_self}";
            Link(start, "Далее", info);
            before = info;
            featured.Values["Title"] = AppIdentity.Title;
        }
        else if (kind == "TextFileArray")
        {
            var item = Add("ArraySet", 1);
            item.Values["Name"] = "lines";
            item.Values["Value"] = "example line";
            Link(start, "Далее", item);
            before = item;
            featured.Values["Action"] = "Записать массив";
            featured.Values["Path"] = "KlikerBlocks-help-lines.txt";
        }
        else if (kind == "TranslateArray")
        {
            var item = Add("ArraySet", 1);
            item.Values["Name"] = "lines";
            item.Values["Value"] = "Hello, world!";
            Link(start, "Далее", item);
            before = item;
        }
        else if (kind == "MessageReceive")
        {
            var send = Add("MessageSend", 1);
            send.Values["Handle"] = "${_hwnd_self}";
            send.Values["WParam"] = "42";
            send.Values["Mode"] = "Синхронно";
            Link(start, "Далее", send);
            before = send;
        }
        else if (kind == "FindImage")
        {
            var template = Add("Screenshot", 1);
            template.Values["Path"] = "KlikerBlocks-help-template.png";
            template.Values["Right"] = "100";
            template.Values["Bottom"] = "100";
            Link(start, "Далее", template);
            before = template;
            featured.Values["Path"] = template.Values["Path"];
        }

        if (kind != "Start" && kind != "Description" && kind != "SubStart" &&
            kind != "SubReturn" && kind != "BranchEnd" && kind != "MoveFound")
            Link(before, "Далее", featured);

        switch (kind)
        {
            case "Start":
                Link(start, "Далее", Say("Граф начался со Старт", "The graph started at Start", 1));
                break;
            case "MouseButton":
            case "KeyState":
            case "VirtualKey":
            {
                var release = Add(kind, 3);
                release.Values["Action"] = "Отпустить";
                Link(featured, "Далее", release);
                Link(release, "Далее", Say("Клавиша или кнопка отпущена", "Key or button released", 4));
                noteRu += " Вторая копия блока отпускает кнопку сразу после нажатия.";
                noteEn += " A second copy of the block releases the button immediately after the press.";
                break;
            }
            case "ElementSetText":
            {
                featured.Values["Text"] = AppIdentity.Title + " — example";
                var restore = Add("ElementSetText", 3);
                restore.Values["Handle"] = "${element}";
                restore.Values["Text"] = AppIdentity.Title;
                Link(featured, "Далее", restore);
                Link(restore, "Далее", Say("Заголовок окна восстановлен", "Window title restored", 4));
                noteRu += " Пример на мгновение меняет заголовок окна Kliker Blocks и сразу восстанавливает его.";
                noteEn += " The example briefly changes the Kliker Blocks window title, then restores it.";
                break;
            }
            case "MoveWindow":
            {
                var restore = Add("WindowAction", 3);
                restore.Values["Handle"] = "${_hwnd_self}";
                restore.Values["Action"] = "Геометрия";
                restore.Values["WindowX"] = "${window_x}";
                restore.Values["WindowY"] = "${window_y}";
                restore.Values["WindowWidth"] = "${window_w}";
                restore.Values["WindowHeight"] = "${window_h}";
                Link(featured, "Далее", restore);
                Link(restore, "Далее", Say("Размер и положение восстановлены", "Window bounds restored", 4));
                noteRu += " Пример двигает окно Kliker Blocks и затем возвращает прежнюю геометрию.";
                noteEn += " The example moves the Kliker Blocks window, then restores its original bounds.";
                break;
            }
            case "Description":
                featured.X = 275; featured.Y = 280;
                featured.Values["Title"] = UiLanguage.Code == "ru" ? "Группа примера" : "Example group";
                Link(start, "Далее", Say("Описание оформляет поле и не исполняется", "Description decorates the canvas and does not execute", 1));
                break;
            case "ShowcaseStep":
                featured.Values["Action"] = "Intro";
                noteRu = "Служебный блок встроенной демонстрации. Для запуска всего графа откройте меню «Что я умею». Скопированный фрагмент показывает его структуру; отдельно в обычном сценарии шаг недоступен.";
                noteEn = "Internal block for the built-in showcase. Run the complete graph from the What I Can Do menu. This copy shows its structure; the step cannot run alone in a normal scenario.";
                Link(featured, "Далее", Say("Шаг демонстрации выполнен", "Showcase step completed", 3));
                break;
            case "Repeat":
            case "InputGroup":
            case "Parallel":
            {
                var first = Say("Тело A выполнилось", "Body A ran", 3);
                var endA = Add("BranchEnd", 4);
                Link(first, "Далее", endA);
                Link(featured, kind == "Parallel" ? "Ветка A" : "Тело", first);
                if (kind == "Parallel")
                {
                    var second = Say("Тело B выполнилось", "Body B ran", 3, 1);
                    var endB = Add("BranchEnd", 4, 1);
                    Link(second, "Далее", endB);
                    Link(featured, "Ветка B", second);
                }
                Link(featured, "Далее", Say("Все ветки завершены", "All branches completed", 5));
                break;
            }
            case "WhileExpression":
            {
                featured.Values["Expression"] = "=$counter<3";
                featured.Values["MaxIterations"] = "3";
                var increment = Add("MathVariable", 3);
                var count = Say("Проход ${counter}", "Iteration ${counter}", 4);
                var end = Add("BranchEnd", 5);
                Link(featured, "Тело", increment);
                Link(increment, "Далее", count);
                Link(count, "Далее", end);
                Link(featured, "Далее", Say("Цикл завершён: ${counter}", "Loop finished: ${counter}", 4, 1));
                break;
            }
            case "CallSub":
            case "SubStart":
            case "SubReturn":
            {
                var call = kind == "CallSub" ? featured : Add("CallSub", 1);
                var definition = kind == "SubStart" ? featured : Add("SubStart", 1, 2);
                var ret = kind == "SubReturn" ? featured : Add("SubReturn", 4, 2);
                call.Values["Name"] = "example_sub";
                definition.Values["Name"] = "example_sub";
                Link(start, "Далее", call);
                Link(call, "Далее", Say("Возврат в основную цепочку", "Returned to the main chain", 3));
                var body = Say("Внутри подпрограммы", "Inside the subroutine", 3, 2);
                Link(definition, "Тело", body);
                Link(body, "Далее", ret);
                break;
            }
            case "WaitScreenText":
                Link(featured, "Найдено", Say("Текст появился", "Text appeared", 4));
                Link(featured, "Таймаут", Say("Текст не найден за 1 секунду", "Text was not found within 1 second", 4, 1));
                break;
            case "ExtractText":
                Link(featured, "Далее", Say("Номер счёта: ${invoice_id}", "Invoice number: ${invoice_id}", 4));
                break;
            case "BranchEnd":
            {
                var repeat = Add("Repeat", 1);
                repeat.Values["Times"] = "2";
                Link(start, "Далее", repeat);
                var body = Say("Выполняется тело повтора", "Repeat body is running", 3);
                Link(repeat, "Тело", body);
                Link(body, "Далее", featured);
                Link(repeat, "Далее", Say("Повтор завершён", "Repeat finished", 4, 1));
                break;
            }
            case "Stop":
                noteRu += " После блока Стоп дальнейших действий нет.";
                noteEn += " No actions follow Stop.";
                break;
            default:
            {
                Configure(kind, featured, ref noteRu, ref noteEn);
                if (featured.Links.ContainsKey("Далее"))
                    Link(featured, "Далее", Say(ResultRu(kind), ResultEn(kind), 3));
                else
                    foreach (var output in featured.Links.Keys.ToArray())
                        Link(featured, output, Say("Выход: " + output, "Output: " + output, 3, Array.IndexOf(BlockCatalog.Get(kind).Outputs, output)));
                break;
            }
        }
        var graph = new ProjectModel { ScriptName = "Help: " + kind, Nodes = nodes, VariableNamesAsciiOnly = true };
        return new Example(GraphCodeCodec.Serialize(graph), noteRu, noteEn);
    }

    private static void Configure(string kind, BlockNode node, ref string ru, ref string en)
    {
        void Set(string key, string value) { if (node.Values.ContainsKey(key)) node.Values[key] = value; }
        switch (kind)
        {
            case "Note": Set("TextMode", "Заменить"); Set("Text", "Hello from Kliker Blocks");
                ru += " Пример создаёт окно заметки note1 с текстом; его можно свернуть, сохранить или закрыть.";
                en += " The example creates note1 with text; you can minimize, save or close it."; break;
            case "WatchCursor": Set("DurationMs", "2000"); Set("ActivationMode", "Сразу"); Set("RequiredHold", "Нет"); break;
            case "MoveRelative": Set("DeltaX", "15"); Set("DeltaY", "0"); break;
            case "ClipboardSet": Set("Text", "Kliker Blocks example"); break;
            case "CountTextChars": Set("Text", "111 22 A"); Set("Mode", "Только из списка"); Set("Include", "1\n2"); break;
            case "CountFileChars":
            case "EditFileLine":
            case "EditFileText":
                ru += " Перед запуском выберите существующий тестовый текстовый файл. Для сохранения копии задайте выходной путь.";
                en += " Select an existing test text file before running. Set an output path to save a copy.";
                break;
            case "TextFile": Set("Path", "KlikerBlocks-help-example.txt"); Set("Action", "Добавить строку"); Set("Value", "Hello from Kliker Blocks"); break;
            case "CsvTable":
                Set("Path", "KlikerBlocks-help-example.csv");
                Set("Action", "Записать ячейку"); Set("Value", "Hello CSV");
                ru += " Блок создаст CSV в папке сохранённого проекта и запишет значение в первую ячейку; путь виден в свойствах.";
                en += " The block creates a CSV beside the saved project and writes its first cell; the path is visible in properties.";
                break;
            case "ExcelTable":
                Set("Path", "KlikerBlocks-help-example.xlsx");
                Set("Action", "Записать ячейку"); Set("Value", "Hello Excel");
                ru += " Блок создаст .xlsx в папке сохранённого проекта и запишет значение в A1.";
                en += " The block creates an .xlsx beside the saved project and writes a value to A1.";
                break;
            case "PdfText":
                Set("Action", "Количество страниц");
                ru += " Перед запуском выберите существующий PDF в свойствах блока; результат запишется в pdf_result.";
                en += " Select an existing PDF in block properties before running; the page count is stored in pdf_result.";
                break;
            case "FileQueue":
                Set("Folder", "."); Set("StatePath", "KlikerBlocks-help-queue.json");
                ru += " Блок ищет *.txt в папке сохранённого проекта; очередь запоминает выданный файл в отдельном JSON. Для настоящей обработки подключите следующий блок и отметку выполнения.";
                en += " The block finds *.txt beside the saved project and tracks leased files in a separate JSON. Connect processing and a done marker for a real workflow.";
                break;
            case "Checkpoint":
                Set("Path", "KlikerBlocks-help-checkpoint.json");
                ru += " После сохранения запустите «Сценарий → Продолжить с контрольной точки…» с неизменённым графом.";
                en += " After saving, use Scenario → Resume from checkpoint with the same unchanged graph.";
                break;
            case "OcrImage":
                Set("Path", "scan.pdf"); Set("PdfPage", "0");
                ru += " Перед запуском выберите сканированный PDF, укажите страницу или 0 для первых 20 страниц и установите Tesseract с языком eng; движок не входит в выпуск.";
                en += " Choose a scanned PDF, enter a page or 0 for the first 20 pages, and install Tesseract with eng before running; the engine is not bundled.";
                break;
            case "WaitScreenText":
                Set("Text", "Готово"); Set("TimeoutMs", "1000"); Set("IntervalMs", "500");
                ru += " Установите Tesseract и нужный язык; блок распознаёт экран локально. В примере ожидание ограничено одной секундой.";
                en += " Install Tesseract and the required language; the block recognizes screen text locally. This example waits for one second.";
                break;
            case "ExtractText":
                Set("Text", "${source_text}"); Set("Pattern", @"Invoice\s+#(\d+)");
                Set("Group", "1"); Set("Result", "invoice_id");
                ru += " Из исходной строки Invoice #582 будет извлечено значение 582.";
                en += " The value 582 is extracted from the sample string Invoice #582.";
                break;
            case "IniFile": Set("Path", "KlikerBlocks-help-example.ini"); Set("Action", "Записать"); Set("Value", "Kliker Blocks"); break;
            case "DirectoryList": Set("Path", "."); Set("Mask", "*.txt"); break;
            case "IfVariable": Set("Value", "2"); break;
            case "IfExpression":
                Set("Expression", "=if $counter<10");
                ru += " Краткая форма if возвращает TRUE или FALSE; при counter=2 сработает выход «Да».";
                en += " The short if form returns TRUE or FALSE; with counter=2 the Yes output runs.";
                break;
            case "SwitchValue": Set("Expression", "=$counter"); break;
            case "StringSplit": Set("Text", "red,green,blue"); break;
            case "SetVariable": Set("Value", "hello"); break;
            case "DefineVariable": Set("Value", "new value"); break;
            case "SetVariableByName": Set("Value", "changed"); break;
            case "ArraySet": Set("Value", "first item"); break;
            case "ArrayAction": Set("Value", "new item"); break;
            case "Screenshot": Set("Path", "KlikerBlocks-help-screen.png"); break;
            case "BufferSave": Set("Path", "KlikerBlocks-help-buffer.png"); break;
            case "WindowFind": Set("Title", "Kliker Blocks"); break;
            case "IfWindow": Set("Title", AppIdentity.Title); break;
            case "ActivateWindow": Set("Title", AppIdentity.Title); break;
            case "WindowInfo": Set("Handle", "${window}"); break;
            case "WindowAction": Set("Handle", "${window}"); Set("Action", "Активировать"); break;
            case "ElementInfo": Set("Handle", "${element}"); Set("Info", "PID"); break;
            case "ElementSetText": Set("Handle", "${element}"); break;
            case "MessageSend": Set("Handle", "${window}"); Set("WParam", "42"); break;
            case "RunFile": Set("Path", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe")); break;
            case "LogFile": Set("Path", "example-journal.log"); break;
            case "SpeakText": Set("Text", "Hello from Kliker Blocks"); break;
            case "InputDialog": Set("DelaySeconds", "0"); break;
            case "RadioDialog":
                Set("Message", UiLanguage.Code == "ru" ? "Выберите вычисленный вариант" : "Choose a calculated option");
                Set("Options", UiLanguage.Code == "ru"
                    ? @"=[(${counter}+2)*3]& па \&" + "\n11\n22"
                    : @"=[(${counter}+2)*3]& units \&" + "\n11\n22");
                ru += " Первая строка показывает 9 па &: выражение в [] вычисляется, & соединяет текст, а \\& печатает знак &. В свойствах 11 и 22 находятся на отдельных строках; в коде переносы записаны как \\n.";
                en += " The first option displays 9 units &: [] evaluates math, & joins text, and \\& prints a literal &. In properties, 11 and 22 are separate lines; graph code writes the breaks as \\n.";
                break;
            case "HttpGet": Set("Url", "https://example.com"); break;
            case "HttpPost":
                Set("Url", "https://example.com/api");
                Set("Body", "{\"name\":\"Alex\"}");
                ru += " Замените адрес на свой тестовый сервер; POST отправит тело серверу только после запуска.";
                en += " Replace the URL with your own test server; POST sends the body only when you run the graph.";
                break;
            case "JsonValue": Set("Json", "{\"user\":{\"name\":\"Alex\"}}"); Set("Path", "user.name"); break;
            case "WaitEvent": Set("TimeoutMs", "3000"); break;
            case "UiWait":
            case "UiAction":
                ru += " Перед запуском нажмите кнопку захвата элемента в свойствах блока и наведите указатель на тестовый элемент Windows.";
                en += " Before running, capture a test Windows element from the block properties while the pointer is over it.";
                break;
            case "RunScenario": Set("Path", "child.klb"); break;
            case "PlaySound": Set("Path", "sound.wav"); break;
            case "RunAhk": Set("Path", "example.ahk"); break;
            case "FindImage":
            case "BufferFindImage":
            case "BufferScanImage": break;
        }
        if (kind is "InputDialog" or "ChoiceDialog" or "RadioDialog")
        {
            Set("WindowTitle", UiLanguage.Code == "ru" ? "Вопрос мини-игры" : "Mini-game question");
            Set("WindowX", "80");
            Set("WindowY", "100");
            Set("WindowWidth", "560");
            Set("WindowHeight", kind == "RadioDialog" ? "440" : "240");
            ru += " Окно примера получило собственный заголовок и координаты экрана X=80, Y=100; измените их и размер в свойствах блока. Пустые X/Y размещают окно по центру, пустые размеры возвращают стандартный вид.";
            en += " This example sets a custom title and screen position X=80, Y=100. Change its position and size in block properties. Blank X/Y centers it; blank dimensions restore the default size.";
        }
        if (kind is "TranslateText" or "DetectLanguage" or "LanguagePair" or "TranslateArray")
        {
            ru += " До запуска настройте LibreTranslate или установите локальную модель для выбранной пары; текст передаётся внешнему сервису только при его явном выборе.";
            en += " Configure LibreTranslate or install a local model for the selected pair before running; text is sent to a remote service only when you explicitly choose it.";
        }
        else if (kind is "RunAhk" or "RunScenario" or "PlaySound")
        {
            ru += " Перед запуском замените путь к примеру на существующий файл нужного типа в свойствах блока.";
            en += " Before running, replace the sample path with an existing file of the required type in block properties.";
        }
        else if (kind is "TypeText" or "Key" or "KeyState" or "VirtualKey" or "Click" or "MouseButton" or "Wheel" or "ElementSetText")
        {
            ru += " Перед запуском активируйте безопасное тестовое окно: этот блок отправляет ввод в окно или элемент Windows.";
            en += " Focus a safe test window before running: this block sends input to a Windows window or control.";
        }
        else if (kind is "HttpGet" or "HttpPost")
        {
            ru += " Нужен доступ к указанному сайту; замените URL для собственного запроса.";
            en += " The specified site must be reachable; replace the URL for your own request.";
        }
        else if (kind is "LogFile")
        {
            ru += " Сохраните новый проект в доступной для записи папке: example-journal.log появится рядом с проектом; полный путь выводится в журнале.";
            en += " Save the new project in a writable folder: example-journal.log appears beside it; the journal shows the full path.";
        }
    }

    private static string ResultRu(string kind) => kind switch
    {
        "SetVariable" or "DefineVariable" or "SetVariableByName" or "MathVariable" => "Значение counter: ${counter}",
        "ClipboardGet" => "Прочитано из буфера: ${text}",
        "TextFile" or "IniFile" => "Операция с файлом выполнена; результат: ${result}",
        "CsvTable" => "Результат CSV: ${csv_result}",
        "ExcelTable" => "Результат Excel: ${excel_result}",
        "PdfText" => "Страниц PDF: ${pdf_result}",
        "OcrImage" => "Распознано: ${ocr_text}",
        "FileQueue" => "Очередь: ${queue_file}",
        "TextFileArray" or "DirectoryList" or "TranslateArray" => "Количество элементов: ${count}",
        "StringSplit" => "Частей строки: ${count}",
        "BufferPixel" => "Цвет пикселя: ${color}",
        "BufferCount" => "Совпадений цвета: ${count}",
        "BufferHash" => "Контрольная сумма: ${checksum}",
        "BufferReplace" => "Заменено пикселей: ${replaced}",
        "BufferScanColor" or "BufferScanImage" => "Найдено точек: ${found}",
        "WindowFind" => "HWND окна: ${window}",
        "WindowInfo" => "Окно: ${window_w} × ${window_h}",
        "ElementAtPoint" => "HWND элемента: ${element}",
        "ElementInfo" => "Данные элемента: ${element_data}",
        "MessageSend" => "Ответ сообщения: ${response}",
        "MessageReceive" => "Новое сообщение: ${is_new}; wParam=${wparam}",
        "InputDialog" => "Ответ: ${input}",
        "ChoiceDialog" => "Код кнопки: ${response}",
        "RadioDialog" => "Номер варианта: ${choice}",
        "TranslateText" => "Перевод: ${translated}",
        "DetectLanguage" => "Язык: ${language}",
        _ => "Блок выполнен: " + kind
    };

    private static string ResultEn(string kind) => kind switch
    {
        "SetVariable" or "DefineVariable" or "SetVariableByName" or "MathVariable" => "counter value: ${counter}",
        "ClipboardGet" => "Clipboard text: ${text}",
        "TextFile" or "IniFile" => "File operation completed; result: ${result}",
        "CsvTable" => "CSV result: ${csv_result}",
        "ExcelTable" => "Excel result: ${excel_result}",
        "PdfText" => "PDF pages: ${pdf_result}",
        "OcrImage" => "Recognized text: ${ocr_text}",
        "FileQueue" => "Queue file: ${queue_file}",
        "TextFileArray" or "DirectoryList" or "TranslateArray" => "Element count: ${count}",
        "StringSplit" => "String parts: ${count}",
        "BufferPixel" => "Pixel color: ${color}",
        "BufferCount" => "Matching pixels: ${count}",
        "BufferHash" => "Checksum: ${checksum}",
        "BufferReplace" => "Replaced pixels: ${replaced}",
        "BufferScanColor" or "BufferScanImage" => "Found points: ${found}",
        "WindowFind" => "Window HWND: ${window}",
        "WindowInfo" => "Window: ${window_w} × ${window_h}",
        "ElementAtPoint" => "Element HWND: ${element}",
        "ElementInfo" => "Element data: ${element_data}",
        "MessageSend" => "Message result: ${response}",
        "MessageReceive" => "New message: ${is_new}; wParam=${wparam}",
        "InputDialog" => "Answer: ${input}",
        "ChoiceDialog" => "Button code: ${response}",
        "RadioDialog" => "Choice number: ${choice}",
        "TranslateText" => "Translation: ${translated}",
        "DetectLanguage" => "Language: ${language}",
        _ => "Block completed: " + kind
    };
}

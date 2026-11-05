using System.Text;

namespace KlikerBlocks;

internal sealed class AboutForm : Form
{
    private readonly RichTextBox _features = new() { Dock = DockStyle.Fill, ReadOnly = true,
        BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };
    private readonly RichTextBox _releases = new() { Dock = DockStyle.Fill, ReadOnly = true,
        BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };

    public AboutForm()
    {
        var copy = CopyFor(UiLanguage.Code);
        Text = copy.Title;
        Width = 720;
        Height = 640;
        MinimumSize = new Size(510, 430);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        Controls.Add(root);
        root.Controls.Add(new Label { Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold),
            Text = AppIdentity.Title + Environment.NewLine + copy.Subtitle }, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var featuresTab = new TabPage(copy.FeaturesTab);
        featuresTab.Controls.Add(_features);
        tabs.TabPages.Add(featuresTab);
        var releasesTab = new TabPage(copy.ReleasesTab);
        releasesTab.Controls.Add(_releases);
        tabs.TabPages.Add(releasesTab);
        root.Controls.Add(tabs, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 7, 2, 0), WrapContents = false };
        var help = new Button { Text = copy.Help, Width = 112, Height = 36,
            Margin = new Padding(6, 0, 0, 0) };
        help.Click += (_, _) => new HelpForm(null).Show(this);
        var website = new Button { Text = UiLanguage.T("Сайт Kliker Blocks"), Width = 145, Height = 36,
            Margin = new Padding(6, 0, 0, 0) };
        website.Click += (_, _) => WebsiteLinks.Open(this);
        var support = new Button { Text = UiLanguage.T("Поддержать проект"), Width = 155, Height = 36,
            Margin = new Padding(6, 0, 0, 0) };
        support.Click += (_, _) => new SupportForm().Show(this);
        buttons.Controls.Add(help);
        buttons.Controls.Add(website);
        buttons.Controls.Add(support);
        root.Controls.Add(buttons, 0, 2);
        _features.Text = BuildFeatures(copy);
        _releases.Text = BuildReleases(copy);
    }

    private static string BuildFeatures(Copy copy)
    {
        var result = new StringBuilder();
        result.AppendLine(copy.Introduction).AppendLine();
        foreach (var line in copy.Capabilities) result.AppendLine("• " + line);
        result.AppendLine("• " + (UiLanguage.Code == "ru" ? "Экранное меню: текст, речь, снимки, OCR, заметки и пользовательские графы. В заметках доступны форматирование текста и масштабирование изображений. Индикатор перетаскивается мышью." : "Screen menu: text, speech, screenshots, OCR, notes and user graphs. Notes support text formatting and image zoom. Drag the scenario indicator with the mouse."));
        result.AppendLine().AppendLine(copy.BlocksHeading + " (" + BlockCatalog.All.Length + ")");
        foreach (var group in BlockCatalog.All.GroupBy(spec => MainForm.CategoryFor(spec.Kind))
                     .OrderBy(group => MainForm.CategoryOrder(group.Key)))
        {
            result.AppendLine().AppendLine(UiLanguage.T(group.Key) + ":");
            result.AppendLine(string.Join(", ", group.Select(spec => UiLanguage.T(spec.Title))));
        }
        result.AppendLine().AppendLine(copy.HelpPrompt);
        return result.ToString();
    }

    private static string BuildReleases(Copy copy) =>
        "v" + AppIdentity.VersionLabel + "\r\n" + (UiLanguage.Code == "ru"
            ? "Добавлены экранное меню для выделенного текста, снимков и пользовательских графов; заметки с текстом и изображением и блок управления заметками. Индикатор можно перетаскивать, лишние блоки «Старт» — удалять."
            : "Added a screen menu for selected text, screenshots and user graphs; notes with text and images and a Note control block. The indicator can be dragged and extra Start blocks deleted.") +
        "\r\n\r\nv1.2.4\r\n" + (UiLanguage.Code == "ru"
            ? "Добавлены отмена и повтор изменений графа (Ctrl+Z/Ctrl+Y), локальное ожидание появления текста на экране и извлечение фрагментов текста регулярным выражением. Выделенную связанную группу можно преобразовать в вызываемый собственный блок-подпрограмму; исходное тело остаётся в графе отдельно. Каталог функций автоматически сверяется с сайтом и GitHub перед публикацией."
            : "Added graph undo/redo (Ctrl+Z/Ctrl+Y), local waiting for screen text and regular-expression text extraction. A selected connected group can become a callable custom subroutine block; its body remains separately visible in the graph. The feature catalog is automatically checked against the website and GitHub before publication.") +
        "\r\n\r\nv1.2.3\r\n" + (UiLanguage.Code == "ru"
            ? "Обновлена левая панель блоков: её можно свернуть влево и раскрыть обратно; при изменении ширины названия не обрезаются. Исправлено раскрытие всех разделов. Каждый разъём блока расположен на контуре, настраивается и соединяется; подписи скрыты, название показывается при наведении. Значок точки останова размещается с учётом размера и положения разъёмов и не закрывает заголовок; текст заголовка также обходит разъёмы. В настройках блока можно задать ширину 196–480 и размер заголовка 7–16 с автоматической подгонкой. OCR принимает PNG, JPEG, BMP, TIFF, GIF, WebP и сканированные PDF: отдельную страницу или до 20 страниц; рендеринг выполняется локально через PDFium перед Tesseract. Пути движка и файла с кириллицей передаются через короткие имена Windows, если они доступны. Установщик Tesseract запускается с запросом прав Windows; отказ UAC показывает понятный статус."
            : "The block palette can collapse to the left and expand again; labels adapt when its width changes. All categories now expand. Every configurable connector sits on the outline and can be connected; labels are hidden and names appear on hover. The breakpoint badge avoids connectors using their size and position, and the title also avoids ports. Block settings include width 196–480 and title size 7–16 with automatic fitting. OCR accepts PNG, JPEG, BMP, TIFF, GIF, WebP and scanned PDFs: one page or up to 20 pages, rendered locally through PDFium before Tesseract. Engine and file paths with Cyrillic characters use Windows short names when available. Tesseract setup requests Windows elevation and reports a declined UAC prompt clearly.") +
        "\r\n\r\nv1.2.2\r\n" + (UiLanguage.Code == "ru"
            ? "Блок «Распознать текст» предлагает установить Tesseract OCR и только выбранные языковые модели после явного согласия; распознавание выполняется локально."
            : "The Recognize text block offers to install Tesseract OCR and only the selected language models after explicit consent; recognition stays local.") +
        "\r\n\r\nv1.2.1\r\n" + (UiLanguage.Code == "ru"
            ? "Добавлены таблицы CSV и Excel, извлечение текста PDF, очередь файлов, контрольные точки сценария и настраиваемая обработка ошибок. Локальное OCR использует Tesseract. Граф можно отправить в сообщество после просмотра кода и подтверждения в GitHub."
            : "Added CSV and Excel tables, PDF text extraction, a file queue, scenario checkpoints and configurable error handling. Local OCR uses Tesseract. Graphs can be shared with the community after reviewing their code and confirming on GitHub.") +
        "\r\n\r\nv1.2.0\r\n" + (UiLanguage.Code == "ru"
            ? "Исправлена версия в справке. Окно «О программе» закрывается системной кнопкой. Связи получили отдельные отступы от выхода и входа и отдельные радиусы изгиба; маршрут обходит блоки."
            : "Help displays the actual app version. About closes with the window control. Connections have separate output and input distances and corner radii, and route around blocks.") +
        "\r\n\r\nv1.1.9\r\n" + (UiLanguage.Code == "ru"
            ? "Добавлены запуск выбранного сценария при входе в Windows с задержкой и расписания с точностью до секунды. Для каждого задания открывается отдельный экземпляр программы. Тонкие связи легче выбрать: расширена область клика, при наведении видны подсветка и маркер. Проверка обновлений скачивает установщик с GitHub, сверяет SHA-256 и сохраняет резервную копию графа. Файлы .klb по умолчанию хранятся в Документах; доступна проверка совместимости формата."
            : "Added a selected scenario at Windows sign-in with optional delay and scheduled starts to the second. Each task opens a separate app instance. Thin links are easier to select with a wider click area, hover highlight and midpoint handle. Update check downloads the GitHub installer, verifies SHA-256 and backs up the open graph. New .klb files default to Documents; graph compatibility can be checked.") +
        "\r\n\r\nv1.1.8\r\n" + (UiLanguage.Code == "ru"
            ? "Добавлены поиск и действия с элементами Windows через UI Automation, ожидание событий окна и файла, запись семантических кликов, точки останова и пошаговая отладка. Блоки HTTP POST и «Поле JSON» расширяют работу с данными. Подпрограмма принимает входы и возвращает выходы. У точек блока задаются форма, размер и заливка, у связей — размер наконечника и отступы изгиба. Подпись «Далее» рядом с выходом скрыта."
            : "Added Windows UI Automation element search and actions, window and file event waits, semantic click recording, breakpoints and step debugging. HTTP POST and JSON field blocks extend data handling. Child scenarios accept inputs and return outputs. Block ports have configurable shape, size and fill; connections have marker size and bend distances. The Next caption beside output ports is hidden.") +
        "\r\n\r\nv1.1.7\r\n" + (UiLanguage.Code == "ru"
            ? "Отдельные установщики и переносимые сборки для Windows x64 и x86. Блоки получили форму, заливку, окантовку и настраиваемые точки на контуре. Связи обходят блоки; для них настраиваются стрелка, рисунок линии, цвет, прозрачность и толщина. Стандарт блока можно сохранить для всего типа."
            : "Separate installers and portable builds for Windows x64 and x86. Blocks now offer shape, fill, border and adjustable outline ports. Connections route around blocks; arrow, pattern, color, opacity and width can be customized. A block appearance can be saved as the default for its type.") +
        "\r\n\r\nv1.1.6\r\n" + (UiLanguage.Code == "ru"
            ? "Исправлены пустые координаты и размеры у трёх диалоговых блоков: окно теперь открывается по центру и подбирает размер под текст. В свойствах и справке показаны допустимые пределы для каждого диалога. Добавлены сайт, форум и раздел добровольной поддержки проекта с пятью QR-кодами: четыре USDT и Сбербанк."
            : "Fixed blank coordinates and dimensions in all three dialog blocks: the window now opens centered and sizes to its text. Each dialog shows its size limits in properties and Help. Added the website, forum and a voluntary support section with five QR codes: four USDT and Sberbank.") +
        "\r\n\r\nv1.1.5\r\n" + (UiLanguage.Code == "ru"
            ? "Диалоги блоков «Запросить текст», «Вопрос с кнопками» и «Выбрать вариант» получили собственный заголовок, экранные координаты X/Y, ширину и высоту. Пустые поля сохраняют стандартное размещение и размер; минимальные размеры не дают обрезать элементы. Обновлены справка и копируемые примеры."
            : "Text prompt, button question and option picker blocks now have a custom title, screen X/Y coordinates, width and height. Blank fields preserve default placement and size; minimum dimensions prevent clipped controls. Help and copyable examples are updated.") +
        "\r\n\r\nv1.1.4\r\n" + (UiLanguage.Code == "ru"
            ? "Исправлено отображение многострочных вариантов блока «Выбрать вариант» после вставки примера из справки: каждый вариант виден на отдельной строке. Код графа хранит переносы как \\n, приводит Windows \\r\\n к единому формату и сохраняет шаблонные экранирования. В свойства и справку добавлено объяснение записи переносов."
            : "Fixed multiline Choose option properties after pasting a Help example: each option appears on its own line. Graph code records line breaks as \\n, normalizes Windows \\r\\n, and preserves template escapes. Properties and Help now explain the notation.") +
        "\r\n\r\nv1.1.3\r\n" + (UiLanguage.Code == "ru"
            ? "В выражениях появились if/then/else_if/else с любым числом промежуточных веток. Краткое if условие возвращает TRUE или FALSE; невыбранные значения не вычисляются. Запись =[if ...] => ${name} одновременно возвращает результат и создаёт либо обновляет переменную. Встроенная справка и пример блока «Если выражение» обновлены."
            : "Expressions now support if/then/else_if/else with any number of intermediate branches. Short if condition returns TRUE or FALSE; inactive values are not evaluated. =[if ...] => ${name} both returns the result and creates or updates a variable. Help and the If expression block example are updated.") +
        "\r\n\r\nv1.1.2\r\n" + (UiLanguage.Code == "ru"
            ? "Окно выбора языка получило просторные кнопки и читаемый текст. Журнал переносит длинные строки по ширине окна; переключатель доступен и в отдельном окне. Справка выделяет заголовки и показывает пример кода в отдельной панели. Главное меню оформлено в едином стиле. Поля блоков поддерживают смешанный текст с вычислениями и экранированием, а варианты выбора вычисляются по строкам."
            : "The first-run language chooser has roomier buttons and readable text. The journal can wrap long entries to the window width, with a switch in either view. Help highlights headings and displays example code in a separate panel. The main menu uses a consistent visual style. Block fields support mixed text and calculations with escaping; choice options are evaluated line by line.") +
        "\r\n\r\nv1.1.1\r\n" + (UiLanguage.Code == "ru"
            ? "При первом запуске до открытия редактора предлагается выбрать русский или английский. Интерфейс и демонстрация затем открываются на выбранном языке. Выпуск содержит переносимую сборку и отдельный установщик с выбором папки, значка на рабочем столе, запуска после установки и автозапуска. Автозапуск можно изменить в меню «Сценарий»."
            : "At first launch, choose Russian or English before the editor opens. The interface and demo then use that language. This release includes a portable build and a separate installer with folder, desktop shortcut, launch-after-install and startup options. Change startup later in the Scenario menu.") +
        "\r\n\r\nv1.1.0\r\n" + (UiLanguage.Code == "ru"
            ? "Английский язык выбран при первой установке. Главное окно, меню и трей локализованы при смене языка; демонстрация пишет и говорит на языке интерфейса. Редактор и журнал открываются в отдельных окнах. Зелёная точка задаёт место вставки блока; выбранные блоки переносятся щелчком с сохранением расстояний, ПКМ сбрасывает точку и выделение."
            : "English is the first-install default. The main window, menus and tray follow the selected language; the demo types and speaks in that language. The editor and journal open in separate windows. A green point sets where blocks are inserted; selected blocks move by click while preserving spacing, and right-click clears the point and selection.") +
        "\r\n\r\nv1.0\r\n" + (UiLanguage.Code == "ru"
            ? "Первая публичная бета-версия Kliker Blocks. Визуальные графы и редактируемый код, примеры для всех блоков в справке, демонстрация «Что я умею», журнал, управление окнами, файлами и вводом. Блок «Запустить файл» позволяет выбрать установленную программу или файл. Офлайн-модели и Python загружаются отдельно только по выбору пользователя; перевод требует установленной модели или настроенного сервиса."
            : "First public beta of Kliker Blocks. Visual graphs and editable code, an example for every block in Help, the What I Can Do demo, journal, window, file and input automation. Run File can select an installed program or a file. Offline models and Python are optional downloads; translation requires an installed model or a configured service.");

    private static string Release73For(string code) => code == "en"
        ? "Each block's help page now contains a native Kliker Blocks example graph with a one-click copy button. Examples show inputs, branches and results in the journal; prerequisites are noted for external resources and Windows controls. Every example is checked for code round-tripping. Showcase controls now have room for full button labels."
        : "В справке каждого блока есть пример графа в коде Kliker Blocks и кнопка копирования. Примеры показывают входные данные, ветви и результаты в журнале; для внешних ресурсов и элементов Windows указаны условия запуска. Каждый пример проверяется на чтение редактором кода. Кнопки демонстрации больше не обрезаются.";

    private static string Release72For(string code) => code == "en"
        ? "The demo opens a uniquely named Notepad document even if Notepad is already running, verifies the active tab before typing, and leaves other documents untouched. Run file now offers a searchable installed-program list and a file browser. Historical command-import menus were removed. The displayed product name is Kliker Blocks."
        : "Демонстрация открывает собственный документ даже при уже запущенном Блокноте, проверяет вкладку перед вводом и не трогает другие документы. В блоке «Запустить файл» появились поиск установленных программ и выбор файла через Проводник. Меню старого импорта команд удалены. В интерфейсе используется название Kliker Blocks.";

    private static string Release71For(string code) => code == "en"
        ? "The demo no longer includes translation steps. The sound-signal block now calls the real Windows Beep API and falls back to normal audio playback when that API is unsupported, preventing the demo from stopping after the color search. The fallback preserves the requested tone frequency and duration."
        : "В демонстрации нет шагов перевода. Блок «Звуковой сигнал» теперь вызывает правильную функцию Windows Beep, а если она недоступна, воспроизводит тон через обычное аудиоустройство. Это предотвращает остановку демонстрации после поиска цвета; частота и длительность сигнала сохраняются.";

    private static string Release70For(string code) => code == "en"
        ? "The lightweight edition includes Russian and English UI, without bundled Python or language models. Translation blocks use an explicitly configured service or optional offline Argos models downloaded for selected language pairs after confirmation. The demo keeps the drawing tool active on retries by undoing its previous strokes; a regression test checks the retry path."
        : "Облегчённая версия содержит русский и английский интерфейс без встроенных Python и языковых моделей. Блоки перевода используют явно настроенный сервис либо офлайн-модели Argos, скачиваемые для выбранной пары после подтверждения. При повторном рисовании демонстрация отменяет прежние штрихи, сохраняя инструмент Paint; повтор закреплён регрессионным тестом.";

    private static string Release69For(string code) => code switch
    {
        "en" => "The main-window title now reads the version from the running executable instead of displaying an outdated fixed number. The About window uses the same source, and a self-test checks the version label.",
        "fr" => "Le titre de la fenêtre principale lit désormais la version du programme lancé au lieu d'afficher un ancien numéro fixe. La fenêtre À propos utilise la même source et un autotest vérifie le numéro.",
        "es" => "El título de la ventana principal obtiene la versión del programa en ejecución en vez de mostrar un número fijo antiguo. La ventana Acerca de usa la misma fuente y una autoprueba comprueba la versión.",
        "zh" => "主窗口标题现在从正在运行的程序读取版本号，不再显示过时的固定编号。“关于”窗口使用同一来源，并由自测检查版本显示。",
        "de" => "Der Titel des Hauptfensters liest die Version nun aus dem gestarteten Programm, statt eine veraltete feste Nummer anzuzeigen. Das Infofenster nutzt dieselbe Quelle; ein Selbsttest prüft die Versionsanzeige.",
        _ => "Заголовок главного окна теперь берёт номер версии из запущенной программы, а не показывает устаревшее фиксированное число. Окно «О программе» использует тот же источник; отображение версии закреплено самотестом."
    };

    private static string Release68For(string code) => code switch
    {
        "en" => "The demo types twice as slowly as v0.67. Every Notepad addition is checked against the complete expected document and corrected if characters were missed. If Paint does not expose Pencil to UI Automation, the demo tries the current tool and verifies that strokes appeared; it may draw in the current color if red cannot be selected.",
        "fr" => "La démo tape deux fois plus lentement qu'en v0.67. Chaque ajout au Bloc-notes est comparé au document attendu puis corrigé si des caractères manquent. Si Paint n'expose pas le Crayon à l'automatisation, la démo essaie l'outil actif et vérifie les traits ; elle peut utiliser la couleur actuelle si le rouge est inaccessible.",
        "es" => "La demo escribe al doble de lentitud que en v0.67. Cada adición al Bloc de notas se compara con el documento completo y se corrige si faltan caracteres. Si Paint no expone el Lápiz a la automatización, se prueba la herramienta actual y se verifican los trazos; puede usarse el color actual si no se encuentra el rojo.",
        "zh" => "演示打字速度比 v0.67 再慢一倍。每次向记事本追加文字后都会核对整个文档，并纠正遗漏字符。如果画图没有向自动化接口公开铅笔，演示会尝试当前工具并验证笔迹；无法选择红色时可使用当前颜色。",
        "de" => "Die Demo tippt halb so schnell wie in v0.67. Jeder Zusatz in Notepad wird mit dem gesamten erwarteten Dokument verglichen und bei fehlenden Zeichen korrigiert. Wenn Paint den Stift nicht für UI Automation bereitstellt, versucht die Demo das aktuelle Werkzeug und prüft die Striche; falls Rot nicht wählbar ist, nutzt sie die aktuelle Farbe.",
        _ => "Демонстрация печатает вдвое медленнее v0.67. Каждое добавление в Блокнот сверяется со всем ожидаемым документом и исправляется при пропуске символов. Если Paint не показывает «Карандаш» через UI Automation, граф пробует текущий инструмент и проверяет штрихи; при недоступном красном цвете используется текущий."
    };

    private static string Release67For(string code) => code switch
    {
        "en" => "Showcase typing is four times slower than v0.66; exact-text recovery now reselects the complete Notepad document before pasting. New text and array translation blocks default to bundled local Argos, while LibreTranslate remains an explicit network option. Checked blocks log resolved parameters and changed variable/array values; every showcase block has tracing enabled.",
        "fr" => "La saisie de la démo est quatre fois plus lente qu'en v0.66 ; la correction sélectionne de nouveau tout le document du Bloc-notes avant de coller. Les nouveaux blocs de traduction utilisent Argos local par défaut ; LibreTranslate reste une option réseau explicite. Les blocs cochés journalisent les paramètres et les valeurs modifiées, et tous les blocs de la démo sont tracés.",
        "es" => "La escritura de la demo es cuatro veces más lenta que en v0.66; la corrección vuelve a seleccionar todo el documento del Bloc de notas antes de pegar. Los bloques nuevos de traducción usan Argos local por defecto; LibreTranslate sigue siendo una opción de red explícita. Los bloques marcados registran parámetros y valores modificados, y todos los bloques de la demo generan trazas.",
        "zh" => "演示打字速度比 v0.66 慢四倍；纠错粘贴前会重新全选记事本文档。新建文本和数组翻译积木默认使用内置的本地 Argos；LibreTranslate 仍可作为明确选择的网络方案。勾选日志的积木会记录参数及变量、数组的变化；演示中所有积木均启用跟踪。",
        "de" => "Die Demo tippt viermal langsamer als v0.66; zur Korrektur wird vor dem Einfügen das gesamte Notepad-Dokument erneut markiert. Neue Text- und Array-Übersetzungsblöcke verwenden standardmäßig das lokale Argos; LibreTranslate bleibt eine ausdrücklich wählbare Netzwerkoption. Markierte Blöcke protokollieren Parameter und geänderte Werte; alle Demo-Blöcke haben Tracing aktiviert.",
        _ => "Печать демонстрации замедлена в четыре раза относительно v0.66; перед исправлением Блокнот заново выделяет весь документ. Новые блоки перевода по умолчанию используют встроенный локальный Argos, а LibreTranslate остаётся явным сетевым вариантом. Блоки с галочкой журнала записывают параметры и изменения переменных/массивов; трассировка включена для всех блоков демонстрации."
    };

    private static string Release66For(string code) => code switch
    {
        "en" => "The What I can do graph now follows a personalized dialogue: offline translation, visible mouse drawing in Paint with Yes/No feedback and retry, then four color images to rearrange and find. Typing is about five times faster. The release bundles Python and local translation models for six interface languages, including German; model quality varies by language pair.",
        "fr" => "Le graphe Ce que je sais faire suit un dialogue personnalisé : traduction hors ligne, dessin à la souris dans Paint avec réponse Oui/Non et nouvelle tentative, puis recherche parmi quatre images colorées à déplacer. La saisie est environ cinq fois plus rapide. Python et les modèles locaux pour six langues, dont l'allemand, sont inclus ; la qualité varie selon la paire.",
        "es" => "El grafo Lo que puedo hacer sigue un diálogo personalizado: traducción sin conexión, dibujo visible con el ratón en Paint y respuesta Sí/No con reintento, y búsqueda entre cuatro imágenes de colores que se pueden mover. La escritura es unas cinco veces más rápida. Se incluyen Python y modelos locales para seis idiomas, incluido el alemán; la calidad varía según el par.",
        "zh" => "“我能做什么”图表现在包含个性化对话：离线翻译、在 Paint 中用鼠标绘画并根据“是/否”重试，以及移动并查找四张彩色图片。打字速度约提高五倍。发行版内置 Python 和六种界面语言（包括德语）的本地翻译模型；不同语言对的质量有所差异。",
        "de" => "Die Demo „Was ich kann“ führt nun durch einen persönlichen Dialog: Offline-Übersetzung, sichtbares Zeichnen mit der Maus in Paint mit Ja/Nein-Rückmeldung und Wiederholung sowie die Suche unter vier verschiebbaren Farbbildern. Das Tippen ist etwa fünfmal schneller. Python und lokale Modelle für sechs Sprachen sind enthalten; die Qualität hängt vom Sprachpaar ab.",
        _ => "Граф «Что я умею» теперь ведёт персональный диалог: офлайн-перевод, видимое рисование мышью в Paint с оценкой «Да/Нет» и повтором, затем поиск среди четырёх перемещаемых цветных картинок. Печать ускорена примерно в пять раз. В выпуск встроены Python и локальные модели для шести языков, включая немецкий; качество зависит от языковой пары."
    };

    private static string Release65For(string code) => code switch
    {
        "de" => "Die Demo zeichnet mit Mausbewegungen auf einer leeren Paint-Leinwand und sucht den Stift in modernen und klassischen Oberflächen. Die Erststart-Demo wird für jede Version erneut angeboten. Dialog-Schaltflächen und ihre Ergebniscodes werden namentlich gezeigt. Gespeicherte Szenarien können 1–1000-mal mit 0,1–5× Geschwindigkeit gestartet werden; diese Funktion war in v0.65 neu.",
        "en" => "The What I can do demo now draws with mouse strokes on the blank Paint canvas, using UI automation to find the pencil in modern and classic layouts. The first-run offer resets for each version. Dialog button sets and their distinct result codes are shown by name. Added sequential saved-scenario playback with 1–1000 repetitions and 0.1–5.0× speed, plus project playback speed. Offline translation bundles and German localization are still being worked on.",
        "fr" => "La démo dessine maintenant à la souris sur le canevas vide de Paint et recherche le crayon dans les interfaces moderne et classique. La proposition initiale est réinitialisée à chaque version. Les boutons de dialogue et leurs codes de réponse distincts sont affichés par nom. Ajout de l’exécution séquentielle d’un scénario enregistré (1–1000 répétitions, 0,1–5,0×) et de la vitesse du projet. Les modèles hors ligne et l’allemand restent en chantier.",
        "es" => "La demo ahora dibuja con el ratón en el lienzo vacío de Paint y busca el lápiz en las interfaces moderna y clásica. La oferta inicial se reinicia con cada versión. Los conjuntos de botones y sus códigos de respuesta distintos se muestran por nombre. Se añadió la ejecución secuencial de escenarios guardados (1–1000 repeticiones, 0,1–5,0×) y velocidad del proyecto. Los modelos sin conexión y el alemán siguen pendientes.",
        "zh" => "演示现在会在空白画图画布上用鼠标逐笔绘制，并在新版和经典界面中查找铅笔。每个新版本会重新提供首次演示。对话框按钮组及不同的返回代码按名称显示。新增依次运行已保存场景（重复 1–1000 次、速度 0.1–5.0 倍）和项目运行速度。离线翻译模型及德语界面仍在开发中。",
        _ => "Демонстрация теперь рисует мышью на пустом холсте Paint: карандаш ищется в новом и классическом интерфейсе. Предложение первого запуска обновляется для каждой версии. Наборы кнопок диалога и отдельные коды ответов показаны по названиям. Добавлен последовательный запуск сохранённого сценария с 1–1000 повторами и скоростью 0,1–5×, а также скорость всего проекта. Офлайн-модели перевода и немецкий интерфейс ещё в работе."
    };

    private static string Release64For(string code) => code switch
    {
        "de" => "Die Demo prüft den gesamten Notepad-Text; Zeilenumbrüche duplizieren keinen Text mehr. Fehlende Übersetzungsmodelle werden ehrlich gemeldet. Pfeil und Form wurden damals als PNG gerendert und in Paint geöffnet.",
        "en" => "The demo now verifies the entire Notepad document, so wrapped lines cannot duplicate a phrase. A missing ru-to-target model is clearly reported without presenting a translation. Arrow and selected shape are rendered to PNG and opened in Paint, independent of Paint's active tool.",
        "fr" => "La démo vérifie désormais tout le document du Bloc-notes : le retour automatique à la ligne ne duplique plus la phrase. Une absence de modèle ru-vers-langue cible est signalée sans simuler une traduction. La flèche et la forme sont enregistrées en PNG puis ouvertes dans Paint, quel que soit l’outil actif.",
        "es" => "La demo verifica todo el documento de Bloc de notas: el ajuste de línea ya no duplica la frase. La falta del modelo ru-al-idioma de destino se indica sin fingir una traducción. La flecha y la figura se guardan como PNG y se abren en Paint, sin depender de la herramienta activa.",
        "zh" => "演示现验证记事本全文，自动换行不会再重复句子。缺少俄语到目标语言的模型时，会明确说明而不会伪称已翻译。箭头和所选形状先保存为 PNG 再用画图打开，不受当前工具影响。",
        _ => "Демонстрация проверяет весь документ Блокнота: перенос строки больше не дублирует фразу. Отсутствие модели ru → нужный язык обозначается явно, без имитации перевода. Стрелка и выбранная фигура сначала сохраняются в PNG и открываются в Paint независимо от выбранного инструмента."
    };

    private sealed record Copy(string Title, string Subtitle, string FeaturesTab, string ReleasesTab,
        string Close, string Help, string Introduction, string[] Capabilities, string BlocksHeading,
        string HelpPrompt, string Release63, string Release62, string Release61, string Release60, string Release59, string Release58, string Release57, string FutureReleases);

    private static Copy CopyFor(string code) => code switch
    {
        "de" => new("Über Kliker Blocks", "Visuelle Szenarien für Windows", "Funktionen", "Versionen", "Schließen", "Hilfe",
            "Diese Version enthält folgende Werkzeuge:",
            ["Visueller Blockgraph und editierbarer Kliker-Blocks-Code.",
             "Starten, pausieren und stoppen im Editor, kompakten Bedienfeld, Infobereich oder per Tastenkürzel.",
             "Maus- und Tastatursteuerung, Cursorüberwachung, Bildschirmaufnahmen sowie Bild- und Farbsuche.",
             "Makroaufzeichnung, Variablen, Arrays, Ausdrücke, Bedingungen und Schleifen.",
             "Dateien, INI, Zwischenablage, HTTP, Fenster, Dialoge, Ton, Sprachausgabe und Protokoll.",
             "Mausprofile und -ebenen, Szenarioanzeige, sechs Oberflächensprachen und mitgelieferte Offline-Übersetzungsmodelle.",
             "Interaktive Demonstration „Was ich kann“, beim ersten Start angeboten und später erneut ausführbar."],
            "Verfügbare Blöcke", "Details, Grenzen und Beispiele finden Sie in der Hilfe.",
            "Die Demonstration tippt schneller, prüft den Text und übersetzt mit den mitgelieferten lokalen Sprachmodellen.",
            "Interaktiver Demonstrationsgraph mit Notepad, Übersetzung, Sprachausgabe, Paint, Bildanalyse und Bericht.",
            "Windows-Sprachausgabe und Inventar der verfügbaren Stimmen und Computerdaten.",
            "Korrigierte Schaltflächen im Infofenster bei Windows-Anzeigeskalierung.",
            "Lokalisierte Hilfe, Protokolldatei und Block zur Windows-Sprachausgabe.",
            "Sofortiger Sprachwechsel und Unterstützung für lokale Argos-Sprachmodelle.",
            "Lokalisierter Editor, kompaktes Bedienfeld und Infobereich.",
            "Diese Liste wird in neuen Versionen erweitert; geplante Funktionen werden nicht als fertig ausgegeben."),
        "en" => new("About Kliker Blocks", "Visual scenarios for Windows", "Capabilities", "Versions", "Close", "Help",
            "This version includes the following tools:",
            ["Visual block graph and editable Kliker Blocks code; click-to-move blocks, a green insertion point, and separate editor/journal windows.",
             "Run, pause and stop from the editor, compact panel, tray or configurable hotkeys; finite or repeating runs.",
             "Mouse and keyboard automation, cursor monitoring, screen captures and image/color checks.",
             "Two macro recording modes, screenshot library, variables, arrays, expressions and branching.",
             "Files, INI, clipboard, HTTP, windows, dialogs, sound, speech synthesis and script journal.",
             "Mouse profiles and layers, on-screen scenario indicator, Russian/English UI and optional translation services or downloadable offline models.",
             "Interactive What I can do graph, offered once on first launch and replayable from the menu after consent.",
             "Scheduled and sign-in scenario starts, GitHub update checks, graph backups and compatibility checks."],
            "Available blocks", "Select Help for detailed descriptions, restrictions and examples.",
            "Fixed the demo: Notepad types at a controlled pace, verifies the copied text and corrects missed characters via clipboard. The demo, Notepad and question are laid out side by side when space permits; prompts stay visible.",
            "Added the What I can do demo graph with Notepad, optional local translation and speech, a timed Paint reminder, screenshot analysis and a report. First launch shows it once; desktop actions require consent. The graph can be saved and replayed from the menu.",
            "Russian text-to-speech now selects installed modern Windows ru voices and plays audibly; stopping interrupts playback. Added a local computer/voice inventory with manual refresh.",
            "Fixed clipped buttons in the About window at Windows display scaling.",
            "Offline FR/ES/ZH block help and property text, reliable journal path display and file creation, and Windows text-to-speech block. Added this localized About window.",
            "Immediate interface language switching, Argos package setup, and translated block names.",
            "Localized main editor, compact panel, tray and initial interface messages.",
            "This list is extended when a new version is released; planned features are not presented as available."),
        "fr" => new("À propos de Kliker Blocks", "Scénarios visuels pour Windows", "Fonctionnalités", "Versions", "Fermer", "Aide",
            "Cette version comprend les outils suivants :",
            ["Graphe visuel de blocs et code Kliker Blocks modifiable.",
             "Exécution, pause et arrêt depuis l’éditeur, le panneau compact, la zone de notification ou des raccourcis configurables.",
             "Automatisation de la souris et du clavier, suivi du curseur, captures d’écran et vérifications d’images/couleurs.",
             "Deux modes d’enregistrement de macros, bibliothèque de captures, variables, tableaux, expressions et branches.",
             "Fichiers, INI, presse-papiers, HTTP, fenêtres, boîtes de dialogue, sons, synthèse vocale et journal.",
             "Profils et couches de souris, indicateur à l’écran, six langues d'interface et modèles de traduction hors ligne inclus.",
             "Graphe interactif Ce que je sais faire, proposé une fois au premier lancement et réutilisable avec accord."],
            "Blocs disponibles", "Consultez l’aide pour les descriptions détaillées, les limites et les exemples.",
            "Démo corrigée : le Bloc-notes saisit plus lentement, vérifie le texte copié et corrige les caractères manquants par le presse-papiers. La démo et ses fenêtres se placent côte à côte si l’écran le permet ; les questions restent visibles.",
            "Nouveau graphe Ce que je sais faire : Bloc-notes, traduction locale et voix si disponibles, rappel Paint minuté, capture et rapport. Il apparaît une seule fois au premier lancement ; toute action sur le bureau demande votre accord. Le graphe peut être enregistré et relancé depuis le menu.",
            "La synthèse vocale russe choisit désormais une voix moderne Windows ru et la lecture est audible ; l’arrêt interrompt le son. Inventaire local de l’ordinateur et des voix, actualisable manuellement.",
            "Correction des boutons coupés dans la fenêtre À propos avec la mise à l’échelle Windows.",
            "Aide hors ligne FR/ES/ZH pour les blocs, chemin et création du journal fiables, bloc de synthèse vocale Windows et fenêtre À propos localisée.",
            "Changement immédiat de langue, installation des modèles Argos et noms de blocs traduits.",
            "Éditeur principal, panneau compact, zone de notification et messages initiaux localisés.",
            "Cette liste est complétée à chaque nouvelle version ; les fonctions prévues ne sont pas présentées comme disponibles."),
        "es" => new("Acerca de Kliker Blocks", "Escenarios visuales para Windows", "Funciones", "Versiones", "Cerrar", "Ayuda",
            "Esta versión incluye las siguientes herramientas:",
            ["Grafo visual de bloques y código Kliker Blocks editable.",
             "Ejecutar, pausar y detener desde el editor, panel compacto, bandeja o atajos configurables.",
             "Automatización de ratón y teclado, seguimiento del cursor, capturas y comprobaciones de imagen/color.",
             "Dos modos de grabación de macros, biblioteca de capturas, variables, matrices, expresiones y ramas.",
             "Archivos, INI, portapapeles, HTTP, ventanas, diálogos, sonido, síntesis de voz y registro.",
             "Perfiles y capas del ratón, indicador en pantalla, seis idiomas de interfaz y modelos de traducción sin conexión incluidos.",
             "Grafo interactivo Lo que puedo hacer, ofrecido una vez al inicio y repetible con permiso."],
            "Bloques disponibles", "Consulte Ayuda para descripciones detalladas, límites y ejemplos.",
            "Demo corregida: Bloc de notas escribe más despacio, verifica el texto copiado y corrige caracteres omitidos mediante el portapapeles. La demo y sus ventanas se colocan lado a lado si hay espacio; las preguntas permanecen visibles.",
            "Nuevo grafo Lo que puedo hacer: Bloc de notas, traducción local y voz si están disponibles, recordatorio temporizado en Paint, captura e informe. Se muestra una vez en el primer inicio; las acciones en el escritorio requieren permiso. Se puede guardar y repetir desde el menú.",
            "La voz rusa elige ahora una voz moderna ru de Windows y se oye; detener interrumpe la reproducción. Se añadió un inventario local del equipo y sus voces, con actualización manual.",
            "Se corrigieron los botones recortados de Acerca de con la escala de pantalla de Windows.",
            "Ayuda sin conexión FR/ES/ZH para bloques, visualización de ruta y creación del registro, bloque de voz Windows y ventana Acerca de localizada.",
            "Cambio inmediato de idioma, instalación de modelos Argos y nombres de bloques traducidos.",
            "Editor principal, panel compacto, bandeja y mensajes iniciales localizados.",
            "Esta lista se amplía con cada versión; las funciones previstas no se presentan como disponibles."),
        "zh" => new("关于 Kliker Blocks", "Windows 可视化脚本", "功能", "版本", "关闭", "帮助",
            "此版本包含以下工具：",
            ["可视化积木图和可编辑的 Kliker Blocks 代码。",
             "可从编辑器、简洁控制面板、托盘或自定义热键启动、暂停和停止脚本。",
             "鼠标键盘自动化、光标跟踪、屏幕截图以及图像和颜色检测。",
             "两种宏录制模式、截图库、变量、数组、表达式和分支。",
             "文件、INI、剪贴板、HTTP、窗口、对话框、声音、语音合成和日志。",
             "鼠标配置和层、屏幕状态指示器、六种界面语言及内置离线翻译模型。",
             "首次启动仅提示一次的互动“我的功能”图表；获同意后可从菜单重播。"],
            "可用积木", "详细说明、限制和示例请参阅帮助。",
            "修复演示：记事本以适当速度输入，并核对复制的文字；若有漏字则通过剪贴板修正。屏幕空间允许时，演示窗口与记事本并排，问题对话框始终可见。",
            "新增“我的功能”演示图表：记事本、可选本地翻译与语音、定时画图提醒、截图分析及报告。首次启动只显示一次；桌面操作需先征得同意。可从菜单重播并保存图表。",
            "俄语朗读现在会选择已安装的现代 Windows ru 语音并实际发声；停止场景可中断播放。新增可手动刷新的本机计算机与语音清单。",
            "修复 Windows 显示缩放时“关于”窗口底部按钮被裁切的问题。",
            "新增 FR/ES/ZH 离线积木帮助、日志路径显示与文件创建、Windows 文字朗读积木及本地化的关于窗口。",
            "即时切换界面语言、Argos 模型安装和积木名称翻译。",
            "主编辑器、简洁控制面板、托盘及初始消息的本地化。",
            "每次发布新版本时扩充此列表；尚未实现的功能不会标为可用。"),
        _ => new("О программе Kliker Blocks", "Визуальные сценарии для Windows", "Возможности", "Версии", "Закрыть", "Справка",
            "В этой версии доступны:",
            ["Визуальный граф блоков и редактируемый код Kliker Blocks; перенос блоков щелчком, зелёная точка вставки и отдельные окна редактора и журнала.",
             "Запуск, пауза и остановка из редактора, компактной панели, трея или назначенными горячими клавишами; конечный и циклический режимы.",
             "Действия мыши и клавиатуры, слежение за курсором, снимки экрана, проверка изображения и цвета.",
             "Два режима записи макросов, библиотека снимков, переменные, массивы, выражения и ветвления.",
             "Файлы, INI, буфер обмена, HTTP, окна, диалоги, звук, озвучивание текста и журнал выполнения.",
             "Профили и слои мыши, экранный индикатор, русский/английский интерфейс и перевод через сервис либо отдельно загружаемые офлайн-модели.",
             "Интерактивный граф «Что я умею»: предложение один раз при первом запуске и повторный просмотр из меню.",
             "Запуск сценариев при входе в Windows и по расписанию, проверка обновлений GitHub, резервные копии и проверка совместимости графов."],
            "Доступные блоки", "Подробные описания, ограничения и примеры — в справке.",
            "Исправлена демонстрация: Блокнот печатает с контролируемой скоростью, сверяет скопированный текст и исправляет пропущенные символы через буфер обмена. Окна располагаются рядом, когда хватает места; вопрос остаётся видимым.",
            "Добавлен граф «Что я умею»: Блокнот, необязательный локальный перевод и голос, напоминание в Paint по таймеру, снимок и отчёт. При первом запуске показывается один раз; действия с рабочим столом требуют согласия. Граф можно сохранить и повторно открыть из меню.",
            "Русская речь теперь озвучивается современными голосами Windows ru; остановка прерывает воспроизведение. Добавлен локальный список возможностей компьютера и голосов с обновлением вручную.",
            "Исправлено обрезание кнопок окна «О программе» при масштабировании Windows.",
            "Встроенная справка по блокам на FR/ES/ZH, исправлен путь и создание файла журнала, добавлен блок озвучивания средствами Windows и это окно «О программе».",
            "Мгновенная смена языка интерфейса, установка пакетов Argos и перевод названий блоков.",
            "Локализованы основной редактор, компактная панель, трей и начальные сообщения.",
            "При выпуске следующей версии список пополняется; запланированные возможности не выдаются за готовые.")
    };
}

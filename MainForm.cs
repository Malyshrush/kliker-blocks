using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Diagnostics;

namespace KlikerBlocks;

internal sealed record LanguageSession(ProjectModel Project, string? File, bool CodeView,
    string CodeText, bool CodeDirty, float Zoom, PointF Pan, string[] LogLines);

internal sealed class MainForm : Form
{
    private sealed record LocalizedOption(string Value, string? Label = null)
    {
        public override string ToString() => Label ?? UiLanguage.T(Value);
    }
    internal static string AppTitle => AppIdentity.Title;
    private ProjectModel _project = ProjectModel.CreateSample();
    private readonly GraphCanvas _graph = new();
    private Panel _editorHost = null!;
    private readonly Panel _codeHost = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly RichTextBox _graphCode = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5f),
        AcceptsTab = true, WordWrap = false, DetectUrls = false, MaxLength = 10 * 1024 * 1024 };
    private readonly ToolStripLabel _codeStatus = new() { Text = "Код Kliker Blocks: блоки, значения и связи" };
    private ToolStripButton _visualModeButton = null!;
    private ToolStripButton _codeModeButton = null!;
    private ToolStripButton _applyCodeButton = null!;
    private ToolStripButton _revertCodeButton = null!;
    private bool _codeViewActive;
    private bool _codeDirty;
    private bool _loadingGraphCode;
    private Form? _editorWindow;
    private Form? _journalWindow;
    private CompactControlForm? _controller;
    private NotifyIcon? _trayIcon;
    private readonly Dictionary<string, Icon> _trayStateIcons = [];
    private ContextMenuStrip? _trayMenu;
    private ToolStripMenuItem? _trayStatusItem;
    private ToolStripMenuItem? _trayStartItem;
    private ToolStripMenuItem? _trayPauseItem;
    private ToolStripMenuItem? _trayStopItem;
    private bool _exitRequested;
    private readonly bool _uiPreview;
    private readonly ScenarioLaunchRequest? _launch;
    public LanguageSession? PendingLanguageSession { get; private set; }
    private Runner? _runner;
    private DebugMonitorForm? _debugWindow;
    private MouseMappingRuntime? _mouseMappings;
    private ToolStripMenuItem? _mouseEnabledItem;
    private ToolStripMenuItem? _mouseLayersItem;
    private ToolStripMenuItem? _mouseTrayItem;
    private List<BlockNode> _blockClipboard = [];
    private readonly FlowLayoutPanel _fields = new();
    private CancellationTokenSource? _propertyHelpCancel;
    private readonly ListBox _log = new();
    private readonly CheckBox _journalWrapMain = new();
    private CheckBox? _journalWrapDetached;
    private bool _journalWrapEnabled;
    private readonly System.Windows.Forms.Timer _journalResizeTimer = new() { Interval = 130 };
    private ScriptLogWriter? _journalWriter;
    private readonly WindowsMessageInbox _messageInbox = new();
    private TableLayoutPanel _rootLayout = null!;
    private readonly Label _status = new();
    private readonly Label _inspector = new();
    private readonly Label _showcaseStatus = new();
    private ShowcaseForm? _showcaseWindow;
    internal bool IsScenarioRunning => _cancel is not null;
    private readonly System.Windows.Forms.Timer _inspectorTimer = new() { Interval = 250 };
    private readonly Label _selection = new();
    private readonly ToolStripMenuItem _runButton = new("▶ Запустить  Ctrl+Alt+F6");
    private readonly ToolStripMenuItem _stopButton = new("■ Остановить  Ctrl+Alt+F7") { Enabled = false };
    private readonly ToolStripMenuItem _pauseButton = new("⏸ Пауза") { Enabled = false };
    private readonly ToolStripMenuItem _debugStepButton = new("Следующий блок") { Enabled = false };
    private readonly ToolStripMenuItem _showIndicatorItem = new("Индикатор поверх окон") { CheckOnClick = true };
    private readonly ToolStripMenuItem _showLogItem = new("Показать журнал") { Checked = true };
    private readonly ToolStripMenuItem _runModeItem = new("Режим выполнения: один проход…");
    private readonly ToolStripMenuItem _autostartItem = new("Запускать вместе с Windows");
    private readonly ToolStripMenuItem _updateItem = new("Проверить обновления…");
    private readonly ToolStripMenuItem _updateBadge = new() { Visible = false, Tag = "featured" };
    private AvailableUpdate? _availableUpdate;
    private CancellationTokenSource? _cancel;
    private BlockNode? _selected;
    private string? _file;
    private bool _hotkeysRegistered;
    private bool _coordinateHotkeyRegistered;
    private bool _screenshotHotkeyRegistered;
    private bool _indicatorHotkeyRegistered;
    private bool _indicatorHiddenByHotkey;
    private AppSettings _settings = AppSettings.Load();
    private Guid? _pendingRegionNode;
    private bool _captureActive;
    private MacroRecorder? _recorder;
    private RecordingBar? _recordingBar;
    private bool _stoppingRecording;
    private StopHotkeyFallback? _stopHotkeyFallback;
    private ScenarioIndicatorForm? _indicator;
    private readonly Dictionary<Guid, CursorWatchState> _watchStates = [];
    private readonly Dictionary<Guid, bool> _conditionStates = [];
    private readonly Dictionary<Guid, string> _conditionLabels = [];
    private int _runVersion;

    public MainForm(LanguageSession? session = null, bool uiPreview = false,
        ScenarioLaunchRequest? launch = null)
    {
        _launch = launch;
        _uiPreview = uiPreview;
        if (session is not null) { _project = session.Project; _file = session.File; }
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text = AppTitle + " — " + UiLanguage.T("визуальные сценарии");
        Width = 1450;
        Height = 900;
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 246, 249);
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;
        KeyDown += HandleEditorKeys;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        _rootLayout = root;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        Controls.Add(root);

        var menu = new MenuStrip { Dock = DockStyle.Fill, BackColor = Color.FromArgb(247, 249, 252),
            GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 4, 8, 4),
            Renderer = new KlikerMenuRenderer() };
        MainMenuStrip = menu;
        root.Controls.Add(menu, 0, 0);
        var fileMenu = new ToolStripMenuItem("Файл");
        AddMenuItem(fileMenu, "Новый", (_, _) => NewProject());
        AddMenuItem(fileMenu, "Открыть…", (_, _) => OpenProject());
        AddMenuItem(fileMenu, "Сохранить…", (_, _) => SaveProject());
        AddMenuItem(fileMenu, "Поделиться графом…", (_, _) => ShareProject());
        AddMenuItem(fileMenu, "Проверить совместимость графа…", (_, _) => CheckProjectCompatibility());
        var editorMenu = new ToolStripMenuItem("Редактор");
        AddMenuItem(editorMenu, "Переключить блоки / код Kliker Blocks", (_, _) => SwitchEditorView(!_codeViewActive));
        AddMenuItem(editorMenu, "Копировать блоки  Ctrl+C", (_, _) => CopyBlocks());
        AddMenuItem(editorMenu, "Вставить блоки у курсора  Ctrl+V", (_, _) => PasteBlocks());
        AddMenuItem(editorMenu, "Удалить выбранное  Delete", (_, _) => DeleteSelected());
        AddMenuItem(editorMenu, "Открыть редактор в отдельном окне", (_, _) => ShowEditorWindow());
        AddMenuItem(editorMenu, "Увеличить масштаб  Ctrl+колесо", (_, _) => _graph.Zoom *= 1.25f);
        AddMenuItem(editorMenu, "Уменьшить масштаб  Ctrl+колесо", (_, _) => _graph.Zoom /= 1.25f);
        AddMenuItem(editorMenu, "Масштаб 100%", (_, _) => _graph.Zoom = 1f);
        AddMenuItem(editorMenu, "Сбросить положение поля", (_, _) => _graph.ResetView());
        var captureMenu = new ToolStripMenuItem("Захват");
        AddMenuItem(captureMenu, "Сохранить точку", (_, _) => CapturePoint());
        AddMenuItem(captureMenu, "Снимок области…", async (_, _) => await CaptureScreenAsync());
        AddMenuItem(captureMenu, "Библиотека…", (_, _) => ShowLibrary());
        AddMenuItem(captureMenu, "Горячие клавиши…", (_, _) => ConfigureHotkeys());
        var macroMenu = new ToolStripMenuItem("Макрос");
        AddMenuItem(macroMenu, "Начать запись…", async (_, _) => await StartRecordingAsync());
        AddMenuItem(macroMenu, "Остановить запись  Ctrl+Alt+F10", (_, _) => StopRecording());
        var scenarioMenu = new ToolStripMenuItem("Сценарий");
        _runButton.Click += async (_, _) => await RunProject();
        _stopButton.Click += (_, _) => StopScenario();
        _pauseButton.Click += (_, _) => TogglePauseScenario();
        scenarioMenu.DropDownItems.Add(_runButton);
        scenarioMenu.DropDownItems.Add(_pauseButton);
        _debugStepButton.Click += (_, _) => StepScenario();
        scenarioMenu.DropDownItems.Add(_debugStepButton);
        scenarioMenu.DropDownItems.Add(_stopButton);
        AddMenuItem(scenarioMenu, "Показать компактную панель", (_, _) => ShowCompactControl());
        _runModeItem.Click += (_, _) => ConfigureRunMode();
        scenarioMenu.DropDownItems.Add(_runModeItem);
        AddMenuItem(scenarioMenu, "Имя сценария…", (_, _) => ConfigureScriptName());
        AddMenuItem(scenarioMenu, "Продолжить с контрольной точки…", async (_, _) => await ResumeFromCheckpoint());
        AddMenuItem(scenarioMenu, "Привязка к окну…", (_, _) => ConfigureWindowScope());
        _showIndicatorItem.Checked = _project.ShowIndicator;
        _showIndicatorItem.CheckedChanged += (_, _) =>
        {
            _project.ShowIndicator = _showIndicatorItem.Checked;
            AppendLog("Индикатор " + (_showIndicatorItem.Checked ? "включён" : "выключен") + ". Сохраните проект для следующего запуска.");
            if (_cancel != null)
            {
                if (_showIndicatorItem.Checked) ShowIndicator();
                else CloseIndicator();
            }
        };
        scenarioMenu.DropDownItems.Add(_showIndicatorItem);
        _showLogItem.Click += (_, _) => SetLogVisible(!_showLogItem.Checked);
        scenarioMenu.DropDownItems.Add(_showLogItem);
        AddMenuItem(scenarioMenu, "Запись журнала в текстовый файл…", (_, _) => ConfigureJournal());
        AddMenuItem(scenarioMenu, "Положение индикатора…", (_, _) => ConfigureIndicatorPosition());
        AddMenuItem(scenarioMenu, "Оформление и клавиша индикатора…", (_, _) => ConfigureIndicatorAppearance());
        try { _autostartItem.Checked = AutostartSettings.IsEnabled(); }
        catch { _autostartItem.Checked = false; }
        _autostartItem.Click += (_, _) => ToggleAutostart();
        scenarioMenu.DropDownItems.Add(new ToolStripSeparator());
        scenarioMenu.DropDownItems.Add(_autostartItem);
        AddMenuItem(scenarioMenu, "Автозапуск и расписание сценариев…", (_, _) => ConfigureScenarioAutomation());
        var mouseMenu = new ToolStripMenuItem("Назначения мыши");
        AddMenuItem(mouseMenu, "Профили, слои и кнопки…", (_, _) => ConfigureMouseMappings());
        _mouseEnabledItem = new ToolStripMenuItem("Включить назначения") { CheckOnClick = true };
        _mouseEnabledItem.Click += (_, _) => ToggleMouseMappings();
        mouseMenu.DropDownItems.Add(_mouseEnabledItem);
        _mouseLayersItem = new ToolStripMenuItem("Активный слой");
        _mouseLayersItem.DropDownOpening += (_, _) => RefreshMouseLayersMenu(_mouseLayersItem);
        mouseMenu.DropDownItems.Add(_mouseLayersItem);
        var languagesMenu = new ToolStripMenuItem("Языки");
        AddMenuItem(languagesMenu, "Язык интерфейса и модели перевода…", (_, _) => ConfigureLanguages());
        AddMenuItem(languagesMenu, "Возможности компьютера и голоса…", (_, _) => new ComputerCapabilitiesForm().Show(this));
        AddMenuItem(languagesMenu, "Проверить имена переменных проекта…", (_, _) => CheckVariableNames());
        var blocksMenu = new ToolStripMenuItem("Блоки");
        foreach (var group in BlockCatalog.All.Where(s => s.Kind != "ShowcaseStep")
                     .GroupBy(s => CategoryFor(s.Kind)).OrderBy(g => CategoryOrder(g.Key)))
        {
            var category = new ToolStripMenuItem(group.Key);
            foreach (var spec in group)
                AddMenuItem(category, spec.Title, (_, _) => AddBlock(spec.Kind));
            blocksMenu.DropDownItems.Add(category);
        }
        var helpMenu = new ToolStripMenuItem("Справка");
        AddMenuItem(helpMenu, "Справка Kliker Blocks и коды клавиш…", (_, _) => new HelpForm(_selected?.Kind).Show(this));
        AddMenuItem(helpMenu, "Полная таблица кодов клавиш VK…", (_, _) => new VirtualKeyHelpForm().Show(this));
        AddMenuItem(helpMenu, "Сайт и сообщество Kliker Blocks…", (_, _) => WebsiteLinks.Open(this));
        _updateItem.Click += async (_, _) => await CheckForUpdates(true);
        helpMenu.DropDownItems.Add(_updateItem);
        _updateBadge.Click += async (_, _) => await CheckForUpdates(true);
        AddMenuItem(helpMenu, "Поддержать проект", (_, _) => new SupportForm().Show(this));
        AddMenuItem(helpMenu, "О программе…", (_, _) => new AboutForm().Show(this));
        var showcaseMenu = new ToolStripMenuItem(UiLanguage.T("Что я умею")) { Tag = "featured" };
        showcaseMenu.Click += (_, _) => ShowShowcase();
        var websiteMenu = new ToolStripMenuItem(UiLanguage.T("Сайт")) { Tag = "featured" };
        websiteMenu.Click += (_, _) => WebsiteLinks.Open(this);
        var supportMenu = new ToolStripMenuItem(UiLanguage.T("Поддержать проект"));
        supportMenu.Click += (_, _) => new SupportForm().Show(this);
        menu.Items.AddRange([fileMenu, editorMenu, blocksMenu, captureMenu, macroMenu, scenarioMenu, mouseMenu, languagesMenu, showcaseMenu, websiteMenu, supportMenu, _updateBadge, helpMenu]);
        foreach (ToolStripMenuItem item in menu.Items)
        {
            item.Padding = new Padding(9, 5, 9, 5);
            item.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            item.ForeColor = Color.FromArgb(29, 51, 77);
        }
        var information = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(232, 237, 244) };
        root.Controls.Add(information, 0, 1);
        _status.AutoSize = true;
        _status.Margin = new Padding(10, 5, 20, 0);
        _status.Text = UiLanguage.T("Готово. Соедините выход блока с входом следующего.");
        information.Controls.Add(_status);
        _showcaseStatus.AutoSize = true;
        _showcaseStatus.Margin = new Padding(18, 5, 0, 0);
        information.Controls.Add(_showcaseStatus);
        _inspector.AutoSize = true;
        _inspector.Margin = new Padding(10, 5, 0, 0);
        information.Controls.Add(_inspector);
        UpdateShowcaseStatus();
        _inspectorTimer.Tick += (_, _) => UpdateInspector();
        if (!_uiPreview) _inspectorTimer.Start();

        var main = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 320, SplitterWidth = 6 };
        root.Controls.Add(main, 0, 2);
        var palette = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(12, 10, 12, 12), BackColor = Color.FromArgb(241, 245, 250)
        };
        main.Panel1.Controls.Add(palette);
        var paletteTitle = new Label { Text = UiLanguage.T("БЛОКИ ПО РАЗДЕЛАМ"), Width = 260, Height = 30,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold), ForeColor = Color.FromArgb(43, 61, 83),
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 5) };
        palette.Controls.Add(paletteTitle);
        var search = new TextBox { PlaceholderText = UiLanguage.T("Поиск блока…"), BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White, ForeColor = Color.FromArgb(31, 45, 63), Height = 32, Margin = new Padding(0, 0, 0, 7) };
        palette.Controls.Add(search);
        var paletteHint = new Label
        {
            Text = UiLanguage.T("Щёлкните пустое поле для вставки или переноса выделенных блоков. ПКМ сбрасывает точку и выделение. Без точки новые блоки размещаются по порядку."),
            Width = 260, Height = 155, ForeColor = Color.FromArgb(87, 103, 123), Margin = new Padding(0, 14, 0, 0)
        };
        var groups = new List<(FlowLayoutPanel Container, PaletteSectionHeader Header, string Name,
            List<(PaletteBlockButton Button, BlockSpec Spec)> Buttons)>();
        var expandedGroups = new HashSet<FlowLayoutPanel>();
        int PaletteItemWidth() => Math.Max(220,
            palette.ClientSize.Width - palette.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2);
        void ResizePaletteItems()
        {
            var width = PaletteItemWidth();
            paletteTitle.Width = width;
            search.Width = width;
            paletteHint.Width = width;
            foreach (var (container, header, _, buttons) in groups)
            {
                container.Width = width;
                header.Width = width;
                foreach (var (button, _) in buttons) button.Width = width;
            }
        }
        void RefreshPalette()
        {
            var query = search.Text.Trim();
            foreach (var (container, header, name, buttons) in groups)
            {
                var expanded = query.Length > 0 || expandedGroups.Contains(container);
                var visibleCount = 0;
                foreach (var (button, spec) in buttons)
                {
                    var showButton = expanded && (query.Length == 0 || spec.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                        UiLanguage.T(spec.Title).Contains(query, StringComparison.CurrentCultureIgnoreCase));
                    button.Visible = showButton;
                    if (showButton) visibleCount++;
                }
                container.Visible = query.Length == 0 || visibleCount > 0;
                container.Height = header.Height + visibleCount * 44;
                header.SetExpanded(expanded);
                header.SetTitle(UiLanguage.T(name));
            }
            ResizePaletteItems();
        }
        foreach (var group in BlockCatalog.All.Where(s => s.Kind != "ShowcaseStep")
                     .GroupBy(s => CategoryFor(s.Kind)).OrderBy(g => CategoryOrder(g.Key)))
        {
            var container = new FlowLayoutPanel { Width = 260, Height = 34, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = false, Margin = new Padding(0, 3, 0, 2), Padding = Padding.Empty };
            var header = new PaletteSectionHeader(UiLanguage.T(group.Key)) { Width = 260, Height = 34,
                Margin = new Padding(0, 0, 0, 2) };
            container.Controls.Add(header);
            var buttons = new List<(PaletteBlockButton, BlockSpec)>();
            foreach (var spec in group)
            {
                var button = new PaletteBlockButton { Text = UiLanguage.T(spec.Title), Width = 260, Height = 39,
                    Margin = new Padding(2, 1, 2, 4), Visible = false };
                button.Click += (_, _) => AddBlock(spec.Kind);
                container.Controls.Add(button);
                buttons.Add((button, spec));
            }
            void ToggleGroup(object? _, EventArgs __)
            {
                if (!expandedGroups.Add(container)) expandedGroups.Remove(container);
                RefreshPalette();
            }
            header.Clicked += ToggleGroup;
            groups.Add((container, header, group.Key, buttons));
            palette.Controls.Add(container);
        }
        if (groups.Count > 0) expandedGroups.Add(groups[0].Container);
        RefreshPalette();
        search.TextChanged += (_, _) => RefreshPalette();
        palette.Controls.Add(paletteHint);
        palette.ClientSizeChanged += (_, _) => ResizePaletteItems();
        ResizePaletteItems();

        var content = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, SplitterDistance = 880 };
        main.Panel2.Controls.Add(content);
        Shown += (_, _) =>
        {
            main.Panel1MinSize = 300;
            main.Panel2MinSize = 450;
            main.SplitterDistance = Math.Clamp(320, main.Panel1MinSize, main.Width - main.Panel2MinSize - main.SplitterWidth);
            content.SplitterDistance = Math.Max(250, content.Width - 330);
            ResizePaletteItems();
        };
        var editorSurface = new Panel { Dock = DockStyle.Fill };
        content.Panel1.Controls.Add(editorSurface);
        var editorBody = new Panel { Dock = DockStyle.Fill };
        editorSurface.Controls.Add(editorBody);
        var modeBar = new ToolStrip { Dock = DockStyle.Top, Height = 39, AutoSize = false,
            CanOverflow = true, GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(232, 237, 244) };
        editorSurface.Controls.Add(modeBar);
        var togglePalette = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text,
            Padding = new Padding(7, 2, 7, 2), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        void RefreshPaletteToggle()
        {
            togglePalette.Text = UiLanguage.T(main.Panel1Collapsed ? "▶ Показать блоки" : "◀ Скрыть блоки");
            togglePalette.ToolTipText = UiLanguage.T(main.Panel1Collapsed
                ? "Открыть список блоков слева" : "Свернуть список блоков влево");
        }
        togglePalette.Click += (_, _) =>
        {
            main.Panel1Collapsed = !main.Panel1Collapsed;
            RefreshPaletteToggle();
        };
        RefreshPaletteToggle();
        _visualModeButton = new ToolStripButton(UiLanguage.T("▦ Блоки")) { Checked = true };
        _codeModeButton = new ToolStripButton(UiLanguage.T("≡ Код Kliker Blocks"));
        _applyCodeButton = new ToolStripButton(UiLanguage.T("Применить код")) { Enabled = false };
        _revertCodeButton = new ToolStripButton(UiLanguage.T("Отменить правки")) { Enabled = false };
        _visualModeButton.Click += (_, _) => SwitchEditorView(false);
        _codeModeButton.Click += (_, _) => SwitchEditorView(true);
        _applyCodeButton.Click += (_, _) => TryApplyGraphCode();
        _revertCodeButton.Click += (_, _) => LoadGraphCode();
        modeBar.Items.AddRange([togglePalette, new ToolStripSeparator(), _visualModeButton, _codeModeButton, _applyCodeButton,
            _revertCodeButton, new ToolStripSeparator(), _codeStatus]);
        var shareButton = new ToolStripButton(UiLanguage.T("Поделиться графом…"));
        shareButton.Click += (_, _) => ShareProject();
        modeBar.Items.Add(new ToolStripSeparator());
        modeBar.Items.Add(shareButton);
        var editorPopout = new Button
        {
            Text = "↗", Width = 28, Height = 28, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Symbol", 12f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, TabStop = false
        };
        editorPopout.AccessibleName = UiLanguage.T("Открыть редактор в отдельном окне");
        new ToolTip().SetToolTip(editorPopout, UiLanguage.T("Открыть редактор в отдельном окне"));
        editorPopout.Click += (_, _) => ShowEditorWindow();
        editorSurface.Controls.Add(editorPopout);
        void PositionEditorPopout() => editorPopout.Location =
            new Point(Math.Max(0, editorSurface.ClientSize.Width - editorPopout.Width - 12), modeBar.Height + 8);
        editorSurface.Resize += (_, _) => PositionEditorPopout();
        PositionEditorPopout();
        editorPopout.BringToFront();
        _editorHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(247, 249, 252) };
        editorBody.Controls.Add(_editorHost);
        _codeHost.Controls.Add(_graphCode);
        editorBody.Controls.Add(_codeHost);
        _graphCode.TextChanged += (_, _) =>
        {
            if (_loadingGraphCode) return;
            _codeDirty = true;
            _codeStatus.Text = UiLanguage.T("Код изменён — нажмите «Применить код»");
            _applyCodeButton.Enabled = true;
            _revertCodeButton.Enabled = true;
        };
        _graph.SetWorldSize(2200, 1500);
        _graph.Project = _project;
        _graph.SelectionChanged += node => ShowProperties(node);
        _graph.SelectionSetChanged += nodes => { if (nodes.Count > 1) ShowGroupProperties(nodes); };
        _graph.LinkSelectionChanged += ShowLinkProperties;
        _graph.ContentResized += ExpandCanvas;
        _graph.ZoomChanged += zoom => _status.Text = UiLanguage.Format("Масштаб: {0:P0}", zoom);
        _graph.Message += message => _status.Text = UiLanguage.T(message);
        _editorHost.Controls.Add(_graph);

        var propertyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, RowCount = 2, ColumnCount = 1 };
        propertyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        propertyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Panel2.Controls.Add(propertyPanel);
        _selection.Dock = DockStyle.Fill;
        _selection.Padding = new Padding(12, 12, 0, 0);
        _selection.Font = new Font(Font, FontStyle.Bold);
        propertyPanel.Controls.Add(_selection, 0, 0);
        _fields.Dock = DockStyle.Fill;
        _fields.AutoScroll = true;
        _fields.FlowDirection = FlowDirection.TopDown;
        _fields.WrapContents = false;
        _fields.Padding = new Padding(12, 8, 8, 8);
        propertyPanel.Controls.Add(_fields, 0, 1);
        ShowProperties(null);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 6), BackColor = Color.White, RowCount = 2, ColumnCount = 1 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(logPanel, 0, 3);
        var logHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        var logTitle = new Label { Text = UiLanguage.T("Журнал выполнения"), Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var journalPopout = new Button { Text = "↗", Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat, TabStop = false };
        journalPopout.AccessibleName = UiLanguage.T("Открыть журнал в отдельном окне");
        journalPopout.Click += (_, _) => ShowJournalWindow();
        new ToolTip().SetToolTip(journalPopout, UiLanguage.T("Открыть журнал в отдельном окне"));
        logHeader.Controls.Add(logTitle, 0, 0);
        logHeader.Controls.Add(journalPopout, 2, 0);
        _journalWrapMain.Text = UiLanguage.T("Перенос строк");
        _journalWrapMain.Dock = DockStyle.Fill;
        _journalWrapMain.Checked = _settings.JournalWordWrap;
        _journalWrapMain.CheckedChanged += (_, _) => SetJournalWrap(_journalWrapMain.Checked);
        logHeader.Controls.Add(_journalWrapMain, 1, 0);
        logPanel.Controls.Add(logHeader, 0, 0);
        _log.Dock = DockStyle.Fill;
        _log.Font = new Font("Consolas", 9);
        _log.DrawMode = DrawMode.OwnerDrawVariable;
        _log.MeasureItem += MeasureJournalItem;
        _log.DrawItem += DrawJournalItem;
        _journalWrapEnabled = _settings.JournalWordWrap;
        _log.SizeChanged += (_, _) => { if (_journalWrapEnabled) { _journalResizeTimer.Stop(); _journalResizeTimer.Start(); } };
        _journalResizeTimer.Tick += (_, _) => { _journalResizeTimer.Stop(); RefreshJournalLayout(); };
        logPanel.Controls.Add(_log, 0, 1);
        if (!_uiPreview) InitializeTray();
        UiLanguage.ApplyTo(this);
        if (!_uiPreview && _launch is null) Shown += (_, _) => BeginInvoke((Action)TryFirstShowcase);
        if (!_uiPreview && _launch is null) Shown += async (_, _) => await CheckForUpdates(false);
        if (_launch is not null) Shown += async (_, _) =>
        {
            if (_launch.DelaySeconds > 0) await Task.Delay(TimeSpan.FromSeconds(_launch.DelaySeconds));
            if (IsDisposed) return;
            if (TryLoadProjectFromPath(_launch.ProjectPath)) await RunProject();
        };
        if (session is null)
            AppendLog("Проект готов. Блок «Следить за курсором» настроен на центр текущего монитора.");
        else
        {
            _log.Items.AddRange(session.LogLines.Cast<object>().ToArray());
            _graph.Zoom = session.Zoom;
            _graph.PanOffset = session.Pan;
            UpdateWindowTitle();
            if (session.CodeView && SwitchEditorView(true) && session.CodeDirty)
                _graphCode.Text = session.CodeText;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_uiPreview) return;
        var run = NativeMouse.RegisterRunHotKey(Handle);
        var stop = NativeMouse.RegisterStopHotKey(Handle);
        var recordStop = NativeMouse.RegisterRecordingStopHotKey(Handle);
        var mappingToggle = NativeMouse.RegisterUserHotKey(Handle, 7, 0x0003, 0x77); // Ctrl+Alt+F8
        _hotkeysRegistered = run || stop || recordStop;
        RegisterCustomHotkeys();
        RegisterIndicatorHotkey();
        ApplyMouseMappings();
        _stopHotkeyFallback = new StopHotkeyFallback(() => UI(StopScenario), () => UI(StopRecordingHotkey));
        if (!run || !stop || !recordStop) BeginInvoke(() =>
        {
            var fallback = _stopHotkeyFallback?.Available == true;
            AppendLog(fallback
                ? "Некоторые глобальные клавиши заняты. Резервный перехват Ctrl+Alt+F7/F10 включён."
                : "Некоторые глобальные клавиши заняты, а резервный перехват недоступен. Используйте меню остановки.");
            _status.Text = UiLanguage.T(fallback
                ? "Горячая клавиша занята — остановка: Ctrl+Alt+F7/F10 (резерв) или через меню"
                : "Внимание: горячие клавиши остановки недоступны — используйте меню «Сценарий»");
        });
        if (!mappingToggle) BeginInvoke(() => AppendLog("Ctrl+Alt+F8 занята: назначения мыши можно выключить через меню или трей."));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_hotkeysRegistered) NativeMouse.ReleaseHotKeys(Handle);
        if (_coordinateHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 3);
        if (_screenshotHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 4);
        if (_indicatorHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 6);
        _hotkeysRegistered = false;
        _coordinateHotkeyRegistered = false;
        _screenshotHotkeyRegistered = false;
        _indicatorHotkeyRegistered = false;
        _stopHotkeyFallback?.Dispose();
        _stopHotkeyFallback = null;
        _mouseMappings?.Dispose();
        _mouseMappings = null;
        NativeMouse.ReleaseUserHotKey(Handle, 7);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0400)
        {
            _messageInbox.Record(m.WParam.ToInt64(), m.LParam.ToInt64());
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == 0x0312)
        {
            if (m.WParam.ToInt32() == 1 && _cancel == null) _ = RunProject();
            if (m.WParam.ToInt32() == 2) StopScenario();
            if (m.WParam.ToInt32() == 3) CapturePoint();
            if (m.WParam.ToInt32() == 4) _ = CaptureScreenAsync();
            if (m.WParam.ToInt32() == 5) StopRecordingHotkey();
            if (m.WParam.ToInt32() == 6) ToggleIndicatorVisibility();
            if (m.WParam.ToInt32() == 7) ToggleMouseMappings();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_uiPreview && !_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            ShowCompactControl();
            return;
        }
        _cancel?.Cancel();
        _propertyHelpCancel?.Cancel();
        _mouseMappings?.Dispose();
        _mouseMappings = null;
        CloseIndicator();
        if (_recorder != null) StopRecording();
        _inspectorTimer.Stop();
        _controller?.AllowClose();
        _controller?.Close();
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); _trayIcon = null; }
        _trayMenu?.Dispose();
        foreach (var icon in _trayStateIcons.Values) icon.Dispose();
        _trayStateIcons.Clear();
        base.OnFormClosing(e);
    }

    private void InitializeTray()
    {
        _trayMenu = new ContextMenuStrip();
        _trayStatusItem = new ToolStripMenuItem(UiLanguage.T("Состояние: готов")) { Enabled = false };
        _trayStartItem = new ToolStripMenuItem(UiLanguage.T("▶ Запустить"), null, (_, _) => _ = RunProject());
        _trayPauseItem = new ToolStripMenuItem(UiLanguage.T("⏸ Пауза / продолжить"), null, (_, _) => TogglePauseScenario());
        _trayStopItem = new ToolStripMenuItem(UiLanguage.T("■ Остановить"), null, (_, _) => StopScenario());
        _trayMenu.Items.Add(_trayStatusItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(UiLanguage.T("Компактная панель"), null, (_, _) => ShowCompactControl());
        _trayMenu.Items.Add(UiLanguage.T("Открыть редактор"), null, (_, _) => ShowEditorFromController());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.AddRange([_trayStartItem, _trayPauseItem, _trayStopItem]);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(UiLanguage.T("Открыть проект…"), null, (_, _) => { ShowEditorFromController(); OpenProject(); });
        _trayMenu.Items.Add(UiLanguage.T("Сохранить проект…"), null, (_, _) => { ShowEditorFromController(); SaveProject(); });
        _trayMenu.Items.Add(UiLanguage.T("Настройки сценария…"), null, (_, _) => { ShowEditorFromController(); ConfigureRunMode(); });
        var mouseTray = new ToolStripMenuItem(UiLanguage.T("Назначения мыши"));
        _mouseTrayItem = mouseTray;
        mouseTray.DropDownItems.Add(UiLanguage.T("Профили и кнопки…"), null, (_, _) => { ShowEditorFromController(); ConfigureMouseMappings(); });
        mouseTray.DropDownItems.Add(UiLanguage.T("Включить / выключить  Ctrl+Alt+F8"), null, (_, _) => ToggleMouseMappings());
        var layerTray = new ToolStripMenuItem(UiLanguage.T("Активный слой"));
        layerTray.DropDownOpening += (_, _) => RefreshMouseLayersMenu(layerTray);
        mouseTray.DropDownItems.Add(layerTray);
        _trayMenu.Items.Add(mouseTray);
        _trayMenu.Items.Add(UiLanguage.T("Справка…"), null, (_, _) => new HelpForm(null).Show());
        _trayMenu.Items.Add(UiLanguage.T("Сайт и сообщество Kliker Blocks…"), null, (_, _) => WebsiteLinks.Open(this));
        _trayMenu.Items.Add(UiLanguage.T("Поддержать проект"), null, (_, _) => new SupportForm().Show());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(UiLanguage.T("Выход из Kliker Blocks"), null, (_, _) => ExitApplication());
        _trayMenu.Opening += (_, _) => UpdateControlSurfaces();
        _trayStateIcons["ready"] = TrayStatusIcon.Create(Icon ?? SystemIcons.Application, Color.FromArgb(136, 147, 160));
        _trayStateIcons["running"] = TrayStatusIcon.Create(Icon ?? SystemIcons.Application, Color.FromArgb(31, 184, 91));
        _trayStateIcons["paused"] = TrayStatusIcon.Create(Icon ?? SystemIcons.Application, Color.FromArgb(246, 176, 45));
        _trayIcon = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "Kliker Blocks: " + UiLanguage.T("готов"),
            ContextMenuStrip = _trayMenu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => ShowCompactControl();
        UpdateControlSurfaces();
    }

    private void ShowCompactControl()
    {
        _editorWindow?.Close();
        Hide();
        _controller ??= new CompactControlForm(ShowEditorFromController, () => _ = RunProject(),
            TogglePauseScenario, StopScenario,
            () => { ShowEditorFromController(); OpenProject(); },
            () => { ShowEditorFromController(); SaveProject(); },
            () => { ShowEditorFromController(); ConfigureRunMode(); },
            () => { ShowEditorFromController(); ConfigureMouseMappings(); },
            () => new HelpForm(null).Show(_controller), ExitApplication);
        UpdateControlSurfaces();
        if (!_controller.Visible) _controller.Show();
        _controller.Activate();
    }

    private void ShowEditorFromController()
    {
        _controller?.Hide();
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void UpdateControlSurfaces()
    {
        var name = !string.IsNullOrWhiteSpace(_project.ScriptName) ? _project.ScriptName :
            _file is null ? "Без имени" : Path.GetFileNameWithoutExtension(_file);
        var running = _cancel is not null;
        var paused = _runner?.IsPauseRequested == true;
        _debugStepButton.Enabled = running && paused;
        _controller?.UpdateState(name, running, paused, _project.WindowScope?.Enabled == true);
        if (_trayStatusItem is not null)
            _trayStatusItem.Text = $"{UiLanguage.T(name)}: {UiLanguage.T(running ? paused ? "пауза" : "выполняется" : "готов")}";
        if (_trayStartItem is not null) _trayStartItem.Enabled = !running;
        if (_trayPauseItem is not null)
        {
            _trayPauseItem.Enabled = running && _runner is not null;
            _trayPauseItem.Text = UiLanguage.T(paused ? "▶ Продолжить" : "⏸ Пауза");
        }
        if (_trayStopItem is not null) _trayStopItem.Enabled = running;
        if (_trayIcon is not null)
        {
            var tooltip = $"Kliker Blocks — {UiLanguage.T(name)}: {UiLanguage.T(running ? paused ? "пауза" : "выполняется" : "готов")}";
            _trayIcon.Text = tooltip[..Math.Min(tooltip.Length, 63)];
            _trayIcon.Icon = _trayStateIcons[running ? paused ? "paused" : "running" : "ready"];
        }
        if (_mouseEnabledItem is not null) _mouseEnabledItem.Checked = _project.MouseMappings?.Enabled == true;
        if (_mouseLayersItem is not null)
        {
            _mouseLayersItem.Enabled = _mouseMappings?.LayerNames.Count > 0;
            _mouseLayersItem.Text = _mouseMappings is null ? UiLanguage.T("Активный слой") :
                UiLanguage.T("Слой: ") + _mouseMappings.ActiveDescription;
        }
        if (_mouseTrayItem is not null) _mouseTrayItem.Text = _mouseMappings is null
            ? UiLanguage.T("Назначения мыши: выключены") :
            UiLanguage.T("Назначения мыши: ") + _mouseMappings.ActiveDescription;
    }

    private static void AddMenuItem(ToolStripMenuItem menu, string title, EventHandler action)
    {
        var item = new ToolStripMenuItem(title);
        item.Click += action;
        menu.DropDownItems.Add(item);
    }

    internal static string CategoryFor(string kind) => kind switch
    {
        "Description" => "Оформление",
        "Start" or "Stop" or "Log" or "LogFile" or "LogClear" or "LogShow" or "PlaySound" or "SpeakText" or "Beep" or "InputDialog" or "ChoiceDialog" or "RadioDialog" or "HintPopup" or "Wait" or "WaitEvent" or "ShowcaseStep" => "Основные",
        "SetVariable" or "DefineVariable" or "UndefineVariable" or "SetVariableByName" or "MathVariable" or "ArraySet" or "ArrayAction" or "StringSplit" or "IfVariable" => "Переменные",
        "Repeat" or "WhileExpression" or "SwitchValue" or "Parallel" or "BranchEnd" or "CallSub" or "SubStart" or "SubReturn" => "Циклы и ветки",
        "IfExpression" => "Условия «Если» и поиск",
        "IfMoved" or "IfPixel" or "WaitPixel" or "IfKeyDown" or "FindColor" or "FindImage" or "BufferFindColor" or "BufferFindImage" or "IfWindow" => "Условия «Если» и поиск",
        "WatchCursor" or "MoveCursor" or "MoveRelative" or "Click" or "MouseButton" or "Wheel" or "Key" or "KeyState" or "VirtualKey" or "TypeText" or "ClipboardSet" or "InputGroup" => "Мышь и клавиатура",
        "ClipboardGet" or "TextFile" or "TextFileArray" or "CsvTable" or "ExcelTable" or "PdfText" or "FileQueue" or "Checkpoint" or "IniFile" or "DirectoryList" or "HttpGet" or "HttpPost" or "JsonValue" or
        "TranslateText" or "DetectLanguage" or "LanguagePair" or "TranslateArray" => "Данные и файлы",
        "Screenshot" or "OcrImage" or "BufferCapture" or "BufferPixel" or "BufferCount" or "BufferHash" or "BufferReplace" or "BufferScanColor" or "BufferScanImage" or "BufferSave" or "MoveFound" or "ActivateWindow" or "MoveWindow" or "WindowFind" or "WindowInfo" or "WindowAction" or "ElementAtPoint" or "ElementInfo" or "ElementSetText" or "UiWait" or "UiAction" or "MessageSend" or "MessageReceive" => "Экран и окна",
        _ => "Внешние программы"
    };

    internal static int CategoryOrder(string name) => name switch
    {
        "Основные" => 0, "Переменные" => 1, "Циклы и ветки" => 2, "Условия «Если» и поиск" => 3,
        "Мышь и клавиатура" => 4, "Данные и файлы" => 5, "Экран и окна" => 6, "Оформление" => 7, _ => 8
    };

    private void StopScenario()
    {
        if (_cancel == null) return;
        _runner?.SetPaused(false);
        _cancel.Cancel();
        _status.Text = UiLanguage.T("Остановка сценария…");
        AppendLog("Получена команда остановки сценария");
        UpdateControlSurfaces();
    }

    private void TogglePauseScenario()
    {
        if (_cancel is null || _runner is null) return;
        var paused = !_runner.IsPauseRequested;
        if (paused) _runner.SetPaused(true);
        else _runner.ContinueDebug();
        _pauseButton.Text = UiLanguage.T(paused ? "▶ Продолжить" : "⏸ Пауза");
        _status.Text = UiLanguage.T(paused ? "Сценарий: пауза" : "Сценарий выполняется");
        AppendLog(paused ? "Пауза сценария запрошена" : "Сценарий продолжен");
        UpdateControlSurfaces();
    }

    private void StepScenario()
    {
        if (_runner is null || !_runner.IsPauseRequested) return;
        _runner.StepDebug();
        _pauseButton.Text = UiLanguage.T("⏸ Пауза");
        UpdateControlSurfaces();
    }

    private void UpdateDebugState(Guid id, IReadOnlyDictionary<string, string> variables, bool paused)
    {
        if (IsDisposed) return;
        _graph.ActiveDebugNode = id;
        if (!paused) return;
        if (_debugWindow is null || _debugWindow.IsDisposed)
        {
            _debugWindow = new DebugMonitorForm(StepScenario, () =>
            {
                if (_runner is null) return;
                _runner.ContinueDebug();
                _pauseButton.Text = UiLanguage.T("⏸ Пауза");
                UpdateControlSurfaces();
            });
        }
        if (!_debugWindow.Visible) _debugWindow.Show(this);
        var node = _project.Nodes.FirstOrDefault(item => item.Id == id);
        _debugWindow.UpdateState(node is null ? id.ToString() : UiLanguage.T(BlockCatalog.Get(node.Kind).Title), variables);
        _debugStepButton.Enabled = true;
        _status.Text = UiLanguage.T("Отладка: остановка перед блоком");
    }

    private void ConfigureRunMode()
    {
        if (_cancel != null)
        {
            MessageBox.Show(this, "Сначала остановите сценарий: режим следующего запуска меняется только до старта.",
                "Режим выполнения", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new RunModeSettingsForm(_project);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.RunMode = dialog.RunMode;
        _project.CycleDelayMs = dialog.CycleDelayMs;
        _project.PlaybackSpeed = dialog.PlaybackSpeed;
        RefreshRunModeMenu();
        AppendLog($"Режим выполнения: {_project.RunMode}; пауза {_project.CycleDelayMs} мс; скорость {_project.PlaybackSpeed:0.0}×. Сохраните проект.");
    }

    private void ConfigureMouseMappings()
    {
        using var dialog = new MouseMappingSettingsForm(_project.MouseMappings ?? new());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.MouseMappings = dialog.Result;
        ApplyMouseMappings();
        AppendLog("Назначения мыши обновлены. Сохраните проект.");
    }

    private void ToggleMouseMappings()
    {
        _project.MouseMappings ??= new MouseMappingSettings();
        _project.MouseMappings.Enabled = !_project.MouseMappings.Enabled;
        ApplyMouseMappings();
        AppendLog($"Назначения мыши {(_project.MouseMappings.Enabled ? "включены" : "выключены")}. Сохраните проект.");
    }

    private void ApplyMouseMappings()
    {
        _mouseMappings?.Dispose();
        _mouseMappings = null;
        _project.MouseMappings ??= new MouseMappingSettings();
        if (_project.MouseMappings.Enabled && IsHandleCreated)
        {
            var error = MouseMappingValidator.Check(_project.MouseMappings);
            if (error is not null) AppendLog("Назначения мыши не включены: " + error);
            else
            {
                try
                {
                    _mouseMappings = new MouseMappingRuntime(this, _project.MouseMappings,
                        () => _ = RunProject(), StopScenario, AppendLog, UpdateControlSurfaces);
                    _mouseMappings.Start();
                }
                catch (Exception exception)
                {
                    _mouseMappings?.Dispose();
                    _mouseMappings = null;
                    AppendLog("Не удалось включить назначения мыши: " + exception.Message);
                }
            }
        }
        UpdateControlSurfaces();
    }

    private void RefreshMouseLayersMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        if (_mouseMappings is null || _mouseMappings.LayerNames.Count == 0)
        { menu.DropDownItems.Add(UiLanguage.T("Нет активного профиля")).Enabled = false; return; }
        for (var index = 0; index < _mouseMappings.LayerNames.Count; index++)
        {
            var number = index + 1;
            var item = new ToolStripMenuItem($"{number}. {_mouseMappings.LayerNames[index]}")
                { Checked = _mouseMappings.ActiveDescription.EndsWith("/ " + _mouseMappings.LayerNames[index], StringComparison.Ordinal) };
            item.Click += (_, _) => _mouseMappings.SelectLayer(number.ToString());
            menu.DropDownItems.Add(item);
        }
    }

    private void RefreshRunModeMenu() => _runModeItem.Text = UiLanguage.T(_project.RunMode == "Повторять до остановки"
        ? "Режим выполнения: повторять до остановки…" : "Режим выполнения: один проход…");

    private void ConfigureWindowScope()
    {
        if (_cancel != null)
        {
            MessageBox.Show(this, "Сначала остановите сценарий: привязка меняется только до запуска.",
                "Привязка к окну", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new WindowScopeSettingsForm(_project.WindowScope);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.WindowScope = dialog.Settings;
        AppendLog($"Привязка к окну {(_project.WindowScope.Enabled ? "включена" : "выключена")}. Сохраните проект.");
        UpdateControlSurfaces();
    }

    private void RegisterCustomHotkeys()
    {
        try
        {
            var point = HotkeyParser.Parse(_settings.CoordinateHotkey);
            var image = HotkeyParser.Parse(_settings.ScreenshotHotkey);
            _coordinateHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 3, point.Modifiers, point.Key);
            _screenshotHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 4, image.Modifiers, image.Key);
            if (!_coordinateHotkeyRegistered || !_screenshotHotkeyRegistered)
                BeginInvoke(() => AppendLog("Одна из горячих клавиш захвата занята. Измените её в настройках или используйте кнопки."));
        }
        catch (Exception error) { BeginInvoke(() => AppendLog("Ошибка горячих клавиш: " + error.Message)); }
    }

    private void RegisterIndicatorHotkey()
    {
        try
        {
            var key = HotkeyParser.Parse(_project.IndicatorToggleHotkey);
            _indicatorHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 6, key.Modifiers, key.Key);
            if (!_indicatorHotkeyRegistered) BeginInvoke(() => AppendLog(
                UiLanguage.Format("Клавиша индикатора {0} занята. Выберите другую в «Сценарий → Оформление и клавиша индикатора».",
                    _project.IndicatorToggleHotkey)));
        }
        catch (Exception error) { BeginInvoke(() => AppendLog("Ошибка клавиши индикатора: " + error.Message)); }
    }

    private void ToggleIndicatorVisibility()
    {
        if (_cancel is null || !_project.ShowIndicator) return;
        _indicatorHiddenByHotkey = !_indicatorHiddenByHotkey;
        if (_indicatorHiddenByHotkey) _indicator?.Hide();
        else
        {
            if (_indicator is null) ShowIndicator();
            else _indicator.Show();
        }
        AppendLog($"Индикатор {(_indicatorHiddenByHotkey ? "скрыт" : "показан")} клавишей {_project.IndicatorToggleHotkey}; сценарий продолжает работать.");
    }

    private void ConfigureHotkeys()
    {
        using var dialog = new HotkeySettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (_coordinateHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 3);
        if (_screenshotHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 4);
        _coordinateHotkeyRegistered = false;
        _screenshotHotkeyRegistered = false;
        var previous = _settings;
        _settings = dialog.Settings;
        RegisterCustomHotkeys();
        if (!_coordinateHotkeyRegistered || !_screenshotHotkeyRegistered)
        {
            if (_coordinateHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 3);
            if (_screenshotHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 4);
            _coordinateHotkeyRegistered = false;
            _screenshotHotkeyRegistered = false;
            _settings = previous;
            RegisterCustomHotkeys();
            MessageBox.Show(this, "Выбранная клавиша занята или не поддерживается. Прежние настройки восстановлены.", "Горячие клавиши");
            return;
        }
        try { _settings.Save(); AppendLog("Горячие клавиши сохранены."); }
        catch (Exception error) { MessageBox.Show(this, error.Message, "Не удалось сохранить настройки"); }
    }

    private void ToggleAutostart()
    {
        try
        {
            AutostartSettings.SetEnabled(!_autostartItem.Checked);
            _autostartItem.Checked = AutostartSettings.IsEnabled();
            AppendLog(UiLanguage.T(_autostartItem.Checked
                ? "Автозапуск Kliker Blocks включён."
                : "Автозапуск Kliker Blocks выключен."));
        }
        catch (Exception error)
        {
            MessageBox.Show(this, UiLanguage.T("Не удалось изменить автозапуск: ") + error.Message,
                UiLanguage.T("Запускать вместе с Windows"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureScenarioAutomation()
    {
        using var form = new ScenarioAutomationForm(_settings);
        form.ShowDialog(this);
        try { _autostartItem.Checked = AutostartSettings.IsEnabled(); }
        catch { _autostartItem.Checked = false; }
    }

    private async Task CheckForUpdates(bool showResult)
    {
        try
        {
            _updateItem.Enabled = false;
            _availableUpdate = await AppUpdates.CheckAsync();
            _updateItem.Text = _availableUpdate is null ? UiLanguage.T("Проверить обновления…") :
                UiLanguage.Format("Доступна версия {0}…", _availableUpdate.Version);
            _updateBadge.Visible = _availableUpdate is not null;
            if (_availableUpdate is not null)
                _updateBadge.Text = UiLanguage.Format("⬇ Версия {0}", _availableUpdate.Version);
            if (!showResult || IsDisposed) return;
            if (_availableUpdate is null)
            {
                MessageBox.Show(this, UiLanguage.T("У вас актуальная версия Kliker Blocks."),
                    UiLanguage.T("Проверка обновлений"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_cancel is not null)
            {
                MessageBox.Show(this, UiLanguage.T("Остановите сценарий перед обновлением."),
                    UiLanguage.T("Обновление Kliker Blocks"));
                return;
            }
            using var dialog = new UpdateDownloadForm(_availableUpdate);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.InstallerPath is null) return;
            var backup = AppUpdates.BackupGraph(_project, _file, _codeDirty ? _graphCode.Text : null);
            AppendLog(UiLanguage.Format("Резервная копия графа: {0}", backup));
            Process.Start(new ProcessStartInfo(dialog.InstallerPath)
            {
                UseShellExecute = true,
                Arguments = "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
            });
            ExitApplication();
        }
        catch (Exception error)
        {
            if (showResult && !IsDisposed) MessageBox.Show(this, error.Message,
                UiLanguage.T("Проверка обновлений"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { if (!IsDisposed) _updateItem.Enabled = true; }
    }

    private void UpdateInspector()
    {
        if (_captureActive) return;
        try
        {
            var point = NativeMouse.Position();
            var color = ScreenTools.Pixel(point);
            var hwnd = NativeMouse.WindowAt(point);
            _inspector.Text = UiLanguage.Format("Мышь X:{0} Y:{1}   Цвет: #{2:X2}{3:X2}{4:X2}   HWND: {5:X}   Точка: {6}   Снимок: {7}",
                point.X, point.Y, color.R, color.G, color.B, hwnd.ToInt64(), _settings.CoordinateHotkey, _settings.ScreenshotHotkey);
        }
        catch { _inspector.Text = UiLanguage.T("Экран недоступен"); }
    }

    private void CapturePoint()
    {
        if (_captureActive) return;
        try
        {
            var point = NativeMouse.Position();
            var saved = new SavedPoint { X = point.X, Y = point.Y };
            _project.Points.Add(saved);
            if (_project.Points.Count > 500) _project.Points.RemoveAt(0);
            ApplyPoint(saved);
            AppendLog($"Точка сохранена: {point.X},{point.Y}");
        }
        catch (Exception error) { AppendLog("Не удалось захватить точку: " + error.Message); }
    }

    private void ApplyPoint(SavedPoint point)
    {
        if (_selected == null) return;
        var node = _selected;
        var pair = node.Kind switch
        {
            "WatchCursor" or "IfMoved" => ("AnchorX", "AnchorY"),
            "MoveCursor" => ("TargetX", "TargetY"),
            "IfPixel" or "WaitPixel" => ("PixelX", "PixelY"),
            "MoveWindow" => ("WindowX", "WindowY"),
            "FindColor" or "FindImage" or "Screenshot" when _pendingRegionNode == node.Id => ("Right", "Bottom"),
            "FindColor" or "FindImage" or "Screenshot" => ("Left", "Top"),
            _ => ("", "")
        };
        if (pair.Item1 == "") { AppendLog("Выбранный блок не имеет подходящих координатных полей; точка осталась в библиотеке."); return; }
        node.Values[pair.Item1] = point.X.ToString();
        node.Values[pair.Item2] = point.Y.ToString();
        if (node.Kind is "WatchCursor" or "IfMoved") node.Values["Anchor"] = "Задать X/Y";
        if (node.Kind == "MoveCursor") node.Values["TargetMode"] = "Задать X/Y";
        if (pair.Item1 == "Left") { _pendingRegionNode = node.Id; AppendLog("Первый угол области задан. Захватите второй угол той же клавишей."); }
        else if (pair.Item1 == "Right")
        {
            var left = int.Parse(node.Values["Left"]);
            var top = int.Parse(node.Values["Top"]);
            node.Values["Left"] = Math.Min(left, point.X).ToString();
            node.Values["Top"] = Math.Min(top, point.Y).ToString();
            node.Values["Right"] = Math.Max(left, point.X).ToString();
            node.Values["Bottom"] = Math.Max(top, point.Y).ToString();
            _pendingRegionNode = null;
        }
        ShowProperties(node);
        _graph.Invalidate();
    }

    private async Task CaptureScreenAsync()
    {
        if (_captureActive) return;
        if (_cancel != null) { AppendLog("Для ручного снимка сначала остановите сценарий; блок «Снимок экрана» доступен внутри сценария."); return; }
        _captureActive = true;
        try
        {
            Hide();
            await Task.Delay(150);
            using var overlay = new CaptureOverlay();
            if (overlay.ShowDialog() != DialogResult.OK) return;
            await Task.Delay(150);
            var baseDirectory = _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file)!;
            var asset = CaptureService.Save(overlay.SelectedArea, overlay.Shape, overlay.RequestedName, _settings, baseDirectory);
            _project.Captures.Add(asset);
            AppendLog("Снимок сохранён: " + asset.Path);
        }
        catch (Exception error) { MessageBox.Show(error.Message, "Ошибка снимка"); }
        finally { Show(); Activate(); _captureActive = false; }
    }

    private void ShowLibrary()
    {
        using var dialog = new CaptureLibraryForm(_project, UseCapture, ApplyPoint, UseRecording);
        dialog.ShowDialog(this);
    }

    private void UseCapture(CaptureAsset asset)
    {
        var node = _selected?.Kind == "FindImage" ? _selected : null;
        if (node == null)
        {
            node = BlockCatalog.Create("FindImage", 120 + (_project.Nodes.Count % 6) * 235, 120 + (_project.Nodes.Count / 6) * 145);
            _project.Nodes.Add(node);
        }
        node.Values["Path"] = asset.Path;
        var screen = SystemInformation.VirtualScreen;
        node.Values["Left"] = screen.Left.ToString();
        node.Values["Top"] = screen.Top.ToString();
        node.Values["Right"] = screen.Right.ToString();
        node.Values["Bottom"] = screen.Bottom.ToString();
        _graph.Select(node);
        _graph.Invalidate();
        AppendLog("Снимок выбран как шаблон поиска: " + asset.Name);
    }

    private async Task StartRecordingAsync()
    {
        if (_recorder != null || _captureActive) return;
        if (_cancel != null) { MessageBox.Show(this, "Остановите выполняющийся сценарий перед записью."); return; }
        using var setup = new MacroSetupForm();
        if (setup.ShowDialog(this) != DialogResult.OK) return;
        var options = setup.Options;
        if (options.Mode == "Клики и образы" && options.RegionMode == "Фиксированная область")
        {
            Hide();
            await Task.Delay(150);
            try
            {
                using var overlay = new CaptureOverlay();
                if (overlay.ShowDialog() != DialogResult.OK) return;
                options.FixedArea = overlay.SelectedArea;
                options.Shape = overlay.Shape;
            }
            finally { Show(); Activate(); }
        }
        try
        {
            var directory = _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file)!;
            _recordingBar = new RecordingBar(options.Mode);
            _recorder = new MacroRecorder(options, _settings, directory,
                () => _recordingBar?.Bounds ?? Rectangle.Empty,
                count => _recordingBar?.SetCount(count),
                error => AppendLog(error),
                () => BeginInvoke(StopRecording));
            _recordingBar.StopRequested += StopRecording;
            _recordingBar.CloseRequested += StopRecording;
            _recordingBar.PauseRequested += () => SetRecordingPaused(true);
            _recordingBar.ResumeRequested += () => SetRecordingPaused(false);
            Hide();
            await Task.Delay(150);
            _recordingBar.Show();
            _recorder.Start();
            AppendLog("Запись начата: " + options.Name);
        }
        catch (Exception error)
        {
            _recorder?.Dispose();
            _recorder = null;
            _recordingBar?.AllowClose();
            _recordingBar?.Close();
            _recordingBar?.Dispose();
            _recordingBar = null;
            Show();
            MessageBox.Show(this, error.Message, "Не удалось начать запись");
        }
    }

    private void StopRecording() => FinishRecording(false);
    private void StopRecordingHotkey() => FinishRecording(true);

    private void FinishRecording(bool hotkey)
    {
        if (_recorder == null || _stoppingRecording) return;
        _stoppingRecording = true;
        try
        {
            var recording = _recorder.Stop(hotkey);
            _project.Recordings.Add(recording);
            _project.Captures.AddRange(_recorder.Assets);
            _recordingBar?.AllowClose();
            _recordingBar?.Close();
            _recordingBar?.Dispose();
            _recordingBar = null;
            _recorder = null;
            Show();
            Activate();
            var (first, added) = MacroBuilder.AddToProject(_project, recording);
            if (first is Guid id)
            {
                var start = _project.Nodes.SingleOrDefault(n => n.Kind == "Start");
                if (start != null && start.Link("Далее") == null) start.Links["Далее"] = id;
                _graph.Select(_project.Nodes.First(n => n.Id == id));
            }
            ExpandCanvas();
            _graph.Invalidate();
            AppendLog($"Макрос «{recording.Name}»: {recording.Events.Count} событий, {added} блоков. Сохраните проект.");
            if (recording.Mode == "Клики и образы" && recording.Events.Any(e => !string.IsNullOrEmpty(e.CapturePath)))
                AppendLog("У каждого блока «Найти изображение» выход «Нет» свободен для вашей ветки восстановления.");
        }
        catch (Exception error)
        {
            Show();
            MessageBox.Show(this, error.Message, "Ошибка завершения записи");
        }
        finally { _stoppingRecording = false; }
    }

    private void SetRecordingPaused(bool paused)
    {
        if (_recorder == null || _recordingBar == null) return;
        _recorder.SetPaused(paused);
        _recordingBar.SetPaused(paused);
        AppendLog(paused ? "Запись макроса приостановлена" : "Запись макроса продолжена");
    }

    private void UseRecording(MacroRecording recording)
    {
        var (first, added) = MacroBuilder.AddToProject(_project, recording);
        if (first is Guid id) _graph.Select(_project.Nodes.First(n => n.Id == id));
        ExpandCanvas();
        _graph.Invalidate();
        AppendLog($"Добавлена копия макроса «{recording.Name}»: {added} блоков.");
    }

    private void ExpandCanvas()
    {
        _graph.SetWorldSize(Math.Max(2200, _project.Nodes.Select(n => BlockCatalog.Bounds(n).Right + 100).DefaultIfEmpty(2200).Max()),
            Math.Max(1500, _project.Nodes.Select(n => BlockCatalog.Bounds(n).Bottom + 100).DefaultIfEmpty(1500).Max()));
    }

    private void AddBlock(string kind)
    {
        if (_codeViewActive && !TryApplyGraphCode()) return;
        var n = _project.Nodes.Count;
        var position = _graph.InsertionWorldPoint ?? new Point(120 + (n % 6) * 235, 120 + (n / 6) * 145);
        var block = BlockCatalog.Create(kind, position.X, position.Y);
        if (kind is "TranslateText" or "DetectLanguage" or "LanguagePair" or "TranslateArray")
            block.Values["Endpoint"] = _settings.TranslationEndpoint;
        _project.Nodes.Add(block);
        ExpandCanvas();
        _graph.Select(block);
        _graph.Invalidate();
        if (_codeViewActive) LoadGraphCode();
    }

    private void ConfigureLanguages()
    {
        if (_cancel is not null || _recorder is not null || _showcaseWindow?.IsRunning == true)
        {
            MessageBox.Show(this, UiLanguage.T("Перед сменой языка остановите сценарий, демонстрацию или запись макроса."),
                UiLanguage.T("Язык интерфейса"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new LanguageSettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var changed = _settings.UiLanguageCode != dialog.Settings.UiLanguageCode;
        try { dialog.Settings.Save(); }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "Не удалось сохранить настройки",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _settings = dialog.Settings;
        if (!changed) { AppendLog("Настройки языков и перевода сохранены."); return; }
        PendingLanguageSession = new LanguageSession(_project, _file, _codeViewActive,
            _graphCode.Text, _codeDirty, _graph.Zoom, _graph.PanOffset,
            _log.Items.Cast<string>().TakeLast(500).ToArray());
        _exitRequested = true;
        BeginInvoke(Close);
    }

    private void CheckVariableNames()
    {
        var invalid = VariableNameRules.FindInvalid(_project);
        if (invalid.Count > 0)
        {
            MessageBox.Show(this, "Для латинского режима вручную переименуйте переменные и все ссылки на них:\n\n" +
                string.Join("\n", invalid) + (invalid.Count == 30 ? "\n…" : ""),
                "Имена переменных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AppendLog("Проверка имён переменных пройдена.");
        MessageBox.Show(this, "Проверка пройдена. Имена переменных соответствуют правилу A–Z, a–z, 0–9 и _.",
            "Имена переменных", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void LoadGraphCode()
    {
        _loadingGraphCode = true;
        try
        {
            _graphCode.Text = GraphCodeCodec.Serialize(_project);
            _codeDirty = false;
            _codeStatus.Text = UiLanguage.T("Код Kliker Blocks: блоки, значения и связи");
            _applyCodeButton.Enabled = false;
            _revertCodeButton.Enabled = false;
        }
        finally { _loadingGraphCode = false; }
    }

    private bool TryApplyGraphCode()
    {
        if (!_codeViewActive || !_codeDirty) return true;
        try
        {
            var nodes = GraphCodeCodec.Parse(_graphCode.Text);
            var candidate = _project.Copy();
            candidate.Nodes = nodes;
            if (!CheckAsciiNames(candidate)) return false;
            _project.Nodes = nodes;
            _graph.Project = _project;
            _graph.Select(null);
            ExpandCanvas();
            _graph.Invalidate();
            _codeDirty = false;
            _codeStatus.Text = UiLanguage.Format("Применено: {0} блоков", nodes.Count);
            _applyCodeButton.Enabled = false;
            _revertCodeButton.Enabled = false;
            AppendLog($"Код Kliker Blocks применён: {nodes.Count} блоков.");
            return true;
        }
        catch (GraphCodeException error)
        {
            _codeStatus.Text = error.Message;
            var line = error.LineNumber - 1;
            if (line >= 0 && line < _graphCode.Lines.Length)
            {
                var offset = _graphCode.GetFirstCharIndexFromLine(line);
                if (offset >= 0) _graphCode.Select(offset, _graphCode.Lines[line].Length);
            }
            _graphCode.Focus();
            return false;
        }
    }

    private bool SwitchEditorView(bool showCode)
    {
        if (_codeViewActive == showCode) return true;
        if (showCode)
        {
            _editorWindow?.Close();
            try { LoadGraphCode(); }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось открыть код графа");
                return false;
            }
            _editorHost.Visible = false;
            _codeHost.Visible = true;
            _codeHost.BringToFront();
            _codeViewActive = true;
            _visualModeButton.Checked = false;
            _codeModeButton.Checked = true;
            _graphCode.Focus();
        }
        else
        {
            if (!TryApplyGraphCode()) return false;
            _codeHost.Visible = false;
            _editorHost.Visible = true;
            _editorHost.BringToFront();
            _codeViewActive = false;
            _codeModeButton.Checked = false;
            _visualModeButton.Checked = true;
            _graph.Focus();
        }
        return true;
    }

    private void HandleEditorKeys(object? sender, KeyEventArgs e)
    {
        if (_graphCode.ContainsFocus) return;
        if (_fields.Controls.Cast<Control>().Any(control => control.Focused && control is TextBoxBase or ComboBox)) return;
        if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.C) CopyBlocks();
        else if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.V) PasteBlocks();
        else if (e.KeyCode == Keys.Delete) DeleteSelected();
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void CopyBlocks()
    {
        _blockClipboard = BlockClipboard.Copy(_graph.SelectedNodes);
        _status.Text = _blockClipboard.Count == 0
            ? UiLanguage.T("Выберите блоки для копирования. Блок «Старт» не копируется.")
            : UiLanguage.Format("Скопировано блоков: {0}. Щёлкните пустое поле для точки вставки и нажмите Ctrl+V.",
                _blockClipboard.Count);
    }

    private void PasteBlocks()
    {
        if (_codeViewActive && !TryApplyGraphCode()) return;
        if (_blockClipboard.Count == 0) { _status.Text = UiLanguage.T("Буфер блоков пуст. Выберите блоки и нажмите Ctrl+C."); return; }
        var nodes = BlockClipboard.Paste(_blockClipboard,
            _graph.InsertionWorldPoint ?? _graph.GetPasteWorldPoint(), _project);
        ExpandCanvas();
        _graph.SelectMany(nodes);
        _graph.Invalidate();
        if (_codeViewActive) LoadGraphCode();
        AppendLog($"Вставлено блоков: {nodes.Count}. Внутренние связи сохранены.");
    }

    private void ShowEditorWindow()
    {
        if (_codeViewActive && !SwitchEditorView(false)) return;
        if (_editorWindow != null) { _editorWindow.Activate(); return; }
        var window = new Form
        {
            Text = UiLanguage.T("Kliker Blocks — отдельный редактор (закройте окно для возврата)"),
            Icon = Icon, Width = 1350, Height = 850,
            StartPosition = FormStartPosition.CenterScreen,
            KeyPreview = true
        };
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = _graph.BackColor };
        window.Controls.Add(host);
        window.KeyDown += HandleEditorKeys;
        _editorHost.Controls.Remove(_graph);
        host.Controls.Add(_graph);
        _graph.Location = Point.Empty;
        window.FormClosing += (_, _) =>
        {
            host.Controls.Remove(_graph);
            if (!IsDisposed && !Disposing)
            {
                _editorHost.Controls.Add(_graph);
                _graph.Location = Point.Empty;
            }
            _editorWindow = null;
        };
        _editorWindow = window;
        window.Show(this);
        _graph.Focus();
    }

    private void ShowJournalWindow()
    {
        if (_journalWindow is not null) { _journalWindow.Activate(); return; }
        var originalParent = _log.Parent;
        if (originalParent is null) return;
        var window = new Form
        {
            Text = UiLanguage.T("Журнал выполнения"), Icon = Icon,
            Width = 920, Height = 560, StartPosition = FormStartPosition.CenterParent
        };
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(12, 6, 12, 3), BackColor = Color.FromArgb(247, 249, 252) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        var title = new Label { Text = UiLanguage.T("Журнал выполнения"), Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        _journalWrapDetached = new CheckBox { Text = UiLanguage.T("Перенос строк"),
            Dock = DockStyle.Fill,
            Checked = _journalWrapEnabled };
        _journalWrapDetached.CheckedChanged += (_, _) => SetJournalWrap(_journalWrapDetached.Checked);
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(_journalWrapDetached, 1, 0);
        originalParent.Controls.Remove(_log);
        window.Controls.Add(_log);
        window.Controls.Add(header);
        window.FormClosing += (_, _) =>
        {
            window.Controls.Remove(_log);
            if (!IsDisposed && !Disposing && originalParent is TableLayoutPanel table)
                table.Controls.Add(_log, 0, 1);
            else if (!IsDisposed && !Disposing) originalParent.Controls.Add(_log);
            _journalWrapDetached = null;
            _journalWindow = null;
        };
        _journalWindow = window;
        window.Show(this);
    }

    private void DeleteSelected()
    {
        if (_codeViewActive && !TryApplyGraphCode()) return;
        if (_graph.DeleteSelectedLink())
        {
            if (_codeViewActive) LoadGraphCode();
            AppendLog("Выбранная связь удалена");
            return;
        }
        var selected = _graph.SelectedNodes;
        if (selected.Count == 0) return;
        var removable = selected.Where(node => node.Kind != "Start").ToList();
        if (removable.Count == 0) { MessageBox.Show("Блок «Старт» нужен для запуска сценария."); return; }
        var ids = removable.Select(node => node.Id).ToHashSet();
        _project.Nodes.RemoveAll(node => ids.Contains(node.Id));
        foreach (var node in _project.Nodes)
            foreach (var key in node.Links.Keys.ToArray())
                if (node.Links[key] is Guid target && ids.Contains(target)) node.Links[key] = null;
        _graph.Select(null);
        _graph.Invalidate();
        if (_codeViewActive) LoadGraphCode();
        AppendLog($"Удалено блоков: {removable.Count}.");
    }

    private void ShowGroupProperties(IReadOnlyList<BlockNode> nodes)
    {
        _selected = null;
        _fields.Controls.Clear();
        _selection.Text = UiLanguage.Format("Выбрано блоков: {0}", nodes.Count);
        _fields.Controls.Add(new Label
        {
            Text = UiLanguage.T("Потяните любой выбранный блок левой кнопкой мыши, чтобы переместить всю группу. Ctrl+C — копировать; Delete — удалить. Щелчок по пустому месту снимет выделение."),
            Width = 250, AutoSize = true, MaximumSize = new Size(250, 0)
        });
    }

    private void ShowLinkProperties(BlockNode source, string port, BlockNode target)
    {
        _selected = null;
        _fields.Controls.Clear();
        _selection.Text = UiLanguage.T("Связь");
        _fields.Controls.Add(new Label
        {
            Text = UiLanguage.Format("{0}  →  {1}\nВыход: {2}\n\nОдин выход соединяется с одним входом. К одному входу могут вести несколько связей.\n\nНажмите Delete для удаления выбранной линии.",
                UiLanguage.T(BlockCatalog.Get(source.Kind).Title), UiLanguage.T(BlockCatalog.Get(target.Kind).Title), UiLanguage.T(port)),
            Width = 250, AutoSize = true, MaximumSize = new Size(250, 0)
        });
        var appearance = (source.LinkStyles.GetValueOrDefault(port) ?? new LinkAppearance()).Copy();
        void SaveAppearance()
        {
            source.LinkStyles[port] = appearance.Copy();
            _graph.Invalidate();
        }
        _fields.Controls.Add(new Label { Text = UiLanguage.T("Оформление связи"), Width = 250,
            Height = 30, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 12, 0, 0) });
        _fields.Controls.Add(new Label { Text = UiLanguage.T("Наконечник"), Width = 250, Height = 22 });
        var ends = new (string Key, string Label)[] { ("Arrow", "Стрелка"), ("Line", "Линия"),
            ("Diamond", "Ромб"), ("Circle", "Круг") };
        var endChoice = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        endChoice.Items.AddRange(ends.Select(item => UiLanguage.T(item.Label)).Cast<object>().ToArray());
        endChoice.SelectedIndex = Math.Max(0, Array.FindIndex(ends, item => item.Key == appearance.End));
        endChoice.SelectedIndexChanged += (_, _) => { appearance.End = ends[endChoice.SelectedIndex].Key; SaveAppearance(); };
        _fields.Controls.Add(endChoice);
        _fields.Controls.Add(new Label { Text = UiLanguage.T("Рисунок линии"), Width = 250, Height = 22 });
        var patterns = new (string Key, string Label)[] { ("Solid", "Сплошная"),
            ("Dash", "Штриховая"), ("Dot", "Точечная"), ("DashDot", "Штрих-пунктир"),
            ("DashDotDot", "Штрих-точка-точка") };
        var patternChoice = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        patternChoice.Items.AddRange(patterns.Select(item => UiLanguage.T(item.Label)).Cast<object>().ToArray());
        patternChoice.SelectedIndex = Math.Max(0, Array.FindIndex(patterns, item => item.Key == appearance.Pattern));
        patternChoice.SelectedIndexChanged += (_, _) => { appearance.Pattern = patterns[patternChoice.SelectedIndex].Key; SaveAppearance(); };
        _fields.Controls.Add(patternChoice);
        var color = new Button { Width = 250, Height = 36 };
        void RefreshColorButton()
        {
            color.Text = UiLanguage.T("Цвет связи") + ": " + appearance.Color;
            color.BackColor = ColorTranslator.FromHtml(appearance.Color);
            color.ForeColor = color.BackColor.GetBrightness() < .55f ? Color.White : Color.Black;
        }
        RefreshColorButton();
        color.Click += (_, _) =>
        {
            using var picker = new ColorDialog { Color = ColorTranslator.FromHtml(appearance.Color), FullOpen = true };
            if (picker.ShowDialog(_editorWindow ?? this) != DialogResult.OK) return;
            appearance.Color = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
            RefreshColorButton();
            SaveAppearance();
        };
        _fields.Controls.Add(color);
        _fields.Controls.Add(new Label { Text = UiLanguage.T("Непрозрачность связи, % (5–100)"),
            Width = 250, Height = 22 });
        var opacity = new NumericUpDown { Width = 250, Minimum = 5, Maximum = 100,
            Value = appearance.OpacityPercent, Increment = 5 };
        opacity.ValueChanged += (_, _) => { appearance.OpacityPercent = (int)opacity.Value; SaveAppearance(); };
        _fields.Controls.Add(opacity);
        _fields.Controls.Add(new Label { Text = UiLanguage.T("Толщина связи (1–12)"), Width = 250, Height = 22 });
        var thickness = new NumericUpDown { Width = 250, Minimum = 1, Maximum = 12,
            DecimalPlaces = 1, Increment = .5m, Value = appearance.Thickness };
        thickness.ValueChanged += (_, _) => { appearance.Thickness = thickness.Value; SaveAppearance(); };
        _fields.Controls.Add(thickness);
        void AddDistance(string label, int value, Action<int> change)
        {
            _fields.Controls.Add(new Label { Text = UiLanguage.T(label), Width = 250, Height = 42 });
            var distance = new NumericUpDown { Width = 250, Minimum = 0, Maximum = 200,
                Increment = 2, Value = value };
            distance.ValueChanged += (_, _) => { change((int)distance.Value); SaveAppearance(); };
            _fields.Controls.Add(distance);
        }
        AddDistance("Размер наконечника (4–48)", appearance.EndSize,
            value => appearance.EndSize = value);
        AddDistance("Прямой участок от выхода (0–200)", appearance.ExitDistance,
            value => appearance.ExitDistance = value);
        AddDistance("Прямой участок перед входом (0–200)", appearance.EntryDistance,
            value => appearance.EntryDistance = value);
        AddDistance("Радиус изгиба у выхода (0–200)", appearance.ExitRadius,
            value => appearance.ExitRadius = value);
        AddDistance("Радиус изгиба у входа (0–200)", appearance.EntryRadius,
            value => appearance.EntryRadius = value);
        var reset = new Button { Text = UiLanguage.T("Сбросить оформление связи"), Width = 250, Height = 34 };
        reset.Click += (_, _) =>
        {
            source.LinkStyles.Remove(port);
            _graph.Invalidate();
            ShowLinkProperties(source, port, target);
        };
        _fields.Controls.Add(reset);
        var delete = new Button { Text = UiLanguage.T("Удалить связь"), Width = 250, Height = 36 };
        delete.Click += (_, _) => DeleteSelected();
        _fields.Controls.Add(delete);
    }

    private void ShowProperties(BlockNode? node)
    {
        _propertyHelpCancel?.Cancel();
        _propertyHelpCancel = null;
        _selected = node;
        _fields.Controls.Clear();
        if (node == null)
        {
            _selection.Text = UiLanguage.T("Свойства блока");
            _fields.Controls.Add(new Label { Text = UiLanguage.T("Выберите блок на поле."), Width = 250, Height = 50 });
            return;
        }
        var spec = BlockCatalog.Get(node.Kind);
        _selection.Text = UiLanguage.T(spec.Title);
        var helpText = UiLanguage.Code == "ru" ? AppHelp.For(node.Kind)
            : UiLanguage.TOr(spec.Hint, "Настройте параметры блока ниже.");
        var help = new Label { Text = helpText, Width = 250, AutoSize = true,
            MaximumSize = new Size(250, 0), ForeColor = Color.FromArgb(65, 75, 92),
            Margin = new Padding(0, 4, 0, 8) };
        _fields.Controls.Add(help);
        if (UiLanguage.Code != "ru")
        {
            _propertyHelpCancel = new CancellationTokenSource();
            _ = TranslatePropertyHelpAsync(help, node.Id, spec.Kind, _propertyHelpCancel);
        }
        var trace = new CheckBox { Text = UiLanguage.T("Записать действия в журнал"), Width = 250,
            Height = 28, Checked = node.LogActions, Margin = new Padding(0, 4, 0, 8) };
        trace.CheckedChanged += (_, _) => { node.LogActions = trace.Checked; _graph.Invalidate(); };
        _fields.Controls.Add(trace);
        var breakpoint = new CheckBox { Text = UiLanguage.T("Точка останова перед блоком"),
            Width = 250, Height = 28, Checked = node.Breakpoint };
        breakpoint.CheckedChanged += (_, _) =>
        { node.Breakpoint = breakpoint.Checked; _graph.Invalidate(); };
        _fields.Controls.Add(breakpoint);
        if (node.Kind != "Description")
        {
            _fields.Controls.Add(new Label { Text = UiLanguage.T("При ошибке блока"), Width = 250, Height = 23 });
            var errorPolicies = new[] { BlockErrorPolicy.Stop, BlockErrorPolicy.Skip, BlockErrorPolicy.Jump };
            var errorChoice = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            errorChoice.Items.AddRange(errorPolicies.Select(UiLanguage.T).Cast<object>().ToArray());
            errorChoice.SelectedIndex = Math.Max(0, Array.IndexOf(errorPolicies,
                node.Value("__ErrorPolicy", BlockErrorPolicy.Stop)));
            errorChoice.SelectedIndexChanged += (_, _) =>
                node.Values["__ErrorPolicy"] = errorPolicies[errorChoice.SelectedIndex];
            _fields.Controls.Add(errorChoice);
            _fields.Controls.Add(new Label { Text = UiLanguage.T("Повторов перед решением (0–10)"), Width = 250, Height = 23 });
            var errorRetries = new NumericUpDown { Width = 250, Minimum = 0, Maximum = 10,
                Value = Math.Clamp(node.Int("__ErrorRetries", 0), 0, 10) };
            errorRetries.ValueChanged += (_, _) =>
                node.Values["__ErrorRetries"] = ((int)errorRetries.Value).ToString();
            _fields.Controls.Add(errorRetries);
            _fields.Controls.Add(new Label { Text = UiLanguage.T("Задержка повтора, мс (0–60000)"), Width = 250, Height = 23 });
            var errorDelay = new NumericUpDown { Width = 250, Minimum = 0, Maximum = 60000,
                Value = Math.Clamp(node.Int("__ErrorDelayMs", 0), 0, 60000) };
            errorDelay.ValueChanged += (_, _) =>
                node.Values["__ErrorDelayMs"] = ((int)errorDelay.Value).ToString();
            _fields.Controls.Add(errorDelay);
            _fields.Controls.Add(new Label { Text = UiLanguage.T("Цель перехода при ошибке"), Width = 250, Height = 23 });
            var targets = _project.Nodes.Where(item => item.Id != node.Id && item.Kind != "Description").ToArray();
            var errorTarget = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            errorTarget.Items.Add(UiLanguage.T("Не выбрано"));
            errorTarget.Items.AddRange(targets.Select(item =>
                UiLanguage.T(BlockCatalog.Get(item.Kind).Title) + " [" + item.Id.ToString("N")[..8] + "]").Cast<object>().ToArray());
            errorTarget.SelectedIndex = Math.Max(0, Array.FindIndex(targets,
                item => item.Id.ToString() == node.Value("__ErrorTarget")) + 1);
            errorTarget.SelectedIndexChanged += (_, _) =>
                node.Values["__ErrorTarget"] = errorTarget.SelectedIndex == 0 ? "" :
                    targets[errorTarget.SelectedIndex - 1].Id.ToString();
            _fields.Controls.Add(errorTarget);
        }
        var appearanceButton = new Button { Text = UiLanguage.T("Оформление блока…"),
            Width = 250, Height = 36, Margin = new Padding(0, 2, 0, 8) };
        appearanceButton.Click += (_, _) =>
        {
            using var editor = new BlockAppearanceForm(node);
            if (editor.ShowDialog(_editorWindow ?? this) != DialogResult.OK) return;
            if (editor.SaveAsDefault)
            {
                try { BlockAppearanceManager.SaveDefault(node.Kind, editor.Appearance, _settings); }
                catch (Exception error)
                {
                    MessageBox.Show(_editorWindow ?? this, error.Message,
                        UiLanguage.T("Оформление блока"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                node.Appearance = null;
            }
            else node.Appearance = editor.Appearance;
            ExpandCanvas();
            ShowProperties(node);
            _graph.Invalidate();
        };
        _fields.Controls.Add(appearanceButton);
        foreach (var field in spec.Fields)
        {
            _fields.Controls.Add(new Label { Text = UiLanguage.Has(field.Label)
                ? UiLanguage.T(field.Label) : field.Key, Width = 255, Height = 22, Margin = new Padding(0, 6, 0, 0) });
            if (node.Kind == "Description" && field.Key is ("Width" or "Height" or "OpacityPercent"))
            {
                var bounds = DescriptionGeometry.Bounds(node);
                var minimum = field.Key == "Width" ? DescriptionGeometry.MinWidth
                    : field.Key == "Height" ? DescriptionGeometry.MinHeight : 0;
                var maximum = field.Key == "OpacityPercent" ? 100 : DescriptionGeometry.MaxSize;
                var value = field.Key == "Width" ? bounds.Width
                    : field.Key == "Height" ? bounds.Height : DescriptionGeometry.Opacity(node);
                var number = new NumericUpDown { Width = 250, Minimum = minimum, Maximum = maximum,
                    Value = value, Increment = field.Key == "OpacityPercent" ? 5 : 10 };
                number.ValueChanged += (_, _) =>
                {
                    node.Values[field.Key] = decimal.ToInt32(number.Value).ToString();
                    if (field.Key != "OpacityPercent") ExpandCanvas();
                    _graph.Invalidate();
                };
                _fields.Controls.Add(number);
                continue;
            }
            if (field.Options is { Length: > 0 })
            {
                var combo = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
                var options = field.Options.Select(value => new LocalizedOption(value,
                    node.Kind == "ChoiceDialog" && field.Key == "Buttons" && int.TryParse(value, out var code)
                        ? ChoiceDialogCatalog.SetLabel(code)
                        : node.Kind == "ChoiceDialog" && field.Key == "Icon" && int.TryParse(value, out var iconCode)
                            ? ChoiceDialogCatalog.IconLabel(iconCode) : null)).ToArray();
                combo.Items.AddRange(options);
                combo.SelectedItem = options.FirstOrDefault(option => option.Value == node.Value(field.Key, field.Default));
                combo.SelectedIndexChanged += (_, _) =>
                {
                    node.Values[field.Key] = (combo.SelectedItem as LocalizedOption)?.Value ?? field.Default;
                    _graph.Invalidate();
                };
                _fields.Controls.Add(combo);
                if (node.Kind == "ChoiceDialog" && field.Key == "Buttons")
                {
                    var guide = new Label { Width = 250, AutoSize = true,
                        MaximumSize = new Size(250, 0), ForeColor = Color.FromArgb(44, 99, 137) };
                    void RefreshGuide() => guide.Text = int.TryParse(node.Value("Buttons"), out var selected)
                        && selected is >= 0 and <= 5 ? ChoiceDialogCatalog.ResultGuide(selected) : "";
                    combo.SelectedIndexChanged += (_, _) => RefreshGuide();
                    RefreshGuide();
                    _fields.Controls.Add(guide);
                }
            }
            else
            {
                var multiLine = MultilineFieldText.IsMultiline(node.Kind, field.Key);
                var savedValue = node.Value(field.Key, field.Default);
                var displayValue = node.Kind == "Description" && field.Key == "Title" &&
                    savedValue == "Описание сценария" ? UiLanguage.T(savedValue) : savedValue;
                var box = new TextBox { Width = 250, Height = multiLine ? 126 : 24,
                    Multiline = multiLine, ScrollBars = multiLine ? ScrollBars.Vertical : ScrollBars.None,
                    Text = multiLine ? MultilineFieldText.ForDisplay(displayValue) : displayValue };
                box.TextChanged += (_, _) =>
                {
                    node.Values[field.Key] = multiLine ? MultilineFieldText.ForStorage(box.Text) : box.Text;
                    if (VariableNameRules.IsIdentifierField(node.Kind, field) && field.Key != "Reference")
                        box.BackColor = box.Text.Length == 0 || VariableNameRules.IsAscii(box.Text)
                            ? Color.White : Color.MistyRose;
                    _graph.Invalidate();
                };
                if (VariableNameRules.IsIdentifierField(node.Kind, field) && field.Key != "Reference" &&
                    box.Text.Length > 0 && !VariableNameRules.IsAscii(box.Text))
                    box.BackColor = Color.MistyRose;
                _fields.Controls.Add(box);
                if (node.Kind == "RadioDialog" && field.Key == "Options")
                    _fields.Controls.Add(new Label
                    {
                        Text = UiLanguage.T("Один вариант на строке. В коде перенос показан как \\n."),
                        Width = 250, AutoSize = true, MaximumSize = new Size(250, 0),
                        ForeColor = Color.FromArgb(65, 99, 140), Margin = new Padding(0, 2, 0, 6)
                    });
                if (node.Kind == "RunFile" && field.Key == "Path")
                {
                    var selectProgram = new Button { Text = UiLanguage.T("Выбрать программу или файл…"),
                        Width = 250, Height = 34 };
                    selectProgram.Click += (_, _) =>
                    {
                        using var picker = new ProgramPickerForm();
                        if (picker.ShowDialog(_editorWindow ?? this) == DialogResult.OK)
                            box.Text = picker.SelectedPath;
                    };
                    _fields.Controls.Add(selectProgram);
                }
                if (node.Kind == "RunScenario" && field.Key == "Path")
                {
                    var selectScenario = new Button { Text = UiLanguage.T("Выбрать сценарий…"),
                        Width = 250, Height = 34 };
                    selectScenario.Click += (_, _) =>
                    {
                        using var picker = new OpenFileDialog
                        {
                            Filter = "Kliker Blocks (*.klb)|*.klb", CheckFileExists = true,
                            InitialDirectory = _file is null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file)
                        };
                        if (picker.ShowDialog(_editorWindow ?? this) == DialogResult.OK)
                            box.Text = picker.FileName;
                    };
                    _fields.Controls.Add(selectScenario);
                }
                if (node.Kind == "SpeakText" && field.Key == "Voice")
                {
                    var selectVoice = new Button { Text = UiLanguage.T("Доступные голоса Windows…"),
                        Width = 250, Height = 34 };
                    selectVoice.Click += (_, _) =>
                    {
                        try
                        {
                            using var picker = new VoiceSelectionForm(WindowsSpeech.InstalledVoices(), box.Text);
                            if (picker.ShowDialog(this) == DialogResult.OK) box.Text = picker.SelectedVoice;
                        }
                        catch (Exception error)
                        {
                            MessageBox.Show(this, error.Message, UiLanguage.T("Озвучить текст"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    };
                    _fields.Controls.Add(selectVoice);
                }
                if (node.Kind == "Description" && field.Key == "FillColor")
                {
                    var choose = new Button { Text = UiLanguage.T("Выбрать цвет…"), Width = 250, Height = 34 };
                    choose.Click += (_, _) =>
                    {
                        using var picker = new ColorDialog { Color = DescriptionGeometry.FillColor(node), FullOpen = true };
                        if (picker.ShowDialog(_editorWindow ?? this) == DialogResult.OK)
                            box.Text = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
                    };
                    _fields.Controls.Add(choose);
                }
                if (node.Kind == "VirtualKey" && field.Key == "VirtualKeyCode")
                {
                    var description = new Label { Width = 250, Height = 44, ForeColor = Color.FromArgb(44, 99, 137) };
                    void RefreshCode() => description.Text = int.TryParse(box.Text, out var code) && code is >= 0 and <= 255
                        ? $"{code} = {VirtualKeyCatalog.DescribeLocalized(code)}"
                        : UiLanguage.T("Введите код от 0 до 255");
                    box.TextChanged += (_, _) => RefreshCode();
                    RefreshCode();
                    _fields.Controls.Add(description);
                }
                if (field.Key is "Key" or "ActivationKey" or "RequiredKey" or "VirtualKeyCode")
                {
                    var capture = new Button { Text = UiLanguage.T("Назначить нажатием…"), Width = 250, Height = 34 };
                    capture.Click += (_, _) =>
                    {
                        var allowMouse = node.Kind is "IfKeyDown" or "WatchCursor";
                        var code = CaptureBlockKey(allowMouse);
                        if (code is null) return;
                        box.Text = field.Key == "VirtualKeyCode" ? code.Value.ToString() : VirtualKeyCatalog.InputName(code.Value, allowMouse);
                    };
                    _fields.Controls.Add(capture);
                }
            }
        }
        if (node.Kind == "OcrImage")
        {
            var installOcr = new Button
            {
                Text = UiLanguage.Code == "en" ? "Install Tesseract OCR…" : "Установить Tesseract OCR…",
                Width = 250, Height = 36, Margin = new Padding(0, 10, 0, 0)
            };
            installOcr.Click += (_, _) =>
            {
                using var setup = new OcrSetupForm(node.Value("Language"));
                if (setup.ShowDialog(_editorWindow ?? this) != DialogResult.OK) return;
                node.Values["EnginePath"] = setup.EnginePath;
                ShowProperties(node);
                _graph.Invalidate();
            };
            _fields.Controls.Add(installOcr);
        }
        if (node.Kind is "UiWait" or "UiAction")
        {
            var captureElement = new Button { Text = UiLanguage.T("Захватить элемент под указателем через 3 секунды…"),
                Width = 250, Height = 42, Margin = new Padding(0, 10, 0, 0) };
            captureElement.Click += async (_, _) =>
            {
                captureElement.Enabled = false;
                try
                {
                    await Task.Delay(3000);
                    var position = Control.MousePosition;
                    var element = await Task.Run(() => UiAutomationTools.Inspect(position));
                    if (!IsDisposed && _selected?.Id == node.Id)
                    {
                        node.Values["WindowTitle"] = element.WindowTitle;
                        node.Values["AutomationId"] = element.AutomationId;
                        node.Values["ElementName"] = element.AutomationId.Length == 0 ? element.Name : "";
                        node.Values["ControlType"] = element.ControlType;
                        ShowProperties(node);
                    }
                }
                catch (Exception error) when (error is InvalidOperationException or
                    System.Windows.Automation.ElementNotAvailableException or
                    System.Runtime.InteropServices.COMException)
                {
                    MessageBox.Show(_editorWindow ?? this, error.Message,
                        UiLanguage.T("Захват элемента Windows"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { if (!captureElement.IsDisposed) captureElement.Enabled = true; }
            };
            _fields.Controls.Add(captureElement);
        }
        if (node.Kind is "TranslateText" or "TranslateArray" or "LanguagePair")
        {
            var installOffline = new Button
            {
                Text = UiLanguage.Code == "en" ? "Download offline language pair…" : "Скачать офлайн-пару языков…",
                Width = 250, Height = 36, Margin = new Padding(0, 10, 0, 0)
            };
            installOffline.Click += (_, _) =>
            {
                using var setup = new OfflineTranslationSetupForm(node.Value("Source"), node.Value("Target"));
                if (setup.ShowDialog(_editorWindow ?? this) != DialogResult.OK) return;
                _settings.ArgosPythonPath = setup.InstalledPythonPath;
                try { _settings.Save(); }
                catch (Exception error)
                {
                    MessageBox.Show(_editorWindow ?? this, error.Message,
                        UiLanguage.Code == "en" ? "Could not save translation settings" : "Не удалось сохранить настройки перевода",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                node.Values["Engine"] = "Argos локально";
                ShowProperties(node);
                _graph.Invalidate();
            };
            _fields.Controls.Add(installOffline);
        }
        if (spec.Fields.Length == 0)
            _fields.Controls.Add(new Label { Text = UiLanguage.TOr(spec.Hint, "Настройте параметры блока ниже."), Width = 250, Height = 60 });
        if (spec.Fields.Any(field => field.Key is "Key" or "ActivationKey" or "RequiredKey" or "VirtualKeyCode"))
        {
            var codes = new Button { Text = UiLanguage.T("Таблица кодов клавиш VK…"), Width = 250, Height = 36 };
            codes.Click += (_, _) => new VirtualKeyHelpForm().Show(this);
            _fields.Controls.Add(codes);
        }
        if (spec.Outputs.Length > 0)
        {
            _fields.Controls.Add(new Label { Text = UiLanguage.T("СВЯЗИ"), Width = 250, Height = 28, Margin = new Padding(0, 16, 0, 0), Font = new Font(Font, FontStyle.Bold) });
            foreach (var output in spec.Outputs)
            {
                var target = node.Link(output);
                var button = new Button { Width = 250, Height = 32,
                    Text = UiLanguage.T(output) + ": " + UiLanguage.T(target == null ? "свободно" : "отключить") };
                button.Click += (_, _) => { node.Links[output] = null; ShowProperties(node); _graph.Invalidate(); };
                _fields.Controls.Add(button);
            }
        }
    }

    private async Task TranslatePropertyHelpAsync(Label label, Guid blockId, string kind, CancellationTokenSource request)
    {
        try
        {
            if (UiLanguage.Code is "fr" or "es" or "zh" &&
                LocalizedHelp.Block(kind, UiLanguage.Code) is { } summary && !label.IsDisposed)
                label.Text = summary;
            var translated = UiLanguage.Code == "en" ? EnglishHelp.Block(kind) ?? AppHelp.For(kind) :
                await HelpTranslationService.TranslateAsync(AppHelp.For(kind), UiLanguage.Code,
                _settings.ArgosPythonPath, request.Token);
            if (!request.IsCancellationRequested && !IsDisposed && !label.IsDisposed && _selected?.Id == blockId)
                label.Text = translated;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { /* The short localized block hint remains visible without a ru→target model. */ }
        finally
        {
            if (ReferenceEquals(_propertyHelpCancel, request)) _propertyHelpCancel = null;
            request.Dispose();
        }
    }

    private int? CaptureBlockKey(bool allowMouse)
    {
        // RegisterHotKey consumes a key even while its owner has a modal dialog.
        // Free this instance's configurable keys while the dialog listens for one press.
        var pointWasRegistered = _coordinateHotkeyRegistered;
        var screenshotWasRegistered = _screenshotHotkeyRegistered;
        var indicatorWasRegistered = _indicatorHotkeyRegistered;
        if (pointWasRegistered) NativeMouse.ReleaseUserHotKey(Handle, 3);
        if (screenshotWasRegistered) NativeMouse.ReleaseUserHotKey(Handle, 4);
        if (indicatorWasRegistered) NativeMouse.ReleaseUserHotKey(Handle, 6);
        _coordinateHotkeyRegistered = false;
        _screenshotHotkeyRegistered = false;
        _indicatorHotkeyRegistered = false;
        try
        {
            using var dialog = new KeyCaptureForm(allowMouse);
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.CapturedCode : null;
        }
        finally
        {
            if (pointWasRegistered)
            {
                var key = HotkeyParser.Parse(_settings.CoordinateHotkey);
                _coordinateHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 3, key.Modifiers, key.Key);
            }
            if (screenshotWasRegistered)
            {
                var key = HotkeyParser.Parse(_settings.ScreenshotHotkey);
                _screenshotHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 4, key.Modifiers, key.Key);
            }
            if (indicatorWasRegistered)
            {
                var key = HotkeyParser.Parse(_project.IndicatorToggleHotkey);
                _indicatorHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 6, key.Modifiers, key.Key);
            }
            if ((pointWasRegistered && !_coordinateHotkeyRegistered) ||
                (screenshotWasRegistered && !_screenshotHotkeyRegistered) ||
                (indicatorWasRegistered && !_indicatorHotkeyRegistered))
                AppendLog("Не удалось восстановить часть горячих клавиш после назначения. Проверьте их в настройках.");
        }
    }

    private async Task ResumeFromCheckpoint()
    {
        if (_cancel is not null || !TryApplyGraphCode()) return;
        using var dialog = new OpenFileDialog { Filter = UiLanguage.T("Контрольная точка JSON (*.json)|*.json"),
            InitialDirectory = _file is null ? ProjectCompatibility.UserProjectDirectory : Path.GetDirectoryName(_file) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await RunProject(CheckpointTools.Load(dialog.FileName, _project)); }
        catch (Exception error) { MessageBox.Show(this, error.Message,
            UiLanguage.T("Продолжить с контрольной точки"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private async Task RunProject(CheckpointState? resume = null)
    {
        if (_cancel != null) return;
        if (_showcaseWindow?.IsRunning == true)
        {
            MessageBox.Show(this, UiLanguage.T("Сначала остановите демонстрацию «Что я умею»."),
                UiLanguage.T("Что я умею"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!TryApplyGraphCode()) return;
        var snapshot = _project.Copy();
        if (snapshot.Nodes.Count(n => n.Kind == "Start") != 1)
        {
            MessageBox.Show("В проекте должен быть ровно один блок «Старт».");
            return;
        }
        if (!CheckAsciiNames(snapshot)) return;
        if (snapshot.Nodes.Any(node => node.Kind == "ShowcaseStep") &&
            MessageBox.Show(this, UiLanguage.T(
                "Демонстрационный граф откроет Блокнот и Paint, изменит буфер обмена и создаст файлы демонстрации. Продолжить?"),
                UiLanguage.T("Что я умею"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var numericFields = new HashSet<string>
        {
            "AnchorX", "AnchorY", "ThresholdPx", "PollMs", "CooldownMs", "DurationMs",
            "TargetX", "TargetY", "OffsetX", "OffsetY", "DeltaX", "DeltaY", "Count", "Steps", "Milliseconds",
            "Times", "PixelX", "PixelY", "Tolerance", "Left", "Top", "Right", "Bottom", "TimeoutMs",
            "WindowX", "WindowY", "WindowWidth", "WindowHeight", "VirtualKeyCode", "MaxMs"
        };
        foreach (var node in snapshot.Nodes)
        {
            var spec = BlockCatalog.Get(node.Kind);
            foreach (var field in spec.Fields.Where(f => numericFields.Contains(f.Key) &&
                !VariableNameRules.IsIdentifierField(node.Kind, f)))
            {
                if (DialogWindowOptions.IsOptionalNumericField(node.Kind, field.Key, node.Value(field.Key))) continue;
                if (int.TryParse(node.Value(field.Key), out _) || VariableStore.HasReference(node.Value(field.Key))) continue;
                MessageBox.Show(UiLanguage.Format("В блоке «{0}» поле «{1}» должно содержать целое число.",
                    UiLanguage.T(spec.Title), UiLanguage.T(field.Label)));
                return;
            }
            if (node.Kind is "InputDialog" or "ChoiceDialog" or "RadioDialog" &&
                string.IsNullOrWhiteSpace(node.Value("WindowX")) !=
                string.IsNullOrWhiteSpace(node.Value("WindowY")))
            {
                MessageBox.Show(UiLanguage.T("Для позиции окна укажите целые X и Y вместе либо оставьте оба поля пустыми."));
                return;
            }
        }
        if (_launch is null && snapshot.Nodes.Any(node => node.Kind == "WatchCursor" &&
                node.Value("ActivationMode", "Переключатель") == "Сразу") &&
            MessageBox.Show(this,
                "В сценарии есть блок «Следить за курсором» с активацией «Сразу». Он может перемещать мышь без нажатия F9. Если нужен переключатель F9 + ЛКМ, измените свойства блока перед запуском.\n\nВсё равно запустить?",
                "Слежение без переключателя", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        var scopeError = WindowScopeGuard.Validate(snapshot.WindowScope ?? new WindowScopeSettings());
        if (scopeError is not null)
        {
            MessageBox.Show(this, scopeError, "Привязка к окну", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _cancel = new CancellationTokenSource();
        _indicatorHiddenByHotkey = false;
        var runVersion = ++_runVersion;
        _watchStates.Clear();
        _conditionStates.Clear();
        _conditionLabels.Clear();
        foreach (var condition in snapshot.Nodes.Where(node => IndicatorConditionMode.Supports(node.Kind) &&
            IndicatorConditionMode.Resolve(node.Value("IndicatorMode", IndicatorConditionMode.DefaultFor(node.Kind)), false) is not null))
        {
            _conditionStates[condition.Id] = false;
            _conditionLabels[condition.Id] = condition.Kind == "IfKeyDown" ? condition.Value("Key") : BlockCatalog.Get(condition.Kind).Title;
        }
        _runButton.Enabled = false;
        _pauseButton.Enabled = true;
        _stopButton.Enabled = true;
        _status.Text = UiLanguage.T("Сценарий выполняется");
        UpdateControlSurfaces();
        if (snapshot.WriteJournalToFile)
        {
            try
            {
                var writer = new ScriptLogWriter(_file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file)!);
                if (!JournalSettingsForm.IsValidJournalName(snapshot.JournalFileName, _file))
                    throw new InvalidOperationException("Укажите безопасное имя файла журнала, отличное от имени проекта.");
                writer.Configure(snapshot.JournalFileName);
                _journalWriter = writer;
                AppendLog(UiLanguage.Format("Файл журнала: {0}", writer.PathOnDisk));
            }
            catch (Exception error)
            {
                _cancel?.Dispose(); _cancel = null;
                _runButton.Enabled = true; _pauseButton.Enabled = false; _stopButton.Enabled = false;
                _status.Text = UiLanguage.T("Готово");
                UpdateControlSurfaces();
                MessageBox.Show(this, error.Message, "Запись журнала", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        AppendLog(UiLanguage.Format("Запуск сценария — {0}", UiLanguage.T(snapshot.RunMode)));
        ShowcaseSession? showcaseSession = null;
        try
        {
            if (_project.ShowIndicator) ShowIndicator();
            if (snapshot.Nodes.Any(node => node.Kind == "ShowcaseStep"))
                showcaseSession = new ShowcaseSession(this, AppendRunnerLog);
            _runner = new Runner(snapshot,
                AppendRunnerLog,
                (dx, dy) => UI(() => _status.Text = UiLanguage.Format("Смещение курсора: ΔX={0}, ΔY={1}", dx, dy)),
                _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file),
                (id, state) => UI(() => UpdateWatchIndicator(runVersion, id, state)),
                conditionStatus: (id, selected) => UI(() => UpdateConditionIndicator(runVersion, id, selected)),
                clearLog: () => UI(() => _log.Items.Clear()),
                showLog: visible => UI(() => SetLogVisible(visible)),
                inputDialog: (message, value, delay, window, cancel) => OnUiAsync(() => DialogTools.Input(this, message, value, delay, cancel, options: window)),
                choiceDialog: (message, buttons, icon, window, cancel) => OnUiAsync(() => DialogTools.Choice(this, message, buttons, icon, cancel, options: window)),
                radioDialog: (message, options, window, cancel) => OnUiAsync(() => DialogTools.Radio(this, message, options, cancel, window: window)),
                hintPopup: (message, title) => UI(() => ShowHintPopup(message, title)),
                messageInbox: _messageInbox, selfHandle: Handle, argosPythonPath: _settings.ArgosPythonPath,
                showcaseStep: showcaseSession is null ? null :
                    (action, variables, cancel) => showcaseSession.ExecuteAsync(action, variables, cancel),
                debugState: (id, variables, paused) => UI(() => UpdateDebugState(id, variables, paused)),
                ensureOcr: EnsureOcrAsync);
            await _runner.RunAsync(_cancel.Token, resume);
            AppendLog("Сценарий завершён");
        }
        catch (OperationCanceledException) { AppendLog("Остановлено пользователем"); }
        catch (Exception ex) { AppendLog(UiLanguage.T("Ошибка: ") + ex.Message);
            MessageBox.Show(ex.Message, UiLanguage.T("Ошибка сценария")); }
        finally
        {
            showcaseSession?.Dispose();
            ++_runVersion;
            _watchStates.Clear();
            _conditionStates.Clear();
            _conditionLabels.Clear();
            CloseIndicator();
            _indicatorHiddenByHotkey = false;
            _cancel.Dispose();
            _cancel = null;
            _runner = null;
            _graph.ActiveDebugNode = null;
            _debugStepButton.Enabled = false;
            _debugWindow?.Close();
            _debugWindow = null;
            _journalWriter = null;
            _runButton.Enabled = true;
            _pauseButton.Enabled = false;
            _pauseButton.Text = UiLanguage.T("⏸ Пауза");
            _stopButton.Enabled = false;
            _status.Text = UiLanguage.T("Готово");
            UpdateControlSurfaces();
        }
    }

    private void UpdateWatchIndicator(int runVersion, Guid id, CursorWatchState? state)
    {
        if (runVersion != _runVersion) return;
        if (state is null) _watchStates.Remove(id);
        else _watchStates[id] = state;
        RefreshIndicator();
    }

    private void UpdateConditionIndicator(int runVersion, Guid id, bool selected)
    {
        if (runVersion != _runVersion || !_conditionStates.ContainsKey(id)) return;
        _conditionStates[id] = selected;
        RefreshIndicator();
    }

    private void ShowIndicator()
    {
        if (_indicator is not null) return;
        _indicator = new ScenarioIndicatorForm();
        _indicator.SetAppearance(_project.IndicatorAppearance, _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file));
        _indicator.SetPlacement(_project);
        RefreshIndicator();
        if (!_indicatorHiddenByHotkey) _indicator.Show();
    }

    private void ConfigureIndicatorAppearance()
    {
        using var dialog = new IndicatorAppearanceForm(_project.IndicatorAppearance, _project.IndicatorToggleHotkey,
            _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var previousHotkey = _project.IndicatorToggleHotkey;
        var changed = !string.Equals(previousHotkey, dialog.ToggleHotkey, StringComparison.OrdinalIgnoreCase);
        if (changed && IsHandleCreated)
        {
            if (_indicatorHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 6);
            _indicatorHotkeyRegistered = false;
            var key = HotkeyParser.Parse(dialog.ToggleHotkey);
            _indicatorHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 6, key.Modifiers, key.Key);
            if (!_indicatorHotkeyRegistered)
            {
                var oldKey = HotkeyParser.Parse(previousHotkey);
                _indicatorHotkeyRegistered = NativeMouse.RegisterUserHotKey(Handle, 6, oldKey.Modifiers, oldKey.Key);
                MessageBox.Show(this, "Новая комбинация занята другим окном или программой. Оформление не изменено; прежняя клавиша восстановлена, если доступна.",
                    "Горячая клавиша", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        _project.IndicatorToggleHotkey = dialog.ToggleHotkey;
        _project.IndicatorAppearance = dialog.Appearance;
        if (_indicator is not null)
        {
            _indicator.SetAppearance(dialog.Appearance, _file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file));
            _indicator.SetPlacement(_project);
            RefreshIndicator();
        }
        AppendLog($"Оформление индикатора обновлено. Показать/скрыть: {dialog.ToggleHotkey}. Сохраните проект.");
    }

    private void ConfigureIndicatorPosition()
    {
        using var dialog = new IndicatorSettingsForm(_project, _indicator?.Location);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.UseCustomIndicatorPosition = dialog.UseCustomPosition;
        _project.IndicatorX = dialog.IndicatorX;
        _project.IndicatorY = dialog.IndicatorY;
        _indicator?.SetPlacement(_project);
        AppendLog(dialog.UseCustomPosition
            ? $"Индикатор закреплён на X:{dialog.IndicatorX} Y:{dialog.IndicatorY}. Сохраните проект."
            : "Индикатор: автоматическое размещение. Сохраните проект.");
    }

    private void CloseIndicator()
    {
        _indicator?.Close();
        _indicator?.Dispose();
        _indicator = null;
    }

    private void RefreshIndicator()
    {
        if (_indicator is null || _indicator.IsDisposed) return;
        var displayed = _watchStates.Values.FirstOrDefault(value => value.Active)
            ?? _watchStates.Values.FirstOrDefault(value => value.Armed)
            ?? _watchStates.Values.FirstOrDefault();
        if (_conditionStates.Count > 0)
        {
            var keys = string.Join(" + ", _conditionLabels.Values.Distinct(StringComparer.OrdinalIgnoreCase));
            var active = _conditionStates.Values.All(value => value) && (displayed?.Armed ?? true);
            var detail = displayed is null ? keys : $"{keys} + {displayed.ActivationKey}";
            _indicator.SetConditions(active, $"{detail}: {(active ? "условия выполнены" : "ожидание условий")}");
        }
        else if (displayed is not null) _indicator.SetWatch(displayed);
        else _indicator.SetRunning();
    }

    private void UI(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(action);
    }

    private Task<T> OnUiAsync<T>(Func<T> action)
    {
        if (IsDisposed || !IsHandleCreated)
            return Task.FromException<T>(new ObjectDisposedException(nameof(MainForm)));
        if (!InvokeRequired)
        {
            try { return Task.FromResult(action()); }
            catch (Exception error) { return Task.FromException<T>(error); }
        }
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke(() =>
            {
                try { completion.TrySetResult(action()); }
                catch (Exception error) { completion.TrySetException(error); }
            });
        }
        catch (InvalidOperationException error) { completion.TrySetException(error); }
        return completion.Task;
    }

    private Task<string?> EnsureOcrAsync(string languages, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        return OnUiAsync(() =>
        {
            using var setup = new OcrSetupForm(languages);
            return setup.ShowDialog(this) == DialogResult.OK ? setup.EnginePath : null;
        });
    }

    private void AppendLog(string message)
    {
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {UiLanguage.T(message)}";
        WriteJournalEntry(entry);
        ShowLogEntry(entry);
    }

    private void AppendRunnerLog(string message)
    {
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {UiLanguage.T(message)}";
        WriteJournalEntry(entry);
        UI(() => ShowLogEntry(entry));
    }

    private void WriteJournalEntry(string entry)
    {
        var writer = _journalWriter;
        if (writer is null) return;
        try { writer.Write(entry); }
        catch (Exception error)
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _journalWriter, null, writer), writer))
                UI(() => ShowLogEntry($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  Запись журнала в файл отключена: {error.Message}"));
        }
    }

    private void ShowLogEntry(string entry)
    {
        if (_log.Items.Count >= 500) _log.Items.RemoveAt(0);
        _log.Items.Add(entry);
        _log.TopIndex = _log.Items.Count - 1;
    }

    private void SetJournalWrap(bool enabled)
    {
        if (_journalWrapEnabled == enabled) return;
        _journalWrapEnabled = enabled;
        if (_journalWrapMain.Checked != enabled) _journalWrapMain.Checked = enabled;
        if (_journalWrapDetached is { } detached && detached.Checked != enabled) detached.Checked = enabled;
        RefreshJournalLayout();
        _settings.JournalWordWrap = enabled;
        try { _settings.Save(); }
        catch (Exception error) { AppendLog(UiLanguage.T("Не удалось сохранить настройку переноса журнала: ") + error.Message); }
    }

    private void RefreshJournalLayout()
    {
        if (_log.IsDisposed || _log.Items.Count == 0) return;
        var entries = _log.Items.Cast<object>().ToArray();
        _log.BeginUpdate();
        try
        {
            _log.Items.Clear();
            _log.Items.AddRange(entries);
            _log.TopIndex = Math.Max(0, _log.Items.Count - 1);
        }
        finally { _log.EndUpdate(); }
    }

    private void MeasureJournalItem(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _log.Items.Count) return;
        if (!_journalWrapEnabled) { e.ItemHeight = _log.Font.Height + 4; return; }
        var width = Math.Max(100, _log.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
        var size = TextRenderer.MeasureText(e.Graphics, _log.Items[e.Index]?.ToString() ?? "",
            _log.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        e.ItemHeight = Math.Max(_log.Font.Height + 4, size.Height + 4);
    }

    private void DrawJournalItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _log.Items.Count) return;
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) != 0;
        var text = _log.Items[e.Index]?.ToString() ?? "";
        TextRenderer.DrawText(e.Graphics, text, _log.Font,
            Rectangle.Inflate(e.Bounds, -3, -1), selected ? SystemColors.HighlightText : _log.ForeColor,
            selected ? SystemColors.Highlight : _log.BackColor,
            (_journalWrapEnabled ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine) |
            TextFormatFlags.NoPrefix | TextFormatFlags.Top);
        e.DrawFocusRectangle();
    }

    private void SetLogVisible(bool visible)
    {
        _rootLayout.RowStyles[3].Height = visible ? 160 : 0;
        _log.Parent!.Visible = visible;
        _showLogItem.Checked = visible;
    }

    internal void ShowHintPopup(string message, string title)
    {
        if (_trayIcon is null) return;
        _trayIcon.BalloonTipTitle = string.IsNullOrWhiteSpace(title) ? "Kliker Blocks" : title[..Math.Min(title.Length, 63)];
        _trayIcon.BalloonTipText = message[..Math.Min(message.Length, 255)];
        _trayIcon.ShowBalloonTip(3000);
    }

    private void UpdateWindowTitle()
    {
        var name = _project.ScriptName;
        Text = AppTitle + (string.IsNullOrWhiteSpace(name) ? "" : " — " + name) +
            (_file is null ? " — " + UiLanguage.T("визуальные сценарии") : " — " + Path.GetFileName(_file));
        UpdateControlSurfaces();
    }

    private void ConfigureScriptName()
    {
        using var dialog = new ScriptNameForm(_project.ScriptName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.ScriptName = dialog.ScriptName;
        UpdateWindowTitle();
        AppendLog("Имя сценария изменено. Сохраните проект.");
    }

    private void ConfigureJournal()
    {
        if (_cancel is not null)
        {
            MessageBox.Show(this, "Измените настройки журнала после остановки сценария.");
            return;
        }
        using var dialog = new JournalSettingsForm(_project.WriteJournalToFile, _project.JournalFileName, _file);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _project.WriteJournalToFile = dialog.WriteToFile;
        _project.JournalFileName = dialog.FileName;
        AppendLog(_project.WriteJournalToFile
            ? UiLanguage.Format("Запись журнала включена: {0}. Файл создастся при запуске сценария.",
                Path.Combine(_file == null ? AppContext.BaseDirectory : Path.GetDirectoryName(_file)!, _project.JournalFileName))
            : UiLanguage.T("Запись журнала в файл выключена. Сохраните проект."));
    }

    internal void UpdateShowcaseStatus()
    {
        var firstRun = ShowcaseState.FirstRunUtc;
        _showcaseStatus.Text = firstRun is not null
            ? UiLanguage.Format("Первый запуск демонстрации: {0}", firstRun.Value.ToLocalTime().ToString("g"))
            : ShowcaseState.FirstShownUtc is not null
                ? UiLanguage.T("Демонстрация показана, ещё не запускалась")
                : UiLanguage.T("Демонстрация ещё не открывалась");
    }

    private void TryFirstShowcase()
    {
        if (ShowcaseState.FirstShownUtc is not null) { UpdateShowcaseStatus(); return; }
        try
        {
            ShowShowcase(offerStartOnOpen: true);
            ShowcaseState.MarkFirstShown();
            UpdateShowcaseStatus();
            AppendLog(UiLanguage.T("Первый запуск: открыта демонстрация «Что я умею»."));
        }
        catch (Exception error)
        {
            AppendLog(UiLanguage.Format("Не удалось сохранить отметку первого показа: {0}", error.Message));
            if (_showcaseWindow is null) ShowShowcase();
        }
    }

    private void ShowShowcase(bool offerStartOnOpen = false)
    {
        if (_showcaseWindow is { IsDisposed: false }) { _showcaseWindow.Activate(); return; }
        _showcaseWindow = new ShowcaseForm(offerStartOnOpen);
        _showcaseWindow.FormClosed += (_, _) => _showcaseWindow = null;
        _showcaseWindow.Show(this);
    }

    private void NewProject()
    {
        if (_cancel != null) { MessageBox.Show("Сначала остановите сценарий."); return; }
        if (!TryApplyGraphCode()) return;
        if (_indicatorHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 6);
        _indicatorHotkeyRegistered = false;
        _project = ProjectModel.CreateSample();
        ApplyMouseMappings();
        RefreshRunModeMenu();
        RegisterIndicatorHotkey();
        _showIndicatorItem.Checked = _project.ShowIndicator;
        _file = null;
        _graph.Project = _project;
        _graph.ResetView();
        _graph.Select(null);
        UpdateWindowTitle();
        if (_codeViewActive) LoadGraphCode();
    }

    private void OpenProject()
    {
        if (_cancel != null) { MessageBox.Show("Сначала остановите сценарий."); return; }
        if (!TryApplyGraphCode()) return;
        using var dialog = new OpenFileDialog { Filter = "Проекты Kliker Blocks (*.klb)|*.klb|JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        TryLoadProjectFromPath(dialog.FileName);
    }

    private bool TryLoadProjectFromPath(string path)
    {
        try
        {
            var project = ProjectCompatibility.Read(path);
            _project = project;
            _project.VariableNamesAsciiOnly = true;
            ApplyMouseMappings();
            RefreshRunModeMenu();
            if (_indicatorHotkeyRegistered) NativeMouse.ReleaseUserHotKey(Handle, 6);
            _indicatorHotkeyRegistered = false;
            RegisterIndicatorHotkey();
            _showIndicatorItem.Checked = _project.ShowIndicator;
            _file = path;
            _graph.Project = _project;
            _graph.ResetView();
            ExpandCanvas();
            _graph.Select(null);
            UpdateWindowTitle();
            if (_codeViewActive) LoadGraphCode();
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, UiLanguage.T("Не удалось открыть проект")); return false; }
    }

    private void SaveProject()
    {
        if (!TryApplyGraphCode()) return;
        if (!CheckAsciiNames(_project)) return;
        using var dialog = new SaveFileDialog { Filter = "Проекты Kliker Blocks (*.klb)|*.klb", DefaultExt = "klb",
            InitialDirectory = _file is null ? ProjectCompatibility.UserProjectDirectory : Path.GetDirectoryName(_file),
            FileName = _file == null ? "Мой сценарий.klb" : Path.GetFileName(_file) };
        try { Directory.CreateDirectory(ProjectCompatibility.UserProjectDirectory); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, UiLanguage.T("Не удалось сохранить проект")); return; }
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_project, new JsonSerializerOptions { WriteIndented = true }));
            _file = dialog.FileName;
            UpdateWindowTitle();
            AppendLog("Проект сохранён: " + _file);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Не удалось сохранить проект"); }
    }

    private void ShareProject()
    {
        if (!TryApplyGraphCode() || !CheckAsciiNames(_project)) return;
        var code = GraphCodeCodec.Serialize(_project);
        using var dialog = new ShareGraphForm(_project.ScriptName, code);
        dialog.ShowDialog(this);
    }

    private void CheckProjectCompatibility()
    {
        using var dialog = new OpenFileDialog { Filter = "Проекты Kliker Blocks (*.klb)|*.klb|JSON (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var project = ProjectCompatibility.Read(dialog.FileName);
            MessageBox.Show(this, UiLanguage.Format(
                "Граф совместим с этой версией Kliker Blocks. Формат: {0}; блоков: {1}. Файл не изменён.",
                project.Version, project.Nodes.Count), UiLanguage.T("Проверка совместимости"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error) { MessageBox.Show(this, error.Message,
            UiLanguage.T("Проверка совместимости"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private bool CheckAsciiNames(ProjectModel project)
    {
        var invalid = VariableNameRules.FindInvalid(project);
        if (invalid.Count == 0) return true;
        MessageBox.Show(this, "Исправьте имена переменных, массивов и ссылки на них. " +
            "Разрешены A–Z, a–z, 0–9 и _, первый знак — буква или _.\n\n" +
            string.Join("\n", invalid), "Имена переменных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
}

internal sealed class GraphCanvas : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ProjectModel Project { get; set; } = new();
    private Guid? _activeDebugNode;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Guid? ActiveDebugNode
    {
        get => _activeDebugNode;
        set { _activeDebugNode = value; Invalidate(); }
    }
    public event Action<BlockNode?>? SelectionChanged;
    public event Action<IReadOnlyList<BlockNode>>? SelectionSetChanged;
    public event Action<BlockNode, string, BlockNode>? LinkSelectionChanged;
    public event Action<float>? ZoomChanged;
    public event Action<string>? Message;
    public event Action? ContentResized;
    private BlockNode? _selected;
    private readonly HashSet<Guid> _selectedNodes = [];
    private BlockNode? _dragging;
    private BlockNode? _resizingDescription;
    private Point _resizeStartWorld;
    private Size _resizeStartSize;
    private Point _dragStartWorld;
    private Dictionary<Guid, Point> _dragPositions = [];
    private (Guid Id, string Port)? _pending;
    private (Guid Id, string Port)? _selectedLink;
    private (Guid Id, string Port)? _hoveredLink;
    private string? _hoveredPort;
    private readonly ToolTip _portToolTip = new() { ShowAlways = true, InitialDelay = 250, ReshowDelay = 100, AutoPopDelay = 5000 };
    private Size _worldSize = new(2200, 1500);
    private float _zoom = 1f;
    private float _panX;
    private float _panY;
    private bool _panning;
    private Point _lastPan;
    private bool _selectingArea;
    private Point _selectionStart;
    private Point _selectionEnd;
    private Point _lastPointerClient = new(200, 200);
    private Point? _insertionWorldPoint;

    internal Point? InsertionWorldPoint => _insertionWorldPoint;

    public IReadOnlyList<BlockNode> SelectedNodes => Project.Nodes.Where(node => _selectedNodes.Contains(node.Id)).ToList();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Zoom
    {
        get => _zoom;
        set
        {
            var next = Math.Clamp((float)Math.Round(value, 3), 0.05f, 32f);
            if (Math.Abs(next - _zoom) < 0.001f) return;
            _zoom = next;
            ZoomChanged?.Invoke(_zoom);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PointF PanOffset
    {
        get => new(_panX, _panY);
        set { _panX = value.X; _panY = value.Y; Invalidate(); }
    }

    public GraphCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(247, 249, 252);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = true;
    }

    public void SetWorldSize(int width, int height)
    {
        _worldSize = new Size(width, height);
        Size = _worldSize;
        Invalidate();
    }

    public void ResetView()
    {
        _panX = _panY = 0;
        Zoom = 1f;
        SetWorldSize(2200, 1500);
    }

    public void Select(BlockNode? node)
    {
        _selected = node;
        _selectedNodes.Clear();
        if (node != null) _selectedNodes.Add(node.Id);
        _selectedLink = null;
        SelectionChanged?.Invoke(node);
        SelectionSetChanged?.Invoke(SelectedNodes);
        Invalidate();
    }

    public void SelectMany(IEnumerable<BlockNode> nodes)
    {
        _selectedNodes.Clear();
        foreach (var node in nodes) _selectedNodes.Add(node.Id);
        _selected = _selectedNodes.Count == 1 ? Project.Nodes.FirstOrDefault(node => _selectedNodes.Contains(node.Id)) : null;
        _selectedLink = null;
        SelectionChanged?.Invoke(_selected);
        SelectionSetChanged?.Invoke(SelectedNodes);
        Invalidate();
    }

    public void SelectArea(Rectangle area) => SelectMany(Project.Nodes.Where(node => node.Kind == "Description"
        ? area.Contains(BlockCatalog.Bounds(node))
        : area.IntersectsWith(BlockCatalog.Bounds(node))));

    public void MoveSelectedFrom(IReadOnlyDictionary<Guid, Point> originalPositions, int dx, int dy)
    {
        foreach (var node in SelectedNodes)
            if (originalPositions.TryGetValue(node.Id, out var initial))
            {
                node.X = initial.X + dx;
                node.Y = initial.Y + dy;
            }
        Invalidate();
    }

    public Point GetPasteWorldPoint()
    {
        var pointer = PointToClient(Control.MousePosition);
        if (!ClientRectangle.Contains(pointer)) pointer = _lastPointerClient;
        return ToWorld(pointer);
    }

    internal void SetInsertionPoint(Point? worldPoint)
    {
        _insertionWorldPoint = worldPoint;
        Invalidate();
    }

    internal bool MoveSelectionTo(Point center)
    {
        var selected = SelectedNodes;
        if (selected.Count == 0) return false;
        var bounds = selected.Select(node => BlockCatalog.Bounds(node)).ToArray();
        var left = bounds.Min(area => area.Left);
        var top = bounds.Min(area => area.Top);
        var right = bounds.Max(area => area.Right);
        var bottom = bounds.Max(area => area.Bottom);
        var dx = center.X - (left + right) / 2;
        var dy = center.Y - (top + bottom) / 2;
        foreach (var node in selected)
        {
            node.X += dx;
            node.Y += dy;
        }
        ContentResized?.Invoke();
        Invalidate();
        return true;
    }

    public bool DeleteSelectedLink()
    {
        if (_selectedLink is not { } selection) return false;
        var source = Project.Nodes.FirstOrDefault(n => n.Id == selection.Id);
        if (source == null) return false;
        source.Links[selection.Port] = null;
        _selectedLink = null;
        SelectionChanged?.Invoke(null);
        Invalidate();
        return true;
    }

    private void SelectLink(BlockNode source, string port, BlockNode target)
    {
        _selected = null;
        _selectedNodes.Clear();
        _selectedLink = (source.Id, port);
        SelectionChanged?.Invoke(null);
        SelectionSetChanged?.Invoke(SelectedNodes);
        LinkSelectionChanged?.Invoke(source, port, target);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(_panX, _panY);
        g.ScaleTransform(_zoom, _zoom);
        using (var gridPen = new Pen(Color.FromArgb(230, 234, 240)))
        {
            var left = -_panX / _zoom;
            var top = -_panY / _zoom;
            var right = (Width - _panX) / _zoom;
            var bottom = (Height - _panY) / _zoom;
            var step = 32f;
            while (step * _zoom < 12) step *= 2;
            for (var x = (float)Math.Floor(left / step) * step; x < right; x += step)
                g.DrawLine(gridPen, x, top, x, bottom);
            for (var y = (float)Math.Floor(top / step) * step; y < bottom; y += step)
                g.DrawLine(gridPen, left, y, right, y);
        }

        foreach (var description in Project.Nodes.Where(node => node.Kind == "Description"))
            DrawDescription(g, description);

        foreach (var source in Project.Nodes.Where(node => node.Kind != "Description"))
        {
            var ports = BlockCatalog.Get(source.Kind).Outputs;
            for (var i = 0; i < ports.Length; i++)
            {
                if (source.Link(ports[i]) is not Guid targetId) continue;
                var target = Project.Nodes.FirstOrDefault(n => n.Id == targetId);
                if (target == null || target.Kind == "Description") continue;
                var appearance = source.LinkStyles.GetValueOrDefault(ports[i]) ?? new LinkAppearance();
                if (!appearance.IsValid()) appearance = new LinkAppearance();
                var route = GraphLinkRouter.Route(source, i, target, Project.Nodes, appearance);
                if (route.Length < 2) continue;
                var displayRoute = LinkCurveGeometry.Build(route, appearance, Project.Nodes);
                var selected = _selectedLink == (source.Id, ports[i]);
                var hovered = _hoveredLink == (source.Id, ports[i]);
                var baseColor = ColorTranslator.FromHtml(appearance.Color);
                var color = selected ? Color.FromArgb(255, 151, 31)
                    : hovered ? Color.FromArgb(34, 123, 204)
                    : Color.FromArgb((int)Math.Round(255 * appearance.OpacityPercent / 100d), baseColor);
                using var pen = new Pen(color, (float)appearance.Thickness + (selected ? 1.5f : hovered ? 1f : 0f))
                { LineJoin = LineJoin.Round, DashCap = DashCap.Round, DashStyle = appearance.Pattern switch
                    { "Dash" => DashStyle.Dash, "Dot" => DashStyle.Dot,
                      "DashDot" => DashStyle.DashDot, "DashDotDot" => DashStyle.DashDotDot,
                      _ => DashStyle.Solid } };
                g.DrawLines(pen, displayRoute);
                DrawEndDecoration(g, color, displayRoute[^2], displayRoute[^1], appearance.End, appearance.EndSize);
                if (hovered)
                {
                    var center = LinkSelectionGeometry.Midpoint(displayRoute);
                    var radius = 6f / _zoom;
                    using var dot = new SolidBrush(Color.FromArgb(34, 123, 204));
                    using var ring = new Pen(Color.White, 2f / _zoom);
                    g.FillEllipse(dot, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                    g.DrawEllipse(ring, center.X - radius, center.Y - radius, radius * 2, radius * 2);
                }
            }
        }

        foreach (var node in Project.Nodes.Where(node => node.Kind != "Description"))
        {
            var spec = BlockCatalog.Get(node.Kind);
            var rectangle = BlockCatalog.Bounds(node);
            var appearance = BlockAppearanceManager.Resolve(node);
            var fillColor = appearance.FillColor.Length == 0 ? spec.Color : ColorTranslator.FromHtml(appearance.FillColor);
            using var fill = new SolidBrush(Color.FromArgb(
                (int)Math.Round(255 * appearance.FillOpacityPercent / 100d), fillColor));
            var selected = _selectedNodes.Contains(node.Id);
            var borderColor = ColorTranslator.FromHtml(appearance.BorderColor);
            using var border = new Pen(selected ? Color.FromArgb(255, 184, 53) :
                ActiveDebugNode == node.Id ? Color.FromArgb(48, 206, 164) :
                Color.FromArgb((int)Math.Round(255 * appearance.BorderOpacityPercent / 100d), borderColor),
                selected || ActiveDebugNode == node.Id ? Math.Max(3, (float)appearance.BorderWidth + 1.5f)
                    : (float)appearance.BorderWidth);
            using var outline = BlockGeometry.Outline(rectangle, appearance.Shape);
            g.FillPath(fill, outline);
            g.DrawPath(border, outline);
            if (node.Breakpoint)
            {
                using var marker = new SolidBrush(Color.FromArgb(221, 59, 62));
                using var markerOutline = new Pen(Color.White, 1.5f);
                var badge = BreakpointBadgeBounds(node);
                g.FillRectangle(marker, badge);
                g.DrawRectangle(markerOutline, badge);
                using var pause = new Pen(Color.White, 2f);
                g.DrawLine(pause, badge.Left + 5, badge.Top + 4, badge.Left + 5, badge.Bottom - 4);
                g.DrawLine(pause, badge.Left + 11, badge.Top + 4, badge.Left + 11, badge.Bottom - 4);
            }
            var blockBounds = BlockCatalog.Bounds(node);
            using var hintFont = new Font(Font.FontFamily, 8);
            var title = node.Kind == "ShowcaseStep" ? node.Value("Caption", UiLanguage.T(spec.Title)) :
                UiLanguage.T(spec.Title);
            var tall = appearance.Shape is "Square" or "Circle" or "Diamond";
            var titleBounds = TitleTextBounds(node, tall);
            using var centerText = new StringFormat { Alignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using var titleFont = new Font(Font.FontFamily,
                FitTitleFontSize(g, title, Font.FontFamily, appearance.TitleFontSize, titleBounds.Width, appearance.AutoFitTitle),
                FontStyle.Bold);
            g.DrawString(title, titleFont, Brushes.White, titleBounds, centerText);
            var hint = UiLanguage.TOr(spec.Hint, "Настройте параметры блока ниже.");
            if (node.Kind == "WatchCursor")
            {
                var mode = node.Value("ActivationMode", "Переключатель") switch
                {
                    "Переключатель" => "перекл.", "Пока удерживается" => "удерж.", _ => "сразу"
                };
                var required = node.Value("RequiredHold", "Левая кнопка мыши") switch
                {
                    "Левая кнопка мыши" => "ЛКМ", "Правая кнопка мыши" => "ПКМ",
                    "Средняя кнопка мыши" => "СКМ", "Нет" => "без доп.", _ => node.Value("RequiredKey", "SHIFT")
                };
                hint = $"{node.Value("ActivationKey", "F9")} {UiLanguage.T(mode)} + {UiLanguage.T(required)}";
            }
            g.DrawString(hint, hintFont, Brushes.WhiteSmoke,
                tall ? new RectangleF(node.X + 34, node.Y + 89, blockBounds.Width - 68, 19)
                     : new RectangleF(node.X + 14, node.Y + 28, blockBounds.Width - 28, 19),
                tall ? centerText : StringFormat.GenericDefault);
            if (node.Kind != "Start")
            {
                var input = InputPoint(node);
                PortPainter.Draw(g, input, appearance.InputPort, Color.White);
            }
            for (var i = 0; i < spec.Outputs.Length; i++)
            {
                var p = OutputPoint(node, i);
                PortPainter.Draw(g, p,
                    appearance.OutputPorts.GetValueOrDefault(spec.Outputs[i]) ?? new PortAppearance(),
                    _pending == (node.Id, spec.Outputs[i]) ? Color.Gold : Color.White);
            }
        }
        if (_insertionWorldPoint is { } insertion)
        {
            using var halo = new SolidBrush(Color.FromArgb(90, 31, 160, 75));
            using var dot = new SolidBrush(Color.FromArgb(20, 150, 60));
            using var ring = new Pen(Color.White, 2f / _zoom);
            g.FillEllipse(halo, insertion.X - 10f / _zoom, insertion.Y - 10f / _zoom,
                20f / _zoom, 20f / _zoom);
            g.FillEllipse(dot, insertion.X - 5f / _zoom, insertion.Y - 5f / _zoom,
                10f / _zoom, 10f / _zoom);
            g.DrawEllipse(ring, insertion.X - 5f / _zoom, insertion.Y - 5f / _zoom,
                10f / _zoom, 10f / _zoom);
        }
        if (_selectingArea)
        {
            var area = SelectionRectangle();
            using var fill = new SolidBrush(Color.FromArgb(45, 45, 112, 220));
            using var outline = new Pen(Color.FromArgb(45, 112, 220), 2f / _zoom) { DashStyle = DashStyle.Dash };
            g.FillRectangle(fill, area);
            g.DrawRectangle(outline, area);
        }
    }

    private static void DrawEndDecoration(Graphics graphics, Color color, PointF before, PointF target,
        string end, int size)
    {
        if (end == "Line") return;
        var dx = target.X - before.X;
        var dy = target.Y - before.Y;
        var length = Math.Sqrt((double)dx * dx + (double)dy * dy);
        if (length < 1) return;
        var ux = dx / length;
        var uy = dy / length;
        var tip = new PointF((float)(target.X - ux * size * .5),
            (float)(target.Y - uy * size * .5));
        using var brush = new SolidBrush(color);
        if (end == "Circle")
        {
            graphics.FillEllipse(brush, tip.X - size / 2f, tip.Y - size / 2f, size, size);
            return;
        }
        var baseX = tip.X - ux * size;
        var baseY = tip.Y - uy * size;
        if (end == "Diamond")
        {
            graphics.FillPolygon(brush,
            [
                tip,
                new PointF((float)(baseX - uy * size * .5), (float)(baseY + ux * size * .5)),
                new PointF((float)(baseX - ux * size), (float)(baseY - uy * size)),
                new PointF((float)(baseX + uy * size * .5), (float)(baseY - ux * size * .5))
            ]);
            return;
        }
        graphics.FillPolygon(brush,
        [
            tip,
            new PointF((float)(baseX - uy * size * .5), (float)(baseY + ux * size * .5)),
            new PointF((float)(baseX + uy * size * .5), (float)(baseY - ux * size * .5))
        ]);
    }

    private void DrawDescription(Graphics g, BlockNode node)
    {
        var area = DescriptionGeometry.Bounds(node);
        var style = BlockAppearanceManager.Resolve(node);
        var color = style.FillColor.Length == 0 ? DescriptionGeometry.FillColor(node)
            : ColorTranslator.FromHtml(style.FillColor);
        var alpha = (int)Math.Round(255 * DescriptionGeometry.Opacity(node) *
            style.FillOpacityPercent / 10000d);
        using var fill = new SolidBrush(Color.FromArgb(alpha, color));
        using var header = new SolidBrush(Color.FromArgb(190, Color.White));
        using var outline = new Pen(_selectedNodes.Contains(node.Id) ? Color.FromArgb(255, 157, 31)
            : Color.FromArgb((int)Math.Round(255 * style.BorderOpacityPercent / 100d),
                ColorTranslator.FromHtml(style.BorderColor)),
            _selectedNodes.Contains(node.Id) ? 3f : (float)style.BorderWidth);
        using var shape = BlockGeometry.Outline(area, style.Shape);
        g.FillPath(fill, shape);
        var state = g.Save();
        g.SetClip(shape);
        g.FillRectangle(header, area.X, area.Y, area.Width, Math.Min(34, area.Height));
        g.Restore(state);
        g.DrawPath(outline, shape);
        using var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(35, 50, 70));
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
        var title = node.Value("Title", "Описание сценария");
        var displayedTitle = title == "Описание сценария" ? UiLanguage.T(title) : title;
        g.DrawString(string.IsNullOrWhiteSpace(displayedTitle) ? UiLanguage.T("Описание") : displayedTitle, titleFont, titleBrush,
            new RectangleF(area.X + 12, area.Y + 3, Math.Max(1, area.Width - 28), 28), format);
        using var handle = new Pen(Color.FromArgb(155, 65, 85, 110), 1.4f);
        for (var offset = 6; offset <= 14; offset += 4)
            g.DrawLine(handle, area.Right - offset, area.Bottom - 3, area.Right - 3, area.Bottom - offset);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _lastPointerClient = e.Location;
        if (e.Button == MouseButtons.Right) SetInsertionPoint(null);
        if (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Shift) != 0)
        {
            Select(null);
            _pending = null;
            _selectionStart = _selectionEnd = ToWorld(e.Location);
            _selectingArea = true;
            Capture = true;
            Cursor = Cursors.Cross;
            return;
        }
        if (e.Button == MouseButtons.Middle)
        {
            _panning = true;
            _lastPan = e.Location;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return;
        }
        var world = ToWorld(e.Location);
        var hit = Project.Nodes.LastOrDefault(n => n.Kind != "Description" &&
            Rectangle.Inflate(BlockCatalog.Bounds(n), 12, 12).Contains(world));
        if (hit == null && e.Button == MouseButtons.Left && HitLink(world) is { } selectedLink)
        {
            SelectLink(selectedLink.Source, selectedLink.Port, selectedLink.Target);
            _pending = null;
            return;
        }
        var resizeDescription = false;
        hit ??= HitDescription(world, out resizeDescription);
        if (hit == null)
        {
            if (e.Button == MouseButtons.Left)
            {
                SetInsertionPoint(world);
                if (MoveSelectionTo(world)) { _pending = null; return; }
                Select(null);
                _pending = null;
                _panning = true;
                _lastPan = e.Location;
                Capture = true;
                Cursor = Cursors.SizeAll;
            }
            else if (e.Button == MouseButtons.Right)
            {
                Select(null);
                _pending = null;
            }
            return;
        }
        if (hit.Kind == "Description")
        {
            _pending = null;
            if (!_selectedNodes.Contains(hit.Id) || _selectedNodes.Count != 1) Select(hit);
            if (e.Button == MouseButtons.Left)
            {
                _dragStartWorld = world;
                if (resizeDescription)
                {
                    _resizingDescription = hit;
                    _resizeStartWorld = world;
                    _resizeStartSize = DescriptionGeometry.Bounds(hit).Size;
                    Cursor = Cursors.SizeNWSE;
                }
                else
                {
                    _dragging = hit;
                    _dragPositions = SelectedNodes.ToDictionary(node => node.Id, node => new Point(node.X, node.Y));
                    Cursor = Cursors.SizeAll;
                }
                Capture = true;
            }
            return;
        }
        var spec = BlockCatalog.Get(hit.Kind);
        if (e.Button == MouseButtons.Left && _pending is { } pending && hit.Kind != "Start" &&
            Distance(InputPoint(hit), world) <= Math.Max(12, BlockAppearanceManager.Resolve(hit).InputPort.Size / 2f + 4) / _zoom)
        {
            var source = Project.Nodes.FirstOrDefault(n => n.Id == pending.Id);
            if (source != null && source.Id != hit.Id)
            {
                source.Links[pending.Port] = hit.Id;
                Message?.Invoke($"Соединено: {BlockCatalog.Get(source.Kind).Title} → {spec.Title}");
            }
            _pending = null;
            Select(hit);
            Invalidate();
            return;
        }
        for (var i = 0; i < spec.Outputs.Length; i++)
        {
            var port = OutputPoint(hit, i);
            var portSize = BlockAppearanceManager.Resolve(hit).OutputPorts
                .GetValueOrDefault(spec.Outputs[i])?.Size ?? 14;
            if (Distance(port, world) > Math.Max(12, portSize / 2f + 4) / _zoom) continue;
            if (e.Button == MouseButtons.Right)
            {
                hit.Links[spec.Outputs[i]] = null;
                Message?.Invoke("Связь удалена");
            }
            else
            {
                _pending = (hit.Id, spec.Outputs[i]);
                Message?.Invoke($"Выбран выход «{spec.Outputs[i]}». Нажмите на кружок входа нужного блока.");
            }
            Select(hit);
            Invalidate();
            return;
        }
        if (!_selectedNodes.Contains(hit.Id) || _selectedNodes.Count == 1) Select(hit);
        if (e.Button == MouseButtons.Left)
        {
            _dragging = hit;
            _dragStartWorld = world;
            _dragPositions = SelectedNodes.ToDictionary(node => node.Id, node => new Point(node.X, node.Y));
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _lastPointerClient = e.Location;
        if (_selectingArea)
        {
            _selectionEnd = ToWorld(e.Location);
            Invalidate();
            return;
        }
        if (_panning)
        {
            _panX += e.X - _lastPan.X;
            _panY += e.Y - _lastPan.Y;
            _lastPan = e.Location;
            Invalidate();
            return;
        }
        if (_resizingDescription != null)
        {
            var resizeWorld = ToWorld(e.Location);
            var width = Math.Clamp(_resizeStartSize.Width + resizeWorld.X - _resizeStartWorld.X,
                DescriptionGeometry.MinWidth, DescriptionGeometry.MaxSize);
            var height = Math.Clamp(_resizeStartSize.Height + resizeWorld.Y - _resizeStartWorld.Y,
                DescriptionGeometry.MinHeight, DescriptionGeometry.MaxSize);
            _resizingDescription.Values["Width"] = width.ToString();
            _resizingDescription.Values["Height"] = height.ToString();
            Invalidate();
            return;
        }
        if (_dragging == null)
        {
            var hoverWorld = ToWorld(e.Location);
            var port = HitPort(hoverWorld);
            var breakpoint = HitBreakpointBadge(hoverWorld);
            var portKey = port is { } hovered
                ? $"{hovered.Node.Id:N}:{hovered.IsInput}:{hovered.Name}" : null;
            var hintKey = portKey ?? (breakpoint is null ? null : "breakpoint:" + breakpoint.Id.ToString("N"));
            if (_hoveredPort != hintKey)
            {
                _hoveredPort = hintKey;
                if (port is { } point)
                {
                    var label = point.IsInput
                        ? UiLanguage.T("Вход")
                        : UiLanguage.T("Выход") + ": " + UiLanguage.T(point.Name);
                    _portToolTip.Show(label, this, e.Location.X + 14, e.Location.Y + 18, 5000);
                }
                else if (breakpoint != null)
                    _portToolTip.Show(UiLanguage.T("Точка останова перед блоком") + ": " +
                        UiLanguage.T("Приостанавливает сценарий перед выполнением блока."),
                        this, e.Location.X + 14, e.Location.Y + 18, 5000);
                else _portToolTip.Hide(this);
            }
            if (port != null || breakpoint != null)
            {
                Cursor = breakpoint != null ? Cursors.Help : Cursors.Hand;
                return;
            }
            var grip = HitDescription(hoverWorld, out var resize);
            var node = Project.Nodes.LastOrDefault(n => n.Kind != "Description" &&
                Rectangle.Inflate(BlockCatalog.Bounds(n), 12, 12).Contains(hoverWorld));
            var link = grip == null && node == null ? HitLink(hoverWorld) : null;
            var next = link is { } found ? (found.Source.Id, found.Port) : ((Guid Id, string Port)?)null;
            if (_hoveredLink != next) { _hoveredLink = next; Invalidate(); }
            Cursor = resize ? Cursors.SizeNWSE : grip != null ? Cursors.SizeAll : link != null ? Cursors.Hand : Cursors.Default;
            return;
        }
        var world = ToWorld(e.Location);
        var dx = world.X - _dragStartWorld.X;
        var dy = world.Y - _dragStartWorld.Y;
        MoveSelectedFrom(_dragPositions, dx, dy);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _portToolTip.Hide(this);
        _hoveredPort = null;
        if (_hoveredLink is not null) { _hoveredLink = null; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        var resized = _resizingDescription;
        var moved = _dragging != null;
        if (_selectingArea && e.Button == MouseButtons.Left)
        {
            _selectionEnd = ToWorld(e.Location);
            var area = SelectionRectangle();
            SelectArea(area);
        }
        _selectingArea = false;
        _dragging = null;
        _resizingDescription = null;
        _dragPositions.Clear();
        _panning = false;
        Capture = false;
        Cursor = Cursors.Default;
        if (resized != null)
        {
            ContentResized?.Invoke();
            SelectionChanged?.Invoke(resized);
        }
        else if (moved) ContentResized?.Invoke();
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) != 0)
        {
            var worldX = (e.X - _panX) / _zoom;
            var worldY = (e.Y - _panY) / _zoom;
            Zoom *= e.Delta > 0 ? 1.25f : 0.8f;
            _panX = e.X - worldX * _zoom;
            _panY = e.Y - worldY * _zoom;
            Invalidate();
        }
        else base.OnMouseWheel(e);
    }

    private Point ToWorld(Point point) => new((int)Math.Round((point.X - _panX) / _zoom), (int)Math.Round((point.Y - _panY) / _zoom));

    private Rectangle SelectionRectangle() => Rectangle.FromLTRB(
        Math.Min(_selectionStart.X, _selectionEnd.X), Math.Min(_selectionStart.Y, _selectionEnd.Y),
        Math.Max(_selectionStart.X, _selectionEnd.X), Math.Max(_selectionStart.Y, _selectionEnd.Y));

    private BlockNode? HitDescription(Point point, out bool resize)
    {
        resize = false;
        foreach (var node in Project.Nodes.Where(candidate => candidate.Kind == "Description").Reverse())
        {
            var area = DescriptionGeometry.Bounds(node);
            if (!area.Contains(point)) continue;
            var grip = Math.Min(Math.Min(area.Width, area.Height) / 3,
                Math.Max(8, (int)Math.Ceiling(10 / _zoom)));
            if (point.X >= area.Right - grip && point.Y >= area.Bottom - grip)
            {
                resize = true;
                return node;
            }
            if (point.Y < area.Y + 34 || point.X < area.X + grip ||
                point.X >= area.Right - grip || point.Y >= area.Bottom - grip)
                return node;
        }
        return null;
    }

    private (BlockNode Source, string Port, BlockNode Target)? HitLink(Point point)
    {
        (BlockNode Source, string Port, BlockNode Target)? nearest = null;
        double best = double.PositiveInfinity;
        foreach (var source in Project.Nodes)
        {
            var ports = BlockCatalog.Get(source.Kind).Outputs;
            for (var i = 0; i < ports.Length; i++)
            {
                if (source.Link(ports[i]) is not Guid targetId) continue;
                var target = Project.Nodes.FirstOrDefault(n => n.Id == targetId);
                if (target == null || target.Kind == "Description") continue;
                var route = GraphLinkRouter.Route(source, i, target, Project.Nodes,
                    source.LinkStyles.GetValueOrDefault(ports[i]));
                if (route.Length < 2) continue;
                var appearance = source.LinkStyles.GetValueOrDefault(ports[i]) ?? new LinkAppearance();
                var displayRoute = LinkCurveGeometry.Build(route, appearance, Project.Nodes);
                var thickness = source.LinkStyles.GetValueOrDefault(ports[i])?.Thickness ?? 2;
                var radius = LinkSelectionGeometry.HitRadius(_zoom, (float)thickness);
                for (var step = 1; step < displayRoute.Length; step++)
                {
                    var distance = LinkSelectionGeometry.SegmentDistance(point, displayRoute[step - 1], displayRoute[step]);
                    if (distance <= radius && distance < best) { best = distance; nearest = (source, ports[i], target); }
                }
                var center = LinkSelectionGeometry.Midpoint(displayRoute);
                var markerDistance = Distance(point, Point.Round(center));
                if (markerDistance <= 14 / _zoom && markerDistance < best)
                { best = markerDistance; nearest = (source, ports[i], target); }
            }
        }
        return nearest;
    }

    private static Point InputPoint(BlockNode node) => GraphLinkRouter.InputPoint(node);
    private static Point OutputPoint(BlockNode node, int index) => GraphLinkRouter.OutputPoint(node, index);

    private (BlockNode Node, bool IsInput, string Name)? HitPort(Point world)
    {
        foreach (var node in Project.Nodes.Where(node => node.Kind != "Description").Reverse())
        {
            var appearance = BlockAppearanceManager.Resolve(node);
            if (node.Kind != "Start" && Distance(InputPoint(node), world) <=
                Math.Max(12, appearance.InputPort.Size / 2f + 4) / _zoom)
                return (node, true, "Вход");
            var outputs = BlockCatalog.Get(node.Kind).Outputs;
            for (var i = 0; i < outputs.Length; i++)
            {
                var size = appearance.OutputPorts.GetValueOrDefault(outputs[i])?.Size ?? 14;
                if (Distance(OutputPoint(node, i), world) <= Math.Max(12, size / 2f + 4) / _zoom)
                    return (node, false, outputs[i]);
            }
        }
        return null;
    }

    private BlockNode? HitBreakpointBadge(Point world) => Project.Nodes
        .LastOrDefault(node => node.Kind != "Description" && node.Breakpoint &&
            BreakpointBadgeBounds(node).Contains(world));

    internal static Rectangle BreakpointBadgeBounds(BlockNode node)
    {
        var bounds = BlockCatalog.Bounds(node);
        const int size = 16;
        const int inset = 7;
        var top = bounds.Top + inset;
        var min = bounds.Left + inset;
        var max = bounds.Right - inset - size;
        var ports = ConnectorBounds(node).Where(port => port.Top < top + size && port.Bottom > top).ToArray();
        Rectangle? best = null;
        var bestTitleWidth = -1;
        for (var left = min; left <= max; left++)
        {
            var candidate = new Rectangle(left, top, size, size);
            if (ports.Any(port => port.IntersectsWith(candidate))) continue;
            var titleWidth = BuildTitleBounds(node, bounds, false, candidate).Width;
            if (titleWidth <= bestTitleWidth) continue;
            best = candidate;
            bestTitleWidth = titleWidth;
        }
        return best ?? new Rectangle(min, top, size, size);
    }

    internal static Rectangle TitleTextBounds(BlockNode node, bool tall)
    {
        var bounds = BlockCatalog.Bounds(node);
        var badge = node.Breakpoint ? BreakpointBadgeBounds(node) : (Rectangle?)null;
        return BuildTitleBounds(node, bounds, tall, badge);
    }

    private static Rectangle BuildTitleBounds(BlockNode node, Rectangle bounds, bool tall, Rectangle? badge)
    {
        var top = bounds.Top + (tall ? 62 : 7);
        var height = tall ? 24 : 22;
        var left = bounds.Left + (tall ? 23 : 14);
        var right = bounds.Right - (tall ? 23 : 14);
        if (!tall && badge is { } marker) left = Math.Max(left, marker.Right + 7);
        var obstacles = ConnectorBounds(node).Where(port => port.Top < top + height && port.Bottom > top)
            .OrderBy(port => port.Left).ToArray();
        while (true)
        {
            var overlapping = obstacles.FirstOrDefault(port => port.Left <= left && port.Right > left);
            if (overlapping.IsEmpty) break;
            left = overlapping.Right + 4;
        }
        var nextPort = obstacles.FirstOrDefault(port => port.Left > left);
        if (!nextPort.IsEmpty) right = Math.Min(right, nextPort.Left - 4);
        return new Rectangle(left, top, Math.Max(1, right - left), height);
    }

    private static Rectangle[] ConnectorBounds(BlockNode node)
    {
        var appearance = BlockAppearanceManager.Resolve(node);
        var bounds = new List<Rectangle>();
        void Add(Point point, PortAppearance port)
        {
            var radius = (port.Size + 2) / 2;
            bounds.Add(new Rectangle(point.X - radius, point.Y - radius, radius * 2, radius * 2));
        }
        if (node.Kind != "Start") Add(GraphLinkRouter.InputPoint(node), appearance.InputPort);
        var outputs = BlockCatalog.Get(node.Kind).Outputs;
        for (var index = 0; index < outputs.Length; index++)
            Add(GraphLinkRouter.OutputPoint(node, index),
                appearance.OutputPorts.GetValueOrDefault(outputs[index]) ?? new PortAppearance());
        return bounds.ToArray();
    }

    internal static float FitTitleFontSize(Graphics graphics, string title, FontFamily family,
        int preferredSize, int availableWidth, bool autoFit)
    {
        var size = (float)Math.Clamp(preferredSize, 7, 16);
        if (!autoFit) return size;
        for (; size > 7; size -= 0.5f)
        {
            using var font = new Font(family, size, FontStyle.Bold);
            if (graphics.MeasureString(title, font).Width <= availableWidth) return size;
        }
        return 7;
    }
    private static double Distance(Point a, Point b) => Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y));
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rectangle, int radius)
    {
        using var path = Rounded(rectangle, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rectangle, int radius)
    {
        using var path = Rounded(rectangle, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

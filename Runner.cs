using System.Diagnostics;
using System.Drawing;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KlikerBlocks;

internal sealed class Runner(ProjectModel project, Action<string> log, Action<int, int> displacement,
    string? baseDirectory = null, Action<Guid, CursorWatchState?>? watchStatus = null,
    Func<byte, (bool Down, bool Pressed)>? keyState = null, Action<int, int>? relativeMove = null,
    Action<Guid, bool>? conditionStatus = null, ScreenAnalysisBuffer? initialScreenBuffer = null,
    Action? clearLog = null, Action<bool>? showLog = null,
    Action<string>? playSound = null, Func<int, int, CancellationToken, Task>? beep = null,
    Func<string, string, int, DialogWindowOptions, CancellationToken, Task<string>>? inputDialog = null,
    Func<string, int, int, DialogWindowOptions, CancellationToken, Task<int>>? choiceDialog = null,
    WindowsMessageInbox? messageInbox = null, IntPtr? selfHandle = null,
    Func<string, IReadOnlyList<string>, DialogWindowOptions, CancellationToken, Task<int>>? radioDialog = null,
    Action<string, string>? hintPopup = null,
    Func<string, string, string, string, int, CancellationToken, Task<string>>? translate = null,
    string argosPythonPath = "python",
    Func<string, string, string, int, CancellationToken, Task<string>>? translateLocal = null,
    Func<string, string, int, int, CancellationToken, Task>? speakText = null,
    Func<string, VariableStore, CancellationToken, Task>? showcaseStep = null,
    int nestedDepth = 0,
    Action<Guid, IReadOnlyDictionary<string, string>, bool>? debugState = null,
    Func<string, CancellationToken, Task<string?>>? ensureOcr = null)
{
    private sealed class SubReturnSignal : Exception { }
    private readonly Dictionary<Guid, BlockNode> _nodes = project.Nodes.ToDictionary(n => n.Id);
    private readonly SemaphoreSlim _input = new(1, 1);
    private readonly AsyncLocal<DesktopInputLease?> _groupLease = new();
    private DesktopInputLease? _heldLease;
    private readonly ConcurrentDictionary<byte, byte> _heldKeys = new();
    private readonly ConcurrentDictionary<string, byte> _heldButtons = new();
    private readonly AsyncLocal<Point?> _foundPoint = new();
    private ScreenAnalysisBuffer? _screenBuffer = initialScreenBuffer;
    private readonly VariableStore _variables = new(project.VariableNamesAsciiOnly);
    private readonly string _baseDirectory = baseDirectory ?? Environment.CurrentDirectory;
    private readonly ScriptLogWriter _scriptLog = new(baseDirectory ?? Environment.CurrentDirectory);
    private readonly Func<byte, (bool Down, bool Pressed)> _keyState = keyState ?? NativeMouse.ReadKeyState;
    private readonly Action<int, int> _relativeMove = relativeMove ?? NativeMouse.MoveRelative;
    private readonly Action? _clearLog = clearLog;
    private readonly Action<bool>? _showLog = showLog;
    private readonly Action<string> _playSound = playSound ?? AudioTools.PlayWave;
    private readonly Func<string, string, int, int, CancellationToken, Task> _speakText =
        speakText ?? WindowsSpeech.SpeakAsync;
    private readonly Func<string, CancellationToken, Task<string?>>? _ensureOcr = ensureOcr;
    private readonly Func<string, VariableStore, CancellationToken, Task> _showcaseStep = showcaseStep ??
        ((_, _, _) => throw new InvalidOperationException(UiLanguage.T("Этот шаг работает только во встроенной демонстрации Kliker Blocks.")));
    private readonly Func<int, int, CancellationToken, Task> _beep = beep ??
        ((frequency, duration, cancel) => Task.Run(() => AudioTools.Beep(frequency, duration), cancel));
    private readonly Func<string, string, int, DialogWindowOptions, CancellationToken, Task<string>> _inputDialog = inputDialog ??
        ((_, _, _, _, _) => throw new InvalidOperationException(UiLanguage.T("Диалог ввода доступен только из окна Kliker Blocks.")));
    private readonly Func<string, int, int, DialogWindowOptions, CancellationToken, Task<int>> _choiceDialog = choiceDialog ??
        ((_, _, _, _, _) => throw new InvalidOperationException(UiLanguage.T("Диалог выбора доступен только из окна Kliker Blocks.")));
    private readonly Func<string, IReadOnlyList<string>, DialogWindowOptions, CancellationToken, Task<int>> _radioDialog = radioDialog ??
        ((_, _, _, _) => throw new InvalidOperationException(UiLanguage.T("Диалог вариантов доступен только из окна Kliker Blocks.")));
    private readonly Action<string, string> _hintPopup = hintPopup ?? ((message, title) => log($"Уведомление {title}: {message}"));
    private readonly Func<string, string, string, string, int, CancellationToken, Task<string>> _translate =
        translate ?? ((text, source, target, endpoint, timeout, cancel) =>
            TranslationTools.TranslateAsync(text, source, target, endpoint, timeout, cancel));
    private readonly Func<string, string, string, int, CancellationToken, Task<string>> _translateLocal =
        translateLocal ?? ((text, source, target, timeout, cancel) =>
            ArgosBridge.TranslateAsync(text, source, target, argosPythonPath, timeout, cancel));
    private readonly WindowsMessageInbox _messageInbox = messageInbox ?? new WindowsMessageInbox();
    private long _lastMessageVersion;
    private readonly WindowScopeGuard _windowScope = new(project.WindowScope ?? new WindowScopeSettings(), log);
    private readonly ConcurrentDictionary<Guid, CursorActivationGate> _conditionGates = new();
    private readonly ConcurrentDictionary<Guid, KeyPressGate> _pressGates = new();
    private readonly ConcurrentDictionary<Guid, bool> _reportedConditions = new();
    private readonly ConcurrentDictionary<Guid, CycleWatchState> _cycleWatches = new();
    private readonly object _pauseSync = new();
    private TaskCompletionSource<bool>? _resumeSignal;
    private readonly int _cycleDelayMs = Math.Clamp(project.CycleDelayMs, 10, 60000);
    private readonly decimal _playbackSpeed = project.PlaybackSpeed;
    private Runner? _childRunner;
    private bool _looping;
    private int _stopRequested;
    private int _stepRemaining = -1;
    private long _lastDebugUpdate;
    private Dictionary<string, BlockNode> _subroutines = new(StringComparer.OrdinalIgnoreCase);

    public bool IsPauseRequested { get { lock (_pauseSync) return _resumeSignal is not null; } }

    public void StepDebug()
    {
        if (_childRunner is not null) _childRunner.StepDebug();
        else _stepRemaining = 1;
        SetPaused(false);
    }
    public void ContinueDebug()
    {
        _stepRemaining = -1;
        _childRunner?.ContinueDebug();
        SetPaused(false);
    }

    public void SetPaused(bool paused)
    {
        _childRunner?.SetPaused(paused);
        lock (_pauseSync)
        {
            if (paused) _resumeSignal ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            else { _resumeSignal?.TrySetResult(true); _resumeSignal = null; }
        }
    }

    private async Task PausePointAsync(CancellationToken cancel)
    {
        Task? waiting;
        lock (_pauseSync) waiting = _resumeSignal?.Task;
        if (waiting is null || _groupLease.Value is not null) return;
        await _input.WaitAsync(cancel);
        try
        {
            ReleaseHeldInputs();
            _heldLease?.Dispose();
            _heldLease = null;
        }
        finally { _input.Release(); }
        await waiting.WaitAsync(cancel);
    }

    private async Task EnsureWindowScopeAsync(CancellationToken cancel)
    {
        if (!_windowScope.Enabled) return;
        if (_groupLease.Value is not null)
        {
            if (!_windowScope.IsOpen) throw new InvalidOperationException("Целевое окно закрылось во время неделимой группы ввода.");
            return;
        }
        if (!_windowScope.IsForeground && (!_heldKeys.IsEmpty || !_heldButtons.IsEmpty))
        {
            await _input.WaitAsync(cancel);
            try
            {
                ReleaseHeldInputs();
                _heldLease?.Dispose();
                _heldLease = null;
            }
            finally { _input.Release(); }
        }
        await _windowScope.WaitForOpenAsync(cancel);
    }

    private async Task WithInputAsync(Func<Task> action, CancellationToken cancel, bool foregroundOnly = false)
    {
        await PausePointAsync(cancel);
        var inGroup = _groupLease.Value is not null;
        var focusWarningReported = false;
        if (!inGroup) await _input.WaitAsync(cancel);
        try
        {
            while (true)
            {
                if (inGroup && !_windowScope.IsOpen)
                    throw new InvalidOperationException("Целевое окно закрылось во время неделимой группы ввода.");
                if (!inGroup) await _windowScope.WaitForOpenAsync(cancel);
                var lease = _groupLease.Value ?? _heldLease ?? await DesktopInputCoordinator.AcquireAsync(cancel, log);
                try
                {
                    if (foregroundOnly && !_windowScope.IsForeground) return;
                    if (await _windowScope.TryFocusAsync(cancel))
                    {
                        cancel.ThrowIfCancellationRequested();
                        await action();
                        return;
                    }
                    if (inGroup) throw new InvalidOperationException("Windows не разрешила активировать целевое окно во время группы ввода.");
                    if (_heldLease == lease) ReleaseHeldInputs();
                }
                finally
                {
                    if (!inGroup)
                    {
                        if (!_heldKeys.IsEmpty || !_heldButtons.IsEmpty) _heldLease = lease;
                        else
                        {
                            if (_heldLease == lease) _heldLease = null;
                            lease.Dispose();
                        }
                    }
                }
                if (!focusWarningReported) log("Целевое окно пока не удалось активировать; ввод ожидает.");
                focusWarningReported = true;
                await Task.Delay(250, cancel);
            }
        }
        finally { if (!inGroup) _input.Release(); }
    }

    private void ReleaseHeldInputs()
    {
        foreach (var key in _heldKeys.Keys) NativeMouse.KeyUp(key);
        foreach (var button in _heldButtons.Keys) NativeMouse.MouseUp(button);
        _heldKeys.Clear();
        _heldButtons.Clear();
    }

    private async Task DelayWatchingScopeAsync(int milliseconds, CancellationToken cancel)
    {
        var remaining = SpeedTime(milliseconds, _playbackSpeed);
        do
        {
            await PausePointAsync(cancel);
            if (remaining <= 0) break;
            var slice = Math.Min(remaining, 50);
            await Task.Delay(slice, cancel);
            remaining -= slice;
            if (_windowScope.Enabled && _groupLease.Value is null && !_windowScope.IsOpen)
                await _windowScope.WaitForOpenAsync(cancel);
        } while (remaining > 0);
    }

    internal static int SpeedTime(int milliseconds, decimal speed)
    {
        if (speed is < 0.1m or > 5.0m)
            throw new InvalidOperationException("Скорость сценария должна быть от 0,1× до 5×.");
        return checked((int)Math.Round(milliseconds / speed, MidpointRounding.AwayFromZero));
    }

    internal static decimal ParseSpeed(string text)
    {
        if (!decimal.TryParse(text.Trim().Replace(',', '.'),
                System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var speed) || speed is < 0.1m or > 5.0m)
            throw new InvalidOperationException("Скорость сценария должна быть от 0,1× до 5×.");
        return speed;
    }

    private void ReportIndicatorCondition(BlockNode node, bool yes)
    {
        var mode = node.Value("IndicatorMode", IndicatorConditionMode.DefaultFor(node.Kind));
        var indicatorActive = IndicatorConditionMode.Resolve(mode, yes);
        if (indicatorActive is not bool active ||
            (_reportedConditions.TryGetValue(node.Id, out var previous) && previous == active)) return;
        _reportedConditions[node.Id] = active;
        conditionStatus?.Invoke(node.Id, active);
    }

    public Task RunAsync(CancellationToken cancel, CheckpointState? resume = null)
    {
        _ = SpeedTime(0, _playbackSpeed);
        _variables.Set("_hwnd_self", (selfHandle?.ToInt64() ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var starts = _nodes.Values.Where(n => n.Kind == "Start").ToArray();
        if (starts.Length != 1) throw new InvalidOperationException("В проекте должен быть ровно один блок «Старт».");
        if (resume is not null)
        {
            if (resume.GraphHash != CheckpointTools.GraphHash(project) || !_nodes.ContainsKey(resume.NextNode))
                throw new InvalidOperationException("Контрольная точка не соответствует текущему графу.");
            _variables.Restore(resume.Variables, resume.Arrays);
        }
        var definitions = _nodes.Values.Where(n => n.Kind == "SubStart").ToArray();
        if (definitions.Any(n => string.IsNullOrWhiteSpace(n.Value("Name"))) ||
            definitions.GroupBy(n => n.Value("Name").Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Имена подпрограмм должны быть непустыми и уникальными.");
        _subroutines = definitions.ToDictionary(n => n.Value("Name").Trim(), StringComparer.OrdinalIgnoreCase);
        var explicitCycle = ValidateGraph(starts[0].Id);
        foreach (var definition in definitions) ValidateGraph(definition.Id);
        var selectedRepeat = project.RunMode == "Повторять до остановки";
        var repeatFromStart = selectedRepeat || explicitCycle;
        if (project.RunMode is not ("Один проход" or "Повторять до остановки"))
            throw new InvalidOperationException("Неизвестный режим выполнения сценария.");
        _looping = repeatFromStart;
        return Task.Run(async () =>
        {
            try
            {
                log(selectedRepeat ? UiLanguage.Format("Режим: повторять с начала до остановки; пауза {0} мс", _cycleDelayMs)
                    : explicitCycle ? UiLanguage.Format("Режим: явная обратная связь; повторение до остановки с паузой {0} мс", _cycleDelayMs)
                    : UiLanguage.T("Режим: один проход"));
                var laps = 0L;
                do
                {
                    var watchedThisLap = new HashSet<Guid>();
                    await Execute(laps == 0 && resume is not null ? resume.NextNode : starts[0].Id,
                        cancel, watchedThisLap: watchedThisLap);
                    PruneCycleWatches(watchedThisLap);
                    if (!repeatFromStart || Volatile.Read(ref _stopRequested) != 0) break;
                    laps++;
                    if (laps is 1 or 10 or 100 || laps % 1000 == 0)
                        log(UiLanguage.Format("Проходов сценария: {0}", laps));
                    await DelayWatchingScopeAsync(_cycleDelayMs, cancel);
                } while (Volatile.Read(ref _stopRequested) == 0);
            }
            finally
            {
                foreach (var id in _cycleWatches.Keys) watchStatus?.Invoke(id, null);
                ReleaseHeldInputs();
                _heldLease?.Dispose();
                _heldLease = null;
            }
        }, cancel);
    }

    private bool ValidateGraph(Guid start)
    {
        var active = new HashSet<Guid>();
        var done = new HashSet<Guid>();
        var hasCycle = false;
        void Visit(Guid id)
        {
            if (active.Contains(id)) { hasCycle = true; return; }
            if (done.Contains(id)) return;
            active.Add(id);
            if (!_nodes.TryGetValue(id, out var node)) throw new InvalidOperationException("Связь ведёт на отсутствующий блок.");
            foreach (var target in node.Links.Values)
                if (target is Guid next) Visit(next);
            active.Remove(id);
            done.Add(id);
        }
        Visit(start);
        return hasCycle;
    }

    private BlockNode ResolveVariables(BlockNode source)
    {
        if (!source.Values.Values.Any(VariableStore.HasReference)) return source;
        return new BlockNode
        {
            Id = source.Id, Kind = source.Kind, X = source.X, Y = source.Y,
            Links = source.Links,
            Values = source.Values.ToDictionary(entry => entry.Key,
                entry => source.Kind == "RadioDialog" && entry.Key == "Options"
                    ? _variables.ResolveLines(entry.Value) : _variables.Resolve(entry.Value))
        };
    }

    private async Task Execute(Guid? first, CancellationToken cancel, int callDepth = 0,
        bool rootFlow = true, HashSet<Guid>? watchedThisLap = null)
    {
        var current = first;
        var steps = 0;
        var visited = new HashSet<Guid>();
        var errorAttempts = new Dictionary<Guid, int>();
        watchedThisLap ??= [];
        while (current is Guid id && Volatile.Read(ref _stopRequested) == 0)
        {
            cancel.ThrowIfCancellationRequested();
            await PausePointAsync(cancel);
            await EnsureWindowScopeAsync(cancel);
            if (!visited.Add(id))
            {
                if (rootFlow) PruneCycleWatches(watchedThisLap);
                watchedThisLap.Clear();
                visited.Clear();
                visited.Add(id);
                steps = 0;
                await DelayWatchingScopeAsync(_cycleDelayMs, cancel);
            }
            if (++steps > 100000) throw new InvalidOperationException("Превышен предел шагов. Проверьте связи между блоками.");
            if (steps % 100 == 0) await Task.Yield();
            if (!_nodes.TryGetValue(id, out var sourceNode)) throw new InvalidOperationException("Связь ведёт на отсутствующий блок.");
            var debugPause = sourceNode.Breakpoint || _stepRemaining == 0;
            var debugNow = Environment.TickCount64;
            if (debugPause || debugNow - _lastDebugUpdate >= 50)
            {
                debugState?.Invoke(id, _variables.Snapshot(), debugPause);
                _lastDebugUpdate = debugNow;
            }
            if (debugPause)
            {
                SetPaused(true);
                await PausePointAsync(cancel);
            }
            using var traceScope = _variables.BeginTrace(sourceNode.LogActions);
            BlockNode? resolvedNode = null;
            try
            {
            string? traceOutput = null;
            var node = resolvedNode = ResolveVariables(sourceNode);
            switch (node.Kind)
            {
                case "Start": current = node.Link("Далее"); break;
                case "SetVariable":
                    _variables.Set(node.Value("Name"), node.Value("Value"));
                    log(UiLanguage.Format("Переменная {0} = {1}", node.Value("Name"), node.Value("Value")));
                    current = node.Link("Далее");
                    break;
                case "DefineVariable":
                    _variables.Define(node.Value("Name"), node.Value("Value"));
                    current = node.Link("Далее");
                    break;
                case "UndefineVariable":
                    _variables.Undefine(node.Value("Name"));
                    current = node.Link("Далее");
                    break;
                case "SetVariableByName":
                    _variables.SetExistingByReference(node.Value("Reference"), node.Value("Value"));
                    current = node.Link("Далее");
                    break;
                case "MathVariable":
                    log(UiLanguage.Format("Переменная {0} = {1}", node.Value("Name"),
                        _variables.Calculate(node.Value("Name"), node.Value("Operation"), node.Value("Operand"))));
                    current = node.Link("Далее");
                    break;
                case "ArraySet":
                    _variables.SetArray(node.Value("Name"), node.Int("Index"), node.Value("Value"));
                    current = node.Link("Далее");
                    break;
                case "ArrayAction":
                {
                    var name = node.Value("Name");
                    var result = node.Value("Result");
                    switch (node.Value("Action"))
                    {
                        case "Добавить": _variables.ArrayPush(name, node.Value("Value")); break;
                        case "Извлечь": _variables.Set(result, _variables.ArrayPop(name)); break;
                        case "Размер": _variables.Set(result, _variables.ArraySize(name).ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                        case "Сортировать текст": _variables.ArraySort(name, false); break;
                        case "Сортировать числа": _variables.ArraySort(name, true); break;
                        case "Сортировать автоматически": _variables.ArraySortAuto(name); break;
                        default: throw new InvalidOperationException("Неизвестная операция с массивом.");
                    }
                    current = node.Link("Далее");
                    break;
                }
                case "StringSplit":
                {
                    var separator = node.Value("Separator");
                    if (separator.Length == 0) throw new InvalidOperationException("STRSEPARATE: разделитель не может быть пустым.");
                    var parts = node.Value("Text").Split([separator], 10001, StringSplitOptions.None);
                    if (parts.Length > 10000) throw new InvalidOperationException("STRSEPARATE: более 10000 частей; уточните разделитель.");
                    _variables.ReplaceArray(node.Value("Array"), parts);
                    _variables.Set(node.Value("Count"), parts.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "IfVariable":
                {
                    var selected = _variables.Compare(node.Value("Name"), node.Value("Comparison"), node.Value("Value"));
                    ReportIndicatorCondition(node, selected);
                    traceOutput = selected ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "IfExpression":
                {
                    var selected = ExpressionTrue(node.Value("Expression"));
                    ReportIndicatorCondition(node, selected);
                    traceOutput = selected ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "WhileExpression":
                {
                    var limit = node.Int("MaxIterations", 100000);
                    if (limit < 0) throw new InvalidOperationException("Максимум проходов не может быть отрицательным.");
                    var count = 0;
                    while (Volatile.Read(ref _stopRequested) == 0 &&
                           ExpressionTrue(ResolveVariables(sourceNode).Value("Expression")))
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (limit > 0 && count >= limit) throw new InvalidOperationException("Цикл «Пока условие» превысил максимум проходов.");
                        count++;
                        await Execute(node.Link("Тело"), cancel, callDepth, rootFlow: false);
                        if (count % 100 == 0) await Task.Yield();
                    }
                    current = node.Link("Далее");
                    break;
                }
                case "SwitchValue":
                {
                    var value = node.Value("Expression");
                    var selected = "Иначе";
                    for (var i = 1; i <= 5; i++)
                    {
                        var candidate = node.Value("Case" + i);
                        if (string.IsNullOrEmpty(candidate) || !SwitchEquals(value, candidate)) continue;
                        selected = "Случай " + i;
                        break;
                    }
                    traceOutput = selected;
                    current = node.Link(traceOutput);
                    break;
                }
                case "MoveCursor":
                {
                    await WithInputAsync(() =>
                    {
                        var point = NativeMouse.Position();
                        var center = NativeMouse.CenterFor(point);
                        var target = ResolveTarget(node, point, center, center);
                        NativeMouse.Move(target);
                        log($"Курсор: {point.X},{point.Y} → {target.X},{target.Y}");
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "MoveRelative":
                {
                    var dx = node.Int("DeltaX");
                    var dy = node.Int("DeltaY");
                    var delay = Math.Clamp(node.Int("DelayMs"), 0, 3600000);
                    var duration = Math.Clamp(node.Int("DurationMs"), 0, 60000);
                    var stepCount = duration == 0 ? 1 : Math.Clamp(node.Int("Steps", 1), 1, 1000);
                    if (stepCount == 1) duration = 0;
                    if (delay > 0) await DelayWatchingScopeAsync(delay, cancel);
                    duration = SpeedTime(duration, _playbackSpeed);
                    var plan = RelativeMotionPlan.Split(dx, dy, stepCount);
                    await WithInputAsync(async () =>
                    {
                        var clock = Stopwatch.StartNew();
                        for (var i = 0; i < plan.Count && Volatile.Read(ref _stopRequested) == 0; i++)
                        {
                            cancel.ThrowIfCancellationRequested();
                            if (duration > 0 && plan.Count > 1 && i > 0)
                            {
                                var due = (long)duration * i / (plan.Count - 1);
                                var remaining = due - clock.ElapsedMilliseconds;
                                if (remaining > 0) await Task.Delay((int)remaining, cancel);
                            }
                            if (!_windowScope.IsForeground)
                                throw new InvalidOperationException("Целевое окно перестало быть активным во время плавного сдвига.");
                            var part = plan[i];
                            if (part.X != 0 || part.Y != 0) _relativeMove(part.X, part.Y);
                        }
                    }, cancel);
                    log($"Относительное движение мыши: ΔX={dx}, ΔY={dy}; задержка {delay} мс; шагов {stepCount}; время {duration} мс");
                    current = node.Link("Далее");
                    break;
                }
                case "WatchCursor":
                    if (_looping && node.Int("DurationMs") == 0)
                    {
                        await WatchCycleTick(node, cancel);
                        watchedThisLap.Add(node.Id);
                    }
                    else await Watch(node, cancel);
                    current = node.Link("Далее");
                    break;
                case "Click":
                {
                    for (var i = 0; i < Math.Clamp(node.Int("Count", 1), 1, 100); i++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await WithInputAsync(async () =>
                        {
                            NativeMouse.MouseDown(node.Value("Button", "Левая"));
                            try { await Task.Delay(SpeedTime(30, _playbackSpeed), cancel); }
                            finally { NativeMouse.MouseUp(node.Value("Button", "Левая")); }
                        }, cancel);
                        await DelayWatchingScopeAsync(30, cancel);
                    }
                    current = node.Link("Далее");
                    break;
                }
                case "MouseButton":
                {
                    var button = node.Value("Button", "Левая");
                    await WithInputAsync(() =>
                    {
                        if (node.Value("Action") == "Нажать")
                        { NativeMouse.MouseDown(button); _heldButtons[button] = 1; }
                        else { NativeMouse.MouseUp(button); _heldButtons.TryRemove(button, out _); }
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "Wheel":
                {
                    await WithInputAsync(() =>
                    {
                        NativeMouse.Wheel(Math.Clamp(node.Int("Steps", 1), 1, 100) * (node.Value("Direction") == "Вверх" ? 1 : -1));
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "Key":
                {
                    var vk = NativeMouse.ParseKey(node.Value("Key", "A"));
                    await WithInputAsync(async () =>
                    {
                        NativeMouse.KeyDown(vk);
                        try { await Task.Delay(SpeedTime(40, _playbackSpeed), cancel); }
                        finally { NativeMouse.KeyUp(vk); }
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "KeyState":
                {
                    var key = NativeMouse.ParseKey(node.Value("Key", "CTRL"));
                    await WithInputAsync(() =>
                    {
                        if (node.Value("Action") == "Нажать")
                        { NativeMouse.KeyDown(key); _heldKeys[key] = 1; }
                        else { NativeMouse.KeyUp(key); _heldKeys.TryRemove(key, out _); }
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "VirtualKey":
                {
                    var code = node.Int("VirtualKeyCode");
                    if (code is < 1 or > 255) throw new ArgumentException("Код Windows VK должен быть в диапазоне 1–255.");
                    var key = (byte)code;
                    await WithInputAsync(() =>
                    {
                        if (node.Value("Action") == "Нажать")
                        { NativeMouse.KeyDown(key); _heldKeys[key] = 1; }
                        else { NativeMouse.KeyUp(key); _heldKeys.TryRemove(key, out _); }
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "TypeText":
                {
                    await WithInputAsync(() =>
                    {
                        NativeMouse.TypeUnicode(node.Value("Text"));
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "ClipboardSet":
                    await WithInputAsync(() => ClipboardTools.SetTextAsync(node.Value("Text"), cancel), cancel);
                    current = node.Link("Далее");
                    break;
                case "ClipboardGet":
                {
                    var clipboardText = "";
                    await WithInputAsync(async () => clipboardText = await ClipboardTools.GetTextAsync(cancel), cancel);
                    _variables.Set(node.Value("Name"), clipboardText);
                    current = node.Link("Далее");
                    break;
                }
                case "ExtractText":
                {
                    var input = _variables.Expand(node.Value("Text"));
                    var pattern = _variables.Expand(node.Value("Pattern"));
                    if (!int.TryParse(node.Value("Group", "1"), out var groupIndex) || groupIndex < 0)
                        throw new InvalidOperationException("Номер группы должен быть целым числом от 0.");
                    var match = Regex.Match(input, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                    var result = match.Success && groupIndex < match.Groups.Count ? match.Groups[groupIndex].Value : "";
                    _variables.Set(node.Value("Result"), result);
                    log(match.Success ? $"Извлечён фрагмент текста ({result.Length} символов)." : "Совпадение текста не найдено.");
                    current = node.Link("Далее");
                    break;
                }
                case "TextFile":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var action = node.Value("Action");
                    if (action == "Добавить или перезаписать строкой")
                    {
                        var rewrite = node.Int("Rewrite");
                        if (rewrite is not (0 or 1)) throw new InvalidOperationException("STRWRITELN: флаг перезаписи должен быть 0 или 1.");
                        action = rewrite == 1 ? "Перезаписать строкой" : "Добавить строку";
                    }
                    if (action == "Очистить или удалить файл")
                    {
                        var delete = node.Int("DeleteFile");
                        if (delete is not (0 or 1)) throw new InvalidOperationException("TFCLEAR: флаг удаления должен быть 0 или 1.");
                        action = delete == 1 ? "Удалить файл" : "Очистить";
                    }
                    var value = DataTools.TextFile(path, action, node.Int("Line", 1), node.Value("Value"));
                    _variables.Set(node.Value("Result"), value);
                    current = node.Link("Далее");
                    break;
                }
                case "TextFileArray":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var arrayName = node.Value("Array");
                    int count;
                    if (node.Value("Action") == "Прочитать в массив")
                    {
                        var lines = DataTools.ReadTextArray(path);
                        _variables.ReplaceArray(arrayName, lines);
                        count = lines.Length;
                    }
                    else if (node.Value("Action") == "Записать массив")
                    {
                        var lines = _variables.SnapshotArray(arrayName);
                        DataTools.WriteTextArray(path, lines);
                        count = lines.Length;
                    }
                    else throw new InvalidOperationException("Неизвестная операция файл ↔ массив.");
                    _variables.Set(node.Value("Count"), count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "CsvTable":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var output = node.Value("OutputPath");
                    if (output.Length > 0) output = ExternalTools.ResolvePath(output, _baseDirectory);
                    var delimiterText = node.Value("Delimiter", ";");
                    if (string.IsNullOrEmpty(delimiterText))
                        throw new InvalidOperationException(UiLanguage.T("Укажите разделитель CSV: ;, запятая или TAB."));
                    var delimiter = delimiterText == "TAB" ? '\t' : delimiterText[0];
                    var result = CsvTableTools.Execute(path, delimiter, node.Value("Action"),
                        node.Int("Row", 1), node.Int("Column", 1), node.Value("Value"),
                        node.Int("FilterColumn", 1), node.Value("FilterValue"), output);
                    _variables.Set(node.Value("Result"), result);
                    log(UiLanguage.Format("CSV: {0}; результат {1}", path, result));
                    current = node.Link("Далее");
                    break;
                }
                case "ExcelTable":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var result = SpreadsheetTools.Execute(path, node.Value("Sheet"), node.Value("Action"),
                        node.Int("Row", 1), node.Int("Column", 1), node.Value("Value"),
                        node.Int("FilterColumn", 1), node.Value("FilterValue"));
                    _variables.Set(node.Value("Result"), result);
                    log(UiLanguage.Format("Excel: {0}; результат {1}", path, result));
                    current = node.Link("Далее");
                    break;
                }
                case "PdfText":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var result = PdfTextTools.Execute(path, node.Value("Action"), node.Int("Page", 1));
                    _variables.Set(node.Value("Result"), result);
                    log(UiLanguage.Format("PDF: {0}; символов {1}", path, result.Length));
                    current = node.Link("Далее");
                    break;
                }
                case "FileQueue":
                {
                    var folder = ExternalTools.ResolvePath(node.Value("Folder"), _baseDirectory);
                    var state = ExternalTools.ResolvePath(node.Value("StatePath"), _baseDirectory);
                    var (file, outcome) = FileQueueTools.Execute(folder, node.Value("Mask"), state,
                        node.Value("Action"), node.Value("CurrentFile"), DateTimeOffset.UtcNow);
                    _variables.Set(node.Value("Result"), file);
                    log(UiLanguage.Format("Очередь файлов: {0}; состояние {1}; файл {2}",
                        state, outcome, file));
                    current = node.Link(outcome == "picked" ? "Есть файл" :
                        outcome == "empty" ? "Очередь пуста" : "Далее");
                    break;
                }
                case "Checkpoint":
                {
                    current = node.Link("Далее");
                    if (current is Guid next)
                    {
                        var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                        CheckpointTools.Save(path, project, next, _variables);
                        log(UiLanguage.Format("Контрольная точка сохранена: {0}", path));
                    }
                    break;
                }
                case "IniFile":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var value = DataTools.Ini(path, node.Value("Section"), node.Value("Key"),
                        node.Value("Action") == "Записать" ? node.Value("Value") : null);
                    _variables.Set(node.Value("Result"), value);
                    current = node.Link("Далее");
                    break;
                }
                case "DirectoryList":
                {
                    var pattern = node.Value("Pattern", "").Trim();
                    var path = pattern.Length == 0
                        ? ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory)
                        : ExternalTools.ResolvePath(Path.GetDirectoryName(pattern) is { Length: > 0 } folder ? folder : ".", _baseDirectory);
                    var mask = pattern.Length == 0 ? node.Value("Mask", "*") : Path.GetFileName(pattern);
                    if (pattern.Length > 0 && (string.IsNullOrEmpty(mask) || !mask.Contains('*') && !mask.Contains('?')))
                        throw new InvalidOperationException("Список файлов: укажите маску с * или ?.");
                    var names = DataTools.ListFiles(path, mask, node.Value("Kind") == "Папки");
                    if (names.Length > 10000) throw new InvalidOperationException("В каталоге более 10000 элементов; уточните маску.");
                    var array = node.Value("Array");
                    _variables.ReplaceArray(array, names);
                    _variables.Set(node.Value("Count"), names.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "HttpGet":
                    _variables.Set(node.Value("Result"), await DataTools.HttpGetAsync(
                        node.Value("Url"), node.Int("TimeoutMs", 10000), node.Value("Headers", ""), cancel));
                    current = node.Link("Далее");
                    break;
                case "HttpPost":
                {
                    var response = await DataTools.HttpPostAsync(node.Value("Url"), node.Value("Body"),
                        node.Value("ContentType"), node.Int("TimeoutMs", 10000),
                        node.Value("Headers"), cancel);
                    _variables.Set(node.Value("Result"), response.Body);
                    _variables.Set(node.Value("Status"), response.Status.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "JsonValue":
                    _variables.Set(node.Value("Result"), DataTools.JsonValue(
                        node.Value("Json"), node.Value("Path")));
                    current = node.Link("Далее");
                    break;
                case "TranslateText":
                    _variables.Set(node.Value("Result"), node.Value("Engine", "Argos локально") == "Argos локально"
                        ? await _translateLocal(node.Value("Text"), node.Value("Source"), node.Value("Target"),
                            node.Int("TimeoutMs", 10000), cancel)
                        : await _translate(node.Value("Text"), node.Value("Source"), node.Value("Target"),
                            node.Value("Endpoint"), node.Int("TimeoutMs", 10000), cancel));
                    current = node.Link("Далее");
                    break;
                case "DetectLanguage":
                {
                    var detected = await TranslationTools.DetectAsync(node.Value("Text"), node.Value("Endpoint"),
                        node.Int("TimeoutMs", 10000), cancel);
                    _variables.Set(node.Value("Result"), detected.Code);
                    _variables.Set(node.Value("Confidence"), detected.Confidence.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "LanguagePair":
                {
                    var found = node.Value("Engine", "LibreTranslate") == "Argos локально"
                        ? ArgosBridge.CanTranslate(await ArgosBridge.ListAsync(argosPythonPath, cancel),
                            node.Value("Source"), node.Value("Target"))
                        : (await TranslationTools.GetLanguagesAsync(node.Value("Endpoint"),
                            node.Int("TimeoutMs", 10000), cancel)).Any(language =>
                            language.Code.Equals(node.Value("Source"), StringComparison.OrdinalIgnoreCase) &&
                            language.Targets.Contains(node.Value("Target"), StringComparer.OrdinalIgnoreCase));
                    traceOutput = found ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "TranslateArray":
                {
                    var input = _variables.SnapshotArray(node.Value("Input"));
                    if (input.Length > 500) throw new InvalidOperationException("Перевод массива: не более 500 строк за один блок.");
                    var results = new string[input.Length];
                    for (var index = 0; index < input.Length; index++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        results[index] = node.Value("Engine", "Argos локально") == "Argos локально"
                            ? await _translateLocal(input[index], node.Value("Source"), node.Value("Target"),
                                node.Int("TimeoutMs", 10000), cancel)
                            : await _translate(input[index], node.Value("Source"), node.Value("Target"),
                                node.Value("Endpoint"), node.Int("TimeoutMs", 10000), cancel);
                    }
                    _variables.ReplaceArray(node.Value("Output"), results);
                    _variables.Set(node.Value("Count"), results.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "Wait":
                    await DelayWatchingScopeAsync(Math.Clamp(node.Int("Milliseconds", 500), 0, 3600000), cancel);
                    current = node.Link("Далее");
                    break;
                case "WaitEvent":
                {
                    var timeout = node.Int("TimeoutMs", 30000);
                    if (timeout is < 100 or > 3600000)
                        throw new InvalidOperationException("Таймаут события должен быть от 100 до 3600000 мс.");
                    var observed = node.Value("EventType") switch
                    {
                        "Окно появилось" => await EventWaitTools.WindowAsync(node.Value("WindowTitle"), true, timeout, cancel),
                        "Окно закрылось" => await EventWaitTools.WindowAsync(node.Value("WindowTitle"), false, timeout, cancel),
                        "Файл изменился" => await EventWaitTools.FileAsync(
                            ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory), timeout, cancel),
                        _ => throw new InvalidOperationException("Неизвестный тип ожидаемого события.")
                    };
                    traceOutput = observed ? "Событие" : "Таймаут";
                    current = node.Link(traceOutput);
                    break;
                }
                case "InputGroup":
                {
                    if (_groupLease.Value is not null)
                    {
                        await Execute(node.Link("Тело"), cancel, callDepth, rootFlow: false);
                        current = node.Link("Далее");
                        break;
                    }
                    await _input.WaitAsync(cancel);
                    DesktopInputLease? lease = null;
                    try
                    {
                        var focusWarningReported = false;
                        while (lease is null)
                        {
                            await _windowScope.WaitForOpenAsync(cancel);
                            var candidate = _heldLease ?? await DesktopInputCoordinator.AcquireAsync(cancel, log);
                            try
                            {
                                if (await _windowScope.TryFocusAsync(cancel)) lease = candidate;
                                else
                                {
                                    if (_heldLease == candidate)
                                    {
                                        ReleaseHeldInputs();
                                        _heldLease = null;
                                    }
                                    if (!focusWarningReported) log("Целевое окно пока не удалось активировать; группа ввода ожидает.");
                                    focusWarningReported = true;
                                }
                            }
                            finally { if (lease != candidate) candidate.Dispose(); }
                            if (lease is null) await Task.Delay(250, cancel);
                        }
                        _groupLease.Value = lease;
                        using var groupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                        groupTimeout.CancelAfter(Math.Clamp(node.Int("MaxMs", 10000), 100, 60000));
                        try { await Execute(node.Link("Тело"), groupTimeout.Token, callDepth, rootFlow: false); }
                        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                        {
                            throw new InvalidOperationException("Группа ввода превысила указанное максимальное время; очередь освобождена.");
                        }
                    }
                    finally
                    {
                        _groupLease.Value = null;
                        if (lease is not null)
                        {
                            if (!_heldKeys.IsEmpty || !_heldButtons.IsEmpty) _heldLease = lease;
                            else
                            {
                                _heldLease = null;
                                lease.Dispose();
                            }
                        }
                        _input.Release();
                    }
                    current = node.Link("Далее");
                    break;
                }
                case "IfMoved":
                {
                    var point = NativeMouse.Position();
                    var anchor = ResolveAnchor(node, point);
                    var selected = CursorMath.IsOutside(point, anchor, Math.Max(1, node.Int("ThresholdPx", 3)));
                    ReportIndicatorCondition(node, selected);
                    traceOutput = selected ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "IfPixel":
                {
                    var expected = ScreenTools.ParseColor(node.Value("Color"));
                    var actual = Color.Empty;
                    await WithInputAsync(() =>
                    {
                        actual = ScreenTools.Pixel(new Point(node.Int("PixelX"), node.Int("PixelY")));
                        return Task.CompletedTask;
                    }, cancel);
                    var matched = ScreenTools.Matches(actual, expected, Math.Clamp(node.Int("Tolerance"), 0, 255));
                    log($"Пиксель {node.Int("PixelX")},{node.Int("PixelY")}: #{actual.R:X2}{actual.G:X2}{actual.B:X2} — {(matched ? "совпадает" : "не совпадает")}");
                    ReportIndicatorCondition(node, matched);
                    traceOutput = matched ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "WaitPixel":
                {
                    var point = new Point(node.Int("PixelX"), node.Int("PixelY"));
                    var color = ScreenTools.ParseColor(node.Value("Color"));
                    var tolerance = Math.Clamp(node.Int("Tolerance"), 0, 255);
                    var interval = Math.Clamp(node.Int("PollMs", 100), 10, 60000);
                    var timeout = Math.Max(0, node.Int("TimeoutMs", 5000));
                    var timer = Stopwatch.StartNew();
                    var matched = false;
                    while (Volatile.Read(ref _stopRequested) == 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await WithInputAsync(() =>
                        {
                            matched = ScreenTools.Matches(ScreenTools.Pixel(point), color, tolerance);
                            return Task.CompletedTask;
                        }, cancel);
                        if (matched || (timeout > 0 && timer.ElapsedMilliseconds >= timeout)) break;
                        await Task.Delay(interval, cancel);
                    }
                    traceOutput = matched ? "Найден" : "Таймаут";
                    current = node.Link(traceOutput);
                    break;
                }
                case "IfKeyDown":
                {
                    var key = NativeMouse.ParseControlKey(node.Value("Key"));
                    var pressed = _keyState(key);
                    var selected = pressed.Down;
                    var mode = node.Value("Mode", "Пока удерживается");
                    if (mode == "Переключатель")
                    {
                        var gate = _conditionGates.GetOrAdd(node.Id, _ => new CursorActivationGate("Переключатель", false));
                        lock (gate) selected = gate.Update(pressed.Down, true, pressed.Pressed);
                    }
                    else if (mode == "При новом нажатии")
                    {
                        var gate = _pressGates.GetOrAdd(node.Id, _ => new KeyPressGate());
                        lock (gate) selected = gate.Update(pressed.Down, pressed.Pressed);
                    }
                    else if (mode != "Пока удерживается")
                        throw new InvalidOperationException($"Неизвестный режим проверки клавиши: {mode}");
                    ReportIndicatorCondition(node, selected);
                    traceOutput = selected ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "FindColor":
                {
                    var area = Area(node);
                    Point? found = null;
                    await WithInputAsync(() =>
                    {
                        found = ScreenTools.FindColor(area, ScreenTools.ParseColor(node.Value("Color")),
                            Math.Clamp(node.Int("Tolerance"), 0, 255), cancel);
                        return Task.CompletedTask;
                    }, cancel);
                    _foundPoint.Value = found;
                    if (found is Point p) log($"Цвет найден: {p.X},{p.Y}");
                    traceOutput = found == null ? "Нет" : "Найден";
                    current = node.Link(traceOutput);
                    break;
                }
                case "FindImage":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    if (!File.Exists(path)) throw new FileNotFoundException("Шаблон изображения не найден.", path);
                    Point? found = null;
                    await WithInputAsync(() =>
                    {
                        found = ScreenTools.FindImage(Area(node), path, Math.Clamp(node.Int("Tolerance"), 0, 255), cancel);
                        return Task.CompletedTask;
                    }, cancel);
                    _foundPoint.Value = found;
                    if (found is Point p) log($"Изображение найдено: {p.X},{p.Y} (верхний левый угол)");
                    traceOutput = found == null ? "Нет" : "Найден";
                    current = node.Link(traceOutput);
                    break;
                }
                case "Screenshot":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    await WithInputAsync(() =>
                    {
                        ScreenTools.SavePng(Area(node), path);
                        return Task.CompletedTask;
                    }, cancel);
                    log("Снимок сохранён: " + path);
                    current = node.Link("Далее");
                    break;
                }
                case "OcrImage":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var enginePath = node.Value("EnginePath");
                    try
                    {
                        var available = await OcrTools.ListLanguagesAsync(enginePath, cancel);
                        var required = TesseractInstaller.ParseLanguages(node.Value("Language"));
                        var missing = required.Where(language => !available.Contains(language)).ToArray();
                        if (missing.Length > 0) throw new FileNotFoundException(
                            UiLanguage.T("Не установлены языковые данные Tesseract: ") + string.Join(", ", missing));
                    }
                    catch (FileNotFoundException) when (_ensureOcr is not null)
                    {
                        enginePath = await _ensureOcr(node.Value("Language"), cancel) ?? throw new InvalidOperationException(
                            UiLanguage.T("Tesseract OCR не установлен. Установите его или укажите путь к tesseract.exe в блоке."));
                    }
                    var result = Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                        ? await PdfOcrTools.ReadAsync(path, node.Int("PdfPage", 0), node.Value("Language"),
                            enginePath, node.Int("TimeoutMs", 30000), cancel)
                        : await OcrTools.ReadAsync(path, node.Value("Language"), enginePath,
                            node.Int("TimeoutMs", 30000), cancel);
                    _variables.Set(node.Value("Result"), result);
                    log(UiLanguage.Format("OCR: распознано {0} символов из {1}", result.Length, path));
                    current = node.Link("Далее");
                    break;
                }
                case "WaitScreenText":
                {
                    var target = _variables.Expand(node.Value("Text"));
                    if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Введите искомый текст.");
                    var timeoutMs = node.Int("TimeoutMs", 10000);
                    var intervalMs = Math.Clamp(node.Int("IntervalMs", 500), 100, 10000);
                    if (timeoutMs is < 1000 or > 600000) throw new InvalidOperationException("Таймаут ожидания текста: 1000–600000 мс.");
                    var engine = node.Value("Engine");
                    try
                    {
                        var available = await OcrTools.ListLanguagesAsync(engine, cancel);
                        var required = TesseractInstaller.ParseLanguages(node.Value("Language", "eng+rus"));
                        var missing = required.Where(language => !available.Contains(language)).ToArray();
                        if (missing.Length > 0) throw new FileNotFoundException(
                            UiLanguage.T("Не установлены языковые данные Tesseract: ") + string.Join(", ", missing));
                    }
                    catch (FileNotFoundException) when (_ensureOcr is not null)
                    { engine = await _ensureOcr(node.Value("Language"), cancel) ?? throw new InvalidOperationException(
                        UiLanguage.T("Не удалось подготовить Tesseract OCR для распознавания текста на экране.")); }
                    var area = node.Value("AreaMode") == "Весь экран" ? SystemInformation.VirtualScreen :
                        new Rectangle(node.Int("Left"), node.Int("Top"), Math.Clamp(node.Int("Width", 800), 1, 10000), Math.Clamp(node.Int("Height", 600), 1, 10000));
                    if (area.Width <= 0 || area.Height <= 0 || Rectangle.Intersect(area, SystemInformation.VirtualScreen) != area)
                        throw new InvalidOperationException(UiLanguage.T("Область OCR должна находиться внутри виртуального экрана."));
                    var imagePath = Path.Combine(Path.GetTempPath(), "kliker-screen-ocr-" + Guid.NewGuid().ToString("N") + ".png");
                    var found = false;
                    var text = "";
                    try
                    {
                        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                        do
                        {
                            cancel.ThrowIfCancellationRequested();
                            var remainingMs = (int)Math.Floor((until - DateTime.UtcNow).TotalMilliseconds);
                            if (remainingMs < 1000) break;
                            ScreenTools.SavePng(area, imagePath);
                            try
                            {
                                text = await OcrTools.ReadAsync(imagePath, node.Value("Language", "eng+rus"), engine,
                                    Math.Min(30000, remainingMs), cancel);
                            }
                            catch (TimeoutException) when (DateTime.UtcNow >= until) { break; }
                            _variables.Set(node.Value("Result", "screen_text"), text);
                            found = text.Contains(target, StringComparison.CurrentCultureIgnoreCase);
                            if (found) break;
                            remainingMs = (int)Math.Floor((until - DateTime.UtcNow).TotalMilliseconds);
                            if (remainingMs > 0) await Task.Delay(Math.Min(intervalMs, remainingMs), cancel);
                        } while (DateTime.UtcNow < until);
                    }
                    finally { try { File.Delete(imagePath); } catch { } }
                    if (found) { log("Искомый текст появился на экране."); current = node.Link("Найдено"); }
                    else { log("Таймаут: искомый текст не появился на экране."); current = node.Link("Таймаут"); }
                    break;
                }
                case "BufferCapture":
                {
                    ScreenAnalysisBuffer? captured = null;
                    await WithInputAsync(() =>
                    {
                        var area = node.Value("AreaMode") == "Весь рабочий стол"
                            ? SystemInformation.VirtualScreen : Area(node);
                        captured = ScreenAnalysisBuffer.Capture(area);
                        return Task.CompletedTask;
                    }, cancel);
                    _screenBuffer = captured;
                    log($"Экран запомнен: {captured!.Bounds}");
                    current = node.Link("Далее");
                    break;
                }
                case "BufferPixel":
                {
                    var pixel = RequireScreenBuffer().Pixel(new Point(node.Int("PixelX"), node.Int("PixelY")));
                    var value = node.Value("Format") == "#RRGGBB"
                        ? $"#{pixel & 255:X2}{(pixel >> 8) & 255:X2}{(pixel >> 16) & 255:X2}"
                        : pixel.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _variables.Set(node.Value("Result"), value);
                    current = node.Link("Далее");
                    break;
                }
                case "BufferCount":
                {
                    var count = RequireScreenBuffer().Count(Area(node), ScreenTools.ParseColor(node.Value("Color")),
                        node.Int("Tolerance"), cancel);
                    _variables.Set(node.Value("Result"), count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "BufferHash":
                {
                    var algorithm = node.Value("Algorithm", "CRC32");
                    if (algorithm is not ("CRC32" or "XOR")) throw new InvalidOperationException("Неизвестный алгоритм контрольной суммы.");
                    var hash = RequireScreenBuffer().Hash(Area(node), algorithm == "CRC32", cancel);
                    _variables.Set(node.Value("Result"), hash.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "BufferReplace":
                {
                    var oldColor = node.Value("OldColor") == "-1" ? (Color?)null : ScreenTools.ParseColor(node.Value("OldColor"));
                    var changed = RequireScreenBuffer().Replace(Area(node), oldColor,
                        ScreenTools.ParseColor(node.Value("NewColor")), cancel);
                    _variables.Set(node.Value("Result"), changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "BufferFindColor":
                {
                    var point = RequireScreenBuffer().Find(Area(node), ScreenTools.ParseColor(node.Value("Color")),
                        node.Int("Tolerance"), cancel);
                    _foundPoint.Value = point;
                    if (point is Point found)
                    {
                        _variables.Set("_return1", found.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        _variables.Set("_return2", found.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    traceOutput = point is null ? "Нет" : "Найден";
                    current = node.Link(traceOutput);
                    break;
                }
                case "BufferFindImage":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var ignored = node.Value("IgnoreColor") == "-1" ? (Color?)null : ScreenTools.ParseColor(node.Value("IgnoreColor"));
                    var matches = RequireScreenBuffer().ScanImage(Area(node), path, node.Int("Tolerance"), ignored, 1, cancel);
                    var point = matches.Count == 0 ? (Point?)null : matches[0];
                    _foundPoint.Value = point;
                    if (point is Point found)
                    {
                        _variables.Set("_return1", found.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        _variables.Set("_return2", found.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    traceOutput = point is null ? "Нет" : "Найден";
                    current = node.Link(traceOutput);
                    break;
                }
                case "BufferScanColor":
                {
                    var matches = RequireScreenBuffer().ScanColor(Area(node), ScreenTools.ParseColor(node.Value("Color")),
                        node.Int("Tolerance"), node.Int("MaxResults"), cancel);
                    SaveScanResults(node, matches);
                    current = node.Link("Далее");
                    break;
                }
                case "BufferScanImage":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    var ignored = node.Value("IgnoreColor") == "-1" ? (Color?)null : ScreenTools.ParseColor(node.Value("IgnoreColor"));
                    var matches = RequireScreenBuffer().ScanImage(Area(node), path, node.Int("Tolerance"),
                        ignored, node.Int("MaxResults"), cancel);
                    SaveScanResults(node, matches);
                    current = node.Link("Далее");
                    break;
                }
                case "BufferSave":
                {
                    var path = ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory);
                    RequireScreenBuffer().SavePng(path);
                    log("Запомненный снимок сохранён: " + path);
                    current = node.Link("Далее");
                    break;
                }
                case "MoveFound":
                {
                    var found = _foundPoint.Value ?? throw new InvalidOperationException("Нет найденной точки. Сначала используйте «Найти цвет» или «Найти изображение».");
                    var target = new Point(found.X + node.Int("OffsetX"), found.Y + node.Int("OffsetY"));
                    await WithInputAsync(() =>
                    {
                        NativeMouse.Move(target);
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                }
                case "IfWindow":
                {
                    var selected = WindowTools.Find(node.Value("Title")) != IntPtr.Zero;
                    ReportIndicatorCondition(node, selected);
                    traceOutput = selected ? "Да" : "Нет";
                    current = node.Link(traceOutput);
                    break;
                }
                case "UiWait":
                {
                    var selector = new UiElementSelector(node.Value("WindowTitle"),
                        node.Value("AutomationId"), node.Value("ElementName"), node.Value("ControlType"));
                    var timeout = node.Int("TimeoutMs", 10000);
                    if (timeout is < 100 or > 60000)
                        throw new InvalidOperationException("Таймаут поиска элемента должен быть от 100 до 60000 мс.");
                    var clock = Stopwatch.StartNew();
                    var found = false;
                    do
                    {
                        cancel.ThrowIfCancellationRequested();
                        await PausePointAsync(cancel);
                        try { found = UiAutomationTools.Find(selector) is not null; }
                        catch (System.Windows.Automation.ElementNotAvailableException) { found = false; }
                        catch (System.Runtime.InteropServices.COMException) { found = false; }
                        if (found || clock.ElapsedMilliseconds >= timeout) break;
                        await Task.Delay(200, cancel);
                    } while (true);
                    traceOutput = found ? "Найден" : "Таймаут";
                    current = node.Link(traceOutput);
                    break;
                }
                case "UiAction":
                {
                    var selector = new UiElementSelector(node.Value("WindowTitle"),
                        node.Value("AutomationId"), node.Value("ElementName"), node.Value("ControlType"));
                    try
                    {
                        var element = UiAutomationTools.Find(selector) ??
                            throw new InvalidOperationException("Элемент Windows не найден.");
                        _variables.Set(node.Value("Result"), UiAutomationTools.Act(
                            element, node.Value("Action"), node.Value("Text")));
                        _variables.Set(node.Value("Error"), "");
                        traceOutput = "Далее";
                    }
                    catch (Exception error) when (error is InvalidOperationException or
                        System.Windows.Automation.ElementNotAvailableException or
                        System.Runtime.InteropServices.COMException)
                    {
                        _variables.Set(node.Value("Error"), error.Message);
                        traceOutput = "Ошибка";
                    }
                    current = node.Link(traceOutput);
                    break;
                }
                case "ActivateWindow":
                    await WithInputAsync(() =>
                    {
                        WindowTools.Activate(WindowTools.Find(node.Value("Title")));
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                case "MoveWindow":
                    await WithInputAsync(() =>
                    {
                        WindowTools.Move(WindowTools.Find(node.Value("Title")),
                            node.Int("WindowX"), node.Int("WindowY"), node.Int("WindowWidth"), node.Int("WindowHeight"));
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                case "WindowFind":
                {
                    var handle = WindowTools.Find(node.Value("Title"), node.Value("PrefixOnly") == "Да");
                    _variables.Set(node.Value("Result"), handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "WindowInfo":
                {
                    var bounds = WindowTools.Bounds(ParseWindowHandle(node.Value("Handle")));
                    _variables.Set(node.Value("XName"), bounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _variables.Set(node.Value("YName"), bounds.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _variables.Set(node.Value("WidthName"), bounds.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _variables.Set(node.Value("HeightName"), bounds.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "WindowAction":
                    await WithInputAsync(() =>
                    {
                        var handle = ParseWindowHandle(node.Value("Handle"));
                        switch (node.Value("Action"))
                        {
                            case "Позиция": WindowTools.Position(handle, node.Int("WindowX"), node.Int("WindowY")); break;
                            case "Размер": WindowTools.Size(handle, node.Int("WindowWidth"), node.Int("WindowHeight")); break;
                            case "Геометрия": WindowTools.Move(handle, node.Int("WindowX"), node.Int("WindowY"),
                                node.Int("WindowWidth"), node.Int("WindowHeight")); break;
                            case "Состояние":
                                var state = node.Int("State");
                                if (state is not (0 or 1)) throw new InvalidOperationException("Состояние окна должно быть 0 или 1.");
                                WindowTools.State(handle, state == 1);
                                break;
                            case "Активировать": WindowTools.Activate(handle); break;
                            default: throw new InvalidOperationException("Неизвестное действие с окном.");
                        }
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                case "ElementAtPoint":
                {
                    var handle = WindowTools.ElementAt(new Point(node.Int("PointX"), node.Int("PointY")));
                    _variables.Set(node.Value("Result"), handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "ElementInfo":
                {
                    var handle = ParseWindowHandle(node.Value("Handle"));
                    var value = node.Value("Info") switch
                    {
                        "Текст" => WindowTools.ElementText(handle),
                        "PID" => WindowTools.ProcessId(handle).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => throw new InvalidOperationException("Неизвестный тип данных элемента.")
                    };
                    _variables.Set(node.Value("Result"), value);
                    current = node.Link("Далее");
                    break;
                }
                case "ElementSetText":
                    await WithInputAsync(() =>
                    {
                        WindowTools.SetElementText(ParseWindowHandle(node.Value("Handle")), node.Value("Text"));
                        return Task.CompletedTask;
                    }, cancel);
                    current = node.Link("Далее");
                    break;
                case "MessageSend":
                {
                    long result = 0;
                    var mode = node.Value("Mode");
                    if (mode is not ("Асинхронно" or "Синхронно"))
                        throw new InvalidOperationException("Режим отправки должен быть синхронным или асинхронным.");
                    await WithInputAsync(() =>
                    {
                        result = WindowTools.SendNumericMessage(ParseWindowHandle(node.Value("Handle")),
                            node.Int("Message"), ParseLong(node.Value("WParam"), "wParam"),
                            ParseLong(node.Value("LParam"), "lParam"), mode == "Асинхронно");
                        return Task.CompletedTask;
                    }, cancel);
                    _variables.Set(node.Value("Result"), result.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "MessageReceive":
                {
                    var message = _messageInbox.Read(_lastMessageVersion);
                    _lastMessageVersion = message.Version;
                    _variables.Set(node.Value("WName"), message.WParam.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _variables.Set(node.Value("LName"), message.LParam.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    _variables.Set(node.Value("NewName"), message.IsNew ? "1" : "0");
                    current = node.Link("Далее");
                    break;
                }
                case "RunFile":
                case "RunAhk":
                    await ExternalTools.StartAsync(node.Value("Path"), node.Value("Arguments"),
                        node.Value("WaitForExit") == "Да", Math.Max(0, node.Int("TimeoutMs")),
                        _baseDirectory, node.Kind == "RunAhk", node.Value("Interpreter"), log, cancel);
                    current = node.Link("Далее");
                    break;
                case "RunScenario":
                {
                    if (nestedDepth >= 8)
                        throw new InvalidOperationException("Слишком глубокий вызов сценариев (максимум 8). Проверьте рекурсию.");
                    var path = Path.GetFullPath(node.Value("Path"), _baseDirectory);
                    if (!path.EndsWith(".klb", StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
                        new FileInfo(path).Length > 10 * 1024 * 1024)
                        throw new InvalidOperationException("Выберите существующий файл сценария .klb размером до 10 МБ.");
                    var childProject = ProjectCompatibility.Read(path);
                    if (HasReachableCycle(childProject))
                        throw new InvalidOperationException("Вызываемый сценарий содержит бесконечную обратную связь; для повторов используйте настройку блока.");
                    childProject.RunMode = "Один проход";
                    childProject.PlaybackSpeed = ParseSpeed(node.Value("Speed", "1.0"));
                    var runCount = node.Int("Times", 1);
                    if (runCount is < 1 or > 1000)
                        throw new InvalidOperationException("Повторений вызываемого сценария должно быть от 1 до 1000.");
                    for (var repetition = 0; repetition < runCount; repetition++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await PausePointAsync(cancel);
                        log($"Вызов сценария {Path.GetFileName(path)}: {repetition + 1}/{runCount}; {childProject.PlaybackSpeed:0.0}×");
                        var child = new Runner(childProject, log, displacement, Path.GetDirectoryName(path), watchStatus,
                            keyState, relativeMove, conditionStatus, null, _clearLog, _showLog, playSound, beep,
                            inputDialog, choiceDialog, messageInbox, selfHandle, radioDialog, hintPopup,
                            translate, argosPythonPath, translateLocal, speakText, showcaseStep, nestedDepth + 1,
                            debugState, _ensureOcr);
                        foreach (var (name, value) in ParseModulePairs(node.Value("Inputs"), "входы"))
                            child._variables.Set(name, value);
                        _childRunner = child;
                        try
                        {
                            if (IsPauseRequested) child.SetPaused(true);
                            await child.RunAsync(cancel);
                            foreach (var (parentName, childName) in ParseModulePairs(node.Value("Outputs"), "выходы"))
                                _variables.Set(parentName, child._variables.Get(childName));
                        }
                        finally { _childRunner = null; }
                    }
                    current = node.Link("Далее");
                    break;
                }
                case "ShowcaseStep":
                    await _showcaseStep(node.Value("Action"), _variables, cancel);
                    current = node.Link("Далее");
                    break;
                case "Repeat":
                    var repetitions = Math.Clamp(node.Int("Times", 3), 0, 100000);
                    log($"Повторить: запланировано {repetitions} выполнений тела");
                    for (var i = 0; i < repetitions && Volatile.Read(ref _stopRequested) == 0; i++)
                    {
                        if (repetitions <= 100) log($"Повторить: тело {i + 1}/{repetitions}");
                        await Execute(node.Link("Тело"), cancel, callDepth, rootFlow: false);
                    }
                    log("Повторить: переход к выходу «Далее»");
                    current = node.Link("Далее");
                    break;
                case "Parallel":
                    if (_groupLease.Value is not null)
                        throw new InvalidOperationException("Блок «Параллельно» нельзя помещать внутрь «Ввода без прерывания».");
                    await Task.WhenAll(Execute(node.Link("Ветка A"), cancel, callDepth, rootFlow: false),
                        Execute(node.Link("Ветка B"), cancel, callDepth, rootFlow: false));
                    current = node.Link("Далее");
                    break;
                case "CallSub":
                {
                    var name = node.Value("Name").Trim();
                    if (!_subroutines.TryGetValue(name, out var definition))
                        throw new InvalidOperationException($"Подпрограмма «{name}» не найдена.");
                    if (callDepth >= 32) throw new InvalidOperationException("Слишком глубокий вызов подпрограмм (максимум 32). Проверьте рекурсию.");
                    try { await Execute(definition.Link("Тело"), cancel, callDepth + 1, rootFlow: false); }
                    catch (SubReturnSignal) { /* END_SUB returns to this call site. */ }
                    current = node.Link("Далее");
                    break;
                }
                case "SubStart":
                    throw new InvalidOperationException("Блок «Начало подпрограммы» нельзя запускать напрямую. Вызовите его через блок «Вызвать подпрограмму».");
                case "SubReturn":
                    if (callDepth == 0) throw new InvalidOperationException("«Возврат из подпрограммы» находится вне подпрограммы.");
                    throw new SubReturnSignal();
                case "BranchEnd":
                    if (sourceNode.LogActions)
                    {
                        log(BlockTrace.Completed(sourceNode, null));
                        log(BlockTrace.Details(sourceNode, node, traceScope.Changes));
                    }
                    return;
                case "Log":
                {
                    var message = node.Value("Text", "Шаг выполнен");
                    log(message);
                    _scriptLog.Write(message);
                    current = node.Link("Далее");
                    break;
                }
                case "LogFile":
                    _scriptLog.Configure(node.Value("Path"));
                    log(_scriptLog.PathOnDisk is { } scriptPath
                        ? UiLanguage.Format("Файл сообщений сценария: {0}", scriptPath)
                        : UiLanguage.T("Файл сообщений сценария отключён."));
                    current = node.Link("Далее");
                    break;
                case "LogClear":
                    _clearLog?.Invoke();
                    current = node.Link("Далее");
                    break;
                case "LogShow":
                {
                    var visible = node.Int("Visible");
                    if (visible is not (0 or 1)) throw new InvalidOperationException("LOGSHOW принимает 0 или 1.");
                    _showLog?.Invoke(visible == 1);
                    current = node.Link("Далее");
                    break;
                }
                case "PlaySound":
                    _playSound(ExternalTools.ResolvePath(node.Value("Path"), _baseDirectory));
                    current = node.Link("Далее");
                    break;
                case "SpeakText":
                    await _speakText(node.Value("Text"), node.Value("Voice"),
                        node.Int("Rate"), node.Int("Volume"), cancel);
                    current = node.Link("Далее");
                    break;
                case "Beep":
                    await _beep(node.Int("Frequency"), node.Int("DurationMs"), cancel);
                    current = node.Link("Далее");
                    break;
                case "InputDialog":
                {
                    var result = await _inputDialog(node.Value("Message"), node.Value("Default"),
                        node.Int("DelaySeconds"), DialogWindowOptions.From(node), cancel);
                    cancel.ThrowIfCancellationRequested();
                    _variables.Set(node.Value("Result"), result);
                    current = node.Link("Далее");
                    break;
                }
                case "ChoiceDialog":
                {
                    var result = await _choiceDialog(node.Value("Message"), node.Int("Buttons"),
                        node.Int("Icon"), DialogWindowOptions.From(node), cancel);
                    cancel.ThrowIfCancellationRequested();
                    _variables.Set(node.Value("Result"), result.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "RadioDialog":
                {
                    var options = node.Value("Options").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (options.Length is < 1 or > 100 || options.Any(option => option.Length > 250))
                        throw new InvalidOperationException("RADIOBOX требует 1–100 вариантов по одному на строке, до 250 символов каждый.");
                    var result = await _radioDialog(node.Value("Message"), options, DialogWindowOptions.From(node), cancel);
                    cancel.ThrowIfCancellationRequested();
                    if (result < 0 || result > options.Length)
                        throw new InvalidOperationException("Диалог выбора вернул недопустимый номер варианта.");
                    _variables.Set(node.Value("Result"), result.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    current = node.Link("Далее");
                    break;
                }
                case "HintPopup":
                    _hintPopup(node.Value("Message"), node.Value("Title"));
                    current = node.Link("Далее");
                    break;
                case "Stop":
                    Interlocked.Exchange(ref _stopRequested, 1);
                    if (sourceNode.LogActions)
                    {
                        log(BlockTrace.Completed(sourceNode, null));
                        log(BlockTrace.Details(sourceNode, node, traceScope.Changes));
                    }
                    return;
                default: throw new InvalidOperationException($"Неизвестный блок: {node.Kind}");
            }
            if (sourceNode.LogActions)
            {
                log(BlockTrace.Completed(sourceNode, current, traceOutput));
                log(BlockTrace.Details(sourceNode, node, traceScope.Changes));
            }
            errorAttempts.Remove(id);
            if (_stepRemaining > 0) _stepRemaining--;
            }
            catch (SubReturnSignal)
            {
                if (sourceNode.LogActions) log(BlockTrace.Completed(sourceNode, null));
                if (sourceNode.LogActions && resolvedNode is not null) log(BlockTrace.Details(sourceNode, resolvedNode, traceScope.Changes));
                throw;
            }
            catch (OperationCanceledException)
            {
                if (sourceNode.LogActions) log(BlockTrace.Interrupted(sourceNode));
                if (sourceNode.LogActions && resolvedNode is not null) log(BlockTrace.Details(sourceNode, resolvedNode, traceScope.Changes));
                throw;
            }
            catch (Exception error)
            {
                if (sourceNode.LogActions) log(BlockTrace.Failed(sourceNode, error));
                if (sourceNode.LogActions && resolvedNode is not null) log(BlockTrace.Details(sourceNode, resolvedNode, traceScope.Changes));
                var attempt = errorAttempts.GetValueOrDefault(id);
                var decision = BlockErrorPolicy.Decide(sourceNode, attempt);
                if (decision.Retry)
                {
                    errorAttempts[id] = attempt + 1;
                    visited.Remove(id);
                    log(UiLanguage.Format("Блок: повтор после ошибки ({0}/{1}): {2}",
                        attempt + 1, sourceNode.Int("__ErrorRetries"), error.Message));
                    await DelayWatchingScopeAsync(sourceNode.Int("__ErrorDelayMs"), cancel);
                    current = id;
                    continue;
                }
                errorAttempts.Remove(id);
                if (decision.Throw) throw;
                _variables.Set("_last_error", error.Message);
                log(UiLanguage.Format("Блок: ошибка обработана; переход к {0}: {1}",
                    decision.Next?.ToString() ?? "end", error.Message));
                current = decision.Next;
                continue;
            }
        }
    }

    private static bool HasReachableCycle(ProjectModel child)
    {
        var byId = child.Nodes.ToDictionary(node => node.Id);
        var starts = child.Nodes.Where(node => node.Kind == "Start").ToArray();
        if (starts.Length != 1) throw new InvalidDataException("Вызываемый сценарий должен содержать один блок «Старт».");
        var active = new HashSet<Guid>();
        var done = new HashSet<Guid>();
        bool Visit(Guid id)
        {
            if (active.Contains(id)) return true;
            if (done.Contains(id)) return false;
            if (!byId.TryGetValue(id, out var node)) throw new InvalidDataException("Связь вызываемого сценария ведёт на отсутствующий блок.");
            active.Add(id);
            foreach (var target in node.Links.Values)
                if (target is Guid next && Visit(next)) return true;
            active.Remove(id);
            done.Add(id);
            return false;
        }
        if (Visit(starts[0].Id)) return true;
        return child.Nodes.Where(node => node.Kind == "SubStart").Any(node => Visit(node.Id));
    }

    internal static IReadOnlyList<(string Name, string Value)> ParseModulePairs(string text, string label)
    {
        var result = new List<(string, string)>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var equal = line.IndexOf('=');
            if (equal <= 0) throw new InvalidOperationException($"Сценарий: неверные {label}; требуется имя=значение.");
            var name = line[..equal].Trim();
            var value = line[(equal + 1)..].Trim();
            if (!VariableNameRules.IsAscii(name) || result.Any(item => item.Item1 == name))
                throw new InvalidOperationException($"Сценарий: неверное или повторное имя в разделе «{label}»: {name}.");
            if (label == "выходы" && !VariableNameRules.IsAscii(value))
                throw new InvalidOperationException("Сценарий: имя дочерней выходной переменной должно быть латинским.");
            result.Add((name, value));
            if (result.Count > 100) throw new InvalidOperationException("Не более 100 входов или выходов сценария.");
        }
        return result;
    }

    private sealed class CycleWatchState(Point anchor, string activationMode, bool activationInitiallyHeld)
    {
        public readonly SemaphoreSlim Mutex = new(1, 1);
        public readonly Stopwatch Clock = Stopwatch.StartNew();
        public readonly CursorActivationGate Activation = new(activationMode, activationInitiallyHeld);
        public Point Anchor = anchor;
        public Point Expected = anchor;
        public long LastPollMs = -1000;
        public TimeSpan NextAllowed;
        public CursorWatchState? LastStatus;
        public bool WasActive;
    }

    private void PruneCycleWatches(HashSet<Guid> visited)
    {
        foreach (var (id, state) in _cycleWatches)
        {
            if (visited.Contains(id) || state.LastStatus is null) continue;
            state.LastStatus = null;
            state.WasActive = false;
            state.Expected = state.Anchor;
            state.NextAllowed = TimeSpan.Zero;
            watchStatus?.Invoke(id, null);
        }
    }

    private async Task WatchCycleTick(BlockNode node, CancellationToken cancel)
    {
        await PausePointAsync(cancel);
        var activationMode = node.Value("ActivationMode", "Переключатель");
        var activationKey = activationMode == "Сразу" ? (byte)0 : NativeMouse.ParseControlKey(node.Value("ActivationKey", "F9"));
        var requiredMode = node.Value("RequiredHold", "Левая кнопка мыши");
        var requiredKey = requiredMode switch
        {
            "Нет" => (byte)0,
            "Левая кнопка мыши" => (byte)1,
            "Правая кнопка мыши" => (byte)2,
            "Средняя кнопка мыши" => (byte)4,
            "Клавиша" => NativeMouse.ParseControlKey(node.Value("RequiredKey", "SHIFT")),
            _ => throw new InvalidOperationException($"Неизвестное условие удержания: {requiredMode}")
        };
        var state = _cycleWatches.GetOrAdd(node.Id, _ =>
        {
            var position = NativeMouse.Position();
            var anchor = ResolveAnchor(node, position);
            log($"Циклическое наблюдение: опора {anchor.X},{anchor.Y}; активация {activationMode} ({node.Value("ActivationKey", "F9")}); дополнительно {requiredMode}");
            return new CycleWatchState(anchor, activationMode, activationKey != 0 && _keyState(activationKey).Down);
        });
        await state.Mutex.WaitAsync(cancel);
        try
        {
            cancel.ThrowIfCancellationRequested();
            var elapsed = state.Clock.Elapsed;
            var poll = Math.Clamp(node.Int("PollMs", 10), 5, 1000);
            if (state.Clock.ElapsedMilliseconds - state.LastPollMs < poll) return;
            state.LastPollMs = state.Clock.ElapsedMilliseconds;
            var activation = activationKey == 0 ? (Down: false, Pressed: false) : _keyState(activationKey);
            var active = state.Activation.Update(activation.Down,
                requiredKey == 0 || _keyState(requiredKey).Down, activation.Pressed);
            var status = new CursorWatchState(state.Activation.Armed, active, activationMode,
                node.Value("ActivationKey", "F9"), requiredMode, state.Anchor, node.Value("RequiredKey", "SHIFT"));
            if (status != state.LastStatus)
            {
                watchStatus?.Invoke(node.Id, status);
                state.LastStatus = status;
            }
            if (active != state.WasActive)
            {
                log(active ? "Слежение за курсором: включено" : "Слежение за курсором: ожидание клавиш");
                state.WasActive = active;
            }
            if (!active)
            {
                state.Expected = state.Anchor;
                state.NextAllowed = TimeSpan.Zero;
                return;
            }
            var threshold = Math.Max(1, node.Int("ThresholdPx", 3));
            var observed = NativeMouse.Position();
            if (!CursorMath.IsOutside(observed, state.Anchor, threshold)) state.Expected = state.Anchor;
            else if (_windowScope.IsForeground && CursorMath.IsOutside(observed, state.Expected, threshold) && elapsed >= state.NextAllowed)
            {
                await WithInputAsync(() =>
                {
                    observed = NativeMouse.Position();
                    if (CursorMath.IsOutside(observed, state.Expected, threshold))
                    {
                        var dx = observed.X - state.Anchor.X;
                        var dy = observed.Y - state.Anchor.Y;
                        var target = ResolveTarget(node, observed, state.Anchor, NativeMouse.CenterFor(state.Anchor));
                        NativeMouse.Move(target);
                        state.Expected = target;
                        state.NextAllowed = state.Clock.Elapsed + TimeSpan.FromMilliseconds(Math.Clamp(node.Int("CooldownMs", 60), 0, 60000));
                        displacement(dx, dy);
                        log($"Смещение ΔX={dx}, ΔY={dy}; возврат → {target.X},{target.Y}");
                    }
                    return Task.CompletedTask;
                }, cancel, foregroundOnly: true);
            }
        }
        finally { state.Mutex.Release(); }
    }

    private async Task Watch(BlockNode node, CancellationToken cancel)
    {
        var initial = NativeMouse.Position();
        var anchor = ResolveAnchor(node, initial);
        var expected = anchor;
        var threshold = Math.Max(1, node.Int("ThresholdPx", 3));
        var poll = Math.Clamp(node.Int("PollMs", 10), 5, 1000);
        var cooldown = Math.Clamp(node.Int("CooldownMs", 60), 0, 60000);
        var duration = Math.Max(0, node.Int("DurationMs"));
        var activationMode = node.Value("ActivationMode", "Переключатель");
        var activationKey = activationMode == "Сразу" ? (byte)0 : NativeMouse.ParseControlKey(node.Value("ActivationKey", "F9"));
        var requiredMode = node.Value("RequiredHold", "Левая кнопка мыши");
        var requiredKey = requiredMode switch
        {
            "Нет" => (byte)0,
            "Левая кнопка мыши" => (byte)1,
            "Правая кнопка мыши" => (byte)2,
            "Средняя кнопка мыши" => (byte)4,
            "Клавиша" => NativeMouse.ParseControlKey(node.Value("RequiredKey", "SHIFT")),
            _ => throw new InvalidOperationException($"Неизвестное условие удержания: {requiredMode}")
        };
        var gate = new CursorActivationGate(activationMode, activationKey != 0 && _keyState(activationKey).Down);
        var wasActive = false;
        CursorWatchState? lastStatus = null;
        var elapsed = Stopwatch.StartNew();
        var nextAllowed = TimeSpan.Zero;
        log($"Наблюдение: опора {anchor.X},{anchor.Y}; порог {threshold} px; активация {activationMode} ({node.Value("ActivationKey", "F9")}); дополнительно {requiredMode}");

        try
        {
            while (Volatile.Read(ref _stopRequested) == 0 && (duration == 0 || elapsed.ElapsedMilliseconds < duration))
            {
                cancel.ThrowIfCancellationRequested();
                await PausePointAsync(cancel);
                await EnsureWindowScopeAsync(cancel);
                var activation = activationKey == 0 ? (Down: false, Pressed: false) : _keyState(activationKey);
                var active = gate.Update(activation.Down,
                    requiredKey == 0 || _keyState(requiredKey).Down, activation.Pressed);
                var currentStatus = new CursorWatchState(gate.Armed, active, activationMode,
                    node.Value("ActivationKey", "F9"), requiredMode, anchor, node.Value("RequiredKey", "SHIFT"));
                if (currentStatus != lastStatus)
                {
                    watchStatus?.Invoke(node.Id, currentStatus);
                    lastStatus = currentStatus;
                }
                if (active != wasActive)
                {
                    log(active ? "Слежение за курсором: включено" : "Слежение за курсором: ожидание клавиш");
                    wasActive = active;
                }
                if (!active)
                {
                    expected = anchor;
                    nextAllowed = TimeSpan.Zero;
                    await Task.Delay(poll, cancel);
                    continue;
                }
                var observed = NativeMouse.Position();
                if (!CursorMath.IsOutside(observed, anchor, threshold)) expected = anchor;
                else if (_windowScope.IsForeground && CursorMath.IsOutside(observed, expected, threshold) && elapsed.Elapsed >= nextAllowed)
                {
                    await WithInputAsync(() =>
                    {
                        observed = NativeMouse.Position();
                        if (CursorMath.IsOutside(observed, expected, threshold))
                        {
                            var dx = observed.X - anchor.X;
                            var dy = observed.Y - anchor.Y;
                            var target = ResolveTarget(node, observed, anchor, NativeMouse.CenterFor(anchor));
                            NativeMouse.Move(target);
                            expected = target;
                            nextAllowed = elapsed.Elapsed + TimeSpan.FromMilliseconds(cooldown);
                            displacement(dx, dy);
                            log($"Смещение ΔX={dx}, ΔY={dy}; возврат → {target.X},{target.Y}");
                        }
                        return Task.CompletedTask;
                    }, cancel, foregroundOnly: true);
                }
                await Task.Delay(poll, cancel);
            }
        }
        finally { watchStatus?.Invoke(node.Id, null); }
    }

    private static Point ResolveAnchor(BlockNode node, Point observed) => CursorMath.Anchor(
        node.Value("Anchor", "Центр монитора"), observed, NativeMouse.CenterFor(observed),
        node.Int("AnchorX"), node.Int("AnchorY"));

    private static Point ResolveTarget(BlockNode node, Point observed, Point anchor, Point center) => CursorMath.Target(
        node.Value("TargetMode", "Центр монитора"), observed, anchor, center,
        node.Int("TargetX"), node.Int("TargetY"), node.Int("OffsetX"), node.Int("OffsetY"));

    private static Rectangle Area(BlockNode node) => Rectangle.FromLTRB(
        node.Int("Left"), node.Int("Top"), node.Int("Right"), node.Int("Bottom"));

    private ScreenAnalysisBuffer RequireScreenBuffer() => _screenBuffer ??
        throw new InvalidOperationException("Графический буфер пуст. Сначала выполните блок «Запомнить экран».");

    private void SaveScanResults(BlockNode node, IReadOnlyList<Point> matches)
    {
        var array = node.Value("ArrayName").Trim().TrimStart('$');
        var count = node.Value("CountName").Trim().TrimStart('$');
        _variables.ClearArray(array);
        for (var i = 0; i < matches.Count; i++)
        {
            _variables.SetArray(array, i * 2, matches[i].X.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _variables.SetArray(array, i * 2 + 1, matches[i].Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        _variables.Set(count, matches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static IntPtr ParseWindowHandle(string value) =>
        long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
            out var number) && number > 0 ? new IntPtr(number)
            : throw new InvalidOperationException("HWND должен быть положительным целым числом. Найдите окно блоком «Найти HWND окна».");

    private static long ParseLong(string value, string label) =>
        long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
            out var number) ? number : throw new InvalidOperationException($"{label} должен быть целым числом.");

    private static bool ExpressionTrue(string value) => ExpressionEvaluator.IsTrueValue(value);

    private static bool SwitchEquals(string left, string right) =>
        decimal.TryParse(left, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var a) &&
        decimal.TryParse(right, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var b)
            ? a == b : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

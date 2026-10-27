namespace KlikerBlocks;

internal sealed class WindowScopeGuard
{
    private readonly WindowScopeSettings _settings;
    private readonly Func<WindowScopeSettings, IntPtr> _find;
    private readonly Func<WindowScopeSettings, bool> _foreground;
    private readonly Action<IntPtr> _activate;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private bool _waiting;
    private bool _activationWarning;

    public WindowScopeGuard(WindowScopeSettings settings, Action<string> log,
        Func<WindowScopeSettings, IntPtr>? find = null,
        Func<WindowScopeSettings, bool>? foreground = null,
        Action<IntPtr>? activate = null)
    {
        _settings = settings.Copy();
        _log = log;
        _find = find ?? WindowTools.FindTarget;
        _foreground = foreground ?? WindowTools.IsTargetForeground;
        _activate = activate ?? WindowTools.Activate;
        var error = Validate(_settings);
        if (error is not null) throw new ArgumentException(error);
    }

    public bool Enabled => _settings.Enabled;
    public bool IsForeground => !_settings.Enabled || _foreground(_settings);
    public bool IsOpen => !_settings.Enabled || _find(_settings) != IntPtr.Zero;

    public async Task WaitForOpenAsync(CancellationToken cancel)
    {
        if (!_settings.Enabled) return;
        var reported = false;
        while (_find(_settings) == IntPtr.Zero)
        {
            cancel.ThrowIfCancellationRequested();
            if (!reported) _log("Целевое окно не найдено; сценарий ожидает его появления.");
            reported = true;
            await Task.Delay(150, cancel);
        }
        if (reported) _log("Целевое окно найдено; сценарий продолжен.");
    }

    public async Task<bool> TryFocusAsync(CancellationToken cancel)
    {
        if (!_settings.Enabled || _foreground(_settings)) return true;
        var target = _find(_settings);
        if (target == IntPtr.Zero) return false;
        try { _activate(target); }
        catch (InvalidOperationException) { return false; }
        if (_foreground(_settings)) return true;
        await Task.Delay(50, cancel);
        return _foreground(_settings);
    }

    public static string? Validate(WindowScopeSettings settings)
    {
        if (!settings.Enabled) return null;
        return settings.MatchMode switch
        {
            "Процесс" when string.IsNullOrWhiteSpace(settings.ProcessName) => "Укажите имя процесса целевого окна.",
            "Процесс и заголовок" when string.IsNullOrWhiteSpace(settings.ProcessName) ||
                string.IsNullOrWhiteSpace(settings.TitleContains) => "Укажите имя процесса и часть заголовка.",
            "Заголовок" when string.IsNullOrWhiteSpace(settings.TitleContains) => "Укажите часть заголовка целевого окна.",
            "HWND" when settings.WindowHandle <= 0 => "Укажите положительный код HWND целевого окна.",
            "Процесс" or "Процесс и заголовок" or "Заголовок" or "HWND" => null,
            _ => "Неизвестный способ привязки к окну."
        };
    }

    public async Task EnsureReadyAsync(CancellationToken cancel)
    {
        if (!_settings.Enabled) return;
        await _mutex.WaitAsync(cancel);
        try
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                if (_foreground(_settings))
                {
                    if (_waiting) _log("Целевое окно снова активно; сценарий продолжен.");
                    _waiting = false;
                    _activationWarning = false;
                    return;
                }
                var target = _find(_settings);
                if (target == IntPtr.Zero)
                {
                    if (!_waiting) _log("Целевое окно не найдено; сценарий ожидает его появления.");
                    _waiting = true;
                    await Task.Delay(150, cancel);
                    continue;
                }
                try { _activate(target); }
                catch (InvalidOperationException)
                {
                    if (!_activationWarning)
                        _log("Windows не разрешила активировать целевое окно. Переключитесь на него вручную; ввод приостановлен.");
                    _activationWarning = true;
                }
                if (_foreground(_settings)) continue;
                _waiting = true;
                await Task.Delay(100, cancel);
            }
        }
        finally { _mutex.Release(); }
    }
}

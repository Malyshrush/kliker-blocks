namespace KlikerBlocks;

internal sealed class CursorActivationGate
{
    private readonly string _mode;
    private bool _previousDown;
    private bool _switchedOn;

    public CursorActivationGate(string mode, bool initiallyDown)
    {
        if (mode is not ("Сразу" or "Переключатель" or "Пока удерживается"))
            throw new InvalidOperationException($"Неизвестный режим активации: {mode}");
        _mode = mode;
        _previousDown = initiallyDown;
    }

    public bool Armed => _mode switch
    {
        "Сразу" => true,
        "Переключатель" => _switchedOn,
        "Пока удерживается" => _previousDown,
        _ => false
    };

    public bool Update(bool activationDown, bool requiredDown, bool pressedSinceLastRead = false)
    {
        if (_mode == "Переключатель" && !_previousDown && (activationDown || pressedSinceLastRead))
            _switchedOn = !_switchedOn;
        _previousDown = activationDown;
        return Armed && requiredDown;
    }
}

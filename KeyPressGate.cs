namespace KlikerBlocks;

internal sealed class KeyPressGate
{
    private bool _previousDown;

    public bool Update(bool down, bool pressedSinceLastRead = false)
    {
        var pulse = !_previousDown && (down || pressedSinceLastRead);
        _previousDown = down;
        return pulse;
    }
}

namespace KlikerBlocks;

internal readonly record struct MouseMappingCommand(MouseMappingBinding Binding, bool Press = true);
internal readonly record struct MouseMappingResult(bool Suppress, IReadOnlyList<MouseMappingCommand> Commands);

internal sealed class MouseMappingEngine
{
    private readonly MouseMappingSettings _settings;
    private readonly Dictionary<int, int> _layers = [];
    private readonly HashSet<string> _heldSources = [];
    private readonly Dictionary<string, List<MouseMappingBinding>> _active = [];
    private readonly Dictionary<string, long> _downAt = [];
    private readonly Dictionary<Guid, long> _repeatedAt = [];
    private readonly HashSet<Guid> _longFired = [];
    private readonly HashSet<Guid> _toggled = [];
    private int _profileIndex = -1;

    public MouseMappingEngine(MouseMappingSettings settings) => _settings = settings;
    public int ProfileIndex => _profileIndex;
    public int LayerIndex => _profileIndex < 0 ? -1 : _layers.GetValueOrDefault(_profileIndex);
    public string ProfileName => _profileIndex < 0 ? "нет" : _settings.Profiles[_profileIndex].Name;
    public string LayerName => LayerIndex < 0 ? "нет" : _settings.Profiles[_profileIndex].Layers[LayerIndex].Name;

    public IReadOnlyList<MouseMappingCommand> SetContext(MouseMappingContext context)
    {
        var index = _settings.Enabled ? MouseProfileMatcher.Select(_settings, context) : -1;
        if (index == _profileIndex) return [];
        var releases = ReleaseActive();
        _profileIndex = index;
        if (index >= 0 && _settings.Profiles[index].Layers.Count > 0)
            _layers[index] = Math.Clamp(_layers.GetValueOrDefault(index), 0, _settings.Profiles[index].Layers.Count - 1);
        return releases;
    }

    public IReadOnlyList<MouseMappingCommand> Suspend()
    {
        if (_profileIndex < 0) return [];
        var releases = ReleaseActive();
        _profileIndex = -1;
        return releases;
    }

    public IReadOnlyList<MouseMappingCommand> SwitchLayer(string name)
    {
        if (_profileIndex < 0) return [];
        var layers = _settings.Profiles[_profileIndex].Layers;
        var target = int.TryParse(name, out var number) ? number - 1 :
            layers.FindIndex(layer => layer.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (target < 0 || target >= layers.Count || target == LayerIndex) return [];
        var releases = ReleaseActive();
        _layers[_profileIndex] = target;
        return releases;
    }

    public MouseMappingResult Handle(string source, bool isDown, long now)
    {
        if (_profileIndex < 0 || LayerIndex < 0) return new MouseMappingResult(false, []);
        var commands = new List<MouseMappingCommand>();
        commands.AddRange(Tick(now));
        var isWheel = MouseMappingNames.IsWheel(source);
        if (isDown)
        {
            if (!isWheel && !_heldSources.Add(source))
                return new MouseMappingResult(_active.GetValueOrDefault(source)?.Any(binding => binding.BlockOriginal) == true, commands);
            var layer = _settings.Profiles[_profileIndex].Layers[LayerIndex];
            var chord = layer.Bindings.Where(binding => binding.Source == source &&
                binding.Trigger == "Сочетание" && _heldSources.Contains(binding.ChordSource)).ToList();
            var bindings = chord.Count > 0 ? chord : layer.Bindings.Where(binding => binding.Source == source &&
                binding.Trigger != "Сочетание").ToList();
            if (!isWheel)
            {
                _active[source] = bindings;
                _downAt[source] = now;
            }
            foreach (var binding in bindings)
            {
                switch (binding.Trigger)
                {
                    case "Нажатие": case "Сочетание": case "Удержание":
                        commands.Add(new MouseMappingCommand(binding)); break;
                    case "Повтор":
                        commands.Add(new MouseMappingCommand(binding));
                        _repeatedAt[binding.Id] = now; break;
                    case "Переключатель":
                        var turnOn = !_toggled.Remove(binding.Id);
                        if (turnOn) _toggled.Add(binding.Id);
                        commands.Add(new MouseMappingCommand(binding, turnOn));
                        break;
                }
            }
            return new MouseMappingResult(bindings.Any(binding => binding.BlockOriginal), commands);
        }

        if (!_heldSources.Remove(source) || !_active.Remove(source, out var active))
            return new MouseMappingResult(false, commands);
        var longOccurred = active.Any(binding => _longFired.Contains(binding.Id));
        foreach (var binding in active)
        {
            if (binding.Trigger is "Удержание" || binding.Action == "Слой пока удерживается")
                commands.Add(new MouseMappingCommand(binding, false));
            else if (binding.Trigger == "Отпускание" ||
                (binding.Trigger == "Короткое нажатие" && !longOccurred))
                commands.Add(new MouseMappingCommand(binding));
            _longFired.Remove(binding.Id);
            _repeatedAt.Remove(binding.Id);
        }
        _downAt.Remove(source);
        return new MouseMappingResult(active.Any(binding => binding.BlockOriginal), commands);
    }

    public IReadOnlyList<MouseMappingCommand> Tick(long now)
    {
        var commands = new List<MouseMappingCommand>();
        foreach (var (source, bindings) in _active)
        {
            if (!_downAt.TryGetValue(source, out var started)) continue;
            foreach (var binding in bindings)
            {
                if (binding.Trigger == "Долгое удержание" && !_longFired.Contains(binding.Id) &&
                    now - started >= binding.DelayMs)
                {
                    _longFired.Add(binding.Id);
                    commands.Add(new MouseMappingCommand(binding));
                }
                else if (binding.Trigger == "Повтор" &&
                    now - _repeatedAt.GetValueOrDefault(binding.Id, started) >= binding.DelayMs)
                {
                    _repeatedAt[binding.Id] = now;
                    commands.Add(new MouseMappingCommand(binding));
                }
            }
        }
        return commands;
    }

    public IReadOnlyList<MouseMappingCommand> ReleaseActive()
    {
        var commands = new List<MouseMappingCommand>();
        foreach (var bindings in _active.Values)
            foreach (var binding in bindings)
                if (binding.Trigger == "Удержание" || binding.Action == "Слой пока удерживается")
                    commands.Add(new MouseMappingCommand(binding, false));
        foreach (var profile in _settings.Profiles)
            foreach (var layer in profile.Layers)
                foreach (var binding in layer.Bindings)
                    if (_toggled.Contains(binding.Id)) commands.Add(new MouseMappingCommand(binding, false));
        _active.Clear(); _heldSources.Clear(); _downAt.Clear(); _repeatedAt.Clear(); _longFired.Clear(); _toggled.Clear();
        return commands;
    }
}

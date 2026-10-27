using System.Buffers.Binary;
using System.Media;
using WinSynth = Windows.Media.SpeechSynthesis.SpeechSynthesizer;

namespace KlikerBlocks;

internal sealed record SpeechVoice(string Name, string Language);

internal static class WindowsSpeech
{
    public static IReadOnlyList<SpeechVoice> InstalledVoices() => WinSynth.AllVoices
        .Select(voice => new SpeechVoice(voice.DisplayName, voice.Language)).ToArray();

    private static string RequiredLanguage(string text)
    {
        if (text.Any(ch => ch is >= '\u0400' and <= '\u052F')) return "ru";
        if (text.Any(ch => ch is >= '\u3400' and <= '\u9FFF')) return "zh";
        return "";
    }

    private static Windows.Media.SpeechSynthesis.VoiceInformation ChooseVoice(string text, string voiceName)
    {
        var voices = WinSynth.AllVoices;
        var required = RequiredLanguage(text);
        var voice = string.IsNullOrWhiteSpace(voiceName)
            ? required.Length == 0 ? WinSynth.DefaultVoice : voices.FirstOrDefault(item =>
                item.Language.StartsWith(required, StringComparison.OrdinalIgnoreCase))
            : voices.FirstOrDefault(item => item.DisplayName.Equals(voiceName.Trim(),
                StringComparison.OrdinalIgnoreCase));
        if (voice is null)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(voiceName)
                ? UiLanguage.Format("В Windows не найден голос для языка {0}. Добавьте его в языковых настройках Windows.", required)
                : UiLanguage.Format("Голос «{0}» не найден. Выберите его из списка доступных голосов Windows.", voiceName));
        if (required.Length > 0 && !voice.Language.StartsWith(required, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiLanguage.Format(
                "Голос «{0}» ({1}) не подходит для текста на языке {2}. Выберите другой голос или оставьте поле пустым.",
                voice.DisplayName, voice.Language, required));
        return voice;
    }

    public static async Task<byte[]> RenderModernWaveAsync(string text, string voiceName, int rate, int volume,
        CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        using var synthesizer = new WinSynth();
        synthesizer.Voice = ChooseVoice(text, voiceName);
        synthesizer.Options.SpeakingRate = Math.Clamp(1.0 + rate * 0.12, 0.5, 6.0);
        synthesizer.Options.AudioVolume = volume / 100.0;
        var operation = synthesizer.SynthesizeTextToStreamAsync(text);
        using var registration = cancel.Register(operation.Cancel);
        using var winrtStream = await operation;
        cancel.ThrowIfCancellationRequested();
        using var managedStream = winrtStream.AsStreamForRead();
        using var output = new MemoryStream();
        await managedStream.CopyToAsync(output, cancel);
        return output.ToArray();
    }

    public static async Task SpeakAsync(string text, string voiceName, int rate, int volume, CancellationToken cancel)
    {
        if (text.Length > 16000) throw new ArgumentOutOfRangeException(nameof(text), UiLanguage.T("Текст для озвучивания длиннее 16000 символов."));
        if (rate is < -10 or > 10) throw new ArgumentOutOfRangeException(nameof(rate), UiLanguage.T("Скорость речи должна быть от -10 до 10."));
        if (volume is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(volume), UiLanguage.T("Громкость речи должна быть от 0 до 100."));
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var wave = await RenderModernWaveAsync(text, voiceName, rate, volume, cancel);
            if (wave.Length < 100)
                throw new InvalidOperationException(UiLanguage.T(
                    "Голос Windows не создал звуковые данные для этого текста. Выберите голос нужного языка."));
            await PlayWaveAsync(wave, cancel);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                UiLanguage.Format("Не удалось озвучить текст через голос Windows: {0}", error.Message), error);
        }
    }

    internal static async Task PlayWaveAsync(byte[] wave, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var duration = WaveDuration(wave);
        using var stream = new MemoryStream(wave, writable: false);
        using var player = new SoundPlayer(stream);
        player.Load();
        cancel.ThrowIfCancellationRequested();
        using var registration = cancel.Register(player.Stop);
        try
        {
            cancel.ThrowIfCancellationRequested();
            player.Play();
            await Task.Delay(duration + TimeSpan.FromMilliseconds(150), cancel);
        }
        finally { player.Stop(); }
    }

    private static TimeSpan WaveDuration(byte[] wave)
    {
        if (wave.Length < 12 || !wave.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !wave.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Invalid Windows speech WAV header.");
        var byteRate = 0;
        var dataSize = -1;
        for (var position = 12; position <= wave.Length - 8;)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(position + 4, 4));
            if (size < 0 || size > wave.Length - position - 8)
                throw new InvalidDataException("Invalid Windows speech WAV chunk.");
            if (wave.AsSpan(position, 4).SequenceEqual("fmt "u8) && size >= 12)
                byteRate = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(position + 16, 4));
            if (wave.AsSpan(position, 4).SequenceEqual("data"u8)) dataSize = size;
            position += 8 + size + (size & 1);
        }
        if (byteRate <= 0 || dataSize < 0)
            throw new InvalidDataException("Windows speech WAV duration is unavailable.");
        return TimeSpan.FromSeconds((double)dataSize / byteRate);
    }
}

internal sealed class VoiceSelectionForm : Form
{
    private readonly ListBox _voices = new() { Dock = DockStyle.Fill };
    public string SelectedVoice => (_voices.SelectedItem as VoiceChoice)?.Name ?? "";

    public VoiceSelectionForm(IReadOnlyList<SpeechVoice> names, string current)
    {
        Text = UiLanguage.T("Выбор голоса Windows");
        Width = 480; Height = 390; MinimumSize = new Size(400, 300);
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = UiLanguage.T("Выберите установленный голос Windows; список зависит от Windows."),
            Dock = DockStyle.Fill }, 0, 0);
        _voices.Items.Add(new VoiceChoice("", ""));
        foreach (var voice in names) _voices.Items.Add(new VoiceChoice(voice.Name, voice.Language));
        _voices.SelectedIndex = Math.Max(0, _voices.Items.Cast<VoiceChoice>().ToList()
            .FindIndex(choice => choice.Name.Equals(current, StringComparison.OrdinalIgnoreCase)));
        root.Controls.Add(_voices, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = UiLanguage.T("Выбрать"), DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = UiLanguage.T("Отмена"), DialogResult = DialogResult.Cancel, Width = 100 };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 2);
        AcceptButton = ok; CancelButton = cancel;
        _voices.DoubleClick += (_, _) => { DialogResult = DialogResult.OK; Close(); };
    }

    private sealed record VoiceChoice(string Name, string Language)
    {
        public override string ToString() => Name.Length == 0
            ? UiLanguage.T("Автоматически по языку текста") : $"{Name} [{Language}]";
    }
}

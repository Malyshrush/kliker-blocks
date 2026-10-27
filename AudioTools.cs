using System.Runtime.InteropServices;

namespace KlikerBlocks;

internal static class AudioTools
{
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySoundMemory(byte[] sound, IntPtr module, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "Beep", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeBeep(uint frequency, uint duration);

    public static void PlayWave(string path)
    {
        if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Блок «Воспроизвести WAV» принимает только файлы .wav.");
        if (!File.Exists(path)) throw new FileNotFoundException("Звуковой файл не найден.", path);
        const uint sndAsync = 0x0001;
        const uint sndNodefault = 0x0002;
        const uint sndFilename = 0x00020000;
        if (!PlaySound(path, IntPtr.Zero, sndAsync | sndNodefault | sndFilename))
            throw new InvalidOperationException("Windows не смогла воспроизвести WAV-файл.");
    }

    public static void Beep(int frequency, int duration)
    {
        if (frequency is < 37 or > 32767 || duration is < 1 or > 5000)
            throw new InvalidOperationException("BEEP: частота 37–32767 Гц, длительность 1–5000 мс.");
        if (NativeBeep((uint)frequency, (uint)duration)) return;
        // Some modern Windows audio drivers do not implement the old hardware Beep API.
        // Play an equivalent PCM tone through the normal audio device instead.
        const uint sndSync = 0x0000;
        const uint sndNodefault = 0x0002;
        const uint sndMemory = 0x0004;
        if (!PlaySoundMemory(CreateToneWave(frequency, duration), IntPtr.Zero,
                sndSync | sndNodefault | sndMemory))
            throw new InvalidOperationException("Windows не смогла воспроизвести звуковой сигнал: системный Beep и аудиоустройство недоступны.");
    }

    internal static byte[] CreateToneWave(int frequency, int duration)
    {
        const int sampleRate = 96000;
        var samples = sampleRate * duration / 1000;
        using var stream = new MemoryStream(44 + samples * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16); // PCM format chunk length
        writer.Write((short)1); // PCM
        writer.Write((short)1); // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        var fadeSamples = Math.Min(samples / 2, sampleRate / 200);
        for (var i = 0; i < samples; i++)
        {
            var envelope = fadeSamples == 0 ? 1.0 :
                Math.Min(1.0, Math.Min(i, samples - i - 1) / (double)fadeSamples);
            writer.Write((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate)
                * 12000 * envelope));
        }
        return stream.ToArray();
    }
}

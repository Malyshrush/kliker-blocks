using System.Text;

namespace KlikerBlocks;

internal sealed class ScriptLogWriter(string baseDirectory)
{
    private const long MaxBytes = 8 * 1024 * 1024;
    private readonly object _sync = new();
    private string? _path;

    public string? PathOnDisk { get { lock (_sync) return _path; } }

    public static bool IsValidName(string name) =>
        name.Length is >= 1 and <= 160 && name != "." && name != ".." &&
        !Path.IsPathRooted(name) && name == Path.GetFileName(name) &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.EndsWith(' ') && !name.EndsWith('.');

    public void Configure(string name)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(name)) { _path = null; return; }
            if (!IsValidName(name))
                throw new InvalidOperationException("#logfile: укажите только имя файла без пути (до 160 символов).");
            var path = Path.Combine(Path.GetFullPath(baseDirectory), name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("#logfile: запись через файловую ссылку запрещена.");
            using (new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)) { }
            _path = path;
        }
    }

    public void Write(string message)
    {
        lock (_sync)
        {
            if (_path is null) return;
            try
            {
                if ((File.GetAttributes(_path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("#logfile: запись через файловую ссылку запрещена.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var bytes = Encoding.UTF8.GetByteCount(message + Environment.NewLine);
            var length = File.Exists(_path) ? new FileInfo(_path).Length : 0;
            if (length + bytes > MaxBytes)
                throw new InvalidOperationException("#logfile: файл журнала достиг предела 8 МиБ.");
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllText(_path, message + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed record AvailableUpdate(Version Version, string Tag, Uri InstallerUri, long Size,
    string Sha256, string FileName);

internal static class AppUpdates
{
    private const string Api = "https://api.github.com/repos/Malyshrush/kliker-blocks/releases?per_page=20";
    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    internal static async Task<AvailableUpdate?> CheckAsync(CancellationToken cancel = default)
    {
        using var client = Client(TimeSpan.FromSeconds(12));
        using var response = await client.GetAsync(Api, cancel);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancel));
        return SelectRelease(document.RootElement, CurrentVersion,
            RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64");
    }

    internal static AvailableUpdate? SelectRelease(JsonElement releases, Version current, string architecture)
    {
        AvailableUpdate? best = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) ||
                version <= current || best is not null && version <= best.Version) continue;
            var fileName = $"KlikerBlocks-Setup-{version}-{architecture}.exe";
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != fileName) continue;
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var expectedPrefix = $"https://github.com/Malyshrush/kliker-blocks/releases/download/{Uri.EscapeDataString(tag)}/";
                if (!url.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
                    !Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
                var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() ?? "" : "";
                if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71) continue;
                best = new AvailableUpdate(version, tag, uri, asset.GetProperty("size").GetInt64(),
                    digest[7..], fileName);
                break;
            }
        }
        return best;
    }

    internal static async Task<string> DownloadAsync(AvailableUpdate update, IProgress<double>? progress,
        CancellationToken cancel)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KLIKER Blocks", "Updates");
        Directory.CreateDirectory(folder);
        var destination = Path.Combine(folder, update.FileName);
        var partial = destination + ".download";
        try
        {
            using var client = Client(TimeSpan.FromMinutes(10));
            using var response = await client.GetAsync(update.InstallerUri, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancel))
            await using (var output = File.Create(partial))
            {
                var buffer = new byte[131072];
                long copied = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancel);
                    copied += count;
                    if (update.Size > 0) progress?.Report(Math.Clamp(copied / (double)update.Size, 0, 1));
                }
            }
            if (new FileInfo(partial).Length != update.Size || update.Size < 1_000_000)
                throw new InvalidDataException(UiLanguage.T("Размер загруженного установщика не совпал с выпуском."));
            using (var input = File.OpenRead(partial))
            {
                var digest = Convert.ToHexString(SHA256.HashData(input));
                if (!digest.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(UiLanguage.T("Контрольная сумма установщика не совпала."));
            }
            File.Move(partial, destination, true);
            progress?.Report(1);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    internal static string BackupGraph(ProjectModel project, string? source, string? editedCode)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KLIKER Blocks", "GraphBackups");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var name = Path.GetFileNameWithoutExtension(source ?? "untitled");
        var path = Path.Combine(folder, $"{name}-before-update-{stamp}.klb");
        File.WriteAllText(path, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
        if (editedCode is not null) File.WriteAllText(path + ".code.txt", editedCode);
        return path;
    }

    private static HttpClient Client(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KlikerBlocks-Updater/1.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}

internal sealed class UpdateDownloadForm : Form
{
    private readonly AvailableUpdate _update;
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly Button _download = new() { Dock = DockStyle.Fill };
    private readonly Button _cancel = new() { Dock = DockStyle.Fill };
    private CancellationTokenSource? _stop;
    internal string? InstallerPath { get; private set; }

    internal UpdateDownloadForm(AvailableUpdate update)
    {
        _update = update;
        Text = UiLanguage.T("Обновление Kliker Blocks");
        Size = new Size(560, 230);
        MinimumSize = Size;
        StartPosition = FormStartPosition.CenterParent;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18),
            ColumnCount = 2, RowCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(grid);
        var info = new Label { Text = UiLanguage.Format("Доступна версия {0}. Установщик будет загружен с GitHub и проверен по SHA-256. Ваши графы и настройки сохранятся.", update.Version),
            Dock = DockStyle.Fill, AutoSize = false };
        grid.Controls.Add(info, 0, 0);
        grid.SetColumnSpan(info, 2);
        grid.Controls.Add(_progress, 0, 1);
        grid.SetColumnSpan(_progress, 2);
        _download.Text = UiLanguage.T("Скачать и установить");
        _download.Click += async (_, _) => await Download();
        _cancel.Text = UiLanguage.T("Отмена");
        _cancel.Click += (_, _) => { _stop?.Cancel(); if (_stop is null) DialogResult = DialogResult.Cancel; };
        grid.Controls.Add(_download, 0, 2);
        grid.Controls.Add(_cancel, 1, 2);
    }

    private async Task Download()
    {
        _download.Enabled = false;
        _stop = new CancellationTokenSource();
        try
        {
            InstallerPath = await AppUpdates.DownloadAsync(_update,
                new Progress<double>(fraction => _progress.Value = Math.Clamp((int)(fraction * 100), 0, 100)), _stop.Token);
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException) { _download.Enabled = true; }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            _download.Enabled = true;
        }
        finally { _stop.Dispose(); _stop = null; }
    }
}

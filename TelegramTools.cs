using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KlikerBlocks;

internal sealed record TelegramSendResult(long MessageId);

internal static class TelegramBotSettings
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kliker Blocks Telegram credential v1");
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KLIKER Blocks", "telegram-token.bin");

    public static string LoadToken()
    {
        if (!File.Exists(FilePath))
            throw new InvalidOperationException(UiLanguage.T("Настройте токен бота в меню «Настройки → Telegram» перед запуском блока."));
        try
        {
            return UnprotectToken(File.ReadAllBytes(FilePath));
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new InvalidOperationException(UiLanguage.T("Не удалось прочитать защищённый токен Telegram. Задайте его заново в настройках."));
        }
    }

    public static void SaveToken(string token)
    {
        token = token.Trim();
        if (!TelegramTools.IsValidTokenFormat(token))
            throw new InvalidOperationException(UiLanguage.T("Проверьте формат токена Telegram Bot API."));
        var protectedBytes = ProtectToken(token);
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            var temporary = FilePath + ".tmp";
            File.WriteAllBytes(temporary, protectedBytes);
            File.Move(temporary, FilePath, true);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(UiLanguage.T("Не удалось сохранить защищённый токен Telegram."));
        }
        finally { CryptographicOperations.ZeroMemory(protectedBytes); }
    }

    internal static byte[] ProtectToken(string token)
    {
        var clearBytes = Encoding.UTF8.GetBytes(token);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
            return protectedBytes;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    internal static string UnprotectToken(byte[] protectedBytes)
    {
        var clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        try { return new UTF8Encoding(false, true).GetString(clearBytes); }
        finally { CryptographicOperations.ZeroMemory(clearBytes); }
    }

    public static void ClearToken()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(UiLanguage.T("Не удалось удалить защищённый токен Telegram."));
        }
    }

    public static bool IsConfigured => File.Exists(FilePath);
}

internal static class TelegramTools
{
    private const long MaxFileBytes = 50_000_000;
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    internal static bool IsValidTokenFormat(string token)
    {
        var separator = token.IndexOf(':');
        return token.Length is >= 20 and <= 256 && separator > 0 && separator < token.Length - 1 &&
            token[..separator].All(char.IsAsciiDigit) &&
            token[(separator + 1)..].All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-');
    }

    public static async Task<TelegramSendResult> SendAsync(string token, string chatId, string action,
        string message, string filePath, int timeoutMs, CancellationToken cancel, HttpClient? client = null)
    {
        token = token.Trim();
        chatId = chatId.Trim();
        if (!IsValidTokenFormat(token))
            throw new InvalidOperationException(UiLanguage.T("Проверьте формат токена Telegram Bot API."));
        if (chatId.Length is 0 or > 128 || chatId.Any(char.IsControl))
            throw new InvalidOperationException(UiLanguage.T("Укажите @имя чата или числовой ID чата Telegram."));

        var sendText = action == "Сообщение";
        var sendFile = action is "Файл" or "Сообщение и файл";
        if (!sendText && !sendFile)
            throw new InvalidOperationException(UiLanguage.T("Неизвестный режим отправки Telegram."));
        if (sendText && (message.Length == 0 || message.Length > 4096))
            throw new InvalidOperationException(UiLanguage.T("Сообщение Telegram должно содержать от 1 до 4096 символов."));
        if (sendFile && (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)))
            throw new InvalidOperationException(UiLanguage.T("Файл для отправки Telegram не найден."));
        if (sendFile)
        {
            var info = new FileInfo(filePath);
            if (info.Length > MaxFileBytes)
                throw new InvalidOperationException(UiLanguage.T("Файл Telegram превышает ограничение 50 МБ."));
            if (message.Length > 1024)
                throw new InvalidOperationException(UiLanguage.T("Подпись к файлу Telegram не должна превышать 1024 символа."));
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(Math.Clamp(timeoutMs, 1000, 120000));
        using var request = sendFile
            ? CreateDocumentRequest(token, chatId, message, filePath)
            : CreateTextRequest(token, chatId, message);
        HttpResponseMessage response;
        try
        {
            response = await (client ?? Client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new InvalidOperationException(UiLanguage.T("Истекло время ожидания ответа Telegram."));
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(UiLanguage.T("Не удалось связаться с Telegram Bot API по HTTPS."));
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(deadline.Token);
            if (Encoding.UTF8.GetByteCount(body) > 1024 * 1024)
                throw new InvalidOperationException(UiLanguage.T("Ответ Telegram превышает 1 МиБ."));
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                var description = root.TryGetProperty("description", out var detail) && detail.ValueKind == JsonValueKind.String
                    ? detail.GetString() : null;
                var errorCode = root.TryGetProperty("error_code", out var code) && code.TryGetInt32(out var number)
                    ? $" ({number})" : "";
                if (errorCode == " (403)" && description?.Contains("bot is not a member", StringComparison.OrdinalIgnoreCase) == true)
                    throw new InvalidOperationException(UiLanguage.T("Telegram: бот не состоит в канале или группе. Добавьте бота в нужный чат; для канала назначьте его администратором с правом публикации и проверьте адрес чата."));
                throw new InvalidOperationException(UiLanguage.Format("Telegram отклонил отправку{0}: {1}", errorCode,
                    string.IsNullOrWhiteSpace(description) ? UiLanguage.T("неизвестная ошибка") : description));
            }
            if (!response.IsSuccessStatusCode || !root.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("message_id", out var id) || !id.TryGetInt64(out var messageId))
                throw new InvalidOperationException(UiLanguage.T("Telegram вернул некорректный ответ без ID сообщения."));
            return new TelegramSendResult(messageId);
        }
    }

    private static HttpRequestMessage CreateTextRequest(string token, string chatId, string message)
    {
        var uri = ApiUri(token, "sendMessage");
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = message
            })
        };
        return request;
    }

    private static HttpRequestMessage CreateDocumentRequest(string token, string chatId, string caption, string filePath)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId, Encoding.UTF8), "chat_id");
        if (caption.Length > 0) form.Add(new StringContent(caption, Encoding.UTF8), "caption");
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "document", Path.GetFileName(filePath));
        return new HttpRequestMessage(HttpMethod.Post, ApiUri(token, "sendDocument")) { Content = form };
    }

    private static Uri ApiUri(string token, string method) =>
        new($"https://api.telegram.org/bot{token}/{method}", UriKind.Absolute);
}

internal sealed class TelegramSettingsForm : Form
{
    private readonly TextBox _token = new() { Width = 350, UseSystemPasswordChar = true };
    private readonly Label _status = new() { AutoSize = true };

    public TelegramSettingsForm()
    {
        Text = UiLanguage.T("Telegram Bot API — настройки");
        Width = 510;
        Height = 270;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            AutoScroll = true
        };
        Controls.Add(panel);
        panel.Controls.Add(new Label
        {
            Text = UiLanguage.T("Вставьте токен бота из @BotFather. Он будет зашифрован Windows для текущего пользователя и не попадёт в граф или журнал."),
            Width = 450,
            Height = 58
        });
        panel.Controls.Add(new Label { Text = UiLanguage.T("Токен бота"), Width = 450, Height = 22 });
        panel.Controls.Add(_token);
        _status.Text = TelegramBotSettings.IsConfigured
            ? UiLanguage.T("Токен бота настроен")
            : UiLanguage.T("Токен бота не настроен");
        panel.Controls.Add(_status);
        var buttons = new FlowLayoutPanel { Width = 450, Height = 42, FlowDirection = FlowDirection.LeftToRight };
        var save = new Button { Text = UiLanguage.T("Сохранить токен"), AutoSize = true };
        save.Click += (_, _) => SaveToken();
        var clear = new Button { Text = UiLanguage.T("Удалить токен"), AutoSize = true };
        clear.Click += (_, _) => ClearToken();
        var close = new Button { Text = UiLanguage.T("Закрыть"), AutoSize = true };
        close.Click += (_, _) => Close();
        buttons.Controls.AddRange([save, clear, close]);
        panel.Controls.Add(buttons);
    }

    private void SaveToken()
    {
        try
        {
            TelegramBotSettings.SaveToken(_token.Text);
            _token.Clear();
            _status.Text = UiLanguage.T("Токен бота настроен");
            MessageBox.Show(this, UiLanguage.T("Токен зашифрован и сохранён на этом компьютере."), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ClearToken()
    {
        try
        {
            TelegramBotSettings.ClearToken();
            _token.Clear();
            _status.Text = UiLanguage.T("Токен бота не настроен");
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

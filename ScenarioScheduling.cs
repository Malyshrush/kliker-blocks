using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace KlikerBlocks;

internal sealed class ScenarioSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectPath { get; set; } = "";
    public DateTime StartLocal { get; set; }
    public string Repeat { get; set; } = "Once"; // Once or Daily.
    public bool Enabled { get; set; } = true;

    public string Description => $"{Path.GetFileName(ProjectPath)} — {StartLocal:yyyy-MM-dd HH:mm:ss} " +
        (Repeat == "Daily" ? UiLanguage.T("ежедневно") : UiLanguage.T("один раз")) +
        (Enabled ? "" : " (" + UiLanguage.T("выключено") + ")");
}

internal sealed record ScenarioLaunchRequest(string ProjectPath, int DelaySeconds);

internal static class ScenarioScheduling
{
    internal static string TaskName(Guid id) => "KlikerBlocks_Scenario_" + id.ToString("N");

    internal static ScenarioLaunchRequest? Resolve(string[] args, AppSettings settings)
    {
        if (args.Contains("--windows-startup", StringComparer.OrdinalIgnoreCase))
            return settings.StartupScenarioEnabled && File.Exists(settings.StartupScenarioPath)
                ? new ScenarioLaunchRequest(settings.StartupScenarioPath, Math.Clamp(settings.StartupScenarioDelaySeconds, 0, 3600))
                : null;
        var index = Array.FindIndex(args, arg => arg.Equals("--scheduled-run", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length || !Guid.TryParse(args[index + 1], out var id)) return null;
        var scheduled = settings.ScenarioSchedules.FirstOrDefault(item => item.Id == id && item.Enabled);
        return scheduled is not null && File.Exists(scheduled.ProjectPath)
            ? new ScenarioLaunchRequest(scheduled.ProjectPath, 0) : null;
    }

    internal static void Register(ScenarioSchedule schedule, string executable)
    {
        if (!File.Exists(schedule.ProjectPath)) throw new FileNotFoundException(
            UiLanguage.T("Файл сценария не найден."), schedule.ProjectPath);
        if (schedule.Repeat is not ("Once" or "Daily"))
            throw new ArgumentException(UiLanguage.T("Неизвестный режим расписания."));
        if (schedule.Repeat == "Once" && schedule.StartLocal <= DateTime.Now)
            throw new ArgumentException(UiLanguage.T("Выберите будущее время для однократного запуска."));
        var xml = BuildTaskXml(schedule, executable);
        var path = Path.Combine(Path.GetTempPath(), "KlikerBlocks-task-" + schedule.Id.ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            RunSchtasks("/Create", "/TN", TaskName(schedule.Id), "/XML", path, "/F");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    internal static void Delete(Guid id) => RunSchtasks("/Delete", "/TN", TaskName(id), "/F");

    internal static string BuildTaskXml(ScenarioSchedule schedule, string executable)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
        using var writer = XmlWriter.Create(builder, settings);
        const string ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        writer.WriteStartElement("Task", ns);
        writer.WriteAttributeString("version", "1.2");
        writer.WriteStartElement("RegistrationInfo");
        writer.WriteElementString("Description", "Kliker Blocks scenario schedule");
        writer.WriteEndElement();
        writer.WriteStartElement("Triggers");
        writer.WriteStartElement(schedule.Repeat == "Daily" ? "CalendarTrigger" : "TimeTrigger");
        var start = schedule.StartLocal;
        if (schedule.Repeat == "Daily")
        {
            start = DateTime.Today.Add(schedule.StartLocal.TimeOfDay);
            if (start <= DateTime.Now) start = start.AddDays(1);
        }
        writer.WriteElementString("StartBoundary", start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        writer.WriteElementString("Enabled", "true");
        if (schedule.Repeat == "Daily")
        {
            writer.WriteStartElement("ScheduleByDay");
            writer.WriteElementString("DaysInterval", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("Principals");
        writer.WriteStartElement("Principal");
        writer.WriteAttributeString("id", "Author");
        writer.WriteElementString("UserId", WindowsIdentity.GetCurrent().User?.Value ?? "");
        writer.WriteElementString("LogonType", "InteractiveToken");
        writer.WriteElementString("RunLevel", "LeastPrivilege");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("Settings");
        writer.WriteElementString("MultipleInstancesPolicy", "Parallel");
        writer.WriteElementString("DisallowStartIfOnBatteries", "false");
        writer.WriteElementString("StopIfGoingOnBatteries", "false");
        writer.WriteElementString("StartWhenAvailable", "true");
        writer.WriteElementString("AllowHardTerminate", "true");
        writer.WriteElementString("Enabled", "true");
        writer.WriteElementString("ExecutionTimeLimit", "PT0S");
        writer.WriteEndElement();
        writer.WriteStartElement("Actions");
        writer.WriteAttributeString("Context", "Author");
        writer.WriteStartElement("Exec");
        writer.WriteElementString("Command", executable);
        writer.WriteElementString("Arguments", "--scheduled-run " + schedule.Id.ToString("D"));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return builder.ToString();
    }

    private static void RunSchtasks(params string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var outputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        using var process = new Process { StartInfo = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
            RedirectStandardOutput = true, StandardOutputEncoding = outputEncoding,
            StandardErrorEncoding = outputEncoding
        } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new InvalidOperationException(UiLanguage.T("Не удалось запустить планировщик Windows."));
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30000))
        {
            process.Kill();
            throw new TimeoutException(UiLanguage.T("Планировщик Windows не ответил вовремя."));
        }
        if (process.ExitCode != 0) throw new InvalidOperationException(
            UiLanguage.T("Не удалось изменить расписание Windows: ") + (error.Trim().Length > 0 ? error.Trim() : output.Trim()));
    }
}

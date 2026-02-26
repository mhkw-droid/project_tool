using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace TaskTool.Services;

public class OutlookInteropService
{
    private readonly LoggerService _logger;
    private readonly SettingsService _settings;

    public OutlookInteropService(LoggerService logger, SettingsService settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public (bool ok, string entryId, string error) UpsertBlock(string? existingEntryId, string title, string body, DateTime start, DateTime end)
    {
        if (!_settings.Current.OutlookSyncEnabled)
            return (false, existingEntryId ?? string.Empty, "Outlook Sync ist deaktiviert.");

        if (string.IsNullOrWhiteSpace(title))
            return (false, existingEntryId ?? string.Empty, "Titel fehlt.");

        if (start == default || end == default || end <= start || start == DateTime.MinValue || end == DateTime.MinValue)
            return (false, existingEntryId ?? string.Empty, "Ungültiger Zeitraum: Ende muss nach Start liegen.");

        try
        {
            return ExecuteOnSta(() =>
            {
                var outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                    return (false, existingEntryId ?? string.Empty, "Outlook nicht installiert (ProgID nicht gefunden).");

                var app = CreateOrAttachOutlook(outlookType);
                if (app == null)
                    return (false, existingEntryId ?? string.Empty, "Outlook konnte nicht gestartet/verbunden werden.");

                var ns = app.GetNamespace("MAPI");
                TryLogon(ns);

                var calendar = ns.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderCalendar);
                if (calendar == null)
                    return (false, existingEntryId ?? string.Empty, "Standard-Kalender nicht verfügbar.");

                Outlook.AppointmentItem item;
                if (!string.IsNullOrWhiteSpace(existingEntryId))
                {
                    var existing = ns.GetItemFromID(existingEntryId);
                    if (existing is not Outlook.AppointmentItem existingItem)
                        return (false, existingEntryId ?? string.Empty, "Outlook Entry ist kein Terminobjekt.");
                    item = existingItem;
                }
                else
                {
                    item = (Outlook.AppointmentItem)app.CreateItem(Outlook.OlItemType.olAppointmentItem);
                }

                item.Subject = $"Fokus: {title}";
                item.Body = body ?? string.Empty;
                item.Start = start;
                item.End = end;
                item.BusyStatus = Outlook.OlBusyStatus.olBusy;
                item.ReminderSet = false;
                item.Categories = string.IsNullOrWhiteSpace(_settings.Current.OutlookCategoryName)
                    ? "FocusBlock"
                    : _settings.Current.OutlookCategoryName;
                item.Save();

                return (true, item.EntryID ?? string.Empty, string.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(BuildOutlookExceptionLog("UpsertBlock", ex, start, end));
            return (false, existingEntryId ?? string.Empty, BuildUserFacingOutlookError(ex));
        }
    }

    public (bool ok, string error) DeleteBlock(string? entryId)
    {
        if (!_settings.Current.OutlookSyncEnabled || string.IsNullOrWhiteSpace(entryId))
            return (true, string.Empty);

        try
        {
            return ExecuteOnSta(() =>
            {
                var outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                    return (false, "Outlook nicht installiert (ProgID nicht gefunden).");

                var app = CreateOrAttachOutlook(outlookType);
                if (app == null)
                    return (false, "Outlook konnte nicht gestartet/verbunden werden.");

                var ns = app.GetNamespace("MAPI");
                TryLogon(ns);

                var item = ns.GetItemFromID(entryId);
                if (item is not Outlook.AppointmentItem appt)
                    return (false, "Outlook Entry ist kein Terminobjekt.");

                appt.Delete();
                return (true, string.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(BuildOutlookExceptionLog("DeleteBlock", ex, null, null));
            return (false, BuildUserFacingOutlookError(ex));
        }
    }

    public (bool ok, string error) TestConnection()
    {
        var start = DateTime.Now.AddMinutes(5);
        var end = start.AddMinutes(5);

        try
        {
            var upsert = UpsertBlock(string.Empty, "TaskTool Test", "Test appointment", start, end);
            if (!upsert.ok)
                return (false, upsert.error);

            var del = DeleteBlock(upsert.entryId);
            if (!del.ok)
                return (false, del.error);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error(BuildOutlookExceptionLog("TestConnection", ex, start, end));
            return (false, BuildUserFacingOutlookError(ex));
        }
    }

    private static Outlook.Application? CreateOrAttachOutlook(Type outlookType)
    {
        try
        {
            var running = Marshal.GetActiveObject("Outlook.Application");
            if (running is Outlook.Application runningApp)
                return runningApp;
        }
        catch
        {
            // ignore and fallback to creating app
        }

        var created = Activator.CreateInstance(outlookType);
        return created as Outlook.Application;
    }

    private static void TryLogon(Outlook.NameSpace ns)
    {
        try
        {
            ns.Logon("", "", Missing.Value, Missing.Value);
        }
        catch
        {
            // Often already logged on; safe to continue.
        }
    }

    private static T ExecuteOnSta<T>(Func<T> action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return action();

        T? result = default;
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            throw new InvalidOperationException("Outlook COM Aufruf auf STA Thread fehlgeschlagen.", exception);

        return result!;
    }

    private static string BuildUserFacingOutlookError(Exception ex)
    {
        if (ex is FileNotFoundException)
            return "Outlook-Komponente nicht gefunden. Bitte Outlook-Installation und Office-Reparatur prüfen.";

        if (ex is COMException comEx)
        {
            if ((uint)comEx.HResult == 0x800401E3)
                return $"COM Fehler 0x{comEx.HResult:X8}: Kein aktives Outlook-Profil verfügbar.";

            if ((uint)comEx.HResult == 0x80070002)
                return $"COM Fehler 0x{comEx.HResult:X8}: Outlook-Dateien/Registrierung nicht gefunden.";

            return $"COM Fehler 0x{comEx.HResult:X8}: {comEx.Message}";
        }

        var message = string.IsNullOrWhiteSpace(ex.Message) ? "Unbekannter Outlook Fehler." : ex.Message;
        return $"{message} (0x{ex.HResult:X8})";
    }

    private static string BuildOutlookExceptionLog(string operation, Exception ex, DateTime? start, DateTime? end)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Outlook {operation} failed");
        sb.AppendLine($"ThreadId: {Environment.CurrentManagedThreadId}");
        sb.AppendLine($"ApartmentState: {Thread.CurrentThread.GetApartmentState()}");
        sb.AppendLine($"OutlookInstalled: {Type.GetTypeFromProgID("Outlook.Application") != null}");
        sb.AppendLine($"StartLocal: {(start.HasValue ? start.Value.ToString("O") : "null")}");
        sb.AppendLine($"EndLocal: {(end.HasValue ? end.Value.ToString("O") : "null")}");
        sb.AppendLine($"DurationMinutes: {(start.HasValue && end.HasValue ? (end.Value - start.Value).TotalMinutes.ToString("0.##") : "null")}");
        sb.AppendLine($"Exception: {ex}");
        sb.AppendLine($"HResult: 0x{ex.HResult:X8}");

        var inner = ex.InnerException;
        var depth = 0;
        while (inner != null)
        {
            sb.AppendLine($"Inner[{depth}] Type={inner.GetType().FullName} HResult=0x{inner.HResult:X8} Message={inner.Message}");
            sb.AppendLine(inner.ToString());
            inner = inner.InnerException;
            depth++;
        }

        return sb.ToString();
    }
}

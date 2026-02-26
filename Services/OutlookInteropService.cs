using System.Runtime.InteropServices;
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

        if (start == default || end == default || end <= start)
            return (false, existingEntryId ?? string.Empty, "Ungültiger Zeitraum: Ende muss nach Start liegen.");

        try
        {
            return ExecuteOnSta(() =>
            {
                var app = new Outlook.Application();
                var ns = app.GetNamespace("MAPI");
                ns.Logon(Missing.Value, Missing.Value, false, false);

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
            _logger.Error(BuildOutlookExceptionLog("UpsertBlock", ex));
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
                var app = new Outlook.Application();
                var ns = app.GetNamespace("MAPI");
                ns.Logon(Missing.Value, Missing.Value, false, false);

                var item = ns.GetItemFromID(entryId);
                if (item is not Outlook.AppointmentItem appt)
                    return (false, "Outlook Entry ist kein Terminobjekt.");

                appt.Delete();
                return (true, string.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(BuildOutlookExceptionLog("DeleteBlock", ex));
            return (false, BuildUserFacingOutlookError(ex));
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
        if (ex is COMException comEx)
            return $"COM Fehler 0x{comEx.HResult:X8}: {comEx.Message}";

        return ex.Message;
    }

    private static string BuildOutlookExceptionLog(string operation, Exception ex)
    {
        var hResult = ex.HResult;
        return $"Outlook {operation} failed | Message: {ex.Message} | HResult: 0x{hResult:X8} | Type: {ex.GetType().FullName}\n{ex.StackTrace}";
    }
}

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

        try
        {
            var app = new Outlook.Application();
            var ns = app.GetNamespace("MAPI");
            Outlook.AppointmentItem item;

            if (!string.IsNullOrWhiteSpace(existingEntryId))
            {
                var existing = ns.GetItemFromID(existingEntryId);
                item = (Outlook.AppointmentItem)existing;
            }
            else
            {
                item = (Outlook.AppointmentItem)app.CreateItem(Outlook.OlItemType.olAppointmentItem);
            }

            item.Subject = $"Fokus: {title}";
            item.Body = body;
            item.Start = start;
            item.End = end;
            item.BusyStatus = Outlook.OlBusyStatus.olBusy;
            item.ReminderSet = false;
            item.Categories = _settings.Current.OutlookCategoryName;
            item.Save();

            return (true, item.EntryID, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error($"Outlook sync failed: {ex.Message}");
            return (false, existingEntryId ?? string.Empty, ex.Message);
        }
    }

    public (bool ok, string error) DeleteBlock(string? entryId)
    {
        if (!_settings.Current.OutlookSyncEnabled || string.IsNullOrWhiteSpace(entryId))
            return (true, string.Empty);

        try
        {
            var app = new Outlook.Application();
            var ns = app.GetNamespace("MAPI");
            var item = ns.GetItemFromID(entryId);
            ((Outlook.AppointmentItem)item).Delete();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error($"Outlook delete failed: {ex.Message}");
            return (false, ex.Message);
        }
    }
}

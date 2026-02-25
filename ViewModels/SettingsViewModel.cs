using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    public string Title => "Einstellungen";

    public bool OutlookSyncEnabled
    {
        get => _settings.Current.OutlookSyncEnabled;
        set
        {
            _settings.Current.OutlookSyncEnabled = value;
            _settings.Save();
            Raise();
        }
    }

    public string OutlookCategoryName
    {
        get => _settings.Current.OutlookCategoryName;
        set
        {
            _settings.Current.OutlookCategoryName = value;
            _settings.Save();
            Raise();
        }
    }

    public int ReminderLeadMinutes
    {
        get => _settings.Current.ReminderLeadMinutes;
        set
        {
            _settings.Current.ReminderLeadMinutes = value;
            _settings.Save();
            Raise();
        }
    }

    public string DateTimeFormat
    {
        get => _settings.Current.DateTimeFormat;
        set
        {
            _settings.Current.DateTimeFormat = value;
            _settings.Save();
            Raise();
        }
    }

    public SettingsViewModel(SettingsService settings) => _settings = settings;

    public override string ToString() => Title;
}

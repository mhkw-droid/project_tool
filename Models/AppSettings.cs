namespace TaskTool.Models;

public class AppSettings
{
    public bool OutlookSyncEnabled { get; set; } = true;
    public string OutlookCategoryName { get; set; } = "FocusBlock";
    public int ReminderLeadMinutes { get; set; } = 2;
}

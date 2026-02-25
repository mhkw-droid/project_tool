using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class MainViewModel : ObservableObject
{
    public ObservableCollection<object> NavigationItems { get; }
    private object _selectedView;
    public object SelectedView { get => _selectedView; set => Set(ref _selectedView, value); }

    public MainViewModel(TaskService taskService, WorkDayService workDayService, SettingsService settingsService, NotificationService notifications, LoggerService logger)
    {
        var today = new TodayViewModel(taskService, workDayService);
        var week = new WeekViewModel(taskService);
        var reports = new ReportsViewModel(taskService);
        var settings = new SettingsViewModel(settingsService);

        NavigationItems = new ObservableCollection<object> { today, week, reports, settings };
        _selectedView = today;
    }
}

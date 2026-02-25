using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class MainViewModel : ObservableObject
{
    public ObservableCollection<object> NavigationItems { get; }
    public TodayViewModel TodayViewModel { get; }

    private object _selectedView;
    public object SelectedView
    {
        get => _selectedView;
        set
        {
            if (Set(ref _selectedView, value))
            {
                Raise(nameof(IsTodaySelected));
            }
        }
    }

    public bool IsTodaySelected => SelectedView is TodayViewModel;

    public MainViewModel(TaskService taskService, WorkDayService workDayService, SettingsService settingsService, NotificationService notifications, LoggerService logger)
    {
        TodayViewModel = new TodayViewModel(taskService, workDayService, settingsService);
        var week = new WeekViewModel(taskService, workDayService, settingsService);
        var reports = new ReportsViewModel(taskService, workDayService, settingsService);
        var settings = new SettingsViewModel(settingsService);

        NavigationItems = new ObservableCollection<object> { TodayViewModel, week, reports, settings };
        _selectedView = TodayViewModel;
    }
}

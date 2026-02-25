using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Models;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class WeekViewModel : ObservableObject
{
    private readonly TaskService _tasks;
    private readonly WorkDayService _workDays;
    private readonly SettingsService _settings;

    public string Title => "Woche";

    private DateTime _weekStart;
    public DateTime WeekStart
    {
        get => _weekStart;
        set { if (Set(ref _weekStart, value)) Raise(nameof(WeekRangeLabel)); }
    }

    public string WeekRangeLabel => $"{WeekStart:dd.MM.yyyy} - {WeekStart.AddDays(6):dd.MM.yyyy}";
    public ObservableCollection<WeekDayGroup> Days { get; } = new();

    public RelayCommand PreviousWeekCommand { get; }
    public RelayCommand NextWeekCommand { get; }
    public RelayCommand CurrentWeekCommand { get; }

    public WeekViewModel(TaskService tasks, WorkDayService workDays, SettingsService settings)
    {
        _tasks = tasks;
        _workDays = workDays;
        _settings = settings;

        WeekStart = StartOfWeek(DateTime.Today);
        PreviousWeekCommand = new RelayCommand(() => { WeekStart = WeekStart.AddDays(-7); LoadWeek(); });
        NextWeekCommand = new RelayCommand(() => { WeekStart = WeekStart.AddDays(7); LoadWeek(); });
        CurrentWeekCommand = new RelayCommand(() => { WeekStart = StartOfWeek(DateTime.Today); LoadWeek(); });
        LoadWeek();
    }

    private void LoadWeek()
    {
        Days.Clear();
        var from = WeekStart;
        var to = WeekStart.AddDays(7);
        var weekTasks = _tasks.GetTasksForRange(from, to);
        var workDays = _workDays.GetWorkDaysInRange(from, to.AddDays(-1)).ToDictionary(w => w.Day, w => w);

        for (int i = 0; i < 7; i++)
        {
            var day = WeekStart.AddDays(i);
            var key = day.ToString("yyyy-MM-dd");
            var wd = workDays.ContainsKey(key) ? workDays[key] : new WorkDayRecord { Day = key, DayType = "Normal" };
            var breaks = _workDays.GetBreaks(key);
            var pause = breaks.Where(b => b.EndLocal.HasValue).Sum(b => (int)(b.EndLocal!.Value - b.StartLocal).TotalMinutes);
            var net = (wd.ComeLocal.HasValue && wd.GoLocal.HasValue) ? (int)(wd.GoLocal.Value - wd.ComeLocal.Value).TotalMinutes - pause : 0;
            var target = (wd.DayType == "UL" || wd.DayType == "AM") ? 0 : _settings.Current.GetTargetMinutes(day.DayOfWeek);
            var overtime = net - target;

            var dayTasks = weekTasks.Where(t => t.StartLocal.HasValue && t.StartLocal.Value.Date == day.Date).OrderBy(t => t.StartLocal).ToList();

            Days.Add(new WeekDayGroup
            {
                DayLabel = day.ToString("ddd dd.MM"),
                Tasks = new ObservableCollection<TaskItem>(dayTasks),
                DayType = wd.DayType,
                IsBr = wd.IsBr,
                IsHo = wd.IsHo,
                Summary = $"Soll {Fmt(target)} | Ist {Fmt(net)} | Ü {Fmt(overtime)}"
            });
        }
    }

    private static string Fmt(int minutes) => $"{minutes / 60}h {Math.Abs(minutes % 60):00}m";
    private static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7));

    public override string ToString() => Title;
}

public class WeekDayGroup
{
    public string DayLabel { get; set; } = string.Empty;
    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
    public string DayType { get; set; } = "Normal";
    public bool IsBr { get; set; }
    public bool IsHo { get; set; }
    public string Summary { get; set; } = string.Empty;
}

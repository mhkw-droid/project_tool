using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Models;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class WeekViewModel : ObservableObject
{
    private readonly TaskService _tasks;

    public string Title => "Woche";

    private DateTime _weekStart;
    public DateTime WeekStart
    {
        get => _weekStart;
        set
        {
            if (Set(ref _weekStart, value))
            {
                Raise(nameof(WeekRangeLabel));
            }
        }
    }

    public string WeekRangeLabel =>
        $"{WeekStart:dd.MM.yyyy} - {WeekStart.AddDays(6):dd.MM.yyyy}";

    public ObservableCollection<WeekDayGroup> Days { get; } = new();

    public RelayCommand PreviousWeekCommand { get; }
    public RelayCommand NextWeekCommand { get; }
    public RelayCommand CurrentWeekCommand { get; }

    public WeekViewModel(TaskService tasks)
    {
        _tasks = tasks;
        WeekStart = StartOfWeek(DateTime.Today);

        PreviousWeekCommand = new RelayCommand(() =>
        {
            WeekStart = WeekStart.AddDays(-7);
            LoadWeek();
        });

        NextWeekCommand = new RelayCommand(() =>
        {
            WeekStart = WeekStart.AddDays(7);
            LoadWeek();
        });

        CurrentWeekCommand = new RelayCommand(() =>
        {
            WeekStart = StartOfWeek(DateTime.Today);
            LoadWeek();
        });

        LoadWeek();
    }

    private void LoadWeek()
    {
        Days.Clear();
        var from = WeekStart;
        var to = WeekStart.AddDays(7);
        var weekTasks = _tasks.GetTasksForRange(from, to);

        for (int i = 0; i < 7; i++)
        {
            var day = WeekStart.AddDays(i);
            var dayTasks = weekTasks
                .Where(t => t.StartLocal.HasValue && t.StartLocal.Value.Date == day.Date)
                .OrderBy(t => t.StartLocal)
                .ToList();

            Days.Add(new WeekDayGroup
            {
                DayLabel = day.ToString("ddd dd.MM"),
                DayDate = day,
                Tasks = new ObservableCollection<TaskItem>(dayTasks)
            });
        }
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    public override string ToString() => Title;
}

public class WeekDayGroup
{
    public string DayLabel { get; set; } = string.Empty;
    public DateTime DayDate { get; set; }
    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
}

using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class ReportsViewModel : ObservableObject
{
    private readonly TaskService _tasks;

    public string Title => "Reports";

    private string _monthSummary = "0h 0m";
    public string MonthSummary { get => _monthSummary; set => Set(ref _monthSummary, value); }

    private int _totalMinutes;
    public int TotalMinutes { get => _totalMinutes; set => Set(ref _totalMinutes, value); }

    public ObservableCollection<string> Breakdown { get; } = new();
    public ObservableCollection<ReportTaskItem> TopTasks { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public ReportsViewModel(TaskService tasks)
    {
        _tasks = tasks;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        var month = DateTime.Today;
        TotalMinutes = _tasks.GetMonthTicketMinutes(month);
        MonthSummary = $"{TotalMinutes / 60}h {TotalMinutes % 60}m";

        Breakdown.Clear();
        Breakdown.Add($"Gesamt Minuten: {TotalMinutes}");
        Breakdown.Add($"Arbeitstage-Ø (22 Tage): {(TotalMinutes / 22) / 60}h {(TotalMinutes / 22) % 60}m");
        Breakdown.Add($"Heute gebucht (aus Monatswert grob): {(TotalMinutes / Math.Max(1, DateTime.Today.Day)) / 60}h {(TotalMinutes / Math.Max(1, DateTime.Today.Day)) % 60}m");

        TopTasks.Clear();
        foreach (var (title, mins) in _tasks.GetTopTasksForMonth(month))
        {
            TopTasks.Add(new ReportTaskItem
            {
                Title = title,
                Minutes = mins,
                DurationText = $"{mins / 60}h {mins % 60}m"
            });
        }
    }

    public override string ToString() => Title;
}

public class ReportTaskItem
{
    public string Title { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public string DurationText { get; set; } = string.Empty;
}

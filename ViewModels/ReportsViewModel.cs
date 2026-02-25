using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class ReportsViewModel : ObservableObject
{
    public string Title => "Reports";
    public string MonthSummary { get; }
    public int TotalMinutes { get; }
    public ObservableCollection<string> Breakdown { get; } = new();

    public ReportsViewModel(TaskService tasks)
    {
        TotalMinutes = tasks.GetMonthTicketMinutes(DateTime.Today);
        MonthSummary = $"{TotalMinutes / 60}h {TotalMinutes % 60}m";

        Breakdown.Add($"Gesamt Minuten: {TotalMinutes}");
        Breakdown.Add($"Arbeitstage-Ø (22 Tage): {(TotalMinutes / 22) / 60}h {(TotalMinutes / 22) % 60}m");
        Breakdown.Add($"Aktuelle Woche grob: {(TotalMinutes / 4) / 60}h {(TotalMinutes / 4) % 60}m");
    }

    public override string ToString() => Title;
}

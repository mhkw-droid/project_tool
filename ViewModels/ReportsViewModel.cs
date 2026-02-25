using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class ReportsViewModel : ObservableObject
{
    public string Title => "Reports";
    public string MonthSummary { get; }

    public ReportsViewModel(TaskService tasks)
    {
        var mins = tasks.GetMonthTicketMinutes(DateTime.Today);
        MonthSummary = $"Gebuchte Ticketzeit im Monat: {mins / 60}h {mins % 60}m";
    }

    public override string ToString() => Title;
}

using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class WeekViewModel : ObservableObject
{
    public string Title => "Woche";
    public ObservableCollection<string> Items { get; } = new();

    public WeekViewModel(TaskService tasks)
    {
        var start = DateTime.Today;
        for (int i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            var dayTasks = tasks.GetTasksForDay(day);
            if (dayTasks.Count == 0)
            {
                Items.Add($"{day:ddd dd.MM}: -");
                continue;
            }
            foreach (var t in dayTasks)
            {
                Items.Add($"{day:ddd dd.MM} {t.StartLocal:HH:mm} {t.Title} ({t.Status})");
            }
        }
    }

    public override string ToString() => Title;
}

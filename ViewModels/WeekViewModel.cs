using System.Collections.ObjectModel;
using TaskTool.Infrastructure;
using TaskTool.Models;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class WeekViewModel : ObservableObject
{
    public string Title => "Woche";
    public ObservableCollection<WeekDayGroup> Days { get; } = new();

    public WeekViewModel(TaskService tasks)
    {
        var start = DateTime.Today;
        for (int i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            var dayTasks = tasks.GetTasksForDay(day)
                .Where(t => t.StartLocal.HasValue && t.StartLocal.Value.Date == day.Date)
                .ToList();

            Days.Add(new WeekDayGroup
            {
                DayLabel = day.ToString("dddd, dd.MM.yyyy"),
                Tasks = new ObservableCollection<TaskItem>(dayTasks)
            });
        }
    }

    public override string ToString() => Title;
}

public class WeekDayGroup
{
    public string DayLabel { get; set; } = string.Empty;
    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
}

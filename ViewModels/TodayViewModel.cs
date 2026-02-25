using System.Collections.ObjectModel;
using System.Windows;
using TaskTool.Infrastructure;
using TaskTool.Models;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class TodayViewModel : ObservableObject
{
    private readonly TaskService _tasks;
    private readonly WorkDayService _workDays;
    public string Title => "Heute";

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    private TaskItem? _selectedTask;
    public TaskItem? SelectedTask { get => _selectedTask; set { if (Set(ref _selectedTask, value)) Raise(nameof(IsTaskSelected)); } }
    public bool IsTaskSelected => SelectedTask != null;

    private string _quickAddText = string.Empty;
    public string QuickAddText { get => _quickAddText; set => Set(ref _quickAddText, value); }
    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    public string WorkDaySummary { get => _workDaySummary; set => Set(ref _workDaySummary, value); }
    private string _workDaySummary = string.Empty;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand QuickAddCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand DoneCommand { get; }
    public RelayCommand StartTimerCommand { get; }
    public RelayCommand PauseTimerCommand { get; }
    public RelayCommand StopTimerCommand { get; }
    public RelayCommand Add15Command { get; }
    public RelayCommand Add30Command { get; }
    public RelayCommand ComeCommand { get; }
    public RelayCommand GoCommand { get; }
    public RelayCommand BreakStartCommand { get; }
    public RelayCommand BreakEndCommand { get; }

    public TodayViewModel(TaskService tasks, WorkDayService workDays)
    {
        _tasks = tasks;
        _workDays = workDays;

        RefreshCommand = new RelayCommand(Load);
        QuickAddCommand = new RelayCommand(QuickAdd);
        SaveCommand = new RelayCommand(SaveTask, () => SelectedTask != null);
        DeleteCommand = new RelayCommand(DeleteTask, () => SelectedTask != null);
        DoneCommand = new RelayCommand(MarkDone, () => SelectedTask != null);
        StartTimerCommand = new RelayCommand(() => WithTask(_tasks.StartTimer));
        PauseTimerCommand = new RelayCommand(() => WithTask(_tasks.PauseTimer));
        StopTimerCommand = new RelayCommand(() => WithTask(_tasks.StopTimer));
        Add15Command = new RelayCommand(() => WithTask(t => _tasks.AddTicketMinutes(t, 15)));
        Add30Command = new RelayCommand(() => WithTask(t => _tasks.AddTicketMinutes(t, 30)));
        ComeCommand = new RelayCommand(() => { _workDays.SetCome(DateTime.Now); Load(); });
        GoCommand = new RelayCommand(() => { _workDays.SetGo(DateTime.Now); Load(); });
        BreakStartCommand = new RelayCommand(() => { _workDays.StartBreak(DateTime.Today.ToString("yyyy-MM-dd")); Load(); });
        BreakEndCommand = new RelayCommand(() => { _workDays.EndBreak(DateTime.Today.ToString("yyyy-MM-dd")); Load(); });

        Load();
    }

    private void Load()
    {
        Tasks.Clear();
        foreach (var task in _tasks.GetTasksForDay(DateTime.Today)) Tasks.Add(task);
        if (SelectedTask == null && Tasks.Count > 0) SelectedTask = Tasks[0];

        var wd = _workDays.GetOrCreateToday();
        var breaks = _workDays.GetBreaks(wd.Day);
        var come = wd.ComeLocal;
        var go = wd.GoLocal;
        var presence = (come.HasValue && go.HasValue) ? go.Value - come.Value : TimeSpan.Zero;
        var pause = breaks.Where(b => b.EndLocal.HasValue).Aggregate(TimeSpan.Zero, (acc, b) => acc + (b.EndLocal!.Value - b.StartLocal));
        var net = presence - pause;
        var ticket = Tasks.Sum(t => t.TicketMinutesBooked);
        WorkDaySummary = $"Kommen: {Fmt(come)} | Gehen: {Fmt(go)}\nAnwesenheit: {presence:hh\\:mm} | Pause: {pause:hh\\:mm} | Netto: {net:hh\\:mm}\nTicketmin: {ticket}";

        StatusMessage = _tasks.LastError;
    }

    private static string Fmt(DateTime? dt) => dt?.ToString("HH:mm") ?? "-";

    private void QuickAdd()
    {
        var task = _tasks.ParseQuickAdd(QuickAddText);
        _tasks.CreateTask(task);
        QuickAddText = string.Empty;
        Load();
    }

    private void SaveTask()
    {
        if (SelectedTask == null) return;
        _tasks.UpdateTask(SelectedTask);
        Load();
    }

    private void DeleteTask()
    {
        if (SelectedTask == null) return;
        _tasks.DeleteTask(SelectedTask);
        SelectedTask = null;
        Load();
    }

    private void MarkDone()
    {
        if (SelectedTask == null) return;
        _tasks.MarkDone(SelectedTask);
        Load();
    }

    private void WithTask(Action<TaskItem> action)
    {
        if (SelectedTask == null)
        {
            MessageBox.Show("Bitte zuerst eine Aufgabe auswählen.");
            return;
        }
        action(SelectedTask);
        Load();
    }

    public override string ToString() => Title;
}

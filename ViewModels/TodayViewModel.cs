using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using TaskTool.Infrastructure;
using TaskTool.Models;
using TaskTool.Services;

namespace TaskTool.ViewModels;

public class TodayViewModel : ObservableObject
{
    private readonly TaskService _tasks;
    private readonly WorkDayService _workDays;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _clock;

    public string Title => "Heute";
    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<BreakEditRow> BreakRows { get; } = new();
    public ObservableCollection<int> DurationOptions { get; } = new() { 15, 30, 45, 60, 90, 120 };

    private TaskItem? _selectedTask;
    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (Set(ref _selectedTask, value))
            {
                if (value?.StartLocal is DateTime start)
                {
                    StartLocalText = start.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                }
                else
                {
                    StartLocalText = string.Empty;
                }

                if (value?.StartLocal.HasValue == true && value.EndLocal.HasValue)
                {
                    var diff = (int)Math.Max(0, (value.EndLocal.Value - value.StartLocal.Value).TotalMinutes);
                    SelectedDurationMinutes = DurationOptions.Contains(diff) ? diff : DurationOptions[0];
                }
                else
                {
                    SelectedDurationMinutes = DurationOptions[0];
                }

                Raise(nameof(IsTaskSelected));
                Raise(nameof(OutlookBlockerButtonText));
                Raise(nameof(OutlookBlockerState));
                Raise(nameof(ComputedEndText));
                UpdateTimerDisplay();
            }
        }
    }

    public bool IsTaskSelected => SelectedTask != null;

    private string _quickAddText = string.Empty;
    public string QuickAddText { get => _quickAddText; set => Set(ref _quickAddText, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    private string _workDaySummary = string.Empty;
    public string WorkDaySummary { get => _workDaySummary; set => Set(ref _workDaySummary, value); }

    private string _comeTimeText = string.Empty;
    public string ComeTimeText { get => _comeTimeText; set => Set(ref _comeTimeText, value); }

    private string _goTimeText = string.Empty;
    public string GoTimeText { get => _goTimeText; set => Set(ref _goTimeText, value); }

    private string _timerDisplay = "00:00:00";
    public string TimerDisplay { get => _timerDisplay; set => Set(ref _timerDisplay, value); }

    private string _startLocalText = string.Empty;
    public string StartLocalText
    {
        get => _startLocalText;
        set
        {
            if (Set(ref _startLocalText, value))
            {
                Raise(nameof(ComputedEndText));
            }
        }
    }

    private int _selectedDurationMinutes = 30;
    public int SelectedDurationMinutes
    {
        get => _selectedDurationMinutes;
        set
        {
            if (Set(ref _selectedDurationMinutes, value))
            {
                Raise(nameof(ComputedEndText));
            }
        }
    }

    public string DateTimeFormat => string.IsNullOrWhiteSpace(_settings.Current.DateTimeFormat)
        ? "yyyy-MM-dd HH:mm"
        : _settings.Current.DateTimeFormat;

    public string ComputedEndText
    {
        get
        {
            if (TryParseStartLocal(StartLocalText, out var start))
            {
                return start.AddMinutes(SelectedDurationMinutes).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
            }
            return "-";
        }
    }

    public string OutlookBlockerButtonText => string.IsNullOrWhiteSpace(SelectedTask?.OutlookEntryId)
        ? "Outlook Blocker erstellen"
        : "Outlook Blocker aktualisieren";

    public string OutlookBlockerState => string.IsNullOrWhiteSpace(SelectedTask?.OutlookEntryId)
        ? "Blocker: nicht vorhanden"
        : "Blocker: erstellt";

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
    public RelayCommand Add60Command { get; }
    public RelayCommand ComeCommand { get; }
    public RelayCommand GoCommand { get; }
    public RelayCommand BreakStartCommand { get; }
    public RelayCommand BreakEndCommand { get; }
    public RelayCommand ManualSaveCommand { get; }
    public RelayCommand AddBreakRowCommand { get; }
    public RelayCommand SyncOutlookBlockerCommand { get; }
    public RelayCommand DeleteOutlookBlockerCommand { get; }

    public RelayCommand<TaskItem> SelectTaskCommand { get; }
    public RelayCommand<TaskItem> StartTaskCommand { get; }
    public RelayCommand<TaskItem> PauseTaskCommand { get; }
    public RelayCommand<TaskItem> StopTaskCommand { get; }
    public RelayCommand<TaskItem> DoneTaskCommand { get; }

    public TodayViewModel(TaskService tasks, WorkDayService workDays, SettingsService settings)
    {
        _tasks = tasks;
        _workDays = workDays;
        _settings = settings;

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
        Add60Command = new RelayCommand(() => WithTask(t => _tasks.AddTicketMinutes(t, 60)));
        ComeCommand = new RelayCommand(() => { _workDays.SetCome(DateTime.Now); Load(); });
        GoCommand = new RelayCommand(() => { _workDays.SetGo(DateTime.Now); Load(); });
        BreakStartCommand = new RelayCommand(() => { _workDays.StartBreak(DateTime.Today.ToString("yyyy-MM-dd")); Load(); });
        BreakEndCommand = new RelayCommand(() => { _workDays.EndBreak(DateTime.Today.ToString("yyyy-MM-dd")); Load(); });
        ManualSaveCommand = new RelayCommand(SaveManualDay);
        AddBreakRowCommand = new RelayCommand(() => BreakRows.Add(new BreakEditRow()));
        SyncOutlookBlockerCommand = new RelayCommand(SyncOutlookBlocker, () => SelectedTask != null);
        DeleteOutlookBlockerCommand = new RelayCommand(DeleteOutlookBlocker, () => SelectedTask != null);

        SelectTaskCommand = new RelayCommand<TaskItem>(task => SelectedTask = task, task => task != null);
        StartTaskCommand = new RelayCommand<TaskItem>(task => OnCardTaskAction(task, _tasks.StartTimer));
        PauseTaskCommand = new RelayCommand<TaskItem>(task => OnCardTaskAction(task, _tasks.PauseTimer));
        StopTaskCommand = new RelayCommand<TaskItem>(task => OnCardTaskAction(task, _tasks.StopTimer));
        DoneTaskCommand = new RelayCommand<TaskItem>(task => OnCardTaskAction(task, _tasks.MarkDone));

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateTimerDisplay();
        _clock.Start();

        Load();
    }

    private void Load()
    {
        var selectedId = SelectedTask?.Id;
        Tasks.Clear();
        foreach (var task in _tasks.GetTasksForDay(DateTime.Today)) Tasks.Add(task);

        SelectedTask = selectedId.HasValue
            ? Tasks.FirstOrDefault(t => t.Id == selectedId.Value) ?? Tasks.FirstOrDefault()
            : Tasks.FirstOrDefault();

        var day = DateTime.Today.ToString("yyyy-MM-dd");
        var wd = _workDays.GetOrCreateDay(day);
        var breaks = _workDays.GetBreaks(day);

        BreakRows.Clear();
        foreach (var br in breaks)
        {
            BreakRows.Add(new BreakEditRow
            {
                StartTime = br.StartLocal.ToString("HH:mm"),
                EndTime = br.EndLocal?.ToString("HH:mm") ?? string.Empty,
                Note = br.Note
            });
        }
        if (BreakRows.Count == 0) BreakRows.Add(new BreakEditRow());

        ComeTimeText = wd.ComeLocal?.ToString("HH:mm") ?? string.Empty;
        GoTimeText = wd.GoLocal?.ToString("HH:mm") ?? string.Empty;

        var presence = (wd.ComeLocal.HasValue && wd.GoLocal.HasValue) ? wd.GoLocal.Value - wd.ComeLocal.Value : TimeSpan.Zero;
        var pause = breaks.Where(b => b.EndLocal.HasValue).Aggregate(TimeSpan.Zero, (acc, b) => acc + (b.EndLocal!.Value - b.StartLocal));
        var net = presence - pause;
        var ticket = Tasks.Sum(t => t.TicketMinutesBooked);
        WorkDaySummary = $"Kommen: {Fmt(wd.ComeLocal)}   Gehen: {Fmt(wd.GoLocal)}\nAnwesenheit: {presence:hh\\:mm}   Pause gesamt: {pause:hh\\:mm}\nNetto: {net:hh\\:mm}   Ticketminuten: {ticket}";

        StatusMessage = _tasks.LastError;
        Raise(nameof(DateTimeFormat));
        UpdateTimerDisplay();
    }

    private static string Fmt(DateTime? dt) => dt?.ToString("HH:mm") ?? "--:--";

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

        if (TryParseStartLocal(StartLocalText, out var start))
        {
            SelectedTask.StartLocal = start;
            SelectedTask.EndLocal = start.AddMinutes(SelectedDurationMinutes);
        }

        _tasks.UpdateTask(SelectedTask);
        StatusMessage = "Task gespeichert.";
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

    private void SyncOutlookBlocker()
    {
        if (SelectedTask == null) return;

        if (TryParseStartLocal(StartLocalText, out var start))
        {
            SelectedTask.StartLocal = start;
            SelectedTask.EndLocal = start.AddMinutes(SelectedDurationMinutes);
        }

        var ok = _tasks.SyncOutlookBlocker(SelectedTask);
        StatusMessage = ok ? "Outlook Blocker erfolgreich synchronisiert." : _tasks.LastError;
        Load();
    }

    private void DeleteOutlookBlocker()
    {
        if (SelectedTask == null) return;
        var ok = _tasks.DeleteOutlookBlocker(SelectedTask);
        StatusMessage = ok ? "Outlook Blocker gelöscht." : _tasks.LastError;
        Load();
    }

    private bool TryParseStartLocal(string input, out DateTime value)
    {
        if (DateTime.TryParseExact(input, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;

        return DateTime.TryParse(input, out value);
    }

    private void SaveManualDay()
    {
        try
        {
            var day = DateTime.Today;
            var come = ParseLocalTime(day, ComeTimeText);
            var go = ParseLocalTime(day, GoTimeText);

            var mappedBreaks = new List<BreakRecord>();
            foreach (var row in BreakRows)
            {
                var bStart = ParseLocalTime(day, row.StartTime);
                if (!bStart.HasValue) continue;

                mappedBreaks.Add(new BreakRecord
                {
                    Day = day.ToString("yyyy-MM-dd"),
                    StartLocal = bStart.Value,
                    EndLocal = ParseLocalTime(day, row.EndTime),
                    Note = row.Note
                });
            }

            _workDays.SaveManualDay(day.ToString("yyyy-MM-dd"), come, go, mappedBreaks);
            Load();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Manuelles Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    private static DateTime? ParseLocalTime(DateTime day, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTime.TryParse(text, out var full)) return full;
        if (TimeSpan.TryParse(text, out var time)) return day.Date + time;
        return null;
    }

    private void OnCardTaskAction(TaskItem? task, Action<TaskItem> action)
    {
        if (task == null) return;
        SelectedTask = task;
        action(task);
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

    private void UpdateTimerDisplay()
    {
        if (SelectedTask == null)
        {
            TimerDisplay = "00:00:00";
            return;
        }

        var elapsed = _tasks.GetTrackedDuration(SelectedTask.Id);
        TimerDisplay = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    public override string ToString() => Title;
}

public class BreakEditRow : ObservableObject
{
    private string _startTime = string.Empty;
    public string StartTime { get => _startTime; set => Set(ref _startTime, value); }

    private string _endTime = string.Empty;
    public string EndTime { get => _endTime; set => Set(ref _endTime, value); }

    private string _note = string.Empty;
    public string Note { get => _note; set => Set(ref _note, value); }
}

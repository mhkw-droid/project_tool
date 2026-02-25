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
    public ObservableCollection<TaskSegment> Segments { get; } = new();

    public ObservableCollection<int> DurationOptions { get; } = new() { 15, 30, 45, 60, 90, 120, 180 };
    public ObservableCollection<int> Hours { get; } = new(Enumerable.Range(0, 24));
    public ObservableCollection<int> Minutes { get; } = new(new[] { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 });

    private TaskItem? _selectedTask;
    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (Set(ref _selectedTask, value))
            {
                LoadTaskPlanning();
                LoadSegments();
                Raise(nameof(IsTaskSelected));
                Raise(nameof(OutlookBlockerButtonText));
                Raise(nameof(OutlookBlockerCanSync));
            }
        }
    }

    public bool IsTaskSelected => SelectedTask != null;

    public string DateTimeFormat => string.IsNullOrWhiteSpace(_settings.Current.DateTimeFormat) ? "yyyy-MM-dd HH:mm" : _settings.Current.DateTimeFormat;
    public bool AllowMultiDaySegments => _settings.Current.AllowMultiDayTaskPlanning;

    private string _quickAddText = string.Empty;
    public string QuickAddText { get => _quickAddText; set => Set(ref _quickAddText, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    private string _workDaySummary = string.Empty;
    public string WorkDaySummary { get => _workDaySummary; set => Set(ref _workDaySummary, value); }

    private string _todayTotals = string.Empty;
    public string TodayTotals { get => _todayTotals; set => Set(ref _todayTotals, value); }

    private string _comeTimeText = string.Empty;
    public string ComeTimeText { get => _comeTimeText; set => Set(ref _comeTimeText, value); }

    private string _goTimeText = string.Empty;
    public string GoTimeText { get => _goTimeText; set => Set(ref _goTimeText, value); }

    private string _timerDisplay = "00:00:00";
    public string TimerDisplay { get => _timerDisplay; set => Set(ref _timerDisplay, value); }

    private DateTime? _selectedStartDate = DateTime.Today;
    public DateTime? SelectedStartDate { get => _selectedStartDate; set { if (Set(ref _selectedStartDate, value)) Raise(nameof(ComputedEndText)); } }

    private int _selectedHour = DateTime.Now.Hour;
    public int SelectedHour { get => _selectedHour; set { if (Set(ref _selectedHour, value)) Raise(nameof(ComputedEndText)); } }

    private int _selectedMinute = 0;
    public int SelectedMinute { get => _selectedMinute; set { if (Set(ref _selectedMinute, value)) Raise(nameof(ComputedEndText)); } }

    private int _selectedDurationMinutes = 30;
    public int SelectedDurationMinutes { get => _selectedDurationMinutes; set { if (Set(ref _selectedDurationMinutes, value)) Raise(nameof(ComputedEndText)); } }

    private string _manualDurationMinutesText = string.Empty;
    public string ManualDurationMinutesText { get => _manualDurationMinutesText; set { if (Set(ref _manualDurationMinutesText, value)) Raise(nameof(ComputedEndText)); } }

    private string _dayType = "Normal";
    public string DayType { get => _dayType; set => Set(ref _dayType, value); }

    private bool _isBr;
    public bool IsBr { get => _isBr; set => Set(ref _isBr, value); }

    private bool _isHo;
    public bool IsHo { get => _isHo; set => Set(ref _isHo, value); }

    public string ComputedEndText
    {
        get
        {
            if (!OutlookBlockerCanSync || !TryBuildStart(out var start)) return "-";
            return start.AddMinutes(GetDurationMinutes()).ToString(DateTimeFormat);
        }
    }

    public bool OutlookBlockerCanSync => SelectedStartDate.HasValue;
    public string OutlookBlockerButtonText => string.IsNullOrWhiteSpace(SelectedTask?.OutlookEntryId) ? "Outlook Blocker erstellen" : "Outlook Blocker aktualisieren";

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
    public RelayCommand SaveMarkersCommand { get; }
    public RelayCommand AddSegmentCommand { get; }
    public RelayCommand SyncAllSegmentsCommand { get; }

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
        SyncOutlookBlockerCommand = new RelayCommand(SyncOutlookBlocker, () => SelectedTask != null && OutlookBlockerCanSync);
        DeleteOutlookBlockerCommand = new RelayCommand(DeleteOutlookBlocker, () => SelectedTask != null);
        SaveMarkersCommand = new RelayCommand(SaveMarkers);
        AddSegmentCommand = new RelayCommand(AddSegment, () => SelectedTask != null && AllowMultiDaySegments);
        SyncAllSegmentsCommand = new RelayCommand(SyncAllSegments, () => SelectedTask != null && AllowMultiDaySegments);

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
        SelectedTask = selectedId.HasValue ? Tasks.FirstOrDefault(t => t.Id == selectedId.Value) ?? Tasks.FirstOrDefault() : Tasks.FirstOrDefault();

        var day = DateTime.Today.ToString("yyyy-MM-dd");
        var wd = _workDays.GetOrCreateDay(day);
        var breaks = _workDays.GetBreaks(day);

        BreakRows.Clear();
        foreach (var br in breaks)
        {
            BreakRows.Add(new BreakEditRow { StartTime = br.StartLocal.ToString("HH:mm"), EndTime = br.EndLocal?.ToString("HH:mm") ?? string.Empty, Note = br.Note });
        }
        if (BreakRows.Count == 0) BreakRows.Add(new BreakEditRow());

        DayType = wd.DayType;
        IsBr = wd.IsBr;
        IsHo = wd.IsHo;

        ComeTimeText = wd.ComeLocal?.ToString("HH:mm") ?? string.Empty;
        GoTimeText = wd.GoLocal?.ToString("HH:mm") ?? string.Empty;

        var presence = (wd.ComeLocal.HasValue && wd.GoLocal.HasValue) ? wd.GoLocal.Value - wd.ComeLocal.Value : TimeSpan.Zero;
        var pause = breaks.Where(b => b.EndLocal.HasValue).Aggregate(TimeSpan.Zero, (acc, b) => acc + (b.EndLocal!.Value - b.StartLocal));
        var net = presence - pause;
        var ticket = Tasks.Sum(t => t.TicketMinutesBooked);

        var target = (wd.DayType == "UL" || wd.DayType == "AM") ? 0 : _settings.Current.GetTargetMinutes(DateTime.Today.DayOfWeek);
        var overtime = (int)net.TotalMinutes - target;
        var monthOvertime = CalculateMonthOvertime();

        WorkDaySummary = $"Kommen: {Fmt(wd.ComeLocal)}   Gehen: {Fmt(wd.GoLocal)}   Typ: {wd.DayType} {(wd.IsBr ? "BR" : "")} {(wd.IsHo ? "HO" : "")}";
        TodayTotals = $"Soll heute: {FmtMin(target)} | Ist (Netto): {FmtMin((int)net.TotalMinutes)} | Überstunden heute: {FmtMin(overtime)} | Überstunden Monat: {FmtMin(monthOvertime)}";
        StatusMessage = _tasks.LastError;
        UpdateTimerDisplay();
    }

    private int CalculateMonthOvertime()
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var days = _workDays.GetWorkDaysInRange(first, last).ToDictionary(d => d.Day, d => d);
        var total = 0;
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            var wd = days.ContainsKey(key) ? days[key] : new WorkDayRecord { Day = key, DayType = "Normal" };
            var target = (wd.DayType == "UL" || wd.DayType == "AM") ? 0 : _settings.Current.GetTargetMinutes(d.DayOfWeek);
            var netMin = 0;
            if (wd.ComeLocal.HasValue && wd.GoLocal.HasValue)
            {
                var breaks = _workDays.GetBreaks(key);
                var pause = breaks.Where(b => b.EndLocal.HasValue).Sum(b => (int)(b.EndLocal!.Value - b.StartLocal).TotalMinutes);
                netMin = (int)(wd.GoLocal.Value - wd.ComeLocal.Value).TotalMinutes - pause;
            }
            total += netMin - target;
        }
        return total;
    }

    private void LoadTaskPlanning()
    {
        if (SelectedTask == null) return;
        if (SelectedTask.StartLocal.HasValue)
        {
            SelectedStartDate = SelectedTask.StartLocal.Value.Date;
            SelectedHour = SelectedTask.StartLocal.Value.Hour;
            SelectedMinute = (SelectedTask.StartLocal.Value.Minute / 5) * 5;
        }
        else
        {
            SelectedStartDate = DateTime.Today;
            SelectedHour = DateTime.Now.Hour;
            SelectedMinute = 0;
        }

        if (SelectedTask.StartLocal.HasValue && SelectedTask.EndLocal.HasValue)
        {
            SelectedDurationMinutes = Math.Max(15, (int)(SelectedTask.EndLocal.Value - SelectedTask.StartLocal.Value).TotalMinutes);
        }
    }

    private void LoadSegments()
    {
        Segments.Clear();
        if (SelectedTask == null || !AllowMultiDaySegments) return;
        foreach (var seg in _tasks.GetSegments(SelectedTask.Id)) Segments.Add(seg);
    }

    private void AddSegment()
    {
        if (SelectedTask == null || !TryBuildStart(out var start)) return;
        var dur = GetDurationMinutes();
        var seg = new TaskSegment { TaskId = SelectedTask.Id, StartLocal = start, EndLocal = start.AddMinutes(dur), PlannedMinutes = dur };
        _tasks.AddSegment(seg);
        LoadSegments();
    }

    private void SyncAllSegments()
    {
        if (SelectedTask == null) return;
        foreach (var seg in Segments) _tasks.SyncSegmentOutlook(seg, SelectedTask.Title, SelectedTask.Description, SelectedTask.TicketUrl);
        StatusMessage = _tasks.LastError;
        LoadSegments();
    }

    private static string Fmt(DateTime? dt) => dt?.ToString("HH:mm") ?? "--:--";
    private static string FmtMin(int mins) => $"{mins / 60}h {Math.Abs(mins % 60):00}m";

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
        if (TryBuildStart(out var start))
        {
            SelectedTask.StartLocal = start;
            SelectedTask.EndLocal = start.AddMinutes(GetDurationMinutes());
        }
        _tasks.UpdateTask(SelectedTask);
        Load();
    }

    private bool TryBuildStart(out DateTime start)
    {
        start = DateTime.MinValue;
        if (!SelectedStartDate.HasValue) return false;
        start = SelectedStartDate.Value.Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);
        return true;
    }

    private int GetDurationMinutes() => int.TryParse(ManualDurationMinutesText, out var parsed) && parsed > 0 ? parsed : SelectedDurationMinutes;

    private void SaveMarkers()
    {
        _workDays.SetDayMarkers(DateTime.Today.ToString("yyyy-MM-dd"), DayType, IsBr, IsHo);
        Load();
    }

    private void DeleteTask() { if (SelectedTask == null) return; _tasks.DeleteTask(SelectedTask); SelectedTask = null; Load(); }
    private void MarkDone() { if (SelectedTask == null) return; _tasks.MarkDone(SelectedTask); Load(); }

    private void SyncOutlookBlocker()
    {
        if (SelectedTask == null) return;
        if (!TryBuildStart(out var start)) { StatusMessage = "Bitte Startdatum und Uhrzeit wählen."; return; }
        SelectedTask.StartLocal = start;
        SelectedTask.EndLocal = start.AddMinutes(GetDurationMinutes());
        var ok = _tasks.SyncOutlookBlocker(SelectedTask);
        StatusMessage = ok ? "Outlook Blocker synchronisiert." : _tasks.LastError;
        Load();
    }

    private void DeleteOutlookBlocker()
    {
        if (SelectedTask == null) return;
        var ok = _tasks.DeleteOutlookBlocker(SelectedTask);
        StatusMessage = ok ? "Outlook Blocker gelöscht." : _tasks.LastError;
        Load();
    }

    private void SaveManualDay()
    {
        try
        {
            var day = DateTime.Today;
            var come = ParseLocalTime(day, ComeTimeText);
            var go = ParseLocalTime(day, GoTimeText);
            var mappedBreaks = BreakRows.Select(row => new { row, start = ParseLocalTime(day, row.StartTime) })
                .Where(x => x.start.HasValue)
                .Select(x => new BreakRecord { Day = day.ToString("yyyy-MM-dd"), StartLocal = x.start!.Value, EndLocal = ParseLocalTime(day, x.row.EndTime), Note = x.row.Note })
                .ToList();
            _workDays.SaveManualDay(day.ToString("yyyy-MM-dd"), come, go, mappedBreaks);
            _workDays.SetDayMarkers(day.ToString("yyyy-MM-dd"), DayType, IsBr, IsHo);
            Load();
        }
        catch (Exception ex) { StatusMessage = $"Manuelles Speichern fehlgeschlagen: {ex.Message}"; }
    }

    private static DateTime? ParseLocalTime(DateTime day, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTime.TryParse(text, out var full)) return full;
        if (TimeSpan.TryParse(text, out var time)) return day.Date + time;
        return null;
    }

    private void OnCardTaskAction(TaskItem? task, Action<TaskItem> action) { if (task == null) return; SelectedTask = task; action(task); Load(); }
    private void WithTask(Action<TaskItem> action) { if (SelectedTask == null) { MessageBox.Show("Bitte zuerst eine Aufgabe auswählen."); return; } action(SelectedTask); Load(); }

    private void UpdateTimerDisplay()
    {
        if (SelectedTask == null) { TimerDisplay = "00:00:00"; return; }
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

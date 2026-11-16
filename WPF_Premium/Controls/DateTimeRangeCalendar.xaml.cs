using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public partial class DateTimeRangeCalendar : UserControl
{
    private bool _updatingSelection;
    private bool _isApplying;
    private DateTime? _draftStart;
    private DateTime? _draftEnd;
    private TimeSpan _startTime;
    private TimeSpan _endTime = new(23, 59, 59);

    public static readonly DependencyProperty StartDateTimeProperty =
        DependencyProperty.Register(
            nameof(StartDateTime),
            typeof(DateTime?),
            typeof(DateTimeRangeCalendar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty EndDateTimeProperty =
        DependencyProperty.Register(
            nameof(EndDateTime),
            typeof(DateTime?),
            typeof(DateTimeRangeCalendar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty ShowPresetsProperty =
        DependencyProperty.Register(
            nameof(ShowPresets),
            typeof(bool),
            typeof(DateTimeRangeCalendar),
            new PropertyMetadata(true, OnShowPresetsChanged));

    public static readonly DependencyProperty MinuteIntervalProperty =
        DependencyProperty.Register(
            nameof(MinuteInterval),
            typeof(int),
            typeof(DateTimeRangeCalendar),
            new PropertyMetadata(1, OnMinuteIntervalChanged, CoerceMinuteInterval));

    private static readonly DependencyPropertyKey DisplayTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string), typeof(DateTimeRangeCalendar),
            new PropertyMetadata("Chọn khoảng thời gian"));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    public DateTimeRangeCalendar()
    {
        InitializeComponent();

        var hours = Enumerable.Range(0, 24).Select(value => value.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        var minutesAndSeconds = Enumerable.Range(0, 60).Select(value => value.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        foreach (var picker in new[] { StartHour, EndHour })
        {
            picker.ItemsSource = hours;
            picker.SelectionChanged += TimePicker_SelectionChanged;
        }

        foreach (var picker in new[] { StartSecond, EndSecond })
        {
            picker.ItemsSource = minutesAndSeconds;
            picker.SelectionChanged += TimePicker_SelectionChanged;
        }

        UpdateMinuteOptions();
        foreach (var picker in new[] { StartMinute, EndMinute })
        {
            picker.SelectionChanged += TimePicker_SelectionChanged;
        }

        UpdateDisplayText();
    }

    public DateTime? StartDateTime
    {
        get => (DateTime?)GetValue(StartDateTimeProperty);
        set => SetValue(StartDateTimeProperty, value);
    }

    public DateTime? EndDateTime
    {
        get => (DateTime?)GetValue(EndDateTimeProperty);
        set => SetValue(EndDateTimeProperty, value);
    }

    public bool ShowPresets
    {
        get => (bool)GetValue(ShowPresetsProperty);
        set => SetValue(ShowPresetsProperty, value);
    }

    public int MinuteInterval
    {
        get => (int)GetValue(MinuteIntervalProperty);
        set => SetValue(MinuteIntervalProperty, value);
    }

    public string DisplayText => (string)GetValue(DisplayTextProperty);

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DateTimeRangeCalendar)d;
        control.UpdateDisplayText();
        if (!control._isApplying && (control.RangePopup is null || !control.RangePopup.IsOpen))
        {
            control._draftStart = control.StartDateTime;
            control._draftEnd = control.EndDateTime;
            control._startTime = control.StartDateTime?.TimeOfDay ?? TimeSpan.Zero;
            control._endTime = control.EndDateTime?.TimeOfDay ?? new TimeSpan(23, 59, 59);
        }
    }

    private static void OnShowPresetsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeRangeCalendar)d).UpdatePresetVisibility();

    private static object CoerceMinuteInterval(DependencyObject d, object baseValue)
    {
        var interval = (int)baseValue;
        return interval is >= 1 and <= 60 ? interval : 1;
    }

    private static void OnMinuteIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DateTimeRangeCalendar)d;
        if (control.StartMinute is null)
        {
            return;
        }

        control.UpdateMinuteOptions();
        control.UpdateDraftControls();
    }

    private void UpdateMinuteOptions()
    {
        var minutes = Enumerable.Range(0, 60)
            .Where(value => value % MinuteInterval == 0)
            .Select(value => value.ToString("D2", CultureInfo.InvariantCulture))
            .ToArray();
        StartMinute.ItemsSource = minutes;
        EndMinute.ItemsSource = minutes;
    }

    private void UpdatePresetVisibility()
    {
        if (PresetPanel is not null)
        {
            PresetPanel.Visibility = ShowPresets ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateDisplayText()
    {
        if (StartDateTime is DateTime start && EndDateTime is DateTime end)
        {
            SetValue(DisplayTextPropertyKey, $"{FormatDateTime(start)} — {FormatDateTime(end)}");
        }
        else if (StartDateTime is DateTime onlyStart)
        {
            SetValue(DisplayTextPropertyKey, $"{FormatDateTime(onlyStart)} —");
        }
        else if (EndDateTime is DateTime onlyEnd)
        {
            SetValue(DisplayTextPropertyKey, $"— {FormatDateTime(onlyEnd)}");
        }
        else
        {
            SetValue(DisplayTextPropertyKey, "Chọn khoảng thời gian");
        }
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);

    private void RangeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (RangeToggle.IsChecked == true)
        {
            BeginEdit();
            RangePopup.IsOpen = true;
        }
        else
        {
            RangePopup.IsOpen = false;
        }
    }

    private void BeginEdit()
    {
        var today = DateTime.Today;
        var startDate = StartDateTime?.Date ?? EndDateTime?.Date ?? today;
        var endDate = EndDateTime?.Date ?? startDate;
        _startTime = StartDateTime?.TimeOfDay ?? TimeSpan.Zero;
        _endTime = EndDateTime?.TimeOfDay ?? new TimeSpan(23, 59, 59);
        _draftStart = startDate + _startTime;
        _draftEnd = endDate + _endTime;
        ValidationText.Text = string.Empty;
        ValidationText.Visibility = Visibility.Collapsed;
        UpdateDraftControls();
        SetVisibleMonths(startDate);
    }

    private void SetVisibleMonths(DateTime firstMonth)
    {
        var month = new DateTime(firstMonth.Year, firstMonth.Month, 1);
        FirstCalendar.DisplayDate = month;
        SecondCalendar.DisplayDate = month.AddMonths(1);
        UpdateCalendarSelection();
    }

    private void UpdateCalendarSelection()
    {
        if (FirstCalendar is null || SecondCalendar is null)
        {
            return;
        }

        _updatingSelection = true;
        foreach (var calendar in new[] { FirstCalendar, SecondCalendar })
        {
            calendar.RangeStartDate = _draftStart?.Date;
            calendar.RangeEndDate = _draftEnd?.Date;
        }
        _updatingSelection = false;
    }

    private void UpdateDraftControls()
    {
        _updatingSelection = true;
        StartDateLabel.Text = _draftStart?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "—";
        EndDateLabel.Text = _draftEnd?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "—";
        SetTimePickers(StartHour, StartMinute, StartSecond, _startTime);
        SetTimePickers(EndHour, EndMinute, EndSecond, _endTime);
        _updatingSelection = false;
    }

    private void SetTimePickers(
        ComboBoxControl hourPicker,
        ComboBoxControl minutePicker,
        ComboBoxControl secondPicker,
        TimeSpan time)
    {
        hourPicker.SelectedItem = time.Hours.ToString("D2", CultureInfo.InvariantCulture);
        minutePicker.SelectedItem = GetNearestMinute(time.Minutes).ToString("D2", CultureInfo.InvariantCulture);
        secondPicker.SelectedItem = time.Seconds.ToString("D2", CultureInfo.InvariantCulture);
    }

    private int GetNearestMinute(int minute)
    {
        var rounded = (int)Math.Round(minute / (double)MinuteInterval, MidpointRounding.AwayFromZero) * MinuteInterval;
        return Math.Min(rounded, (59 / MinuteInterval) * MinuteInterval);
    }

    private void RangeCalendar_RangeSelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingSelection || sender is not CalendarControl calendar)
        {
            return;
        }

        _draftStart = calendar.RangeStartDate?.Date + _startTime;
        _draftEnd = calendar.RangeEndDate?.Date + _endTime;
        UpdateDraftControls();
        UpdateCalendarSelection();
    }

    private void TimePicker_SelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        _startTime = ReadTime(StartHour, StartMinute, StartSecond, _startTime);
        _endTime = ReadTime(EndHour, EndMinute, EndSecond, _endTime);
        _draftStart = _draftStart?.Date + _startTime;
        _draftEnd = _draftEnd?.Date + _endTime;
    }

    private static TimeSpan ReadTime(
        ComboBoxControl hourPicker,
        ComboBoxControl minutePicker,
        ComboBoxControl secondPicker,
        TimeSpan fallback)
    {
        if (hourPicker.SelectedItem is string hour &&
            minutePicker.SelectedItem is string minute &&
            secondPicker.SelectedItem is string second &&
            int.TryParse(hour, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) &&
            int.TryParse(minute, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) &&
            int.TryParse(second, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return new TimeSpan(hours, minutes, seconds);
        }

        return fallback;
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset })
        {
            return;
        }

        if (preset == "Custom")
        {
            _draftStart = null;
            _draftEnd = null;
            _startTime = TimeSpan.Zero;
            _endTime = new TimeSpan(23, 59, 59);
            UpdateDraftControls();
            SetVisibleMonths(DateTime.Today);
            ShowValidation("Chọn ngày bắt đầu rồi chọn ngày kết thúc.");
            return;
        }

        var today = DateTime.Today;
        var now = DateTime.Now;
        var start = preset switch
        {
            "Yesterday" => today.AddDays(-1),
            "Today" => today,
            "Last7Days" => today.AddDays(-6),
            "Last30Days" => today.AddDays(-29),
            _ => today
        };
        _startTime = TimeSpan.Zero;
        var end = preset == "Yesterday" ? today.AddTicks(-1) : now;
        _endTime = end.TimeOfDay;
        _draftStart = start;
        _draftEnd = end;
        UpdateDraftControls();
        SetVisibleMonths(start);
        ValidationText.Text = string.Empty;
        ValidationText.Visibility = Visibility.Collapsed;
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) =>
        SetVisibleMonths(FirstCalendar.DisplayDate.AddMonths(-1));

    private void NextMonth_Click(object sender, RoutedEventArgs e) =>
        SetVisibleMonths(FirstCalendar.DisplayDate.AddMonths(1));

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_draftStart is not DateTime start || _draftEnd is not DateTime end)
        {
            ShowValidation("Chọn ngày bắt đầu và ngày kết thúc.");
            return;
        }

        if (end < start)
        {
            ShowValidation("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
            return;
        }

        _isApplying = true;
        SetCurrentValue(StartDateTimeProperty, start);
        SetCurrentValue(EndDateTimeProperty, end);
        _isApplying = false;
        RangePopup.IsOpen = false;
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _isApplying = true;
        SetCurrentValue(StartDateTimeProperty, null);
        SetCurrentValue(EndDateTimeProperty, null);
        _isApplying = false;
        _draftStart = null;
        _draftEnd = null;
        UpdateDraftControls();
        UpdateCalendarSelection();
        RangePopup.IsOpen = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        RangePopup.IsOpen = false;

    private void RangePopup_Closed(object? sender, EventArgs e)
    {
        RangeToggle.IsChecked = false;
    }
}

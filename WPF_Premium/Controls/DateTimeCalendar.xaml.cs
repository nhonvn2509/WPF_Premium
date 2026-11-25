using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public partial class DateTimeCalendar : UserControl
{
    private bool _updating;

    public static readonly DependencyProperty SelectedDateTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedDateTime),
            typeof(DateTime?),
            typeof(DateTimeCalendar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateTimeChanged));

    public static readonly DependencyProperty MinimumDateProperty =
        DependencyProperty.Register(nameof(MinimumDate), typeof(DateTime?), typeof(DateTimeCalendar),
            new PropertyMetadata(null, OnDateBoundaryChanged));

    public static readonly DependencyProperty MaximumDateProperty =
        DependencyProperty.Register(nameof(MaximumDate), typeof(DateTime?), typeof(DateTimeCalendar),
            new PropertyMetadata(null, OnDateBoundaryChanged));

    public static readonly DependencyProperty IsPopupOpenProperty =
        DependencyProperty.Register(nameof(IsPopupOpen), typeof(bool), typeof(DateTimeCalendar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsPopupOpenChanged));

    public static readonly DependencyProperty ShowTimeSelectorProperty =
        DependencyProperty.Register(nameof(ShowTimeSelector), typeof(bool), typeof(DateTimeCalendar),
            new PropertyMetadata(true, OnDisplayOptionsChanged));

    public static readonly DependencyProperty DateFormatProperty =
        DependencyProperty.Register(nameof(DateFormat), typeof(string), typeof(DateTimeCalendar),
            new PropertyMetadata("dd/MM/yyyy", OnDisplayOptionsChanged));

    public static readonly DependencyProperty AutomationIdPrefixProperty =
        DependencyProperty.Register(nameof(AutomationIdPrefix), typeof(string), typeof(DateTimeCalendar),
            new PropertyMetadata("DateTimeCalendar", OnAutomationIdPrefixChanged));

    private static readonly DependencyPropertyKey SelectedDateTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SelectedDateText), typeof(string), typeof(DateTimeCalendar),
            new PropertyMetadata("Chọn ngày"));

    public static readonly DependencyProperty SelectedDateTextProperty = SelectedDateTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey SelectedDateTimeTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SelectedDateTimeText), typeof(string), typeof(DateTimeCalendar),
            new PropertyMetadata("Chọn ngày giờ"));

    public static readonly DependencyProperty SelectedDateTimeTextProperty = SelectedDateTimeTextPropertyKey.DependencyProperty;

    public DateTimeCalendar()
    {
        InitializeComponent();
        HourPicker.ItemsSource = ControlHelpers.Hours24;
        MinutePicker.ItemsSource = ControlHelpers.MinutesOrSeconds60;
        HourPicker.SelectionChanged += Time_SelectionChanged;
        MinutePicker.SelectionChanged += Time_SelectionChanged;
        DateSelector.SelectedDateChanged += DateSelector_SelectedDateChanged;
        UpdateControls();
    }

    public DateTime? SelectedDateTime
    {
        get => (DateTime?)GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    public DateTime? MinimumDate
    {
        get => (DateTime?)GetValue(MinimumDateProperty);
        set => SetValue(MinimumDateProperty, value);
    }

    public DateTime? MaximumDate
    {
        get => (DateTime?)GetValue(MaximumDateProperty);
        set => SetValue(MaximumDateProperty, value);
    }

    public bool IsPopupOpen
    {
        get => (bool)GetValue(IsPopupOpenProperty);
        set => SetValue(IsPopupOpenProperty, value);
    }

    public bool ShowTimeSelector
    {
        get => (bool)GetValue(ShowTimeSelectorProperty);
        set => SetValue(ShowTimeSelectorProperty, value);
    }

    public string DateFormat
    {
        get => (string)GetValue(DateFormatProperty);
        set => SetValue(DateFormatProperty, value);
    }

    public string AutomationIdPrefix
    {
        get => (string)GetValue(AutomationIdPrefixProperty);
        set => SetValue(AutomationIdPrefixProperty, value);
    }

    public string SelectedDateText => (string)GetValue(SelectedDateTextProperty);

    public string SelectedDateTimeText => (string)GetValue(SelectedDateTimeTextProperty);

    private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeCalendar)d).UpdateControls();

    private static void OnDateBoundaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeCalendar)d).UpdateDateBoundaries();

    private static void OnDisplayOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var calendar = (DateTimeCalendar)d;
        calendar.UpdateTimeSelectorVisibility();
        calendar.UpdateControls();
    }

    private static void OnAutomationIdPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeCalendar)d).UpdateAutomationIds();

    private static void OnIsPopupOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeCalendar)d).UpdatePopupState();

    private void UpdateControls()
    {
        if (DateSelector is null)
        {
            return;
        }

        _updating = true;
        var value = SelectedDateTime;
        DateSelector.SelectedDate = value?.Date;
        UpdateDateBoundaries();
        HourPicker.SelectedItem = value?.Hour.ToString("D2", CultureInfo.InvariantCulture) ?? "00";
        MinutePicker.SelectedItem = value?.Minute.ToString("D2", CultureInfo.InvariantCulture) ?? "00";
        var dateText = value?.ToString(DateFormat, CultureInfo.CurrentCulture) ?? "Chọn ngày";
        SetValue(SelectedDateTextPropertyKey, dateText);
        SetValue(SelectedDateTimeTextPropertyKey, value is null
            ? ShowTimeSelector ? "Chọn ngày giờ" : "Chọn ngày"
            : ShowTimeSelector
                ? $"{dateText} {value.Value:HH:mm}"
                : dateText);
        AutomationProperties.SetName(SelectedDateTextBlock, SelectedDateTimeText);
        _updating = false;
    }

    private void UpdateDateBoundaries()
    {
        if (DateSelector is null)
        {
            return;
        }

        DateSelector.MinimumDate = MinimumDate;
        DateSelector.MaximumDate = MaximumDate;
    }

    private void UpdateTimeSelectorVisibility()
    {
        if (TimeSelector is not null)
        {
            TimeSelector.Visibility = ShowTimeSelector ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateAutomationIds()
    {
        if (DateToggle is null)
        {
            return;
        }

        DateToggle.SetValue(AutomationProperties.AutomationIdProperty, $"{AutomationIdPrefix}OpenButton");
        SelectedDateTextBlock.SetValue(AutomationProperties.AutomationIdProperty, $"{AutomationIdPrefix}SelectedDate");
        CalendarPopup.SetValue(AutomationProperties.AutomationIdProperty, $"{AutomationIdPrefix}Popup");
        TodayButton.SetValue(AutomationProperties.AutomationIdProperty, $"{AutomationIdPrefix}Today");
        DateSelector.AutomationIdPrefix = $"{AutomationIdPrefix}Day";
    }

    private void UpdatePopupState()
    {
        if (CalendarPopup is null || DateToggle is null)
        {
            return;
        }

        DateToggle.IsChecked = IsPopupOpen;
        CalendarPopup.IsOpen = IsPopupOpen;
    }

    private void DateSelector_SelectedDateChanged(object? sender, EventArgs e)
    {
        if (!_updating && DateSelector.SelectedDate is DateTime date)
        {
            UpdateSelectedDateTime(date);
            if (!ShowTimeSelector)
            {
                SetCurrentValue(IsPopupOpenProperty, false);
            }
        }
    }

    private void Time_SelectionChanged(object? sender, EventArgs e)
    {
        if (!_updating && DateSelector.SelectedDate is DateTime date)
        {
            UpdateSelectedDateTime(date);
        }
    }

    private void UpdateSelectedDateTime(DateTime date)
    {
        if (ShowTimeSelector &&
            (HourPicker.SelectedItem is not string || MinutePicker.SelectedItem is not string))
        {
            return;
        }

        var hourText = ShowTimeSelector ? HourPicker.SelectedItem as string : "00";
        var minuteText = ShowTimeSelector ? MinutePicker.SelectedItem as string : "00";
        if (hourText is null ||
            minuteText is null ||
            !int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            return;
        }

        SetCurrentValue(SelectedDateTimeProperty, date.Date.AddHours(hour).AddMinutes(minute));
    }

    private void DateToggle_CheckedChanged(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsPopupOpenProperty, DateToggle.IsChecked == true);
    }

    private void CalendarPopup_Closed(object? sender, EventArgs e)
    {
        SetCurrentValue(IsPopupOpenProperty, false);
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e) => DateSelector.SelectedDate = DateTime.Today;
}

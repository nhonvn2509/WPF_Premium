using System.Globalization;
using System.Windows;
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
        HourPicker.ItemsSource = Enumerable.Range(0, 24).Select(hour => hour.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
        MinutePicker.ItemsSource = Enumerable.Range(0, 60).Select(minute => minute.ToString("D2", CultureInfo.InvariantCulture)).ToArray();
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

    public string SelectedDateText => (string)GetValue(SelectedDateTextProperty);

    public string SelectedDateTimeText => (string)GetValue(SelectedDateTimeTextProperty);

    private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DateTimeCalendar)d).UpdateControls();

    private void UpdateControls()
    {
        if (DateSelector is null)
        {
            return;
        }

        _updating = true;
        var value = SelectedDateTime;
        DateSelector.SelectedDate = value?.Date;
        HourPicker.SelectedItem = value?.Hour.ToString("D2", CultureInfo.InvariantCulture) ?? "00";
        MinutePicker.SelectedItem = value?.Minute.ToString("D2", CultureInfo.InvariantCulture) ?? "00";
        SetValue(SelectedDateTextPropertyKey, value?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "Chọn ngày");
        SetValue(SelectedDateTimeTextPropertyKey, value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) ?? "Chọn ngày giờ");
        _updating = false;
    }

    private void DateSelector_SelectedDateChanged(object? sender, EventArgs e)
    {
        if (!_updating && DateSelector.SelectedDate is DateTime date)
        {
            UpdateSelectedDateTime(date);
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
        if (HourPicker.SelectedItem is not string hourText ||
            MinutePicker.SelectedItem is not string minuteText ||
            !int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            return;
        }

        SetCurrentValue(SelectedDateTimeProperty, date.Date.AddHours(hour).AddMinutes(minute));
    }

    private void DateToggle_Click(object sender, RoutedEventArgs e)
    {
        CalendarPopup.IsOpen = DateToggle.IsChecked == true;
    }

    private void CalendarPopup_Closed(object? sender, EventArgs e)
    {
        DateToggle.IsChecked = false;
    }
}

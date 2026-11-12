using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum CalendarSelectionMode
{
    Single,
    Range
}

public sealed class CalendarControl : UserControl
{
    private readonly TextBlock _monthLabel = new()
    {
        FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly UniformGrid _weekdays = new() { Columns = 7 };
    private readonly UniformGrid _days = new() { Columns = 7, Rows = 6 };
    private readonly Button _previousMonthButton;
    private readonly Button _nextMonthButton;

    public static readonly DependencyProperty DisplayDateProperty =
        DependencyProperty.Register(nameof(DisplayDate), typeof(DateTime), typeof(CalendarControl),
            new FrameworkPropertyMetadata(DateTime.Today, OnDisplayDateChanged));

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(CalendarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(nameof(SelectionMode), typeof(CalendarSelectionMode), typeof(CalendarControl),
            new FrameworkPropertyMetadata(CalendarSelectionMode.Single, OnSelectionModeChanged));

    public static readonly DependencyProperty RangeStartDateProperty =
        DependencyProperty.Register(nameof(RangeStartDate), typeof(DateTime?), typeof(CalendarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty RangeEndDateProperty =
        DependencyProperty.Register(nameof(RangeEndDate), typeof(DateTime?), typeof(CalendarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty ShowNavigationButtonsProperty =
        DependencyProperty.Register(nameof(ShowNavigationButtons), typeof(bool), typeof(CalendarControl),
            new PropertyMetadata(true, OnShowNavigationButtonsChanged));

    public CalendarControl()
    {
        _previousMonthButton = CreateNavigationButton(PackIconMaterialKind.ChevronLeft, "Tháng trước", -1);
        _nextMonthButton = CreateNavigationButton(PackIconMaterialKind.ChevronRight, "Tháng sau", 1);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        header.Children.Add(_previousMonthButton);
        Grid.SetColumn(_monthLabel, 1);
        header.Children.Add(_monthLabel);
        Grid.SetColumn(_nextMonthButton, 2);
        header.Children.Add(_nextMonthButton);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(header);
        Grid.SetRow(_weekdays, 1);
        _weekdays.Margin = new Thickness(0, 0, 0, 4);
        layout.Children.Add(_weekdays);
        Grid.SetRow(_days, 2);
        layout.Children.Add(_days);

        var border = new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = layout
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;
        UpdateNavigationVisibility();
        UpdateCalendar();
    }

    public DateTime DisplayDate
    {
        get => (DateTime)GetValue(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public CalendarSelectionMode SelectionMode
    {
        get => (CalendarSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public DateTime? RangeStartDate
    {
        get => (DateTime?)GetValue(RangeStartDateProperty);
        set => SetValue(RangeStartDateProperty, value);
    }

    public DateTime? RangeEndDate
    {
        get => (DateTime?)GetValue(RangeEndDateProperty);
        set => SetValue(RangeEndDateProperty, value);
    }

    public bool ShowNavigationButtons
    {
        get => (bool)GetValue(ShowNavigationButtonsProperty);
        set => SetValue(ShowNavigationButtonsProperty, value);
    }

    public event EventHandler? SelectedDateChanged;
    public event EventHandler? RangeSelectionChanged;
    public event EventHandler? DisplayDateChanged;

    private Button CreateNavigationButton(PackIconMaterialKind icon, string label, int monthOffset)
    {
        var button = new Button
        {
            Content = new PackIconMaterial { Kind = icon, Width = 14, Height = 14 },
            Width = 30,
            Height = 30,
            MinHeight = 0,
            Padding = new Thickness(0),
            ToolTip = label
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonGhost");
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => SetCurrentValue(DisplayDateProperty, DisplayDate.AddMonths(monthOffset));
        return button;
    }

    private static void OnDisplayDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var calendar = (CalendarControl)d;
        calendar.UpdateCalendar();
        calendar.DisplayDateChanged?.Invoke(calendar, EventArgs.Empty);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var calendar = (CalendarControl)d;
        if (e.NewValue is DateTime selected && (selected.Year != calendar.DisplayDate.Year || selected.Month != calendar.DisplayDate.Month))
        {
            calendar.SetCurrentValue(DisplayDateProperty, selected);
        }
        calendar.UpdateCalendar();
        calendar.SelectedDateChanged?.Invoke(calendar, EventArgs.Empty);
    }

    private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CalendarControl)d).UpdateCalendar();

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CalendarControl)d).UpdateCalendar();

    private static void OnShowNavigationButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CalendarControl)d).UpdateNavigationVisibility();

    private void UpdateNavigationVisibility()
    {
        var visibility = ShowNavigationButtons ? Visibility.Visible : Visibility.Collapsed;
        _previousMonthButton.Visibility = visibility;
        _nextMonthButton.Visibility = visibility;
    }

    private void UpdateCalendar()
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var firstOfMonth = new DateTime(DisplayDate.Year, DisplayDate.Month, 1);
        _monthLabel.Text = firstOfMonth.ToString("MMMM yyyy", culture);

        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        _weekdays.Children.Clear();
        for (var day = 0; day < 7; day++)
        {
            var weekday = (DayOfWeek)(((int)firstDay + day) % 7);
            _weekdays.Children.Add(new TextBlock
            {
                Text = culture.DateTimeFormat.GetShortestDayName(weekday),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetBrush("MutedForegroundBrush"),
                FontSize = 11,
                Height = 22
            });
        }

        _days.Children.Clear();
        var offset = ((int)firstOfMonth.DayOfWeek - (int)firstDay + 7) % 7;
        var firstVisibleDate = firstOfMonth.AddDays(-offset);
        for (var index = 0; index < 42; index++)
        {
            var date = firstVisibleDate.AddDays(index);
            _days.Children.Add(CreateDayButton(date, firstOfMonth.Month));
        }
    }

    private Button CreateDayButton(DateTime date, int month)
    {
        var button = new Button
        {
            Content = date.Day.ToString(CultureInfo.CurrentCulture),
            Tag = date,
            MinWidth = 0,
            MinHeight = 0,
            Height = 30,
            Margin = new Thickness(1),
            Padding = new Thickness(0),
            Focusable = true
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "CalendarDayButton");
        AutomationProperties.SetName(button, date.ToString("D", CultureInfo.CurrentCulture));

        var isSelected = SelectionMode == CalendarSelectionMode.Single && SelectedDate?.Date == date.Date;
        var isRangeStart = SelectionMode == CalendarSelectionMode.Range && RangeStartDate?.Date == date.Date;
        var isRangeEnd = SelectionMode == CalendarSelectionMode.Range && RangeEndDate?.Date == date.Date;
        var rangeStart = RangeStartDate?.Date;
        var rangeEnd = RangeEndDate?.Date;
        if (rangeStart is DateTime start && rangeEnd is DateTime end && end < start)
        {
            (rangeStart, rangeEnd) = (end, start);
        }
        var isInRange = SelectionMode == CalendarSelectionMode.Range &&
                        rangeStart is DateTime from &&
                        (rangeEnd is DateTime to
                            ? date.Date >= from && date.Date <= to
                            : date.Date == from);

        if (isSelected || isRangeStart || isRangeEnd)
        {
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonPrimary");
        }
        else if (isInRange)
        {
            button.SetResourceReference(FrameworkElement.StyleProperty, "CalendarRangeDayButton");
        }
        else if (date.Month != month)
        {
            button.Foreground = GetBrush("MutedForegroundBrush");
        }

        if (date.Date == DateTime.Today)
        {
            button.BorderBrush = GetBrush("FocusBrush");
            button.BorderThickness = new Thickness(1);
        }

        button.Click += DayButton_Click;
        return button;
    }

    private void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateTime date })
        {
            return;
        }

        SetCurrentValue(DisplayDateProperty, date);
        if (SelectionMode == CalendarSelectionMode.Single)
        {
            SetCurrentValue(SelectedDateProperty, date);
            return;
        }

        if (RangeStartDate is null || RangeEndDate is not null)
        {
            SetCurrentValue(RangeStartDateProperty, date);
            SetCurrentValue(RangeEndDateProperty, null);
        }
        else if (date < RangeStartDate.Value)
        {
            SetCurrentValue(RangeStartDateProperty, date);
        }
        else
        {
            SetCurrentValue(RangeEndDateProperty, date);
        }

        RangeSelectionChanged?.Invoke(this, EventArgs.Empty);
        UpdateCalendar();
    }

    private System.Windows.Media.Brush GetBrush(string resourceKey) =>
        (System.Windows.Media.Brush)FindResource(resourceKey);
}

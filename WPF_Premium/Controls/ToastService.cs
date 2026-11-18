using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum ToastKind
{
    Default,
    Info,
    Success,
    Warning,
    Destructive
}

public static class ToastService
{
    private const double ToastWidth = 380;
    private const double ToastMargin = 24;
    private const double ToastGap = 10;
    private static readonly ConditionalWeakTable<Window, List<Window>> ActiveToastsByOwner = new();

    public static Window Show(Window owner, string message, ToastKind kind = ToastKind.Default, TimeSpan? duration = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (duration is TimeSpan interval && interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Toast duration must be greater than zero.");
        }

        var iconKind = kind switch
        {
            ToastKind.Success => PackIconMaterialKind.CheckCircleOutline,
            ToastKind.Warning => PackIconMaterialKind.AlertOutline,
            ToastKind.Destructive => PackIconMaterialKind.AlertCircleOutline,
            _ => PackIconMaterialKind.InformationOutline
        };
        var icon = new PackIconMaterial
        {
            Kind = iconKind,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 2, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        icon.SetResourceReference(Control.ForegroundProperty, kind switch
        {
            ToastKind.Success => "SuccessBrush",
            ToastKind.Warning => "WarningBrush",
            ToastKind.Destructive => "DestructiveBrush",
            ToastKind.Info => "TraceActiveBrush",
            _ => "MutedForegroundBrush"
        });

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        messageText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");

        var closeIcon = new PackIconMaterial
        {
            Kind = PackIconMaterialKind.Close,
            Width = 12,
            Height = 12
        };
        var closeButton = new Button
        {
            Content = closeIcon,
            Padding = new Thickness(4),
            MinHeight = 22,
            MinWidth = 22,
            Margin = new Thickness(8, -2, -4, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Đóng"
        };
        closeButton.SetResourceReference(FrameworkElement.StyleProperty, "ButtonGhost");

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.Children.Add(icon);
        Grid.SetColumn(messageText, 1);
        contentGrid.Children.Add(messageText);
        Grid.SetColumn(closeButton, 2);
        contentGrid.Children.Add(closeButton);

        var border = new Border
        {
            MaxWidth = ToastWidth,
            Child = contentGrid
        };
        border.SetResourceReference(FrameworkElement.StyleProperty, "Toast");
        border.SetResourceReference(Border.BorderBrushProperty, kind switch
        {
            ToastKind.Success => "SuccessBrush",
            ToastKind.Warning => "WarningBrush",
            ToastKind.Destructive => "DestructiveBrush",
            ToastKind.Info => "TraceActiveBrush",
            _ => "BorderBrush"
        });

        var toastList = ActiveToastsByOwner.GetOrCreateValue(owner);
        var position = CalculateToastPosition(owner, toastList);

        var toast = new Window
        {
            Owner = owner,
            Style = (Style)owner.FindResource("ToastWindow"),
            Content = border,
            Width = ToastWidth,
            Left = position.Left,
            Top = position.Top
        };

        closeButton.Click += (_, _) => toast.Close();
        toastList.Add(toast);
        toast.Show();

        var timer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toast.Close();
        };
        toast.Closed += (_, _) =>
        {
            timer.Stop();
            if (ActiveToastsByOwner.TryGetValue(owner, out var active))
            {
                active.Remove(toast);
                RepositionToasts(owner, active);
            }
        };
        timer.Start();
        return toast;
    }

    private static (double Left, double Top) CalculateToastPosition(Window owner, List<Window> existing)
    {
        var workArea = SystemParameters.WorkArea;
        var isMaximized = owner.WindowState == WindowState.Maximized;
        var ownerLeft = isMaximized ? workArea.Left : owner.Left;
        var ownerTop = isMaximized ? workArea.Top : owner.Top;
        var ownerWidth = isMaximized || owner.ActualWidth <= 0 ? workArea.Width : owner.ActualWidth;

        var left = Math.Clamp(ownerLeft + Math.Max(0, ownerWidth - ToastWidth - ToastMargin), workArea.Left + 8, Math.Max(workArea.Left + 8, workArea.Right - ToastWidth - 8));
        var top = Math.Max(workArea.Top + 8, ownerTop + ToastMargin);

        foreach (var active in existing)
        {
            var height = active.ActualHeight > 0 ? active.ActualHeight : 56;
            top += height + ToastGap;
        }

        if (top + 64 > workArea.Bottom)
        {
            top = Math.Max(workArea.Top + 8, workArea.Bottom - 72);
        }

        return (left, top);
    }

    private static void RepositionToasts(Window owner, List<Window> active)
    {
        var workArea = SystemParameters.WorkArea;
        var isMaximized = owner.WindowState == WindowState.Maximized;
        var top = Math.Max(workArea.Top + 8, (isMaximized ? workArea.Top : owner.Top) + ToastMargin);

        foreach (var toast in active)
        {
            toast.Top = top;
            var height = toast.ActualHeight > 0 ? toast.ActualHeight : 56;
            top += height + ToastGap;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum ToastKind
{
    Default,
    Success,
    Destructive
}

public static class ToastService
{
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
            ToastKind.Destructive => PackIconMaterialKind.AlertCircleOutline,
            _ => PackIconMaterialKind.InformationOutline
        };
        var icon = new PackIconMaterial
        {
            Kind = iconKind,
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 1, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        icon.SetResourceReference(Control.ForegroundProperty, kind switch
        {
            ToastKind.Success => "SuccessBrush",
            ToastKind.Destructive => "DestructiveBrush",
            _ => "MutedForegroundBrush"
        });
        var messageText = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        messageText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        var border = new Border
        {
            MaxWidth = 380,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { icon, messageText }
            }
        };
        border.SetResourceReference(FrameworkElement.StyleProperty, "Toast");
        border.SetResourceReference(Border.BorderBrushProperty, kind switch
        {
            ToastKind.Success => "SuccessBrush",
            ToastKind.Destructive => "DestructiveBrush",
            _ => "BorderBrush"
        });
        var toast = new Window
        {
            Owner = owner,
            Style = (Style)owner.FindResource("ToastWindow"),
            Content = border,
            Width = 380,
            Left = owner.Left + Math.Max(0, owner.ActualWidth - 404),
            Top = owner.Top + 24
        };
        toast.Show();

        var timer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toast.Close();
        };
        toast.Closed += (_, _) => timer.Stop();
        timer.Start();
        return toast;
    }
}

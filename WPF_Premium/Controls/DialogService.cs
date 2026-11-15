using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum OverlayDirection
{
    Left,
    Top,
    Right,
    Bottom
}

public static class DialogService
{
    public static bool? ShowDialog(Window owner, string title, object content) =>
        ShowOverlay(owner, title, content, OverlayKind.Dialog, OverlayDirection.Right);

    public static bool? ShowAlertDialog(
        Window owner,
        string title,
        object content,
        string confirmText = "Tiếp tục",
        string cancelText = "Hủy") =>
        ShowOverlay(owner, title, content, OverlayKind.AlertDialog, OverlayDirection.Right, confirmText, cancelText);

    public static bool? ShowDrawer(
        Window owner,
        string title,
        object content,
        OverlayDirection direction = OverlayDirection.Right) =>
        ShowOverlay(owner, title, content, OverlayKind.Drawer, direction);

    public static bool? ShowSheet(
        Window owner,
        string title,
        object content,
        OverlayDirection direction = OverlayDirection.Bottom) =>
        ShowOverlay(owner, title, content, OverlayKind.Sheet, direction);

    private static bool? ShowOverlay(
        Window owner,
        string title,
        object content,
        OverlayKind kind,
        OverlayDirection direction,
        string? confirmText = null,
        string? cancelText = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);
        if ((kind is OverlayKind.Drawer or OverlayKind.Sheet) &&
            !Enum.IsDefined(typeof(OverlayDirection), direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (!owner.IsVisible || owner.ActualWidth <= 0 || owner.ActualHeight <= 0)
        {
            throw new InvalidOperationException("The owner window must be visible before opening an overlay.");
        }

        var overlay = new Window
        {
            Owner = owner,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = owner.Left,
            Top = owner.Top,
            Width = owner.ActualWidth,
            Height = owner.ActualHeight
        };
        overlay.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");
        overlay.SetResourceReference(Control.FontFamilyProperty, "WpfPremium.FontFamily");
        overlay.SetResourceReference(Control.FontSizeProperty, "WpfPremium.FontSize");
        Action dismiss = kind == OverlayKind.AlertDialog
            ? () => overlay.DialogResult = false
            : overlay.Close;
        overlay.Content = CreateOverlayContent(owner, title, content, kind, direction, confirmText, cancelText, dismiss);

        var alertDialog = kind == OverlayKind.AlertDialog;
        overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (alertDialog)
                {
                    overlay.DialogResult = false;
                }
                else
                {
                    overlay.Close();
                }
            }
        };

        void SyncBounds(object? sender, EventArgs e)
        {
            overlay.Left = owner.Left;
            overlay.Top = owner.Top;
            overlay.Width = owner.ActualWidth;
            overlay.Height = owner.ActualHeight;
        }

        owner.LocationChanged += SyncBounds;
        owner.SizeChanged += SyncBounds;
        overlay.Closed += (_, _) =>
        {
            owner.LocationChanged -= SyncBounds;
            owner.SizeChanged -= SyncBounds;
        };

        return overlay.ShowDialog();
    }

    private static UIElement CreateOverlayContent(
        Window owner,
        string title,
        object content,
        OverlayKind kind,
        OverlayDirection direction,
        string? confirmText,
        string? cancelText,
        Action close)
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(112, 0, 0, 0)) };
        var isEdgeOverlay = kind is OverlayKind.Drawer or OverlayKind.Sheet;
        var isSideOverlay = direction is OverlayDirection.Left or OverlayDirection.Right;
        var cornerRadius = owner.TryFindResource("WpfPremium.SurfaceCornerRadius") is CornerRadius surfaceRadius
            ? surfaceRadius.TopLeft
            : 10;
        var panel = new Border
        {
            Width = !isEdgeOverlay ? 440 : isSideOverlay ? Math.Min(400, owner.ActualWidth) : double.NaN,
            Height = isEdgeOverlay && !isSideOverlay ? owner.ActualHeight * 0.9 : double.NaN,
            MaxWidth = isEdgeOverlay ? owner.ActualWidth : double.PositiveInfinity,
            MaxHeight = isEdgeOverlay ? owner.ActualHeight : Math.Max(160, owner.ActualHeight - 48),
            HorizontalAlignment = !isEdgeOverlay
                ? HorizontalAlignment.Center
                : isSideOverlay
                    ? direction == OverlayDirection.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right
                    : HorizontalAlignment.Stretch,
            VerticalAlignment = !isEdgeOverlay
                ? VerticalAlignment.Center
                : isSideOverlay
                    ? VerticalAlignment.Stretch
                    : direction == OverlayDirection.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom,
            CornerRadius = !isEdgeOverlay
                ? new CornerRadius(cornerRadius)
                : direction switch
                {
                    OverlayDirection.Left => new CornerRadius(0, cornerRadius, cornerRadius, 0),
                    OverlayDirection.Top => new CornerRadius(0, 0, cornerRadius, cornerRadius),
                    OverlayDirection.Right => new CornerRadius(cornerRadius, 0, 0, cornerRadius),
                    OverlayDirection.Bottom => new CornerRadius(cornerRadius, cornerRadius, 0, 0),
                    _ => new CornerRadius(cornerRadius)
                }
        };
        var styleKey = kind switch
        {
            OverlayKind.Drawer => "DrawerPanel",
            OverlayKind.Sheet => "SheetPanel",
            _ => "DialogPanel"
        };
        panel.SetResourceReference(FrameworkElement.StyleProperty, styleKey);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (kind == OverlayKind.AlertDialog)
        {
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var heading = new DockPanel { Margin = new Thickness(16, 12, 12, 8), LastChildFill = true };
        var closeButton = CreateButton(new PackIconMaterial
        {
            Kind = PackIconMaterialKind.Close,
            Width = 14,
            Height = 14
        }, "ButtonGhost");
        closeButton.Width = 30;
        closeButton.MinHeight = 30;
        closeButton.Padding = new Thickness(0);
        closeButton.ToolTip = "Đóng";
        closeButton.Click += (_, _) => close();
        AutomationProperties.SetName(closeButton, "Đóng");
        DockPanel.SetDock(closeButton, Dock.Right);
        heading.Children.Add(closeButton);
        heading.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        layout.Children.Add(heading);

        var body = new ContentControl
        {
            Content = content,
            Margin = new Thickness(16, 4, 16, kind == OverlayKind.AlertDialog ? 12 : 16),
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(body, 1);
        layout.Children.Add(body);

        if (kind == OverlayKind.AlertDialog)
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };
            var cancel = CreateButton(cancelText ?? "Hủy", "ButtonOutline");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => Window.GetWindow(panel)!.DialogResult = false;
            var confirm = CreateButton(confirmText ?? "Tiếp tục", "ButtonDestructive");
            confirm.Click += (_, _) => Window.GetWindow(panel)!.DialogResult = true;
            actions.Children.Add(cancel);
            actions.Children.Add(confirm);
            Grid.SetRow(actions, 2);
            layout.Children.Add(actions);
        }

        panel.Child = layout;
        root.Children.Add(panel);
        root.MouseDown += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, root))
            {
                close();
            }
        };
        return root;
    }

    private static Button CreateButton(object content, string styleKey)
    {
        var button = new Button { Content = content };
        button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        return button;
    }

    private enum OverlayKind
    {
        Dialog,
        AlertDialog,
        Drawer,
        Sheet
    }
}

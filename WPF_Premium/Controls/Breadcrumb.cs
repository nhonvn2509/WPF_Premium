using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class BreadcrumbItem
{
    public string Title { get; set; } = string.Empty;
    public object? Tag { get; set; }
    public bool IsCurrent { get; set; }

    public override string ToString() => Title;
}

/// <summary>
/// A clickable horizontal breadcrumb trail for hierarchical navigation.
/// </summary>
public sealed class Breadcrumb : UserControl
{
    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private INotifyCollectionChanged? _notifyingSource;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(Breadcrumb),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ItemClickCommandProperty =
        DependencyProperty.Register(
            nameof(ItemClickCommand),
            typeof(ICommand),
            typeof(Breadcrumb));

    public Breadcrumb()
    {
        AutomationProperties.SetName(this, "Đường dẫn điều hướng");
        Content = _panel;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? ItemClickCommand
    {
        get => (ICommand?)GetValue(ItemClickCommandProperty);
        set => SetValue(ItemClickCommandProperty, value);
    }

    public event EventHandler<object>? ItemClicked;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Breadcrumb)d;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged -= control.Source_CollectionChanged;
        }

        control._notifyingSource = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged += control.Source_CollectionChanged;
        }

        control.RebuildTrail();
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildTrail();

    private void RebuildTrail()
    {
        _panel.Children.Clear();
        var items = ItemsSource?.Cast<object>().ToList() ?? [];

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isLast = i == items.Count - 1;
            var isCurrent = item is BreadcrumbItem bi ? bi.IsCurrent || isLast : isLast;
            var label = item is BreadcrumbItem bItem ? bItem.Title : item?.ToString() ?? string.Empty;

            if (i > 0)
            {
                var separator = new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.ChevronRight,
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                separator.SetResourceReference(Control.ForegroundProperty, "MutedForegroundBrush");
                _panel.Children.Add(separator);
            }

            if (isCurrent)
            {
                var currentText = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(6, 4, 6, 4)
                };
                currentText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
                _panel.Children.Add(currentText);
            }
            else
            {
                var button = new Button
                {
                    Content = label,
                    Padding = new Thickness(6, 3, 6, 3),
                    MinHeight = 24
                };
                button.SetResourceReference(StyleProperty, "ButtonGhost");
                button.SetResourceReference(ForegroundProperty, "MutedForegroundBrush");
                button.Click += (_, _) =>
                {
                    ItemClicked?.Invoke(this, item!);
                    if (ItemClickCommand?.CanExecute(item) == true)
                    {
                        ItemClickCommand.Execute(item);
                    }
                };
                _panel.Children.Add(button);
            }
        }
    }
}

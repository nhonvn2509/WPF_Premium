using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class AttachmentControl : UserControl
{
    private readonly ListBox _filesList;

    public AttachmentControl()
    {
        Files = new ObservableCollection<string>();

        var addButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new PackIconMaterial { Kind = PackIconMaterialKind.Paperclip, Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0) },
                    new TextBlock { Text = "Thêm tệp đính kèm", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };
        addButton.SetResourceReference(StyleProperty, "ButtonOutline");
        addButton.Click += AddFiles_Click;

        var removeButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new PackIconMaterial { Kind = PackIconMaterialKind.TrashCanOutline, Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0) },
                    new TextBlock { Text = "Xóa mục đã chọn", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Margin = new Thickness(8, 0, 0, 0)
        };
        removeButton.SetResourceReference(StyleProperty, "ButtonGhost");
        removeButton.Click += RemoveSelected_Click;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { addButton, removeButton }
        };

        _filesList = new ListBox
        {
            ItemsSource = Files,
            MinHeight = 72,
            MaxHeight = 180,
            Margin = new Thickness(0, 12, 0, 0)
        };
        _filesList.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
        _filesList.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");

        var panel = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "Tệp đính kèm",
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                actions,
                _filesList
            }
        };

        var border = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = panel
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Content = border;
    }

    public ObservableCollection<string> Files { get; }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Chọn tệp đính kèm",
            Filter = "Tất cả tệp (*.*)|*.*"
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (var file in picker.FileNames)
        {
            if (!Files.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                Files.Add(file);
            }
        }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_filesList.SelectedItem is string file)
        {
            Files.Remove(file);
        }
    }
}

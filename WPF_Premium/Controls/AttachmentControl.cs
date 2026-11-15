using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
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
                    new PackIconMaterial { Kind = PackIconMaterialKind.Paperclip, Width = 12, Height = 12, Margin = new Thickness(0, 0, 5, 0) },
                    new TextBlock { Text = "Thêm tệp đính kèm", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Padding = new Thickness(8, 4, 8, 4),
            MinHeight = 26
        };
        addButton.SetResourceReference(StyleProperty, "ButtonOutline");
        AutomationProperties.SetAutomationId(addButton, "AttachmentAddButton");
        AutomationProperties.SetName(addButton, "Thêm tệp đính kèm");
        addButton.Click += AddFiles_Click;

        var removeButton = new Button
        {
            Content = new PackIconMaterial { Kind = PackIconMaterialKind.TrashCanOutline, Width = 12, Height = 12 }
        };
        removeButton.SetResourceReference(StyleProperty, "ButtonGhost");
        removeButton.Width = 26;
        removeButton.Height = 26;
        removeButton.Padding = new Thickness(0);
        removeButton.Margin = new Thickness(6, 0, 0, 0);
        removeButton.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetAutomationId(removeButton, "AttachmentRemoveButton");
        AutomationProperties.SetName(removeButton, "Xóa mục đã chọn");
        removeButton.ToolTip = "Xóa mục đã chọn";
        removeButton.Click += RemoveSelected_Click;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { addButton, removeButton }
        };

        _filesList = new ListBox
        {
            ItemsSource = Files,
            MinHeight = 64,
            MaxHeight = 150,
            Margin = new Thickness(0, 8, 0, 0)
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
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 6)
                },
                actions,
                _filesList
            }
        };

        var border = new Border
        {
            Padding = new Thickness(10),
            Child = panel
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        border.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.SurfaceBorderThickness");
        border.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.SurfaceCornerRadius");
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

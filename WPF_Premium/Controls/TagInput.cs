using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

/// <summary>
/// An interactive tag/chip input control allowing users to add and remove string tags.
/// </summary>
public sealed class TagInput : UserControl
{
    private readonly WrapPanel _wrapPanel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _input;
    private readonly Border _border;
    private INotifyCollectionChanged? _notifyingTags;

    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(
            nameof(Tags),
            typeof(ObservableCollection<string>),
            typeof(TagInput),
            new PropertyMetadata(null, OnTagsChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(TagInput),
            new PropertyMetadata("Nhập thẻ rồi nhấn Enter...", OnPlaceholderChanged));

    public static readonly DependencyProperty AllowDuplicatesProperty =
        DependencyProperty.Register(
            nameof(AllowDuplicates),
            typeof(bool),
            typeof(TagInput),
            new PropertyMetadata(false));

    public static readonly DependencyProperty MaxTagsProperty =
        DependencyProperty.Register(
            nameof(MaxTags),
            typeof(int),
            typeof(TagInput),
            new PropertyMetadata(0));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(TagInput),
            new PropertyMetadata(false, OnReadOnlyChanged));

    public TagInput()
    {
        AutomationProperties.SetName(this, "Trường nhập thẻ");

        _input = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            MinWidth = 120,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2)
        };
        _input.SetResourceReference(ForegroundProperty, "ForegroundBrush");
        _input.KeyDown += Input_KeyDown;

        SetValue(TagsProperty, new ObservableCollection<string>());

        _border = new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            Child = _wrapPanel
        };
        _border.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        _border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _border.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.ControlBorderThickness");
        _border.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.ControlCornerRadius");
        _border.SetResourceReference(MinHeightProperty, "WpfPremium.ControlMinHeight");
        _border.MouseLeftButtonDown += (_, _) => _input.Focus();

        IsKeyboardFocusWithinChanged += (_, _) =>
            _border.SetResourceReference(Border.BorderBrushProperty, IsKeyboardFocusWithin ? "FocusBrush" : "BorderBrush");

        Content = _border;
        RebuildTags();
    }

    public ObservableCollection<string> Tags
    {
        get => (ObservableCollection<string>)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool AllowDuplicates
    {
        get => (bool)GetValue(AllowDuplicatesProperty);
        set => SetValue(AllowDuplicatesProperty, value);
    }

    public int MaxTags
    {
        get => (int)GetValue(MaxTagsProperty);
        set => SetValue(MaxTagsProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public event EventHandler? TagsChanged;

    private static void OnTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TagInput)d;
        if (control._notifyingTags is not null)
        {
            control._notifyingTags.CollectionChanged -= control.Tags_CollectionChanged;
        }

        control._notifyingTags = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingTags is not null)
        {
            control._notifyingTags.CollectionChanged += control.Tags_CollectionChanged;
        }

        control.RebuildTags();
    }

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TagInput)d).UpdateInputState();

    private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((TagInput)d).RebuildTags();

    private void Tags_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTags();
        TagsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryAddTag(string rawTag)
    {
        if (IsReadOnly || Tags is null)
        {
            return false;
        }

        var tag = rawTag?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        if (MaxTags > 0 && Tags.Count >= MaxTags)
        {
            return false;
        }

        if (!AllowDuplicates && Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Tags.Add(tag);
        return true;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.OemComma)
        {
            if (TryAddTag(_input.Text))
            {
                _input.Clear();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Back && string.IsNullOrEmpty(_input.Text) && Tags?.Count > 0)
        {
            Tags.RemoveAt(Tags.Count - 1);
            e.Handled = true;
        }
    }

    private void RebuildTags()
    {
        _wrapPanel.Children.Clear();
        if (Tags is not null)
        {
            foreach (var tag in Tags)
            {
                _wrapPanel.Children.Add(CreateChip(tag));
            }
        }

        if (!IsReadOnly)
        {
            _wrapPanel.Children.Add(_input);
        }

        UpdateInputState();
    }

    private void UpdateInputState()
    {
        _input.ToolTip = Placeholder;
        _input.Visibility = IsReadOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    private Border CreateChip(string tag)
    {
        var text = new TextBlock
        {
            Text = tag,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryForegroundBrush");

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { text }
        };

        if (!IsReadOnly)
        {
            var removeButton = new Button
            {
                Content = new PackIconMaterial { Kind = PackIconMaterialKind.Close, Width = 10, Height = 10 },
                Padding = new Thickness(3, 1, 3, 1),
                Margin = new Thickness(4, 0, -2, 0),
                MinHeight = 16,
                MinWidth = 16,
                ToolTip = $"Xóa {tag}"
            };
            removeButton.SetResourceReference(StyleProperty, "ButtonGhost");
            AutomationProperties.SetName(removeButton, $"Xóa {tag}");
            removeButton.Click += (_, _) => Tags?.Remove(tag);
            content.Children.Add(removeButton);
        }

        var chip = new Border
        {
            Padding = new Thickness(8, 2, 6, 2),
            Margin = new Thickness(2),
            Child = content
        };
        chip.SetResourceReference(Border.BackgroundProperty, "SecondaryBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        chip.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.ControlBorderThickness");
        chip.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.ControlCornerRadius");
        return chip;
    }
}

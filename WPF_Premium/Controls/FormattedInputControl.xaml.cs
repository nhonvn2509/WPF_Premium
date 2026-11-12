using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF_Premium.Controls;

/// <summary>Text input with a formatting mask. Mask tokens: # digit, A letter, * letter or digit.</summary>
public partial class FormattedInputControl : UserControl
{
    private bool _formatting;

    public static readonly DependencyProperty MaskProperty = DependencyProperty.Register(
        nameof(Mask), typeof(string), typeof(FormattedInputControl), new PropertyMetadata(string.Empty, OnMaskChanged));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(FormattedInputControl), new PropertyMetadata(string.Empty, OnPlaceholderChanged));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(FormattedInputControl), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public FormattedInputControl() => InitializeComponent();

    /// <summary># accepts a digit, A accepts a letter, and * accepts a letter or digit. Other characters are literals.</summary>
    public string Mask { get => (string)GetValue(MaskProperty); set => SetValue(MaskProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    /// <summary>Unformatted characters accepted by the mask.</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FormattedInputControl)d).RefreshFromValue();
    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FormattedInputControl)d;
        control.PlaceholderText.Text = (string)e.NewValue;
        control.UpdatePlaceholder();
    }
    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FormattedInputControl)d).RefreshFromValue();

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_formatting) return;
        var caret = PART_Input.CaretIndex;
        var raw = ExtractRaw(PART_Input.Text);
        var formatted = Format(raw);
        _formatting = true;
        PART_Input.Text = formatted;
        PART_Input.CaretIndex = Math.Min(formatted.Length, caret + Math.Max(0, formatted.Length - raw.Length));
        SetCurrentValue(ValueProperty, raw);
        _formatting = false;
        UpdatePlaceholder();
    }

    private void Input_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Mask.Length == 0) return;
        var raw = ExtractRaw(PART_Input.Text);
        var insert = ExtractRaw(e.Text);
        if (insert.Length == 0) { e.Handled = true; return; }
        var start = ExtractRaw(PART_Input.Text[..Math.Min(PART_Input.SelectionStart, PART_Input.Text.Length)]).Length;
        var selected = ExtractRaw(PART_Input.SelectedText).Length;
        var capacity = Mask.Count(IsToken);
        raw = raw.Remove(Math.Min(start, raw.Length), Math.Min(selected, Math.Max(0, raw.Length - start)));
        var added = 0;
        foreach (var c in insert)
        {
            if (raw.Length + added >= capacity) break;
            raw = raw.Insert(Math.Min(start + added, raw.Length), c.ToString());
            added++;
        }
        var next = Format(raw);
        _formatting = true;
        PART_Input.Text = next;
        PART_Input.CaretIndex = Math.Min(next.Length, start + added + LiteralCountBefore(start + added));
        _formatting = false;
        SetCurrentValue(ValueProperty, ExtractRaw(next));
        UpdatePlaceholder();
        e.Handled = true;
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Back or Key.Delete) || Mask.Length == 0 || PART_Input.SelectionLength > 0) return;
        var caret = PART_Input.CaretIndex;
        var raw = ExtractRaw(PART_Input.Text);
        var rawCaret = ExtractRaw(PART_Input.Text[..Math.Min(caret, PART_Input.Text.Length)]).Length;
        var index = e.Key == Key.Back ? rawCaret - 1 : rawCaret;
        if (index < 0 || index >= raw.Length) { e.Handled = true; return; }
        raw = raw.Remove(index, 1);
        var formatted = Format(raw);
        _formatting = true;
        PART_Input.Text = formatted;
        var newRawCaret = Math.Max(0, rawCaret - (e.Key == Key.Back ? 1 : 0));
        PART_Input.CaretIndex = Math.Min(formatted.Length, newRawCaret + LiteralCountBefore(newRawCaret));
        _formatting = false;
        SetCurrentValue(ValueProperty, raw);
        UpdatePlaceholder();
        e.Handled = true;
    }

    private string ExtractRaw(string text)
    {
        if (Mask.Length == 0) return text;
        var result = new List<char>(); var input = 0;
        foreach (var token in Mask)
        {
            if (!IsToken(token))
            {
                if (input < text.Length && text[input] == token) input++;
                continue;
            }
            while (input < text.Length && !Accepts(token, text[input])) input++;
            if (input >= text.Length) break;
            result.Add(text[input++]);
        }
        return new string(result.ToArray());
    }

    private string Format(string raw)
    {
        if (Mask.Length == 0) return raw;
        var output = new System.Text.StringBuilder(); var index = 0;
        foreach (var token in Mask)
        {
            if (!IsToken(token)) { if (index < raw.Length) output.Append(token); continue; }
            while (index < raw.Length && !Accepts(token, raw[index])) index++;
            if (index >= raw.Length) break;
            output.Append(raw[index++]);
        }
        return output.ToString();
    }

    private int LiteralCountBefore(int tokenCount)
    {
        var tokens = 0; var literals = 0;
        foreach (var c in Mask)
        {
            if (IsToken(c)) { if (tokens >= tokenCount) break; tokens++; }
            else if (tokens <= tokenCount) literals++;
        }
        return literals;
    }
    private static bool IsToken(char c) => c is '#' or 'A' or '*';
    private static bool Accepts(char token, char value) => token switch { '#' => char.IsDigit(value), 'A' => char.IsLetter(value), '*' => char.IsLetterOrDigit(value), _ => false };
    private void RefreshFromValue()
    {
        if (PART_Input is null) return;
        var formatted = Format(Value ?? string.Empty);
        _formatting = true; PART_Input.Text = formatted; PART_Input.CaretIndex = formatted.Length; _formatting = false;
        var raw = ExtractRaw(formatted);
        if (raw != Value) SetCurrentValue(ValueProperty, raw);
        UpdatePlaceholder();
    }
    private void UpdatePlaceholder() => PlaceholderText.Visibility = string.IsNullOrEmpty(PART_Input?.Text) ? Visibility.Visible : Visibility.Collapsed;
}

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace WPF_Premium.Controls;

public partial class NotepadTextViewer : UserControl
{
    private const double BaseFontSize = 13d;

    public NotepadTextViewer()
    {
        InitializeComponent();
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.EnableHyperlinks = false;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatusBar();
        Editor.TextArea.SelectionChanged += (_, _) => UpdateStatusBar();
        Editor.TextChanged += (_, _) => UpdateStatusBar();
        ApplyEditorAppearance();
        UpdateStatusBar();
    }

    public string Text
    {
        get => Editor.Text;
        set => SetContent(value);
    }

    public bool WordWrap
    {
        get => Editor.WordWrap;
        set
        {
            Editor.WordWrap = value;
            WordWrapButton.IsChecked = value;
        }
    }

    public void SetContent(string? content, string? filePath = null, string? extension = null, string? encodingLabel = null)
    {
        var text = content ?? string.Empty;
        Editor.Text = text;
        Editor.ScrollToHome();
        ApplySyntaxHighlighting(extension ?? Path.GetExtension(filePath ?? string.Empty));
        EncodingStatusLabel.Text = string.IsNullOrWhiteSpace(encodingLabel) ? "UTF-8" : encodingLabel;
        EolStatusLabel.Text = text.Contains("\r\n", StringComparison.Ordinal)
            ? "Windows (CR LF)"
            : text.Contains('\n') ? "Unix (LF)" : "Windows (CR LF)";
        UpdateStatusBar();
    }

    public void CopySelectedOrAll()
    {
        var selected = Editor.SelectedText;
        Clipboard.SetText(string.IsNullOrEmpty(selected) ? Editor.Text : selected);
    }

    public bool FindNext(string query, bool backwards = false)
    {
        if (string.IsNullOrEmpty(query))
        {
            FindStatusText.Text = string.Empty;
            return false;
        }

        var text = Editor.Text;
        if (text.Length == 0)
        {
            FindStatusText.Text = "Không tìm thấy";
            return false;
        }

        var options = RegexOptions.Multiline;
        if (MatchCaseButton.IsChecked != true)
        {
            options |= RegexOptions.IgnoreCase;
        }

        var pattern = RegexButton.IsChecked == true ? query : Regex.Escape(query);
        if (WholeWordButton.IsChecked == true)
        {
            pattern = $@"\b(?:{pattern})\b";
        }

        MatchCollection matches;
        try
        {
            matches = Regex.Matches(text, pattern, options, TimeSpan.FromSeconds(1));
        }
        catch
        {
            FindStatusText.Text = "Regex không hợp lệ";
            return false;
        }

        if (matches.Count == 0)
        {
            FindStatusText.Text = "Không tìm thấy";
            return false;
        }

        var caret = Editor.CaretOffset;
        Match? target = null;
        var targetIndex = 0;

        if (!backwards)
        {
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i].Index >= caret)
                {
                    target = matches[i];
                    targetIndex = i + 1;
                    break;
                }
            }

            if (target is null)
            {
                target = matches[0];
                targetIndex = 1;
            }
        }
        else
        {
            var anchor = Math.Max(0, Editor.SelectionStart - 1);
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                if (matches[i].Index <= anchor)
                {
                    target = matches[i];
                    targetIndex = i + 1;
                    break;
                }
            }

            if (target is null)
            {
                target = matches[^1];
                targetIndex = matches.Count;
            }
        }

        Editor.Select(target.Index, target.Length);
        Editor.CaretOffset = backwards ? target.Index : target.Index + target.Length;
        var location = Editor.Document.GetLocation(target.Index);
        Editor.ScrollTo(location.Line, location.Column);
        FindStatusText.Text = $"{targetIndex} / {matches.Count}";
        return true;
    }

    private void ApplySyntaxHighlighting(string? extension)
    {
        var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && !ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        var mappedExt = ext switch
        {
            ".hdr" or ".config" or ".csproj" or ".slnx" or ".xaml" or ".svg" or ".manifest" => ".xml",
            ".jsonl" or ".json" => ".js",
            ".ini" or ".cfg" or ".conf" or ".log" => ".ini",
            _ => ext
        };

        var definition = string.IsNullOrEmpty(mappedExt)
            ? null
            : HighlightingManager.Instance.GetDefinitionByExtension(mappedExt);
        Editor.SyntaxHighlighting = definition;
        SyntaxLabel.Text = definition is not null ? $"{definition.Name} ({ext})" : "Văn bản thuần";
    }

    private void ApplyEditorAppearance()
    {
        Editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x26, 0x7F, 0xFF));
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(0x1E, 0x4A, 0x90, 0xE2));
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(0x38, 0x4A, 0x90, 0xE2)), 1);
    }

    private void UpdateStatusBar()
    {
        var doc = Editor.Document;
        var caret = Editor.TextArea.Caret;
        LengthLinesLabel.Text = $"Độ dài : {doc?.TextLength ?? 0:N0}   Tổng dòng : {doc?.LineCount ?? 1:N0}";
        CaretLabel.Text = $"Dòng : {caret.Line}   Cột : {caret.Column}   Chọn : {Editor.SelectionLength}";
        ZoomStatusLabel.Text = $"{Math.Round(Editor.FontSize / BaseFontSize * 100)}%";
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!string.IsNullOrEmpty(Editor.SelectedText) && !Editor.SelectedText.Contains('\n'))
            {
                FindInputBox.Text = Editor.SelectedText;
            }
            FindInputBox.Focus();
            FindInputBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ToggleGoToLine(true);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindNext(FindInputBox.Text, backwards: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && GoToLinePanel.Visibility == Visibility.Visible)
        {
            ToggleGoToLine(false);
            Editor.Focus();
            e.Handled = true;
        }
    }

    private void ToggleGoToLine(bool open)
    {
        GoToLineToggleButton.IsChecked = open;
        GoToLinePanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open)
        {
            GoToLineBox.Text = Editor.TextArea.Caret.Line.ToString();
            GoToLineBox.Focus();
            GoToLineBox.SelectAll();
        }
    }

    private void WordWrapButton_Click(object sender, RoutedEventArgs e) =>
        Editor.WordWrap = WordWrapButton.IsChecked == true;

    private void ShowSymbolsButton_Click(object sender, RoutedEventArgs e)
    {
        var show = ShowSymbolsButton.IsChecked == true;
        Editor.Options.ShowSpaces = show;
        Editor.Options.ShowTabs = show;
        Editor.Options.ShowEndOfLine = show;
    }

    private void GoToLineButton_Click(object sender, RoutedEventArgs e) =>
        ToggleGoToLine(GoToLineToggleButton.IsChecked == true);

    private void GoToLineBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            JumpToEnteredLine();
            e.Handled = true;
        }
    }

    private void GoToLineConfirm_Click(object sender, RoutedEventArgs e) => JumpToEnteredLine();

    private void JumpToEnteredLine()
    {
        if (int.TryParse(GoToLineBox.Text.Trim(), out var line) && Editor.Document is { } doc)
        {
            var clamped = Math.Clamp(line, 1, doc.LineCount);
            Editor.TextArea.Caret.Line = clamped;
            Editor.TextArea.Caret.Column = 1;
            Editor.ScrollToLine(clamped);
            Editor.Focus();
        }
    }

    private void FindInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(FindInputBox.Text))
        {
            FindStatusText.Text = string.Empty;
        }
    }

    private void FindInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNext(FindInputBox.Text, backwards: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    private void FindNextButton_Click(object sender, RoutedEventArgs e) => FindNext(FindInputBox.Text, backwards: false);
    private void FindPrevButton_Click(object sender, RoutedEventArgs e) => FindNext(FindInputBox.Text, backwards: true);
    private void SearchOption_Changed(object sender, RoutedEventArgs e) => FindNext(FindInputBox.Text, backwards: false);

    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => AdjustZoom(1);
    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => AdjustZoom(-1);

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            AdjustZoom(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private void AdjustZoom(int delta)
    {
        Editor.FontSize = Math.Clamp(Editor.FontSize + delta, 8d, 36d);
        UpdateStatusBar();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopySelectedOrAll();
}

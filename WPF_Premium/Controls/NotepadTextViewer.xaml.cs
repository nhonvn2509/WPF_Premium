using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace WPF_Premium.Controls;

public partial class NotepadTextViewer : UserControl
{
    private const double BaseFontSize = 13d;
    private const int MaxEncodingReloadBytes = 2 * 1024 * 1024;

    private string? _currentFilePath;
    private string? _currentExtension;
    private string _activeEncodingKey = "UTF-8";
    private ContextMenu? _encodingMenu;

    static NotepadTextViewer()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

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
        _currentFilePath = filePath;
        _currentExtension = extension ?? Path.GetExtension(filePath ?? string.Empty);
        var text = content ?? string.Empty;
        Editor.Text = text;
        Editor.ScrollToHome();
        ApplySyntaxHighlighting(_currentExtension);
        _activeEncodingKey = string.IsNullOrWhiteSpace(encodingLabel) ? DetectBomEncodingKey(filePath) : encodingLabel;
        EncodingStatusLabel.Text = _activeEncodingKey;
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
        if (definition is not null && IsDarkThemeActive())
        {
            foreach (var color in definition.NamedHighlightingColors)
            {
                if (color.Foreground?.GetColor(null) is { } fg)
                {
                    var lum = (0.299 * fg.R + 0.587 * fg.G + 0.114 * fg.B) / 255.0;
                    if (lum < 0.52)
                    {
                        var r = (byte)Math.Min(255, fg.R + (255 - fg.R) * 0.55);
                        var g = (byte)Math.Min(255, fg.G + (255 - fg.G) * 0.55);
                        var b = (byte)Math.Min(255, fg.B + (255 - fg.B) * 0.55);
                        color.Foreground = new SimpleHighlightingBrush(Color.FromArgb(fg.A, r, g, b));
                    }
                }
            }
        }
        Editor.SyntaxHighlighting = definition;
        var bareExt = ext.TrimStart('.');
        SyntaxLabel.Text = definition is not null ? $"{definition.Name} ({bareExt})" : "Văn bản thuần";
    }

    private static bool IsDarkThemeActive()
    {
        if (Application.Current?.TryFindResource("BackgroundBrush") is SolidColorBrush bg)
        {
            var lum = (0.299 * bg.Color.R + 0.587 * bg.Color.G + 0.114 * bg.Color.B) / 255.0;
            return lum < 0.45;
        }
        return false;
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

    private void EncodingMenuButton_Click(object sender, RoutedEventArgs e)
    {
        _encodingMenu = BuildEncodingMenu();
        _encodingMenu.PlacementTarget = EncodingMenuButton;
        _encodingMenu.Placement = PlacementMode.Bottom;
        _encodingMenu.IsOpen = true;
    }

    private ContextMenu BuildEncodingMenu()
    {
        var menu = new ContextMenu();
        if (TryFindResource("DropdownMenu") is Style dropdownStyle)
        {
            menu.Style = dropdownStyle;
        }

        MenuItem MakeViewItem(string header, string key, Func<Encoding> encodingFactory, bool stripBom = false)
        {
            var item = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = string.Equals(_activeEncodingKey, key, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => ApplyViewEncoding(key, encodingFactory(), stripBom);
            return item;
        }

        MenuItem MakeConvertItem(string header, string key, Func<Encoding> encodingFactory)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => ConvertDisplayedTextToEncoding(key, encodingFactory());
            return item;
        }

        menu.Items.Add(MakeViewItem("ANSI", "ANSI", () => Encoding.GetEncoding(1252)));
        menu.Items.Add(MakeViewItem("UTF-8", "UTF-8", () => new UTF8Encoding(false)));
        menu.Items.Add(MakeViewItem("UTF-8-BOM", "UTF-8-BOM", () => new UTF8Encoding(true), stripBom: true));
        menu.Items.Add(MakeViewItem("UTF-16 BE BOM", "UTF-16 BE BOM", () => Encoding.BigEndianUnicode, stripBom: true));
        menu.Items.Add(MakeViewItem("UTF-16 LE BOM", "UTF-16 LE BOM", () => Encoding.Unicode, stripBom: true));

        var charSetsMenu = new MenuItem { Header = "Tập ký tự" };
        foreach (var (groupName, entries) in GetCharacterSetGroups())
        {
            var groupItem = new MenuItem { Header = groupName };
            foreach (var (label, codePage) in entries)
            {
                var cp = codePage;
                groupItem.Items.Add(MakeViewItem(label, label, () => Encoding.GetEncoding(cp)));
            }
            charSetsMenu.Items.Add(groupItem);
        }
        menu.Items.Add(charSetsMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeConvertItem("Chuyển đổi sang ANSI", "ANSI", () => Encoding.GetEncoding(1252)));
        menu.Items.Add(MakeConvertItem("Chuyển đổi sang UTF-8", "UTF-8", () => new UTF8Encoding(false)));
        menu.Items.Add(MakeConvertItem("Chuyển đổi sang UTF-8-BOM", "UTF-8-BOM", () => new UTF8Encoding(true)));
        menu.Items.Add(MakeConvertItem("Chuyển đổi sang UTF-16 BE BOM", "UTF-16 BE BOM", () => Encoding.BigEndianUnicode));
        menu.Items.Add(MakeConvertItem("Chuyển đổi sang UTF-16 LE BOM", "UTF-16 LE BOM", () => Encoding.Unicode));

        return menu;
    }

    private static (string Group, (string Label, int CodePage)[] Items)[] GetCharacterSetGroups() =>
    [
        ("Ả Rập", [("ISO 8859-6", 28596), ("OEM 720", 720), ("Windows-1256", 1256)]),
        ("Ban-tích", [("ISO 8859-4", 28594), ("ISO 8859-13", 28603), ("OEM 775", 775), ("Windows-1257", 1257)]),
        ("Xen-tơ", [("ISO 8859-14", 28604)]),
        ("Ki-rin", [("ISO 8859-5", 28595), ("KOI8-R", 20866), ("KOI8-U", 21866), ("Macintosh", 10007), ("OEM 855", 855), ("OEM 866", 866), ("Windows-1251", 1251)]),
        ("Trung Âu", [("ISO 8859-2", 28592), ("OEM 852", 852), ("Windows-1250", 1250)]),
        ("Hán", [("Big5 (Phồn thể)", 950), ("GB2312 (Giản thể)", 936)]),
        ("Đông Âu", [("ISO 8859-2", 28592)]),
        ("Hy Lạp", [("ISO 8859-7", 28597), ("OEM 737", 737), ("OEM 869", 869), ("Windows-1253", 1253)]),
        ("Do Thái", [("ISO 8859-8", 28598), ("OEM 862", 862), ("Windows-1255", 1255)]),
        ("Nhật", [("Shift-JIS", 932)]),
        ("Hàn", [("EUC-KR", 51949), ("Windows 949", 949)]),
        ("Bắc Âu", [("OEM 865", 865), ("OEM 861", 861)]),
        ("Thái", [("TIS-620 (Windows-874)", 874)]),
        ("Thổ Nhĩ Kỳ", [("ISO 8859-3", 28593), ("ISO 8859-9", 28599), ("OEM 857", 857), ("Windows-1254", 1254)]),
        ("Tây Âu", [("ISO 8859-1", 28591), ("ISO 8859-15", 28605), ("OEM 850", 850), ("OEM 858", 858), ("OEM 860", 860), ("OEM 863", 863), ("Windows-1252", 1252)]),
        ("Việt", [("Windows-1258", 1258)])
    ];

    private void ApplyViewEncoding(string encodingKey, Encoding encoding, bool stripBom)
    {
        string decoded;
        if (!string.IsNullOrWhiteSpace(_currentFilePath) && File.Exists(_currentFilePath))
        {
            var bytes = ReadSampleBytes(_currentFilePath);
            decoded = DecodeBytes(bytes, encoding, stripBom);
        }
        else
        {
            var currentEnc = ResolveEncodingByKey(_activeEncodingKey);
            var bytes = currentEnc.GetBytes(Editor.Text);
            decoded = DecodeBytes(bytes, encoding, stripBom);
        }

        if (decoded.Contains('\0'))
        {
            decoded = decoded.Replace('\0', ' ');
        }

        Editor.Text = decoded;
        Editor.ScrollToHome();
        _activeEncodingKey = encodingKey;
        EncodingStatusLabel.Text = encodingKey;
        UpdateStatusBar();
    }

    private void ConvertDisplayedTextToEncoding(string targetKey, Encoding targetEncoding)
    {
        var bytes = targetEncoding.GetBytes(Editor.Text);
        var converted = targetEncoding.GetString(bytes);
        if (converted.Contains('\0'))
        {
            converted = converted.Replace('\0', ' ');
        }

        Editor.Text = converted;
        _activeEncodingKey = targetKey;
        EncodingStatusLabel.Text = targetKey;
        UpdateStatusBar();
    }

    private static byte[] ReadSampleBytes(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var len = (int)Math.Min(MaxEncodingReloadBytes, fs.Length);
        var buffer = new byte[len];
        var offset = 0;
        while (offset < len)
        {
            var read = fs.Read(buffer, offset, len - offset);
            if (read == 0) break;
            offset += read;
        }
        return offset == buffer.Length ? buffer : buffer[..offset];
    }

    private static string DecodeBytes(byte[] bytes, Encoding encoding, bool stripBom)
    {
        var offset = 0;
        if (stripBom)
        {
            var preamble = encoding.GetPreamble();
            if (preamble.Length > 0 && bytes.Length >= preamble.Length && bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble))
            {
                offset = preamble.Length;
            }
        }
        return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    private static string DetectBomEncodingKey(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return "UTF-8";
        try
        {
            Span<byte> bom = stackalloc byte[3];
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var read = fs.Read(bom);
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return "UTF-8-BOM";
            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return "UTF-16 LE BOM";
            if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return "UTF-16 BE BOM";
        }
        catch
        {
        }
        return "UTF-8";
    }

    private static Encoding ResolveEncodingByKey(string key) => key switch
    {
        "ANSI" => Encoding.GetEncoding(1252),
        "UTF-8-BOM" => new UTF8Encoding(true),
        "UTF-16 BE BOM" => Encoding.BigEndianUnicode,
        "UTF-16 LE BOM" => Encoding.Unicode,
        _ => new UTF8Encoding(false)
    };
}

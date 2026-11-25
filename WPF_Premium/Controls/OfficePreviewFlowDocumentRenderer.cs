using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using DocTable = System.Windows.Documents.Table;

namespace WPF_Premium.Controls;

/// <summary>
/// Renders Office documents natively into a WPF <see cref="FlowDocument"/> when WebView2 runtime is unavailable.
/// </summary>
internal static class OfficePreviewFlowDocumentRenderer
{
    private const int MaxSpreadsheetRows = 1000;
    private const int MaxSpreadsheetColumns = 80;
    private const int MaxSpreadsheetSheets = 20;
    private const long MaxXmlEntrySize = 32 * 1024 * 1024;
    private const long MaxImageEntrySize = 10 * 1024 * 1024;

    private static readonly Brush PaperBackground = Brushes.White;
    private static readonly Brush PaperForeground = new SolidColorBrush(Color.FromRgb(0x20, 0x21, 0x24));
    private static readonly Brush MutedForeground = new SolidColorBrush(Color.FromRgb(0x62, 0x67, 0x6E));
    private static readonly Brush CellBorderBrush = new SolidColorBrush(Color.FromRgb(0xAE, 0xB5, 0xBD));
    private static readonly Brush HeaderCellBackground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF3, 0xF5));

    public static FlowDocument Render(string path)
    {
        var doc = new FlowDocument
        {
            Background = PaperBackground,
            Foreground = PaperForeground,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 14,
            PagePadding = new Thickness(32),
            ColumnWidth = double.PositiveInfinity
        };

        var blocks = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" or ".docm" => RenderWord(path),
            ".xlsx" or ".xlsm" => RenderSpreadsheet(path),
            ".pptx" => RenderPresentation(path),
            ".odt" or ".ott" => RenderOpenDocumentText(path),
            ".ods" => RenderOpenDocumentSpreadsheet(path),
            ".odp" => RenderOpenDocumentPresentation(path),
            ".doc" => RenderLegacyWord(path),
            ".xls" => RenderLegacySpreadsheet(path),
            ".ppt" => RenderLegacyPresentation(path),
            _ => throw new NotSupportedException("Định dạng Office chưa được hỗ trợ.")
        };

        foreach (var block in blocks)
        {
            doc.Blocks.Add(block);
        }

        return doc;
    }

    private static IEnumerable<Block> RenderWord(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = LoadXml(archive, "word/document.xml");
        var body = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "body")
            ?? throw new InvalidDataException("Không tìm thấy nội dung Word.");
        var relationships = LoadRelationships(archive, "word/_rels/document.xml.rels", "word");
        var blocks = new List<Block>();

        foreach (var element in body.Elements())
        {
            if (element.Name.LocalName == "p")
            {
                blocks.Add(RenderWordParagraph(element, archive, relationships));
            }
            else if (element.Name.LocalName == "tbl")
            {
                blocks.Add(RenderWordTable(element, archive, relationships));
            }
        }

        return blocks;
    }

    private static Paragraph RenderWordParagraph(XElement paragraph, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 };
        var properties = paragraph.Elements().FirstOrDefault(e => e.Name.LocalName == "pPr");
        var alignment = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "jc")?.Attribute("val")?.Value;
        p.TextAlignment = alignment switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            "both" => TextAlignment.Justify,
            _ => TextAlignment.Left
        };

        var indentation = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "ind");
        if (double.TryParse(indentation?.Attribute("left")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left) && left > 0)
        {
            p.Margin = new Thickness(left / 20d * 96d / 72d, 0, 0, 8);
        }

        if (double.TryParse(indentation?.Attribute("firstLine")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstLine) && firstLine > 0)
        {
            p.TextIndent = firstLine / 20d * 96d / 72d;
        }

        var styleName = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "pStyle")?.Attribute("val")?.Value ?? "";
        if (styleName.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            p.FontSize = 18;
            p.FontWeight = FontWeights.SemiBold;
            p.Margin = new Thickness(p.Margin.Left, 10, 0, 10);
        }

        foreach (var child in paragraph.Elements().Where(e => e.Name.LocalName != "pPr"))
        {
            AppendWordInline(p.Inlines, child, archive, relationships);
        }

        return p;
    }

    private static void AppendWordInline(InlineCollection inlines, XElement element, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        switch (element.Name.LocalName)
        {
            case "r":
                var runProperties = element.Elements().FirstOrDefault(c => c.Name.LocalName == "rPr");
                var span = new Span();
                if (runProperties?.Elements().Any(c => c.Name.LocalName == "b") == true)
                {
                    span.FontWeight = FontWeights.Bold;
                }

                if (runProperties?.Elements().Any(c => c.Name.LocalName == "i") == true)
                {
                    span.FontStyle = FontStyles.Italic;
                }

                if (runProperties?.Elements().Any(c => c.Name.LocalName == "u") == true)
                {
                    span.TextDecorations = TextDecorations.Underline;
                }

                var color = runProperties?.Elements().FirstOrDefault(c => c.Name.LocalName == "color")?.Attribute("val")?.Value;
                if (TryParseHexBrush(color, out var brush))
                {
                    span.Foreground = brush;
                }

                var size = runProperties?.Elements().FirstOrDefault(c => c.Name.LocalName == "sz")?.Attribute("val")?.Value;
                if (double.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var halfPoints) && halfPoints > 0)
                {
                    span.FontSize = halfPoints / 2d * 96d / 72d;
                }

                foreach (var child in element.Elements().Where(c => c.Name.LocalName != "rPr"))
                {
                    AppendWordInline(span.Inlines, child, archive, relationships);
                }

                inlines.Add(span);
                break;

            case "t":
                inlines.Add(new Run(element.Value));
                break;

            case "tab":
                inlines.Add(new Run("\t"));
                break;

            case "br":
                inlines.Add(new LineBreak());
                break;

            case "drawing":
                AppendWordDrawing(inlines, element, archive, relationships);
                break;

            case "pPr":
            case "rPr":
                break;

            default:
                foreach (var child in element.Elements())
                {
                    AppendWordInline(inlines, child, archive, relationships);
                }
                break;
        }
    }

    private static void AppendWordDrawing(InlineCollection inlines, XElement drawing, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var embedded = drawing.Descendants().FirstOrDefault(e => e.Name.LocalName == "blip")
            ?.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value;
        if (embedded is not null && relationships.TryGetValue(embedded, out var target))
        {
            var imageSource = ReadBitmapImage(archive, target);
            if (imageSource is not null)
            {
                var image = new Image
                {
                    Source = imageSource,
                    MaxWidth = 700,
                    MaxHeight = 500,
                    Stretch = Stretch.Uniform
                };
                inlines.Add(new InlineUIContainer(image));
                return;
            }
        }

        var description = drawing.Descendants().FirstOrDefault(e => e.Name.LocalName == "docPr")?.Attribute("descr")?.Value;
        inlines.Add(new Run($"[{(string.IsNullOrWhiteSpace(description) ? "Đối tượng nhúng" : description)}]")
        {
            Foreground = MutedForeground,
            FontStyle = FontStyles.Italic
        });
    }

    private static DocTable RenderWordTable(XElement tableElement, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var table = CreateStyledTable();
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        foreach (var rowElement in tableElement.Elements().Where(e => e.Name.LocalName == "tr"))
        {
            var row = new TableRow();
            foreach (var cellElement in rowElement.Elements().Where(e => e.Name.LocalName == "tc"))
            {
                var cell = CreateStyledCell(isHeader: false);
                var fill = cellElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "shd")?.Attribute("fill")?.Value;
                if (TryParseHexBrush(fill, out var fillBrush))
                {
                    cell.Background = fillBrush;
                }

                foreach (var block in cellElement.Elements())
                {
                    if (block.Name.LocalName == "p")
                    {
                        cell.Blocks.Add(RenderWordParagraph(block, archive, relationships));
                    }
                    else if (block.Name.LocalName == "tbl")
                    {
                        cell.Blocks.Add(RenderWordTable(block, archive, relationships));
                    }
                }

                row.Cells.Add(cell);
            }

            group.Rows.Add(row);
        }

        return table;
    }

    private static IEnumerable<Block> RenderSpreadsheet(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var stringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        var sharedStrings = stringsEntry is null
            ? []
            : LoadXml(archive, "xl/sharedStrings.xml").Descendants()
                .Where(e => e.Name.LocalName == "si")
                .Select(e => string.Concat(e.Descendants().Where(c => c.Name.LocalName == "t").Select(c => c.Value)))
                .ToArray();

        var sheets = new List<(string Name, string Path)>();
        if (archive.GetEntry("xl/workbook.xml") is not null)
        {
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var relationships = LoadRelationships(archive, "xl/_rels/workbook.xml.rels", "xl");
            foreach (var sheet in workbook.Descendants().Where(e => e.Name.LocalName == "sheet"))
            {
                var relId = sheet.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
                if (relId is not null && relationships.TryGetValue(relId, out var sheetPath))
                {
                    sheets.Add((sheet.Attribute("name")?.Value ?? "Trang tính", sheetPath));
                }
            }
        }
        else
        {
            sheets.AddRange(archive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => TrailingNumber(e.FullName))
                .Select((e, i) => ($"Trang tính {i + 1}", e.FullName)));
        }

        if (sheets.Count == 0)
        {
            throw new InvalidDataException("Không tìm thấy trang tính Excel.");
        }

        var blocks = new List<Block>();
        foreach (var sheet in sheets.Take(MaxSpreadsheetSheets))
        {
            blocks.Add(CreateHeadingParagraph(sheet.Name));
            var xml = LoadXml(archive, sheet.Path);
            var rows = xml.Descendants().Where(e => e.Name.LocalName == "row").Take(MaxSpreadsheetRows + 1).ToArray();
            var truncated = rows.Length > MaxSpreadsheetRows;

            var table = CreateStyledTable();
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            foreach (var rowElement in rows.Take(MaxSpreadsheetRows))
            {
                var row = new TableRow();
                row.Cells.Add(CreateTextCell(rowElement.Attribute("r")?.Value ?? "", isHeader: true));

                foreach (var cell in rowElement.Elements().Where(e => e.Name.LocalName == "c")
                    .Where(c => ColumnIndex(c.Attribute("r")?.Value ?? "A1") <= MaxSpreadsheetColumns))
                {
                    row.Cells.Add(CreateTextCell(ReadSpreadsheetCell(cell, sharedStrings), isHeader: false));
                }

                group.Rows.Add(row);
            }

            blocks.Add(table);
            if (truncated)
            {
                blocks.Add(CreateNoticeParagraph("Đã giới hạn phần xem trước ở 1000 dòng."));
            }
        }

        if (sheets.Count > MaxSpreadsheetSheets)
        {
            blocks.Add(CreateNoticeParagraph("Đã giới hạn phần xem trước ở 20 trang tính."));
        }

        return blocks;
    }

    private static string ReadSpreadsheetCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell.Attribute("t")?.Value == "inlineStr")
        {
            return string.Concat(cell.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
        }

        var value = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value ?? "";
        if (cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }

        return value;
    }

    private static IEnumerable<Block> RenderPresentation(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var slides = archive.Entries
            .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => TrailingNumber(e.FullName))
            .ToArray();
        if (slides.Length == 0)
        {
            throw new InvalidDataException("Không tìm thấy trang chiếu PowerPoint.");
        }

        var blocks = new List<Block>();
        for (var i = 0; i < slides.Length; i++)
        {
            var slide = slides[i];
            var xml = LoadXml(archive, slide.FullName);
            var relsPath = GetRelationshipsPath(slide.FullName);
            var relationships = LoadRelationships(archive, relsPath, Path.GetDirectoryName(slide.FullName) ?? "");

            blocks.Add(CreateHeadingParagraph($"Trang chiếu {i + 1}"));

            foreach (var shape in xml.Descendants().Where(e => e.Name.LocalName is "sp" or "pic"))
            {
                var relId = shape.Descendants().FirstOrDefault(e => e.Name.LocalName == "blip")
                    ?.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value;
                if (relId is not null && relationships.TryGetValue(relId, out var target))
                {
                    var bitmap = ReadBitmapImage(archive, target);
                    if (bitmap is not null)
                    {
                        blocks.Add(new BlockUIContainer(new Image
                        {
                            Source = bitmap,
                            MaxWidth = 700,
                            MaxHeight = 450,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 4, 0, 8)
                        }));
                    }
                }

                foreach (var paragraph in shape.Descendants().Where(e => e.Name.LocalName == "p"))
                {
                    var text = string.Concat(paragraph.Descendants().Where(c => c.Name.LocalName == "t").Select(c => c.Value));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0, 0, 0, 6) });
                    }
                }
            }
        }

        return blocks;
    }

    private static IEnumerable<Block> RenderOpenDocumentText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var body = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "text")
            ?? throw new InvalidDataException("Không tìm thấy nội dung OpenDocument.");

        var blocks = new List<Block>();
        foreach (var element in body.Elements())
        {
            CollectOpenDocumentBlocks(element, blocks);
        }

        return blocks;
    }

    private static void CollectOpenDocumentBlocks(XElement element, ICollection<Block> blocks)
    {
        switch (element.Name.LocalName)
        {
            case "h":
                blocks.Add(CreateHeadingParagraph(element.Value));
                break;
            case "p":
                blocks.Add(new Paragraph(new Run(element.Value)) { Margin = new Thickness(0, 0, 0, 8) });
                break;
            case "list":
                var list = new List();
                foreach (var item in element.Elements().Where(c => c.Name.LocalName == "list-item"))
                {
                    var listItem = new ListItem();
                    var itemBlocks = new List<Block>();
                    foreach (var child in item.Elements())
                    {
                        CollectOpenDocumentBlocks(child, itemBlocks);
                    }
                    foreach (var b in itemBlocks)
                    {
                        listItem.Blocks.Add(b);
                    }
                    list.ListItems.Add(listItem);
                }
                blocks.Add(list);
                break;
            case "table":
                var table = CreateStyledTable();
                var group = new TableRowGroup();
                table.RowGroups.Add(group);
                foreach (var rowElement in element.Descendants().Where(c => c.Name.LocalName == "table-row"))
                {
                    var row = new TableRow();
                    foreach (var cell in rowElement.Elements().Where(c => c.Name.LocalName is "table-cell" or "covered-table-cell"))
                    {
                        var text = string.Concat(cell.Descendants().Where(b => b.Name.LocalName is "p" or "h").Select(b => b.Value));
                        row.Cells.Add(CreateTextCell(text, isHeader: false));
                    }
                    group.Rows.Add(row);
                }
                blocks.Add(table);
                break;
            default:
                foreach (var child in element.Elements())
                {
                    CollectOpenDocumentBlocks(child, blocks);
                }
                break;
        }
    }

    private static IEnumerable<Block> RenderOpenDocumentSpreadsheet(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var blocks = new List<Block>();

        foreach (var tableElement in xml.Descendants().Where(e => e.Name.LocalName == "table"))
        {
            var name = tableElement.Attributes().FirstOrDefault(a => a.Name.LocalName == "name")?.Value ?? "Trang tính";
            blocks.Add(CreateHeadingParagraph(name));

            var table = CreateStyledTable();
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            var rowCount = 0;
            foreach (var rowElement in tableElement.Descendants().Where(e => e.Name.LocalName == "table-row"))
            {
                if (rowCount++ >= MaxSpreadsheetRows) break;
                var row = new TableRow();
                foreach (var cell in rowElement.Elements()
                    .Where(e => e.Name.LocalName is "table-cell" or "covered-table-cell")
                    .Take(MaxSpreadsheetColumns))
                {
                    var text = string.Join(" ", cell.Descendants().Where(c => c.Name.LocalName is "p" or "h").Select(c => c.Value));
                    row.Cells.Add(CreateTextCell(text, isHeader: false));
                }
                group.Rows.Add(row);
            }

            blocks.Add(table);
        }

        if (blocks.Count == 0)
        {
            throw new InvalidDataException("Không tìm thấy trang tính OpenDocument.");
        }

        return blocks;
    }

    private static IEnumerable<Block> RenderOpenDocumentPresentation(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var pages = xml.Descendants().Where(e => e.Name.LocalName == "page").ToArray();
        if (pages.Length == 0)
        {
            throw new InvalidDataException("Không tìm thấy trang chiếu OpenDocument.");
        }

        var blocks = new List<Block>();
        for (var i = 0; i < pages.Length; i++)
        {
            blocks.Add(CreateHeadingParagraph($"Trang chiếu {i + 1}"));
            foreach (var frame in pages[i].Elements().Where(e => e.Name.LocalName == "frame"))
            {
                foreach (var p in frame.Descendants().Where(e => e.Name.LocalName is "p" or "h"))
                {
                    if (!string.IsNullOrWhiteSpace(p.Value))
                    {
                        blocks.Add(new Paragraph(new Run(p.Value)) { Margin = new Thickness(0, 0, 0, 6) });
                    }
                }
            }
        }

        return blocks;
    }

    private static IEnumerable<Block> RenderLegacyWord(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HWPF.UserModel.HWPFDocument, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic document = Activator.CreateInstance(type, stream)!;
        string text = document.getText();
        return text.Split('\n')
            .Select(line => new Paragraph(new Run(line.TrimEnd('\r'))) { Margin = new Thickness(0, 0, 0, 6) });
    }

    private static IEnumerable<Block> RenderLegacySpreadsheet(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HSSF.UserModel.HSSFWorkbook, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic workbook = Activator.CreateInstance(type, stream)!;
        var blocks = new List<Block>();
        var sheetCount = (int)workbook.getNumberOfSheets();

        for (var sheetIndex = 0; sheetIndex < sheetCount; sheetIndex++)
        {
            dynamic sheet = workbook.getSheetAt(sheetIndex);
            blocks.Add(CreateHeadingParagraph((string)sheet.SheetName));

            var table = CreateStyledTable();
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            var rowLimit = Math.Min((int)sheet.getLastRowNum() + 1, MaxSpreadsheetRows);
            for (var rowIndex = 0; rowIndex < rowLimit; rowIndex++)
            {
                dynamic? srcRow = sheet.getRow(rowIndex);
                if (srcRow is null) continue;
                var row = new TableRow();
                row.Cells.Add(CreateTextCell((rowIndex + 1).ToString(CultureInfo.InvariantCulture), isHeader: true));

                var cellLimit = Math.Min((int)srcRow.getLastCellNum(), MaxSpreadsheetColumns);
                for (var col = 0; col < cellLimit; col++)
                {
                    dynamic? cell = srcRow.getCell(col);
                    row.Cells.Add(CreateTextCell(cell is null ? "" : ReadLegacyCell(cell), isHeader: false));
                }

                group.Rows.Add(row);
            }

            blocks.Add(table);
        }

        return blocks;
    }

    private static IEnumerable<Block> RenderLegacyPresentation(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HSLF.UserModel.HSLFSlideShow, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic presentation = Activator.CreateInstance(type, stream)!;
        var blocks = new List<Block>();
        var slideNumber = 0;

        foreach (dynamic slide in presentation.getSlides())
        {
            blocks.Add(CreateHeadingParagraph($"Trang chiếu {++slideNumber}"));
            foreach (dynamic shape in slide.getTextBlocks())
            {
                var text = (string?)shape.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    blocks.Add(new Paragraph(new Run(text)) { Margin = new Thickness(0, 0, 0, 6) });
                }
            }
        }

        if (slideNumber == 0)
        {
            throw new InvalidDataException("Không tìm thấy trang chiếu PowerPoint.");
        }

        return blocks;
    }

    private static string ReadLegacyCell(dynamic cell)
    {
        return cell.getCellType().ToString() switch
        {
            "String" => (string)cell.getStringCellValue(),
            "Numeric" => Convert.ToString((double)cell.getNumericCellValue(), CultureInfo.InvariantCulture) ?? "",
            "Boolean" => Convert.ToString((bool)cell.getBooleanCellValue(), CultureInfo.InvariantCulture) ?? "",
            "Formula" => "=" + (string)cell.getCellFormula(),
            "Error" => (string)cell.getErrorCellString(),
            _ => ""
        };
    }

    private static Paragraph CreateHeadingParagraph(string text) => new(new Run(text))
    {
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 14, 0, 10)
    };

    private static Paragraph CreateNoticeParagraph(string text) => new(new Run(text))
    {
        Foreground = MutedForeground,
        FontStyle = FontStyles.Italic,
        Margin = new Thickness(0, 6, 0, 10)
    };

    private static DocTable CreateStyledTable() => new()
    {
        CellSpacing = 0,
        BorderBrush = CellBorderBrush,
        BorderThickness = new Thickness(0.5),
        Margin = new Thickness(0, 8, 0, 16)
    };

    private static TableCell CreateStyledCell(bool isHeader) => new()
    {
        BorderBrush = CellBorderBrush,
        BorderThickness = new Thickness(0.5),
        Padding = new Thickness(6, 4, 6, 4),
        Background = isHeader ? HeaderCellBackground : Brushes.Transparent
    };

    private static TableCell CreateTextCell(string text, bool isHeader)
    {
        var cell = CreateStyledCell(isHeader);
        var p = new Paragraph(new Run(text)) { Margin = new Thickness(0) };
        if (isHeader) p.FontWeight = FontWeights.SemiBold;
        cell.Blocks.Add(p);
        return cell;
    }

    private static bool TryParseHexBrush(string? hex, out Brush brush)
    {
        if (hex is { Length: 6 } && hex.All(Uri.IsHexDigit))
        {
            var r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            return true;
        }

        brush = Brushes.Transparent;
        return false;
    }

    private static BitmapImage? ReadBitmapImage(ZipArchive archive, string target)
    {
        var entry = archive.GetEntry(target);
        if (entry is null || entry.Length > MaxImageEntrySize ||
            Path.GetExtension(target).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = memory;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static XDocument LoadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Thiếu thành phần tài liệu: {entryName}");
        if (entry.Length > MaxXmlEntrySize)
        {
            throw new InvalidDataException($"Thành phần tài liệu vượt quá giới hạn xem trước ({entryName}).");
        }

        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static Dictionary<string, string> LoadRelationships(ZipArchive archive, string path, string basePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var entry = archive.GetEntry(path);
        if (entry is null) return result;

        using var stream = entry.Open();
        var relationships = XDocument.Load(stream);
        foreach (var relationship in relationships.Descendants().Where(e => e.Name.LocalName == "Relationship"))
        {
            if (relationship.Attribute("TargetMode")?.Value == "External") continue;
            var id = relationship.Attribute("Id")?.Value;
            var target = relationship.Attribute("Target")?.Value;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target)) continue;
            result[id] = NormalizeZipPath(basePath, Uri.UnescapeDataString(target));
        }

        return result;
    }

    private static string NormalizeZipPath(string basePath, string target)
    {
        var parts = (target.StartsWith("/", StringComparison.Ordinal) ? "" : basePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var part in target.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }

        return string.Join("/", parts);
    }

    private static string GetRelationshipsPath(string partPath)
    {
        var directory = Path.GetDirectoryName(partPath)?.Replace('\\', '/') ?? "";
        return $"{directory}/_rels/{Path.GetFileName(partPath)}.rels";
    }

    private static int ColumnIndex(string cellReference)
    {
        var column = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character)) break;
            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }

        return column;
    }

    private static int TrailingNumber(string value)
    {
        var match = Regex.Match(Path.GetFileNameWithoutExtension(value), @"(\d+)$", RegexOptions.CultureInvariant);
        return int.TryParse(match.Value, out var number) ? number : int.MaxValue;
    }
}

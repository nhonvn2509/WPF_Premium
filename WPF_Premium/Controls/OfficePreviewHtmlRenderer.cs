using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WPF_Premium.Controls;

internal static class OfficePreviewHtmlRenderer
{
    private const int MaxSpreadsheetRows = 1000;
    private const int MaxSpreadsheetColumns = 80;
    private const int MaxSpreadsheetSheets = 20;
    private const long MaxXmlEntrySize = 32 * 1024 * 1024;
    private const long MaxImageEntrySize = 10 * 1024 * 1024;
    private const long EmusPerInch = 914400;

    public static string Render(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
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
    }

    private static string RenderWord(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var document = LoadXml(archive, "word/document.xml");
        var body = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "body")
            ?? throw new InvalidDataException("Không tìm thấy nội dung Word.");
        var relationships = LoadRelationships(archive, "word/_rels/document.xml.rels", "word");
        var content = new StringBuilder();
        foreach (var block in body.Elements())
        {
            if (block.Name.LocalName == "p") content.Append(RenderWordParagraph(block, archive, relationships));
            else if (block.Name.LocalName == "tbl") content.Append(RenderWordTable(block, archive, relationships));
        }

        var section = body.Elements().LastOrDefault(element => element.Name.LocalName == "sectPr");
        var pageSize = section?.Elements().FirstOrDefault(element => element.Name.LocalName == "pgSz");
        var pageWidth = CssTwips(pageSize?.Attribute("w")?.Value, 12240);
        var pageHeight = CssTwips(pageSize?.Attribute("h")?.Value, 15840);
        var margins = section?.Elements().FirstOrDefault(element => element.Name.LocalName == "pgMar");
        var marginTop = CssTwips(margins?.Attribute("top")?.Value, 1440);
        var marginRight = CssTwips(margins?.Attribute("right")?.Value, 1440);
        var marginBottom = CssTwips(margins?.Attribute("bottom")?.Value, 1440);
        var marginLeft = CssTwips(margins?.Attribute("left")?.Value, 1440);
        return WrapDocument(
            $"<article class=\"page\" style=\"width:{pageWidth}pt;min-height:{pageHeight}pt;padding:{marginTop}pt {marginRight}pt {marginBottom}pt {marginLeft}pt\">{content}</article>");
    }

    private static string RenderWordParagraph(XElement paragraph, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var properties = paragraph.Elements().FirstOrDefault(element => element.Name.LocalName == "pPr");
        var styles = new List<string>();
        var alignment = properties?.Elements().FirstOrDefault(element => element.Name.LocalName == "jc")?.Attribute("val")?.Value;
        if (alignment is "center" or "right" or "both") styles.Add($"text-align:{(alignment == "both" ? "justify" : alignment)}");
        var indentation = properties?.Elements().FirstOrDefault(element => element.Name.LocalName == "ind");
        if (double.TryParse(indentation?.Attribute("left")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left))
            styles.Add($"margin-left:{CssTwips((long)left, 0)}pt");
        if (double.TryParse(indentation?.Attribute("firstLine")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstLine))
            styles.Add($"text-indent:{CssTwips((long)firstLine, 0)}pt");
        var styleName = properties?.Elements().FirstOrDefault(element => element.Name.LocalName == "pStyle")?.Attribute("val")?.Value ?? "";
        var tag = styleName.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) ? "h2" : "p";
        var inline = string.Concat(paragraph.Elements().Where(element => element.Name.LocalName != "pPr")
            .Select(element => RenderWordInline(element, archive, relationships)));
        var pageBreak = paragraph.Descendants().Any(element =>
            element.Name.LocalName == "br" && element.Attribute("type")?.Value == "page");
        return (pageBreak ? "<div class=\"page-break\"></div>" : "") +
            $"<{tag}{StyleAttribute(styles)}>{inline}</{tag}>";
    }

    private static string RenderWordInline(XElement element, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        switch (element.Name.LocalName)
        {
            case "r":
                var runProperties = element.Elements().FirstOrDefault(child => child.Name.LocalName == "rPr");
                var styles = new List<string>();
                if (runProperties?.Elements().Any(child => child.Name.LocalName == "b") == true) styles.Add("font-weight:bold");
                if (runProperties?.Elements().Any(child => child.Name.LocalName == "i") == true) styles.Add("font-style:italic");
                if (runProperties?.Elements().Any(child => child.Name.LocalName == "u") == true) styles.Add("text-decoration:underline");
                var color = runProperties?.Elements().FirstOrDefault(child => child.Name.LocalName == "color")?.Attribute("val")?.Value;
                if (IsHexColor(color)) styles.Add($"color:#{color}");
                var size = runProperties?.Elements().FirstOrDefault(child => child.Name.LocalName == "sz")?.Attribute("val")?.Value;
                if (double.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var halfPoints))
                    styles.Add($"font-size:{halfPoints / 2d:0.##}pt");
                var runContent = string.Concat(element.Elements().Where(child => child.Name.LocalName != "rPr")
                    .Select(child => RenderWordInline(child, archive, relationships)));
                return styles.Count == 0 ? runContent : $"<span{StyleAttribute(styles)}>{runContent}</span>";
            case "t":
                return Encode(element.Value);
            case "tab":
                return "&emsp;";
            case "br":
                return element.Attribute("type")?.Value == "page" ? "<span class=\"page-break\"></span>" : "<br>";
            case "drawing":
                return RenderWordDrawing(element, archive, relationships);
            case "pPr":
            case "rPr":
                return "";
            default:
                return string.Concat(element.Elements().Select(child => RenderWordInline(child, archive, relationships)));
        }
    }

    private static string RenderWordDrawing(XElement drawing, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var embedded = drawing.Descendants().FirstOrDefault(element => element.Name.LocalName == "blip")
            ?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "embed")?.Value;
        if (embedded is not null && relationships.TryGetValue(embedded, out var target))
        {
            var image = ReadImageData(archive, target);
            if (image is not null) return $"<img class=\"inline-image\" src=\"{image}\" alt=\"Hình trong tài liệu\">";
        }

        var description = drawing.Descendants().FirstOrDefault(element => element.Name.LocalName == "docPr")
            ?.Attribute("descr")?.Value;
        return $"<span class=\"embedded-object\">{Encode(string.IsNullOrWhiteSpace(description) ? "Đối tượng nhúng" : description)}</span>";
    }

    private static string RenderWordTable(XElement table, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var rows = table.Elements().Where(element => element.Name.LocalName == "tr").Select(row =>
        {
            var cells = row.Elements().Where(element => element.Name.LocalName == "tc").Select(cell =>
            {
                var value = string.Concat(cell.Elements().Select(block =>
                    block.Name.LocalName == "p" ? RenderWordParagraph(block, archive, relationships) :
                    block.Name.LocalName == "tbl" ? RenderWordTable(block, archive, relationships) : ""));
                var fill = cell.Descendants().FirstOrDefault(element => element.Name.LocalName == "shd")?.Attribute("fill")?.Value;
                return $"<td{(IsHexColor(fill) ? $" style=\"background:#{fill}\"" : "")}>{value}</td>";
            });
            return $"<tr>{string.Concat(cells)}</tr>";
        });
        return $"<table>{string.Concat(rows)}</table>";
    }

    private static string RenderSpreadsheet(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var stringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        var sharedStrings = stringsEntry is null
            ? []
            : LoadXml(archive, "xl/sharedStrings.xml").Descendants()
                .Where(element => element.Name.LocalName == "si")
                .Select(element => string.Concat(element.Descendants().Where(child => child.Name.LocalName == "t").Select(child => child.Value)))
                .ToArray();
        var sheets = new List<(string Name, string Path)>();
        if (archive.GetEntry("xl/workbook.xml") is not null)
        {
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var relationships = LoadRelationships(archive, "xl/_rels/workbook.xml.rels", "xl");
            foreach (var sheet in workbook.Descendants().Where(element => element.Name.LocalName == "sheet"))
            {
                var relationshipId = sheet.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value;
                if (relationshipId is not null && relationships.TryGetValue(relationshipId, out var sheetPath))
                    sheets.Add((sheet.Attribute("name")?.Value ?? "Trang tính", sheetPath));
            }
        }
        else
        {
            sheets.AddRange(archive.Entries
                .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => TrailingNumber(entry.FullName))
                .Select((entry, index) => ($"Trang tính {index + 1}", entry.FullName)));
        }
        if (sheets.Count == 0) throw new InvalidDataException("Không tìm thấy trang tính Excel.");
        sheets = sheets.Take(MaxSpreadsheetSheets + 1).ToList();

        var content = new StringBuilder();
        foreach (var (sheet, index) in sheets.Select((entry, index) => (entry, index)))
        {
            var xml = LoadXml(archive, sheet.Path);
            var rows = xml.Descendants().Where(element => element.Name.LocalName == "row").Take(MaxSpreadsheetRows + 1).ToArray();
            var truncated = rows.Length > MaxSpreadsheetRows;
            content.Append($"<section class=\"sheet\"><h2>{Encode(sheet.Name)}</h2><table><tbody>");
            foreach (var row in rows.Take(MaxSpreadsheetRows))
            {
                var cells = row.Elements().Where(element => element.Name.LocalName == "c")
                    .Where(cell => ColumnIndex(cell.Attribute("r")?.Value ?? "A1") <= MaxSpreadsheetColumns)
                    .Select(cell => $"<td>{Encode(ReadSpreadsheetCell(cell, sharedStrings))}</td>");
                content.Append($"<tr><th>{Encode(row.Attribute("r")?.Value ?? "")}</th>{string.Concat(cells)}</tr>");
            }
            content.Append("</tbody></table>");
            if (truncated) content.Append("<p class=\"notice\">Đã giới hạn phần xem trước ở 1000 dòng.</p>");
            content.Append("</section>");
        }
        if (sheets.Count > MaxSpreadsheetSheets)
            content.Append("<p class=\"notice\">Đã giới hạn phần xem trước ở 20 trang tính.</p>");
        return WrapDocument(content.ToString());
    }

    private static string ReadSpreadsheetCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        if (cell.Attribute("t")?.Value == "inlineStr")
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
        var value = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
        if (cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count)
            return sharedStrings[index];
        return value;
    }

    private static string RenderPresentation(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var size = LoadXml(archive, "ppt/presentation.xml").Descendants().FirstOrDefault(element => element.Name.LocalName == "sldSz");
        var slideWidth = ParseLong(size?.Attribute("cx")?.Value, 12192000);
        var slideHeight = ParseLong(size?.Attribute("cy")?.Value, 6858000);
        var slides = archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => TrailingNumber(entry.FullName))
            .ToArray();
        if (slides.Length == 0) throw new InvalidDataException("Không tìm thấy trang chiếu PowerPoint.");
        var content = new StringBuilder();
        for (var index = 0; index < slides.Length; index++)
        {
            var slide = slides[index];
            var xml = LoadXml(archive, slide.FullName);
            var relsPath = GetRelationshipsPath(slide.FullName);
            var relationships = LoadRelationships(archive, relsPath, Path.GetDirectoryName(slide.FullName) ?? "");
            var width = Math.Max(1, slideWidth / EmusPerInch * 96d);
            var height = Math.Max(1, slideHeight / EmusPerInch * 96d);
            content.Append($"<section class=\"slide\" style=\"width:{width:0.##}px;height:{height:0.##}px\"><span class=\"slide-number\">{index + 1}</span>");
            foreach (var shape in xml.Descendants().Where(element => element.Name.LocalName is "sp" or "pic"))
            {
                var transform = shape.Descendants().FirstOrDefault(element => element.Name.LocalName == "xfrm");
                var offset = transform?.Elements().FirstOrDefault(element => element.Name.LocalName == "off");
                var extent = transform?.Elements().FirstOrDefault(element => element.Name.LocalName == "ext");
                var x = ParseLong(offset?.Attribute("x")?.Value, 0) / (double)slideWidth * width;
                var y = ParseLong(offset?.Attribute("y")?.Value, 0) / (double)slideHeight * height;
                var boxWidth = ParseLong(extent?.Attribute("cx")?.Value, slideWidth / 3) / (double)slideWidth * width;
                var boxHeight = ParseLong(extent?.Attribute("cy")?.Value, slideHeight / 8) / (double)slideHeight * height;
                var text = string.Join("<br>", shape.Descendants().Where(element => element.Name.LocalName == "p")
                    .Select(paragraph => string.Concat(paragraph.Descendants().Where(child => child.Name.LocalName == "t").Select(child => Encode(child.Value))))
                    .Where(value => value.Length > 0));
                var image = RenderSlideImage(shape, archive, relationships);
                if (text.Length == 0 && image.Length == 0) continue;
                content.Append($"<div class=\"slide-shape\" style=\"left:{x:0.##}px;top:{y:0.##}px;width:{boxWidth:0.##}px;height:{boxHeight:0.##}px\">{image}{text}</div>");
            }
            content.Append("</section>");
        }
        return WrapDocument(content.ToString());
    }

    private static string RenderSlideImage(XElement shape, ZipArchive archive, IReadOnlyDictionary<string, string> relationships)
    {
        var relationshipId = shape.Descendants().FirstOrDefault(element => element.Name.LocalName == "blip")
            ?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "embed")?.Value;
        if (relationshipId is null || !relationships.TryGetValue(relationshipId, out var target)) return "";
        var source = ReadImageData(archive, target);
        return source is null ? "" : $"<img class=\"slide-image\" src=\"{source}\" alt=\"Hình trong trang chiếu\">";
    }

    private static string RenderOpenDocumentText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var body = xml.Descendants().FirstOrDefault(element => element.Name.LocalName == "text");
        if (body is null) throw new InvalidDataException("Không tìm thấy nội dung OpenDocument.");
        return WrapDocument(string.Concat(body.Elements().Select(RenderOpenDocumentBlock)));
    }

    private static string RenderOpenDocumentSpreadsheet(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var tables = xml.Descendants().Where(element => element.Name.LocalName == "table");
        var content = new StringBuilder();
        foreach (var table in tables)
        {
            var name = table.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "name")?.Value ?? "Trang tính";
            content.Append($"<section class=\"sheet\"><h2>{Encode(name)}</h2><table><tbody>");
            var rowCount = 0;
            foreach (var row in table.Descendants().Where(element => element.Name.LocalName == "table-row"))
            {
                if (rowCount++ >= MaxSpreadsheetRows) break;
                var cells = row.Elements().Where(element => element.Name.LocalName is "table-cell" or "covered-table-cell")
                    .Take(MaxSpreadsheetColumns)
                    .Select(cell => $"<td>{Encode(string.Join(" ", cell.Descendants().Where(child => child.Name.LocalName is "p" or "h").Select(child => child.Value)))}</td>");
                content.Append($"<tr>{string.Concat(cells)}</tr>");
            }
            content.Append("</tbody></table></section>");
        }
        if (content.Length == 0) throw new InvalidDataException("Không tìm thấy trang tính OpenDocument.");
        return WrapDocument(content.ToString());
    }

    private static string RenderOpenDocumentPresentation(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var xml = LoadXml(archive, "content.xml");
        var pages = xml.Descendants().Where(element => element.Name.LocalName == "page").ToArray();
        if (pages.Length == 0) throw new InvalidDataException("Không tìm thấy trang chiếu OpenDocument.");
        var content = new StringBuilder();
        foreach (var (page, index) in pages.Select((page, index) => (page, index)))
        {
            content.Append($"<section class=\"slide\"><span class=\"slide-number\">{index + 1}</span>");
            foreach (var frame in page.Elements().Where(element => element.Name.LocalName == "frame"))
            {
                var style = new List<string>();
                AddOdfPosition(frame, "x", "left", style);
                AddOdfPosition(frame, "y", "top", style);
                AddOdfPosition(frame, "width", "width", style);
                AddOdfPosition(frame, "height", "height", style);
                var text = string.Concat(frame.Descendants().Where(element => element.Name.LocalName is "p" or "h")
                    .Select(element => $"<p>{Encode(element.Value)}</p>"));
                content.Append($"<div class=\"odf-shape\"{StyleAttribute(style)}>{text}</div>");
            }
            content.Append("</section>");
        }
        return WrapDocument(content.ToString());
    }

    private static string RenderOpenDocumentBlock(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "h":
                return $"<h2>{Encode(element.Value)}</h2>";
            case "p":
                return $"<p>{Encode(element.Value)}</p>";
            case "list":
                return $"<ul>{string.Concat(element.Elements().Where(child => child.Name.LocalName == "list-item").Select(RenderOpenDocumentBlock))}</ul>";
            case "list-item":
                return $"<li>{string.Concat(element.Elements().Select(RenderOpenDocumentBlock))}</li>";
            case "table":
                return "<table>" + string.Concat(element.Descendants().Where(child => child.Name.LocalName == "table-row").Select(row =>
                    "<tr>" + string.Concat(row.Elements().Where(cell => cell.Name.LocalName is "table-cell" or "covered-table-cell")
                        .Select(cell => $"<td>{string.Concat(cell.Descendants().Where(block => block.Name.LocalName is "p" or "h").Select(block => Encode(block.Value)))}</td>")) + "</tr>")) + "</table>";
            default:
                return string.Concat(element.Elements().Select(RenderOpenDocumentBlock));
        }
    }

    private static string RenderLegacyWord(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HWPF.UserModel.HWPFDocument, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic document = Activator.CreateInstance(type, stream)!;
        string text = document.getText();
        return WrapDocument(string.Concat(text.Split('\n').Select(line => $"<p>{Encode(line.TrimEnd('\r'))}</p>")));
    }

    private static string RenderLegacySpreadsheet(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HSSF.UserModel.HSSFWorkbook, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic workbook = Activator.CreateInstance(type, stream)!;
        var content = new StringBuilder();
        var sheetCount = (int)workbook.getNumberOfSheets();
        for (var sheetIndex = 0; sheetIndex < sheetCount; sheetIndex++)
        {
            dynamic sheet = workbook.getSheetAt(sheetIndex);
            content.Append($"<section class=\"sheet\"><h2>{Encode((string)sheet.SheetName)}</h2><table><tbody>");
            var rowLimit = Math.Min((int)sheet.getLastRowNum() + 1, MaxSpreadsheetRows);
            for (var rowIndex = 0; rowIndex < rowLimit; rowIndex++)
            {
                dynamic? row = sheet.getRow(rowIndex);
                if (row is null) continue;
                var cells = new List<string>();
                var cellLimit = Math.Min((int)row.getLastCellNum(), MaxSpreadsheetColumns);
                for (var column = 0; column < cellLimit; column++)
                {
                    dynamic? cell = row.getCell(column);
                    cells.Add($"<td>{Encode(cell is null ? "" : ReadLegacyCell(cell))}</td>");
                }
                content.Append($"<tr><th>{rowIndex + 1}</th>{string.Concat(cells)}</tr>");
            }
            content.Append("</tbody></table></section>");
        }
        return WrapDocument(content.ToString());
    }

    private static string RenderLegacyPresentation(string path)
    {
        using var stream = File.OpenRead(path);
        var type = Type.GetType("DotnetPoi.HSLF.UserModel.HSLFSlideShow, DotnetPoi.Legacy", throwOnError: true)!;
        dynamic presentation = Activator.CreateInstance(type, stream)!;
        var content = new StringBuilder();
        var slideNumber = 0;
        foreach (dynamic slide in presentation.getSlides())
        {
            content.Append($"<section class=\"slide\"><span class=\"slide-number\">{++slideNumber}</span>");
            foreach (dynamic shape in slide.getTextBlocks())
            {
                var text = (string?)shape.Text;
                if (!string.IsNullOrWhiteSpace(text)) content.Append($"<div class=\"legacy-slide-text\">{Encode(text)}</div>");
            }
            content.Append("</section>");
        }
        if (slideNumber == 0) throw new InvalidDataException("Không tìm thấy trang chiếu PowerPoint.");
        return WrapDocument(content.ToString());
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

    private static XDocument LoadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Thiếu thành phần tài liệu: {entryName}");
        if (entry.Length > MaxXmlEntrySize)
            throw new InvalidDataException($"Thành phần tài liệu vượt quá giới hạn xem trước ({entryName}).");
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
        foreach (var relationship in relationships.Descendants().Where(element => element.Name.LocalName == "Relationship"))
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
            else parts.Add(part);
        }
        return string.Join("/", parts);
    }

    private static string GetRelationshipsPath(string partPath)
    {
        var directory = Path.GetDirectoryName(partPath)?.Replace('\\', '/') ?? "";
        return $"{directory}/_rels/{Path.GetFileName(partPath)}.rels";
    }

    private static string? ReadImageData(ZipArchive archive, string target)
    {
        var entry = archive.GetEntry(target);
        if (entry is null || entry.Length > MaxImageEntrySize || Path.GetExtension(target).Equals(".svg", StringComparison.OrdinalIgnoreCase)) return null;
        var extension = Path.GetExtension(target).ToLowerInvariant();
        var mime = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return $"data:{mime};base64,{Convert.ToBase64String(memory.ToArray())}";
    }

    private static string WrapDocument(string content)
    {
        return """
            <!doctype html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                *{box-sizing:border-box} body{margin:0;padding:24px;background:#dfe3e8;color:#202124;font:14px "Segoe UI",Arial,sans-serif}
                .page,.sheet,.slide{position:relative;margin:0 auto 22px;background:#fff;padding:64px;box-shadow:0 2px 9px #0003;overflow:hidden}
                .page{min-height:1056px;width:816px} .sheet{width:max-content;min-width:816px;max-width:100%;padding:28px;overflow:auto}
                .slide{width:960px;height:540px;padding:0} p{line-height:1.55;margin:0 0 10px;white-space:pre-wrap}
                h2{margin:0 0 16px} table{border-collapse:collapse;max-width:100%;margin:12px 0 18px}
                td,th{border:1px solid #aeb5bd;padding:5px 8px;min-width:68px;vertical-align:top}
                th{background:#f1f3f5;font-weight:600} .page-break{break-before:page;height:18px;border-top:1px dashed #bbb;margin:16px 0}
                .inline-image{max-width:100%;max-height:700px;object-fit:contain;vertical-align:middle}
                .embedded-object{display:inline-block;padding:8px 14px;border:1px solid #8a9099;background:#f4f6f8}
                .slide-number{position:absolute;right:10px;bottom:8px;color:#777;font-size:12px}
                .slide-shape{position:absolute;overflow:hidden;white-space:pre-wrap;padding:4px}
                .slide-image{width:100%;height:100%;object-fit:contain} .odf-shape{position:absolute;overflow:hidden}
                .legacy-slide-text{padding:10px 24px} .notice{color:#62676e}
              </style>
            </head>
            <body>
            """ + content + "</body></html>";
    }

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static string StyleAttribute(IEnumerable<string> styles)
    {
        var value = string.Join(";", styles.Where(style => !string.IsNullOrWhiteSpace(style)));
        return value.Length == 0 ? "" : $" style=\"{Encode(value)}\"";
    }

    private static string CssTwips(string? value, double fallback)
    {
        return CssTwips(ParseLong(value, (long)fallback), fallback).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double CssTwips(long value, double fallback) => value > 0 ? value / 20d : fallback;

    private static bool IsHexColor(string? value) =>
        value is { Length: 6 } && value.All(Uri.IsHexDigit);

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

    private static long ParseLong(string? value, long fallback) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static void AddOdfPosition(XElement frame, string sourceName, string cssName, ICollection<string> styles)
    {
        var value = frame.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == sourceName)?.Value;
        if (value is null) return;
        var match = Regex.Match(value, @"^\s*(-?\d+(?:\.\d+)?)\s*(cm|mm|in|pt)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var number)) return;
        var pixels = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "cm" => number * 96d / 2.54d,
            "mm" => number * 96d / 25.4d,
            "in" => number * 96d,
            "pt" => number * 96d / 72d,
            _ => number * 96d / 2.54d
        };
        styles.Add($"{cssName}:{pixels:0.##}px");
    }
}

using System.Text;
using System.Text.RegularExpressions;
using AIStudyHub.Business.Interfaces.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using UglyToad.PdfPig.Rendering.Skia.Helpers;
using Tesseract;
using SkiaSharp;
using PdfDocument = UglyToad.PdfPig.PdfDocument;
using WpDrawing = DocumentFormat.OpenXml.Wordprocessing.Drawing;
using DocProperties = DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Business.Enums;


namespace AIStudyHub.Business.Services;

public sealed class DocumentProcessingService : IDocumentProcessingService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx", ".jpg", ".png", ".jpeg", ".webp", ".gif"
    };

    private static readonly string TessDataPath = ResolveTessDataPath();

    private static string ResolveTessDataPath()
    {
        var envPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        var localCurrentDir = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
        if (Directory.Exists(localCurrentDir))
            return localCurrentDir;

        var localBaseDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (Directory.Exists(localBaseDir))
            return localBaseDir;

        return @"C:\Program Files\Tesseract-OCR\tessdata";
    }

    public async Task<string> ExtractTextAsync(byte[] fileContent, string fileExtension)
    {
        var extension = fileExtension.ToLowerInvariant().TrimStart('.');

        if (!SupportedExtensions.Contains($".{extension}"))
            throw new NotSupportedException($"File type '.{extension}' is not supported. Supported types: .txt, .md, .pdf, .docx");

        return extension switch
        {
            "txt" or "md" => await ExtractTextFromTxtAsync(fileContent),
            "pdf" => ExtractTextFromPdf(fileContent),
            "docx" => ExtractTextFromDocx(fileContent),
            "jpg" or "png" or "jpeg" or "webp" or "gif" => ExtractTextFromImage(fileContent, extension),
            _ => throw new NotSupportedException($"File type '.{extension}' is not supported.")
        };
    }

    public async Task<IReadOnlyList<ExtractedTextSegment>> ExtractSegmentsAsync(
        byte[] fileContent,
        string fileExtension)
    {
        var extension = fileExtension.ToLowerInvariant().TrimStart('.');
        var extracted = await ExtractTextAsync(fileContent, fileExtension);
        if (string.IsNullOrWhiteSpace(extracted) || IsBackendErrorMarker(extracted.Trim()))
            return Array.Empty<ExtractedTextSegment>();

        return extension switch
        {
            "pdf" => ParsePageSegments(extracted),
            "docx" => ParseDocxSegments(extracted),
            "jpg" or "png" or "jpeg" or "webp" or "gif" =>
                [new ExtractedTextSegment(extracted, DocumentContentType.Ocr, null, false)],
            _ => [new ExtractedTextSegment(extracted, DocumentContentType.Verbatim, null, true)]
        };
    }

    private static IReadOnlyList<ExtractedTextSegment> ParsePageSegments(string text)
    {
        var result = new List<ExtractedTextSegment>();
        var current = new StringBuilder();
        int? page = null;

        foreach (var line in Regex.Split(text, @"\r?\n"))
        {
            if (IsPageMarker(line.Trim()))
            {
                AddCurrentPage();
                page = ParsePageMarker(line.Trim());
                continue;
            }
            current.AppendLine(line);
        }
        AddCurrentPage();
        return result;

        void AddCurrentPage()
        {
            var value = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value) && !IsBackendErrorMarker(value))
                result.Add(new ExtractedTextSegment(value, DocumentContentType.Verbatim, page, true));
            current.Clear();
        }
    }

    private static IReadOnlyList<ExtractedTextSegment> ParseDocxSegments(string text)
    {
        var result = new List<ExtractedTextSegment>();
        var current = new StringBuilder();
        var type = DocumentContentType.Verbatim;

        foreach (var line in Regex.Split(text, @"\r?\n"))
        {
            var trimmed = line.Trim();
            var nextType = trimmed.StartsWith("[Diagram:", StringComparison.OrdinalIgnoreCase)
                ? DocumentContentType.AltText
                : Regex.IsMatch(trimmed, @"^\[--- Image \d+ ---\]$", RegexOptions.IgnoreCase)
                    ? DocumentContentType.Ocr
                    : type;

            if (nextType != type)
            {
                AddCurrent();
                type = nextType;
            }
            if (!IsTechnicalMarker(trimmed) && !IsBackendErrorMarker(trimmed))
                current.AppendLine(line);
        }
        AddCurrent();
        return result;

        void AddCurrent()
        {
            var value = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(new ExtractedTextSegment(value, type, null, type == DocumentContentType.Verbatim));
            current.Clear();
        }
    }

    public Task<List<DocumentChunkDto>> ChunkTextAsync(string text, int chunkSize, int overlap, bool preserveTables = true)
    {
        var chunks = new List<DocumentChunkDto>();
        var cleanText = CleanText(text);

        if (string.IsNullOrWhiteSpace(cleanText))
            return Task.FromResult(chunks);

        IEnumerable<string> units = preserveTables
            ? SplitPreservingTables(cleanText)
            : new[] { cleanText };

        var sentences = units
            .SelectMany(u => IsMarkdownTableRow(u) || IsPageMarker(u)
                ? new[] { u }
                : SplitIntoSentences(u).SelectMany(s => SplitLongSentence(s, chunkSize)))
            .ToList();
        var currentChunk = new StringBuilder();
        var currentLength = 0;
        int? currentPageNumber = null;

        foreach (var sentence in sentences)
        {
            if (IsPageMarker(sentence))
            {
                currentPageNumber = ParsePageMarker(sentence);
                continue;
            }

            var sentenceLength = sentence.Length;

            if (currentLength + sentenceLength > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(new DocumentChunkDto { Text = currentChunk.ToString().Trim(), PageNumber = currentPageNumber });
                currentChunk.Clear();
                currentLength = 0;

                if (overlap > 0 && chunks.Count > 0)
                {
                    var lastChunk = chunks.Last().Text;
                    var overlapWindow = lastChunk.Length > overlap
                        ? lastChunk[^overlap..]
                        : lastChunk;
                    var searchWindow = overlapWindow.Length > 1 ? overlapWindow[..^1] : overlapWindow;
                    var lastSentenceBoundary = searchWindow.LastIndexOfAny(
                        new[] { '.', '!', '?', '\n' });
                    var overlapText = lastSentenceBoundary >= 0
                        ? overlapWindow[(lastSentenceBoundary + 1)..].Trim()
                        : overlapWindow.Trim();
                    currentChunk.Append(overlapText + " ");
                    currentLength = overlapText.Length + 1;
                }
            }

            currentChunk.Append(sentence).Append(" ");
            currentLength += sentenceLength + 1;
        }

        if (currentChunk.Length > 0)
            chunks.Add(new DocumentChunkDto { Text = currentChunk.ToString().Trim(), PageNumber = currentPageNumber });

        return Task.FromResult(chunks);
    }

    public async Task<List<DocumentChunkDto>> ChunkSegmentsAsync(
        IReadOnlyList<ExtractedTextSegment> segments,
        int chunkSize,
        int overlap,
        bool preserveTables = true)
    {
        var chunks = new List<DocumentChunkDto>();
        var groups = new List<(DocumentContentType Type, int? Page, bool Highlightable, StringBuilder Text)>();

        foreach (var segment in segments)
        {
            if (segment.ContentType == DocumentContentType.SystemError)
                continue;

            var sanitized = SanitizeSegmentText(segment.Text);
            if (string.IsNullOrWhiteSpace(sanitized))
                continue;

            var current = groups.LastOrDefault();
            if (current.Text is null || current.Type != segment.ContentType
                || current.Page != segment.PageNumber
                || current.Highlightable != segment.IsHighlightable)
            {
                current = (segment.ContentType, segment.PageNumber, segment.IsHighlightable, new StringBuilder());
                groups.Add(current);
            }

            if (current.Text.Length > 0)
                current.Text.AppendLine();
            current.Text.Append(sanitized);
        }

        foreach (var group in groups)
        {
            var groupChunks = await ChunkTextAsync(group.Text.ToString(), chunkSize, overlap, preserveTables);
            foreach (var chunk in groupChunks)
            {
                chunk.PageNumber = group.Page;
                chunk.ContentType = group.Type;
                chunk.IsHighlightable = group.Highlightable;
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private static string SanitizeSegmentText(string text)
    {
        text = Regex.Replace(text, @"\r\n|\r", "\n");
        text = Regex.Replace(text, @"^\s*\d+\s*\|\s*Page\s+", "",
            RegexOptions.IgnoreCase);
        var keptLines = text.Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]+", " ").Trim())
            .Where(line => !IsTechnicalMarker(line))
            .Where(line => !line.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            .Where(line => !IsBackendErrorMarker(line));

        return Regex.Replace(string.Join("\n", keptLines), @"\n{3,}", "\n\n").Trim();
    }

    private static bool IsTechnicalMarker(string line) =>
        Regex.IsMatch(line, @"^\[---\s+(Page|Image)\s+\d+\s+---\]$", RegexOptions.IgnoreCase);

    private static bool IsBackendErrorMarker(string line) =>
        Regex.IsMatch(line,
            @"^\[(PDF|DOCX|OCR image) extraction failed:.*\]$|^\[Image \d+ - content not extractable:.*\]$",
            RegexOptions.IgnoreCase);

    private static bool IsPageMarker(string line) => line.StartsWith("[--- Page ") && line.EndsWith(" ---]");
    private static int? ParsePageMarker(string line)
    {
        var match = Regex.Match(line, @"\[--- Page (\d+) ---\]");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var p)) return p;
        return null;
    }

    private static async Task<string> ExtractTextFromTxtAsync(byte[] fileContent)
    {
        var encoding = DetectEncoding(fileContent);
        return await Task.Run(() => encoding.GetString(fileContent));
    }

    private static string ExtractTextFromDocx(byte[] fileContent)
    {
        var text = new StringBuilder();
        var imageTexts = new List<(int Index, string Content)>();

        try
        {
            using var stream = new MemoryStream(fileContent);
            using var document = WordprocessingDocument.Open(stream, false);

            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
                return string.Empty;

            // 1. Extract text from paragraph and table elements
            foreach (var element in body.Elements())
            {
                if (element is Table table)
                {
                    var tableText = ExtractTableFromDocx(table);
                    if (!string.IsNullOrWhiteSpace(tableText))
                        text.AppendLine(tableText);
                }
                else
                {
                    var paraText = GetParagraphText(element);
                    if (!string.IsNullOrWhiteSpace(paraText))
                        text.AppendLine(paraText);
                }
            }

            // 2. Extract alt-text from OOXML drawing elements (diagram labels/captions)
            //    This captures diagram names and descriptions WITHOUT OCR — works even if the image is unreadable.
            foreach (var drawing in body.Descendants<WpDrawing>())
            {
                var docPr = drawing.Descendants<DocProperties>().FirstOrDefault();
                if (docPr == null) continue;

                var diagramName = docPr.Name?.Value;
                var diagramDesc = docPr.Description?.Value;

                if (!string.IsNullOrWhiteSpace(diagramName))
                {
                    text.AppendLine($"[Diagram: {diagramName}]");
                    if (!string.IsNullOrWhiteSpace(diagramDesc))
                        text.AppendLine(diagramDesc);
                }
            }

            // 3. Extract embedded images and OCR them with preprocessing
            var imageParts = document.MainDocumentPart?.ImageParts ?? [];
            int imageIndex = 0;
            foreach (var imagePart in imageParts)
            {
                try
                {
                    using var imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
                    using var ms = new MemoryStream();
                    imageStream.CopyTo(ms);
                    var imageBytes = ms.ToArray();

                    using var engine = new TesseractEngine(TessDataPath, "eng+vie", EngineMode.Default);
                    using var pix = Pix.LoadFromMemory(imageBytes);

                    // Step 3a: preprocess image for better OCR
                    using var processedPix = PreprocessImageForOcr(pix);

                    // Step 3b: choose PSM mode based on image aspect ratio
                    var isNarrowImage = processedPix.Width < 300;
                    var psm = isNarrowImage ? PageSegMode.SingleBlock : PageSegMode.Auto;

                    string? ocrText = null;
                    using (var page = engine.Process(processedPix, psm))
                    {
                        ocrText = page.GetText();
                    }

                    // Step 3c: fallback if result is too short
                    if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < 10)
                    {
                        using var fallbackPage = engine.Process(processedPix, PageSegMode.SparseText);
                        ocrText = fallbackPage.GetText();
                    }

                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        imageTexts.Add((imageIndex, ocrText.Trim()));
                        text.AppendLine($"[--- Image {imageIndex + 1} ---]");
                        text.AppendLine(ocrText.Trim());
                    }
                }
                catch (Exception imgEx)
                {
                    // Step 4: structured fallback instead of silent fail — user knows which image was skipped
                    text.AppendLine($"[Image {imageIndex + 1} - content not extractable: {imgEx.Message}]");
                }
                imageIndex++;
            }
        }
        catch (Exception ex)
        {
            return $"[DOCX extraction failed: {ex.Message}]";
        }

        var result = text.ToString();

        // 5. If paragraph text is near-empty but we found image text, return image text
        if (imageTexts.Count > 0)
        {
            var markersAndContent = string.Join("\n", imageTexts.Select(t => $"[--- Image {t.Index + 1} ---]\n{t.Content}"));
            if (!string.IsNullOrEmpty(markersAndContent))
            {
                var paragraphText = result.Replace(markersAndContent, "").Trim();
                if (string.IsNullOrWhiteSpace(paragraphText))
                {
                    return text.ToString();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Preprocesses a Pix image for optimal OCR accuracy.
    /// - Converts to 8bpp grayscale (required by binarization methods).
    /// - Upscales small images (width &lt; 200 or height &lt; 100) by 3-4x to meet Tesseract's minimum DPI requirement.
    /// - Applies Sauvola adaptive binarization to improve contrast on diagram-style images.
    /// </summary>
    private static Pix PreprocessImageForOcr(Pix pix)
    {
        // BinarizeSauvola requires 8bpp grayscale; convert first if needed
        if (pix.Depth != 8)
        {
            pix = pix.ConvertRGBToGray();
        }

        var width = pix.Width;
        var height = pix.Height;

        // Upscale small images to meet Tesseract's minimum pixel-density requirement
        if (width < 200 || height < 100)
        {
            var scaleFactor = Math.Max(3.0f, Math.Min(4.0f,
                (float)Math.Ceiling(200.0 / Math.Min(width, height)) + 1));
            pix = pix.Scale(scaleFactor, scaleFactor);
        }

        // Sauvola adaptive binarization — ideal for black-on-white diagram images
        // Uses a window half-size of 10 and factor 0.35 (standard for document images)
        pix = pix.BinarizeSauvola(whsize: 10, factor: 0.35f, addborder: false);

        return pix;
    }

    private static string? OcrFixGarbledVietnamese(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var nonAscii = text.Count(c => c > 127 || char.IsLetter(c));
        if (nonAscii == 0) return text;

        var weirdCount = text.Count(c =>
            (c >= 0x0080 && c <= 0x00FF && c != 'é' && c != 'è' && c != 'ê' && c != 'à' && c != 'â' && c != 'á' && c != 'ã' && c != 'ả' && c != 'ạ' &&
             c != 'đ' && c != 'í' && c != 'ì' && c != 'ỉ' && c != 'ĩ' && c != 'ị' &&
             c != 'ó' && c != 'ò' && c != 'ô' && c != 'õ' && c != 'ỏ' && c != 'ọ' && c != 'ơ' && c != 'ư' &&
             c != 'ú' && c != 'ù' && c != 'ủ' && c != 'ũ' && c != 'ụ' && c != 'ừ' && c != 'ứ' && c != 'ự' && c != 'ử' && c != 'ữ' && c != 'ư' &&
             c != 'ợ' && c != 'ỏ' && c != 'ạ' && c != 'ả' && c != 'ấ' && c != 'ầ' && c != 'ẩ' && c != 'ẫ' && c != 'ậ' &&
             c != 'ắ' && c != 'ằ' && c != 'ẳ' && c != 'ẵ' && c != 'ặ') ||
            c == '\0');

        if (weirdCount > nonAscii * 0.4)
        {
            // Likely CID-font / glyph-map garbling — fall back to OCR
            return null;
        }
        return text;
    }

    private static string ExtractTextFromPdf(byte[] fileContent)
    {
        var text = new StringBuilder();
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var document = PdfDocument.Open(stream);

            int pageNum = 1;
            foreach (var page in document.GetPages())
            {
                text.AppendLine($"[--- Page {pageNum} ---]");
                text.AppendLine(page.Text);
                pageNum++;
            }

            var extractedText = text.ToString().Trim();

            // If text layer is empty or garbled, fall back to OCR
            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
            {
                var ocrText = ExtractTextFromPdfViaOcr(fileContent);
                if (!string.IsNullOrWhiteSpace(ocrText))
                    return ocrText;
                return extractedText;
            }

            // If text appears garbled (CID-font Vietnamese), use OCR instead
            var fixedText = OcrFixGarbledVietnamese(extractedText);
            if (fixedText == null)
            {
                var ocrText = ExtractTextFromPdfViaOcr(fileContent);
                if (!string.IsNullOrWhiteSpace(ocrText))
                    return ocrText;
            }

            return fixedText ?? extractedText;
        }
        catch (Exception ex)
        {
            text.AppendLine($"[PDF extraction failed: {ex.Message}]");
        }

        return text.ToString();
    }

    private static string GetParagraphText(OpenXmlElement element)
    {
        var sb = new StringBuilder();
        foreach (var text in element.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
        {
            sb.Append(text.Text);
        }
        return sb.ToString();
    }

    private static string ExtractTableFromDocx(Table table)
    {
        var sb = new StringBuilder();
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return string.Empty;

        var headerCells = rows[0].Elements<TableCell>().Select(GetCellText).ToList();
        sb.AppendLine("| " + string.Join(" | ", headerCells) + " |");
        sb.AppendLine("| " + string.Join(" | ", headerCells.Select(_ => "---")) + " |");

        foreach (var row in rows.Skip(1))
        {
            var cells = row.Elements<TableCell>().Select(GetCellText);
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }
        return sb.ToString();
    }

    private static string GetCellText(TableCell cell)
    {
        return cell.Elements<Paragraph>()
            .Select(p => GetParagraphText(p))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .FirstOrDefault() ?? "";
    }

    private static string CleanText(string text)
    {
        text = Regex.Replace(text, @"\r\n|\r", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static bool IsMarkdownTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 2 && trimmed[0] == '|' && trimmed[trimmed.Length - 1] == '|';
    }

    private static IEnumerable<string> SplitPreservingTables(string text)
    {
        var lines = text.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var trimmed = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("[---"))
            {
                yield return lines[i];
                i++;
                continue;
            }

            if (IsMarkdownTableRow(lines[i]))
            {
                var blockLines = new List<string>();

                while (i < lines.Length && IsMarkdownTableRow(lines[i]))
                {
                    blockLines.Add(lines[i]);
                    i++;
                }

                yield return string.Join("\n", blockLines);
            }
            else
            {
                yield return lines[i];
                i++;
            }
        }
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = Regex.Split(text, @"(?<![A-Za-z]\.)(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(text))
            sentences.Add(text);

        return sentences;
    }

    private static IEnumerable<string> SplitLongSentence(string sentence, int chunkSize)
    {
        if (sentence.Length <= chunkSize)
            return new[] { sentence };

        var parts = Regex.Split(sentence, @"(?<=[,;])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (parts.Count > 1)
        {
            var result = new List<string>();
            foreach (var part in parts)
            {
                if (part.Length <= chunkSize)
                    result.Add(part);
                else
                    result.AddRange(WrapWords(part, chunkSize));
            }
            return result;
        }

        return WrapWords(sentence, chunkSize);
    }

    private static IEnumerable<string> WrapWords(string text, int maxLength)
    {
        var words = text.Split(' ');
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxLength && current.Length > 0)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            yield return last;
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        try
        {
            var testString = Encoding.UTF8.GetString(bytes);
            if (!testString.Contains('\0'))
                return Encoding.UTF8;
        }
        catch { }

        return Encoding.Default;
    }

    // ══════════════════════════════════════════════════════════════════════
// METHOD 1: OCR cho ẢNH THUẦN TÚY (.jpg/.png/.gif/...)
// Input: bytes ảnh → Output: text nhận diện được
// ══════════════════════════════════════════════════════════════════════
private static string ExtractTextFromImage(byte[] fileContent, string extension)
{
    var text = new StringBuilder();

    try
    {
        using var engine = new TesseractEngine(TessDataPath, "eng+vie", EngineMode.Default);

        using var img = Pix.LoadFromMemory(fileContent);
        using var page = engine.Process(img, PageSegMode.Auto);

        text.Append(page.GetText());

        var confidence = page.GetMeanConfidence();
        Console.WriteLine($"[OCR-Image] Extension={extension}, "
            + $"Confidence={confidence:P2}, TextLength={text.Length}");

        if (text.Length == 0)
        {
            Console.WriteLine("[OCR-Image] WARNING: No text extracted. "
                + "Image may be empty, blurry, or contain only graphics.");
        }
    }
    catch (Exception ex)
    {
        text.AppendLine($"[OCR image extraction failed: {ex.Message}]");
        Console.WriteLine($"[OCR-Image] ERROR: {ex}");
    }

    return text.ToString();
}

    // ══════════════════════════════════════════════════════════════════════
// METHOD 2: OCR cho PDF SCAN (PDF chứa ảnh thay vì text layer)
// Input: bytes PDF → Output: text từ tất cả trang
// Flow: PDF → PdfPig.Skia render page → SKBitmap → Tesseract OCR
// ══════════════════════════════════════════════════════════════════════
private static string ExtractTextFromPdfViaOcr(byte[] fileContent)
{
    var allText = new StringBuilder();

    try
    {
        using var pdfStream = new MemoryStream(fileContent);
        using var pdfDocument = PdfDocument.Open(pdfStream, SkiaRenderingParsingOptions.Instance);

        pdfDocument.AddSkiaPageFactory();

        var pageCount = pdfDocument.NumberOfPages;
        Console.WriteLine($"[PDF-OCR] Rendering {pageCount} pages...");

        const int dpi = 300;
        float scale = dpi / 72f;

        using var engine = new TesseractEngine(TessDataPath, "eng+vie", EngineMode.Default);

        for (int pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            try
            {
                using var skBitmap = pdfDocument.GetPageAsSKBitmap(pageIndex, scale, SKColors.White);

                using var pngMs = new MemoryStream();
                skBitmap.Encode(pngMs, SKEncodedImageFormat.Png, 100);
                pngMs.Position = 0;
                using var pix = Pix.LoadFromMemory(pngMs.ToArray());
                using var page = engine.Process(pix, PageSegMode.Auto);
                var pageText = page.GetText();

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    allText.AppendLine($"[--- Page {pageIndex} ---]");
                    allText.AppendLine(pageText);
                }

                Console.WriteLine($"[PDF-OCR] Page {pageIndex}/{pageCount}: "
                    + $"{pageText.Length} chars extracted");
            }
            catch (Exception pageEx)
            {
                Console.WriteLine($"[PDF-OCR] Failed on page {pageIndex}: {pageEx.Message}");
            }
        }

        Console.WriteLine($"[PDF-OCR] Total extracted: {allText.Length} chars from {pageCount} pages");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[PDF-OCR] FATAL: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }

    return allText.ToString();
}
    public bool IsScannedPdf(byte[] fileContent)
{
    try
    {
        using var stream = new MemoryStream(fileContent);
        using var document = PdfDocument.Open(stream);
        var page = document.GetPages().FirstOrDefault();
        // Nếu page đầu tiên có ít hơn 50 ký tự text → coi là scanned
        return string.IsNullOrWhiteSpace(page?.Text) || page.Text.Length < 50;
    }
    catch
    {
        return false;
    }
}
}

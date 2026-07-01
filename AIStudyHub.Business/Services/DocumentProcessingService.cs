using System.Text;
using System.Text.RegularExpressions;
using AIStudyHub.Business.Interfaces.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using Tesseract;
using PdfiumViewer;
using System.Drawing;
using System.Drawing.Imaging;
using PdfDocument = UglyToad.PdfPig.PdfDocument;
using PdfiumDocument = PdfiumViewer.PdfDocument;
using ImageFormat = System.Drawing.Imaging.ImageFormat;


namespace AIStudyHub.Business.Services;

public sealed class DocumentProcessingService : IDocumentProcessingService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx", ".jpg", ".png", ".jpeg", ".webp", ".gif"
    };

    private static readonly string TessDataPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? @"C:\Program Files\Tesseract-OCR\tessdata";

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

    public Task<List<string>> ChunkTextAsync(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var cleanText = CleanText(text);

        if (string.IsNullOrWhiteSpace(cleanText))
            return Task.FromResult(chunks);

        var sentences = SplitIntoSentences(cleanText);
        var currentChunk = new StringBuilder();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;

            if (currentLength + sentenceLength > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                currentLength = 0;

                if (overlap > 0 && chunks.Count > 0)
                {
                    var lastChunk = chunks.Last();
                    var overlapChars = lastChunk.Length > overlap ? lastChunk[^overlap..] : lastChunk;
                    currentChunk.Append(overlapChars + " ");
                    currentLength = overlapChars.Length + 1;
                }
            }

            currentChunk.Append(sentence).Append(" ");
            currentLength += sentenceLength + 1;
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString().Trim());

        return Task.FromResult(chunks);
    }

    private static async Task<string> ExtractTextFromTxtAsync(byte[] fileContent)
    {
        var encoding = DetectEncoding(fileContent);
        return await Task.Run(() => encoding.GetString(fileContent));
    }

    private static string ExtractTextFromPdf(byte[] fileContent)
    {
        var text = new StringBuilder();
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var document = PdfDocument.Open(stream);

            foreach (var page in document.GetPages())
            {
                text.AppendLine(page.Text);
            }

            var extractedText = text.ToString().Trim();

            if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
            {
                var ocrText = ExtractTextFromPdfViaOcr(fileContent);
                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    return ocrText;
                }
                return extractedText;
            }
        }
        catch (Exception ex)
        {
            text.AppendLine($"[PDF extraction failed: {ex.Message}]");
        }

        return text.ToString();
    }

    private static string ExtractTextFromDocx(byte[] fileContent)
    {
        var text = new StringBuilder();
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var document = WordprocessingDocument.Open(stream, false);

            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
                return string.Empty;

            foreach (var element in body.Elements())
            {
                var paraText = GetParagraphText(element);
                if (!string.IsNullOrWhiteSpace(paraText))
                {
                    text.AppendLine(paraText);
                }
            }
        }
        catch (Exception ex)
        {
            return $"[DOCX extraction failed: {ex.Message}]";
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

    private static string CleanText(string text)
    {
        text = Regex.Replace(text, @"\r\n|\r", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(text))
            sentences.Add(text);

        return sentences;
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
// Flow: PDF → PdfiumViewer render page → Bitmap → Tesseract OCR
// ══════════════════════════════════════════════════════════════════════
private static string ExtractTextFromPdfViaOcr(byte[] fileContent)
{
    var allText = new StringBuilder();

    try
    {
        using var pdfStream = new MemoryStream(fileContent);
        using var pdfDocument = PdfiumDocument.Load(pdfStream);

        var pageCount = pdfDocument.PageCount;
        Console.WriteLine($"[PDF-OCR] Rendering {pageCount} pages...");

        const int dpi = 300;

        using var engine = new TesseractEngine(TessDataPath, "eng+vie", EngineMode.Default);

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            try
            {
                using var image = pdfDocument.Render(pageIndex, dpi, dpi, false);
                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                using var img = Pix.LoadFromMemory(ms.ToArray());
                using var page = engine.Process(img, PageSegMode.Auto);
                var pageText = page.GetText();

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    allText.AppendLine($"[--- Page {pageIndex + 1} ---]");
                    allText.AppendLine(pageText);
                }

                Console.WriteLine($"[PDF-OCR] Page {pageIndex + 1}/{pageCount}: "
                    + $"{pageText.Length} chars extracted");
            }
            catch (Exception pageEx)
            {
                Console.WriteLine($"[PDF-OCR] Failed on page {pageIndex + 1}: {pageEx.Message}");
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

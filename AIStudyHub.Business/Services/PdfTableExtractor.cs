using System.Text;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Rendering.Skia;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Extracts tables from PDF pages using a two-stage cascade:
/// <list type="number">
///   <item>
///     <term>Stage 1 — Y-cluster word positions</term>
///     Groups PdfPig words by their top-Y (row) and detects column boundaries
///     from the largest horizontal gaps. Best for whitespace-aligned tables
///     in digitally-created PDFs (~70% of academic PDFs).
///   </item>
///   <item>
///     <term>Stage 2 — OCR fallback</term>
///     When the page has no text layer (scanned PDF), renders it to an image,
///     runs Tesseract OCR, then applies the same Y-cluster logic on the
///     recognized word coordinates. Best for scanned tables (~65% accuracy).
///   </item>
/// </list>
/// All output is markdown pipe-table format so the existing
/// <c>SplitPreservingTables</c> and <c>ParsePageSegments</c> in
/// <c>DocumentProcessingService</c> work unchanged.
/// </summary>
internal sealed class PdfTableExtractor
{
    /// <summary>
    /// Returns all tables detected on the given page, each as rows of cell strings.
    /// </summary>
    public static IReadOnlyList<List<string>> Extract(
        PdfDocument document,
        int pageIndex,
        string tessDataPath)
    {
        try
        {
            var page = document.GetPage(pageIndex);

            // Stage 1: Y-cluster on text layer words
            var ws = ExtractWhitespaceTable(page);
            if (ws.Count > 0)
                return ws;

            // Stage 2: OCR fallback — only for pages with no text layer
            if (!HasTextLayer(page))
            {
                var ocr = ExtractTablesFromOcr(document, pageIndex, tessDataPath);
                if (ocr.Count > 0)
                    return ocr;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfTableExtractor] Stage failed: {ex.Message}");
        }

        return [];
    }

    /// <summary>
    /// Extracts tables from a single pre-rendered PDF page image using OCR.
    /// Used by the OCR fallback path in DocumentProcessingService when the PDF
    /// text layer is absent or garbled.
    /// </summary>
    /// <param name="imageBytes">PNG bytes of the rendered page.</param>
    /// <param name="tessDataPath">Path to the Tesseract tessdata directory.</param>
    public static IReadOnlyList<List<string>> ExtractFromOcrPage(
        byte[] imageBytes,
        string tessDataPath)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            return [];

        var ocrWords = OcrWordBoxes(imageBytes, tessDataPath);
        if (ocrWords == null || ocrWords.Count < 6)
            return [];

        return ClusterRowsIntoTable(ocrWords);
    }

    /// <summary>Converts rows of cell strings into a markdown pipe table string.</summary>
    internal static string ToMarkdown(IReadOnlyList<List<string>> rows)
    {
        if (rows.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var header = rows[0];
        sb.AppendLine("| " + string.Join(" | ", header) + " |");
        sb.AppendLine("| " + string.Join(" | ", header.Select(_ => "---")) + " |");

        foreach (var row in rows.Skip(1))
        {
            sb.AppendLine("| " + string.Join(" | ", row) + " |");
        }
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Stage 1 — Y-cluster words (whitespace / digitally-created PDFs)
    // ──────────────────────────────────────────────────────────────────────────

    private static List<List<string>> ExtractWhitespaceTable(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords()
            .Select(w => new WordPos(
                w.Text,
                w.BoundingBox.Left,
                w.BoundingBox.Right,
                w.BoundingBox.Bottom,
                w.BoundingBox.Top))
            .OrderByDescending(w => w.Y2)
            .ThenBy(w => w.X1)
            .ToList();

        if (words.Count < 6)
            return [];

        return ClusterRowsIntoTable(words);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Stage 2 — OCR fallback (scanned PDFs)
    // ──────────────────────────────────────────────────────────────────────────

    private static List<List<string>> ExtractTablesFromOcr(
        PdfDocument document,
        int pageIndex,
        string tessDataPath)
    {
        // Ensure Skia page factory is registered (idempotent if already registered)
        document.AddSkiaPageFactory();

        byte[]? imageBytes = null;
        try
        {
            // Render page at 300 DPI — same approach as ExtractTextFromPdfViaOcr (line 751)
            float scale = 300f / 72f;
            using var skBitmap = document.GetPageAsSKBitmap(pageIndex, scale, SKColors.White);
            using var pngMs = new MemoryStream();
            skBitmap.Encode(pngMs, SKEncodedImageFormat.Png, 100);
            imageBytes = pngMs.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfTableExtractor] Page render failed: {ex.Message}");
            return [];
        }

        if (imageBytes == null || imageBytes.Length == 0)
            return [];

        List<WordPos>? ocrWords = null;
        try
        {
            ocrWords = OcrWordBoxes(imageBytes, tessDataPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfTableExtractor] OCR word-box extraction failed: {ex.Message}");
        }

        if (ocrWords == null || ocrWords.Count < 6)
            return [];

        return ClusterRowsIntoTable(ocrWords);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared Y-cluster logic
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Detects table rows from word positions and splits each row into columns
    /// using the largest inter-word gaps as boundary markers.
    /// Requires at least 3 rows with a consistent column count to accept a candidate.
    /// </summary>
    private static List<List<string>> ClusterRowsIntoTable(List<WordPos> words)
    {
        // 1. Detect row boundaries from gaps between consecutive Y baselines
        var baselines = words
            .Select(w => w.Y2)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        var gaps = baselines
            .Zip(baselines.Skip(1), (a, b) => a - b)
            .Where(g => g > 0)
            .ToList();

        var avgGap = gaps.Count > 0 ? gaps.Average() : 12.0;

        var rows = new List<List<WordPos>>();
        var currentRow = new List<WordPos>();
        double currentY = double.NaN;

        foreach (var w in words)
        {
            if (double.IsNaN(currentY) || Math.Abs(w.Y2 - currentY) > avgGap * 0.8)
            {
                if (currentRow.Count > 0)
                    rows.Add(currentRow);
                currentRow = [];
                currentY = w.Y2;
            }
            currentRow.Add(w);
        }
        if (currentRow.Count > 0)
            rows.Add(currentRow);

        if (rows.Count < 3)
            return [];

        // 2. Detect column boundaries from the largest horizontal gaps
        var allGaps = rows
            .Select(r => r.OrderBy(w => w.X1).ToList())
            .Where(r => r.Count >= 2)
            .SelectMany(r => r
                .Zip(r.Skip(1), (a, b) => b.X1 - a.X2)
                .Where(g => g > 0))
            .OrderByDescending(g => g)
            .ToList();

        if (allGaps.Count == 0)
            return [];

        // Use the 3rd-largest gap as threshold — large enough to ignore normal
        // word spacing but small enough to catch column breaks
        var threshold = allGaps[Math.Min(3, allGaps.Count - 1)];

        // 3. Split each row into cells
        var rows2D = new List<List<string>>();
        foreach (var row in rows)
        {
            var sorted = row.OrderBy(w => w.X1).ToList();
            var cells = new List<string>();
            var current = new List<string>();
            double lastX2 = double.MinValue;

            foreach (var w in sorted)
            {
                if (current.Count == 0 || (w.X1 - lastX2) < threshold)
                {
                    current.Add(w.Text);
                }
                else
                {
                    cells.Add(string.Join(" ", current));
                    current = [w.Text];
                }
                lastX2 = w.X2;
            }
            if (current.Count > 0)
                cells.Add(string.Join(" ", current));

            if (cells.Count >= 2)
                rows2D.Add(cells);
        }

        if (rows2D.Count < 3)
            return [];

        // 4. Require consistent column count across at least 3 rows
        var dominant = rows2D
            .GroupBy(r => r.Count)
            .OrderByDescending(g => g.Count())
            .First();

        return dominant.Count() >= 3
            ? dominant.ToList()
            : [];
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static bool HasTextLayer(UglyToad.PdfPig.Content.Page page) =>
        !string.IsNullOrWhiteSpace(page.Text) && page.Text.Length >= 50;

    private static List<WordPos> OcrWordBoxes(byte[] imageBytes, string tessDataPath)
    {
        var words = new List<WordPos>();

        using var engine = new TesseractEngine(tessDataPath, "eng+vie", EngineMode.Default);
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var tesseractPage = engine.Process(pix, PageSegMode.AutoOsd);

        using var iter = tesseractPage.GetIterator();
        iter.Begin();
        do
        {
            if (iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
            {
                var text = iter.GetText(PageIteratorLevel.Word)?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    // Tesseract: rect.X1 = left, rect.Y1 = top (origin top-left)
                    // PdfPig word Y2 = top, Y1 = bottom
                    words.Add(new WordPos(text, rect.X1, rect.X2, rect.Y1, rect.Y2));
                }
            }
        } while (iter.Next(PageIteratorLevel.Word));

        return words;
    }

    private sealed record WordPos(string Text, double X1, double X2, double Y1, double Y2);
}

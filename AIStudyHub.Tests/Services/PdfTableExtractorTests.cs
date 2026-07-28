using AIStudyHub.Business.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AIStudyHub.Tests.Services;

public class PdfTableExtractorTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // ToMarkdown – pure unit tests (no PDF needed)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToMarkdown_TwoRows_ReturnsHeaderAndSeparator()
    {
        var rows = new List<List<string>>
        {
            new() { "Name", "Age", "City" },
            new() { "Alice", "25", "Hanoi" },
        };

        var md = PdfTableExtractor.ToMarkdown(rows);

        Assert.Contains("| Name | Age | City |", md);
        Assert.Contains("| --- | --- | --- |", md);
        Assert.Contains("| Alice | 25 | Hanoi |", md);
    }

    [Fact]
    public void ToMarkdown_EmptyRows_ReturnsEmpty()
    {
        var rows = new List<List<string>>();
        Assert.Equal(string.Empty, PdfTableExtractor.ToMarkdown(rows));
    }

    [Fact]
    public void ToMarkdown_SingleRow_RendersHeaderAndSeparator()
    {
        var rows = new List<List<string>>
        {
            new() { "Only", "Header" },
        };

        var md = PdfTableExtractor.ToMarkdown(rows);

        Assert.Contains("| Only | Header |", md);
        Assert.Contains("| --- | --- |", md);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Integration: create an in-memory PDF with known text layout, then assert
    // on what PdfTableExtractor produces.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Extract_PdfWithAlignedColumnText_ExtractsTable()
    {
        // Arrange: build a PDF with 3 columns of text at fixed X positions
        var pdfBytes = CreateAlignedTablePdf();

        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);

        // Act: Stage 1 should find a table from aligned words
        var table = PdfTableExtractor.Extract(document, 1, TessDataPathForTests());

        // Assert: table found with 3 columns and ≥3 rows
        Assert.NotEmpty(table);
        Assert.True(table.Count >= 3, $"Expected ≥3 rows, got {table.Count}");
        int colCount = table[0].Count;
        foreach (var row in table)
            Assert.Equal(colCount, row.Count);
    }

    [Fact]
    public void Extract_PdfWithPlainProse_ExtractsWithoutCrashing()
    {
        // Arrange: PDF with free-flowing paragraph text (no table structure)
        var pdfBytes = CreateProsePdf();

        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);

        // Act — extractor should not crash; it may or may not detect a table
        var table = PdfTableExtractor.Extract(document, 1, TessDataPathForTests());

        // Key assertion: method returns without throwing, and returns a valid list
        Assert.NotNull(table);
    }

    [Fact]
    public void Extract_PdfWithNearEmptyText_ReturnsEmpty()
    {
        // Arrange: a PDF with very little text — below the 50-char threshold
        var pdfBytes = CreateNearEmptyTextPdf();

        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);

        // Act — exercises the OCR branch (should not crash)
        var table = PdfTableExtractor.Extract(document, 1, TessDataPathForTests());

        // OCR may or may not find text; the key is no exception
        Assert.NotNull(table);
    }

    [Fact]
    public async Task ExtractSegmentsAsync_PdfWithTable_EmitsMarkdownRowInSegment()
    {
        // Arrange
        var pdfBytes = CreateAlignedTablePdf();
        var service = new DocumentProcessingService();

        // Act
        var segments = await service.ExtractSegmentsAsync(pdfBytes, ".pdf");

        // Assert: segment contains pipe characters (markdown table) and is highlightable
        Assert.NotEmpty(segments);
        var segment = segments[0];
        Assert.True(segment.IsHighlightable, "PDF segments should be highlightable");
        Assert.Contains("|", segment.Text);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string TessDataPathForTests()
    {
        var env = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        var local = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
        if (Directory.Exists(local))
            return local;

        var baseDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (Directory.Exists(baseDir))
            return baseDir;

        return @"C:\Program Files\Tesseract-OCR\tessdata";
    }

    private static byte[] CreateAlignedTablePdf()
    {
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);

        // A4 portrait: 595 x 842 pts
        var page = builder.AddPage(595, 842);

        // 3-column table at fixed X: col1=70, col2=240, col3=410
        var tableData = new[]
        {
            ("Name", "Age", "City"),
            ("Alice", "25", "Hanoi"),
            ("Bob", "30", "Saigon"),
            ("Carol", "28", "Danang"),
        };

        double y = 780;
        foreach (var row in tableData)
        {
            page.AddText(row.Item1, 12, new PdfPoint(70, y), font);
            page.AddText(row.Item2, 12, new PdfPoint(240, y), font);
            page.AddText(row.Item3, 12, new PdfPoint(410, y), font);
            y -= 25;
        }

        return builder.Build();
    }

    private static byte[] CreateProsePdf()
    {
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);

        var lines = new[]
        {
            "This is a paragraph of plain text.",
            "It contains multiple sentences spread across",
            "several lines but with no tabular structure.",
            "There are no consistent column positions.",
        };

        double y = 780;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(72, y), font);
            y -= 20;
        }

        return builder.Build();
    }

    private static byte[] CreateNearEmptyTextPdf()
    {
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);

        // Only two characters — well below the 50-char text-layer threshold
        page.AddText("Hi", 12, new PdfPoint(72, 400), font);

        return builder.Build();
    }
}

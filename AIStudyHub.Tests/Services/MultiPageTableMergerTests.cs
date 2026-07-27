using AIStudyHub.Business.Services;

namespace AIStudyHub.Tests.Services;

public class MultiPageTableMergerTests
{
    // Represents "no table on this page" — an empty list (not a list with one empty row).
    private static IReadOnlyList<List<string>> NoTable => Array.Empty<List<string>>();

    private static IReadOnlyList<List<string>> MakeTable(params string[][] rows) =>
        rows.Select(r => r.ToList()).ToList().AsReadOnly();

    // ──────────────────────────────────────────────────────────────────────────
    // MergeFromOcrPages – public entry point used by both PDF paths
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MergeFromOcrPages_TwoPagesMatchingHeader_MergesIntoOneTable()
    {
        // Two pages with identical header: rows are [header + body1] and [header + body2].
        // Expected: one MergedTable spanning pages 1–2 with 3 rows (header deduplicated).
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Code", "Description"], ["BR-01", "Item 1"]),
            MakeTable(["Code", "Description"], ["BR-53", "Item 53"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Single(result);
        Assert.Equal(1, result[0].StartPage);
        Assert.Equal(2, result[0].EndPage);
        Assert.Equal(3, result[0].Rows.Count); // header + 2 body rows
        Assert.Equal("Code", result[0].Rows[0][0]);  // header preserved
        Assert.Equal("BR-01", result[0].Rows[1][0]); // page 1 body
        Assert.Equal("BR-53", result[0].Rows[2][0]);  // page 2 body (header not duplicated)
    }

    [Fact]
    public void MergeFromOcrPages_ThreePagesMatchingHeader_MergesAllThree()
    {
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Col A", "Col B"], ["A1", "B1"]),
            MakeTable(["Col A", "Col B"], ["A2", "B2"]),
            MakeTable(["Col A", "Col B"], ["A3", "B3"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Single(result);
        Assert.Equal(1, result[0].StartPage);
        Assert.Equal(3, result[0].EndPage);
        Assert.Equal(4, result[0].Rows.Count); // header + 3 body rows
    }

    [Fact]
    public void MergeFromOcrPages_MiddlePageHasNoTable_SplitsAroundGap()
    {
        // Pages 1 and 3 have matching headers but are separated by a page with no table.
        // Per the current implementation they are NOT merged (different table contexts).
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Code", "Desc"], ["BR-01", "Item 1"]),
            NoTable,
            MakeTable(["Code", "Desc"], ["BR-53", "Item 53"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        // Log count for diagnostics
        Assert.True(result.Count == 2, $"Expected Count=2 but got Count={result.Count}");
        Assert.Equal(1, result[0].StartPage); Assert.Equal(1, result[0].EndPage);
        Assert.Equal(2, result[0].Rows.Count); // header + 1 body

        Assert.Equal(3, result[1].StartPage); Assert.Equal(3, result[1].EndPage);
        Assert.Equal(2, result[1].Rows.Count); // header + 1 body
    }

    [Fact]
    public void MergeFromOcrPages_DifferentColumnCount_DoesNotMerge()
    {
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Col A", "Col B"], ["A1", "B1"]),
            MakeTable(["Col A", "Col B", "Col C"], ["A2", "B2", "C2"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].StartPage); Assert.Equal(1, result[0].EndPage);
        Assert.Equal(2, result[1].StartPage); Assert.Equal(2, result[1].EndPage);
    }

    [Fact]
    public void MergeFromOcrPages_AllPagesEmpty_ReturnsEmptyList()
    {
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            NoTable, NoTable, NoTable,
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Empty(result);
    }

    [Fact]
    public void MergeFromOcrPages_HeaderCaseInsensitive_Merges()
    {
        // ASCII headers differing only in case should merge.
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Product ID", "Name"], ["P-01", "Alpha"]),
            MakeTable(["product id", "name"], ["P-02", "Beta"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Single(result);
        Assert.Equal(3, result[0].Rows.Count); // header + 2 body rows
    }

    [Fact]
    public void MergeFromOcrPages_WhitespaceInHeader_StillMerges()
    {
        // ASCII headers with leading/trailing whitespace should still merge.
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["  Product ID  ", "  Name  "], ["P-01", "Alpha"]),
            MakeTable(["Product ID", "Name"], ["P-02", "Beta"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Single(result);
        Assert.Equal(3, result[0].Rows.Count);
    }

    [Fact]
    public void MergeFromOcrPages_ThreeGroupsOfMerges_ThreeSeparateResults()
    {
        // Group 1: pages 1-2 share header A
        // Page 3: different header B
        // Group 2: pages 4-5 share header B
        // Page 6: header C
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["HeaderA", "X"], ["A1", "x1"]),
            MakeTable(["HeaderA", "X"], ["A2", "x2"]),
            MakeTable(["HeaderB", "Y"], ["B1", "y1"]),
            MakeTable(["HeaderB", "Y"], ["B2", "y2"]),
            MakeTable(["HeaderB", "Y"], ["B3", "y3"]),
            MakeTable(["HeaderC", "Z"], ["C1", "z1"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Equal(3, result.Count);

        Assert.Equal(1,  result[0].StartPage); Assert.Equal(2,  result[0].EndPage);
        Assert.Equal(3,  result[0].Rows.Count); // headerA + 2 body

        Assert.Equal(3,  result[1].StartPage); Assert.Equal(5,  result[1].EndPage);
        Assert.Equal(4,  result[1].Rows.Count); // headerB + 3 body

        Assert.Equal(6,  result[2].StartPage); Assert.Equal(6,  result[2].EndPage);
        Assert.Equal(2,  result[2].Rows.Count); // headerC + 1 body
    }

    [Fact]
    public void MergeFromOcrPages_SinglePageWithTable_ReturnsOneTableSpanningOnePage()
    {
        var perPage = new List<IReadOnlyList<List<string>>>
        {
            MakeTable(["Col A", "Col B"], ["A1", "B1"]),
        };

        var result = MultiPageTableMerger.MergeFromOcrPages(perPage);

        Assert.Single(result);
        Assert.Equal(1, result[0].StartPage);
        Assert.Equal(1, result[0].EndPage);
        Assert.Equal(2, result[0].Rows.Count); // header + 1 body
    }
}

using System.Text;

namespace AIStudyHub.Business.Services;

/// <summary>
/// Holds a table that spans one or more consecutive PDF pages after merging
/// duplicate headers from continuation pages.
/// </summary>
public sealed class MergedTable
{
    public int StartPage { get; init; }
    public int EndPage { get; init; }
    public IReadOnlyList<List<string>> Rows { get; init; } = [];
}

/// <summary>
/// Merges per-page table extractions into contiguous, multi-page tables.
/// Removes duplicate header rows from continuation pages so the AI sees a
/// single table instead of N tables with identical headers.
/// </summary>
internal static class MultiPageTableMerger
{
    /// <summary>
    /// Extracts tables from every page of a PdfPig document using the
    /// text-layer extractor, then merges consecutive pages whose tables share
    /// the same column count and header row text.
    /// </summary>
    public static List<MergedTable> MergeAllTablesAcrossPages(
        UglyToad.PdfPig.PdfDocument document,
        string tessDataPath)
    {
        var pageCount = document.NumberOfPages;
        var perPageTables = new List<IReadOnlyList<List<string>>>(pageCount);

        for (int i = 1; i <= pageCount; i++)
        {
            var rows = PdfTableExtractor.Extract(document, i, tessDataPath);
            perPageTables.Add(rows);
        }

        return MergeSequences(perPageTables);
    }

    /// <summary>
    /// Merges pre-extracted per-page table rows. Used by the OCR path where
    /// <paramref name="perPageTables"/> is built from image-based extraction.
    /// </summary>
    public static List<MergedTable> MergeFromOcrPages(
        IReadOnlyList<IReadOnlyList<List<string>>> perPageTables)
    {
        return MergeSequences(perPageTables);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core merge logic
    // ─────────────────────────────────────────────────────────────────────────

    private static List<MergedTable> MergeSequences(
        IReadOnlyList<IReadOnlyList<List<string>>> perPageTables)
    {
        var result = new List<MergedTable>();

        var openGroup = new GroupAcc();

        for (int pageIdx = 0; pageIdx < perPageTables.Count; pageIdx++)
        {
            var rows = perPageTables[pageIdx];

            if (rows.Count == 0)
            {
                // No table on this page — close any open group.
                if (openGroup.IsOpen)
                {
                    FlushGroup(openGroup, result);
                    openGroup.IsOpen = false;
                }
                continue;
            }

            var headerKey = HeaderKey(rows[0]);

            if (!openGroup.IsOpen)
            {
                // Start a new group.
                openGroup.IsOpen = true;
                openGroup.StartPage = pageIdx + 1;
                openGroup.EndPage = pageIdx + 1;
                openGroup.Rows = rows.Select(r => new List<string>(r)).ToList();
            }
            else if (HeaderKey(openGroup.Rows[0]) == headerKey)
            {
                // Same table — append body rows (skip duplicate header) and extend range.
                openGroup.EndPage = pageIdx + 1;
                openGroup.Rows.AddRange(rows.Skip(1));
            }
            else
            {
                // Different table — close current and start new.
                FlushGroup(openGroup, result);
                openGroup.IsOpen = true;
                openGroup.StartPage = pageIdx + 1;
                openGroup.EndPage = pageIdx + 1;
                openGroup.Rows = rows.Select(r => new List<string>(r)).ToList();
            }
        }

        if (openGroup.IsOpen)
            FlushGroup(openGroup, result);

        return result;
    }

    private static string HeaderKey(List<string> headerRow)
    {
        return string.Join("|",
            headerRow.Select(c =>
                c.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant()));
    }

    private static void FlushGroup(GroupAcc group, List<MergedTable> result)
    {
        result.Add(new MergedTable
        {
            StartPage = group.StartPage,
            EndPage = group.EndPage,
            Rows = group.Rows.ToList()
        });
    }

    private sealed class GroupAcc
    {
        public bool IsOpen { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public List<List<string>> Rows { get; set; } = [];
    }
}

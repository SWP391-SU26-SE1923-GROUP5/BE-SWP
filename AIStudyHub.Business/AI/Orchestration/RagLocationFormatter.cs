using System.Text;

namespace AIStudyHub.Business.AI.Orchestration;

public static class RagLocationFormatter
{
    private const string LocationHeading = "Vị trí nội dung liên quan trong tài liệu:";
    private const string UnknownPage = "không xác định được trang";
    private const string MixedUnknownPage = "một số đoạn không xác định được trang";

    public static string AppendToAnswer(
        string answer,
        IReadOnlyList<RagContextSource> contexts)
    {
        if (contexts.Count == 0)
            return answer;

        var locations = new StringBuilder();
        locations.Append(LocationHeading);

        foreach (var group in contexts.GroupBy(source => source.DocumentId))
        {
            var pages = group
                .Where(source => source.PageNumber is > 0)
                .Select(source => source.PageNumber!.Value)
                .Distinct()
                .OrderBy(page => page)
                .ToList();

            var hasUnknownPage = group.Any(source => source.PageNumber is null);
            var displayName = group
                .Select(source => source.Result.Source)
                .First(source => !string.IsNullOrWhiteSpace(source));

            locations.AppendLine();
            locations.Append("- ");
            locations.Append(displayName);
            locations.Append(": ");

            if (pages.Count == 0)
            {
                locations.Append(UnknownPage);
                continue;
            }

            locations.Append(string.Join(" và ", BuildRanges(pages).Select(RenderRange)));
            if (hasUnknownPage)
                locations.Append($"; {MixedUnknownPage}");
        }

        return $"{answer.TrimEnd()}\n\n{locations}";
    }

    private static IReadOnlyList<(int Start, int End)> BuildRanges(
        IReadOnlyList<int> pages)
    {
        if (pages.Count == 0)
            return [];

        var ranges = new List<(int Start, int End)>();
        var start = pages[0];
        var end = pages[0];

        foreach (var page in pages.Skip(1))
        {
            if (page == end + 1)
            {
                end = page;
                continue;
            }

            ranges.Add((start, end));
            start = page;
            end = page;
        }

        ranges.Add((start, end));
        return ranges;
    }

    private static string RenderRange((int Start, int End) range) =>
        range.Start == range.End
            ? $"trang {range.Start}"
            : $"trang {range.Start}-{range.End}";
}

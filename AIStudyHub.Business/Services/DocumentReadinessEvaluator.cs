using AIStudyHub.Business.AI;
using AIStudyHub.Business.DTOs.Documents;
using AIStudyHub.Data.Entities;
using AIStudyHub.Data.Enums;

namespace AIStudyHub.Business.Services;

public static class DocumentReadinessEvaluator
{
    public static DocumentReadinessDto Evaluate(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var status = document.Status?.ToString() ?? "Unknown";

        if (document.LifecycleStatus != DocumentLifecycleStatus.Active
            || document.Status is DocumentStatus.Draft
                or DocumentStatus.Archived
                or DocumentStatus.Banned
                or DocumentStatus.Trashed)
        {
            return new(status, false, "Tài liệu không khả dụng cho Chat.", false);
        }

        if (!DocumentRagFilePolicy.SupportsChat(
                document.FileName,
                document.FileExtension))
        {
            return new(status, false, "Loại tài liệu này không hỗ trợ Chat.", false);
        }

        return document.Status switch
        {
            DocumentStatus.Done =>
                new(status, true, "Tài liệu đã sẵn sàng.", false),
            DocumentStatus.Processing =>
                new(status, false, "Tài liệu đang được chuẩn bị.", false),
            DocumentStatus.Failed =>
                new(status, false, "Không thể chuẩn bị tài liệu.", true),
            _ =>
                new(status, false, "Tài liệu chưa sẵn sàng.", false)
        };
    }
}

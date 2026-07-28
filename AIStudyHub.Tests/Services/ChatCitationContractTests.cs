using System.Text.Json;
using AIStudyHub.Business.DTOs.AIChat;

namespace AIStudyHub.Tests.Services;

public sealed class ChatCitationContractTests
{
    [Fact]
    public void ChatCitationDto_WebJson_SeparatesMarkerFromDocumentIdentity()
    {
        var documentId = Guid.NewGuid();
        var dto = new ChatCitationDto(
            1,
            documentId,
            "doc.pdf",
            "exact",
            2,
            0.9,
            "hybrid",
            true,
            null);

        var json = JsonSerializer.Serialize(
            dto,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.Equal(1, root.GetProperty("citationIndex").GetInt32());
        Assert.Equal(documentId, root.GetProperty("documentId").GetGuid());
        Assert.NotEqual("1", root.GetProperty("documentId").GetString());
        Assert.NotEqual(Guid.Empty, root.GetProperty("documentId").GetGuid());
    }
}

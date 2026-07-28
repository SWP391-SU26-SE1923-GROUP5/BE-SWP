using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Tests.Services;

public sealed class ChatMessageCitationConfigurationTests
{
    [Fact]
    public void Model_ConfiguresOrderedCitationSnapshotsAsChatMessageChildren()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options);

        var entity = db.Model.FindEntityType(typeof(ChatMessageCitation));

        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(ChatMessageCitation.ChatMessageId), nameof(ChatMessageCitation.CitationIndex) }));

        var relationship = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(ChatMessage), relationship.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, relationship.DeleteBehavior);
        Assert.DoesNotContain(entity.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Document));
    }
}

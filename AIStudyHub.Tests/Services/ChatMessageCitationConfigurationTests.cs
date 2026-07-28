using AIStudyHub.Data;
using AIStudyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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

    [Fact]
    public void Model_RequiresValidCitationIdentityAndPersistsMessageRelevance()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options);

        var designTimeModel = db.GetService<IDesignTimeModel>().Model;
        var messageEntity = designTimeModel.FindEntityType(typeof(ChatMessage));
        var citationEntity = designTimeModel.FindEntityType(typeof(ChatMessageCitation));

        Assert.NotNull(messageEntity);
        Assert.NotNull(citationEntity);

        var isRelevant = messageEntity!.FindProperty(nameof(ChatMessage.IsRelevant));
        Assert.NotNull(isRelevant);
        Assert.False(isRelevant!.IsNullable);
        Assert.Equal("is_relevant", isRelevant.GetColumnName());
        Assert.Equal(false, isRelevant.GetDefaultValue());

        var citationIndexConstraint = Assert.Single(
            citationEntity!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_ChatMessageCitation_CitationIndex_Positive");
        Assert.Equal(
            "[citation_index] > 0",
            citationIndexConstraint.Sql);
        var documentIdConstraint = Assert.Single(
            citationEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_ChatMessageCitation_DocumentId_NotEmpty");
        Assert.Equal(
            "[document_id] <> '00000000-0000-0000-0000-000000000000'",
            documentIdConstraint.Sql);
    }
}

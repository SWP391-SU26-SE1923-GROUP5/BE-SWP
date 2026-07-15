using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDocumentFileNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @DocumentId uniqueidentifier;
                DECLARE @UserId uniqueidentifier;
                DECLARE @OriginalName nvarchar(255);
                DECLARE @Stem nvarchar(255);
                DECLARE @Extension nvarchar(255);
                DECLARE @Candidate nvarchar(255);
                DECLARE @Suffix nvarchar(32);
                DECLARE @SuffixNumber int;
                DECLARE @ReverseDotPosition int;

                DECLARE duplicate_names CURSOR LOCAL FAST_FORWARD FOR
                    SELECT doc_id, u_id, file_name
                    FROM
                    (
                        SELECT doc_id, u_id, file_name,
                               ROW_NUMBER() OVER (
                                   PARTITION BY u_id, file_name
                                   ORDER BY create_at, doc_id) AS duplicate_number
                        FROM Document
                        WHERE LifecycleStatus = 0 AND file_name IS NOT NULL
                    ) duplicates
                    WHERE duplicate_number > 1
                    ORDER BY u_id, file_name, duplicate_number;

                OPEN duplicate_names;
                FETCH NEXT FROM duplicate_names INTO @DocumentId, @UserId, @OriginalName;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @ReverseDotPosition = CHARINDEX('.', REVERSE(@OriginalName));
                    IF @ReverseDotPosition > 1
                    BEGIN
                        SET @Extension = RIGHT(@OriginalName, @ReverseDotPosition);
                        SET @Stem = LEFT(@OriginalName, LEN(@OriginalName) - @ReverseDotPosition);
                    END
                    ELSE
                    BEGIN
                        SET @Extension = N'';
                        SET @Stem = @OriginalName;
                    END;

                    SET @SuffixNumber = 1;
                    WHILE 1 = 1
                    BEGIN
                        SET @Suffix = N' (' + CONVERT(nvarchar(20), @SuffixNumber) + N')';
                        SET @Candidate = LEFT(@Stem, 255 - LEN(@Extension) - LEN(@Suffix))
                            + @Suffix + @Extension;

                        IF NOT EXISTS
                        (
                            SELECT 1
                            FROM Document
                            WHERE u_id = @UserId
                              AND LifecycleStatus = 0
                              AND file_name = @Candidate
                              AND doc_id <> @DocumentId
                        )
                            BREAK;

                        SET @SuffixNumber += 1;
                    END;

                    UPDATE Document
                    SET file_name = @Candidate,
                        update_at = GETUTCDATE()
                    WHERE doc_id = @DocumentId;

                    FETCH NEXT FROM duplicate_names INTO @DocumentId, @UserId, @OriginalName;
                END;

                CLOSE duplicate_names;
                DEALLOCATE duplicate_names;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Document_UserId_FileName_Active",
                table: "Document",
                columns: new[] { "u_id", "file_name" },
                unique: true,
                filter: "[LifecycleStatus] = 0 AND [file_name] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Document_UserId_FileName_Active",
                table: "Document");
        }
    }
}

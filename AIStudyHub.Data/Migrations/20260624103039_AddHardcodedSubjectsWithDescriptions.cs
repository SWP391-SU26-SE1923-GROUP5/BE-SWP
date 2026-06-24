using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIStudyHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardcodedSubjectsWithDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "subject_id", "create_at", "description", "subject_code", "subject_name", "update_at" },
                values: new object[,]
                {
                    { new Guid("3d093807-a8d5-4a51-aa77-635a5548ad58"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents related to algorithms, programming languages, software development, and computing theory.", "CS", "Computer Science", null },
                    { new Guid("3d9d068a-bf45-48e3-80b4-ad2c438354e0"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials on investments, corporate finance, financial markets, and wealth management.", "FIN", "Finance", null },
                    { new Guid("4d6dd566-14a4-46e8-9c1f-e7b064b41354"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources on information security, cryptography, network protection, and ethical hacking.", "CYBER", "Cybersecurity", null },
                    { new Guid("54084e95-a302-4c09-bbf2-5fb34f6b5b2e"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents related to financial reporting, auditing, taxation, and bookkeeping.", "ACC", "Accounting", null },
                    { new Guid("63be61df-3336-4d71-aac4-c4e03f77337a"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials covering algebra, calculus, geometry, statistics, and applied mathematics.", "MATH", "Mathematics", null },
                    { new Guid("7217e917-a145-487f-b597-d2066d7f9ec9"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources covering consumer behavior, market research, advertising, and digital marketing.", "MKT", "Marketing", null },
                    { new Guid("88ee3df8-aeea-4440-b610-74f7ba55ac9e"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents about past events, ancient civilizations, world wars, and historical analysis.", "HIST", "History", null },
                    { new Guid("8a716c82-f3de-472c-ab02-acf5e9fd51d6"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents about genetics, anatomy, ecology, botany, and zoology.", "BIO", "Biology", null },
                    { new Guid("8ca7c447-5702-4e45-b4af-406953ab030d"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents on machine learning, neural networks, robotics, and natural language processing.", "AI", "Artificial Intelligence", null },
                    { new Guid("8e159166-e735-4db5-a32d-5931f8401483"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Documents related to legal systems, civil rights, corporate law, and criminal justice.", "LAW", "Law", null },
                    { new Guid("955e64cf-01ad-42b1-9e2f-3176527c0eaa"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources on management, entrepreneurship, corporate strategy, and organizational behavior.", "BUS", "Business", null },
                    { new Guid("9c12d917-5c56-4238-b96b-d1a3c831bf40"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "For any document that does not fit into the predefined categories above.", "OTHER", "Other", null },
                    { new Guid("9ee16682-c880-4074-8bf7-c9299e690d76"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials covering data analysis, big data, data visualization, and statistical modeling.", "DS", "Data Science", null },
                    { new Guid("9fad3be5-9e10-4d1b-b2de-55e585c7f98c"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources for civil, mechanical, electrical, and other engineering disciplines.", "ENGR", "Engineering", null },
                    { new Guid("a2af6d40-1d8c-4940-bd0e-13629c85a480"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials discussing microeconomics, macroeconomics, market behavior, and economic policies.", "ECON", "Economics", null },
                    { new Guid("a611af01-0c29-45af-9d1a-ad792d97e863"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Study materials on human behavior, cognitive processes, and mental health.", "PSY", "Psychology", null },
                    { new Guid("a8c0cdcd-7939-44f1-887a-1a9ebc70b9ad"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources for English literature, grammar, linguistics, and writing skills.", "ENG", "English", null },
                    { new Guid("aadad9a0-a847-4437-8c1a-2443ef5c4543"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Study materials for classical mechanics, electromagnetism, thermodynamics, and quantum physics.", "PHYS", "Physics", null },
                    { new Guid("c5790423-d558-4347-bf0b-39f6addfe9fb"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials covering human health, diseases, pharmacology, and clinical practices.", "MED", "Medicine", null },
                    { new Guid("dfe0ea06-daa1-469d-b4cf-0774c3bac0c5"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Materials on physical environments, human geography, maps, and earth sciences.", "GEO", "Geography", null },
                    { new Guid("f6dd951c-b8e4-4f41-b6e6-ad04707e6c61"), new DateTime(2026, 6, 24, 0, 0, 0, 0, DateTimeKind.Utc), "Resources on organic, inorganic, physical chemistry, and chemical reactions.", "CHEM", "Chemistry", null }
                });

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 24, 10, 30, 39, 399, DateTimeKind.Utc).AddTicks(106));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 24, 10, 30, 39, 399, DateTimeKind.Utc).AddTicks(112));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("3d093807-a8d5-4a51-aa77-635a5548ad58"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("3d9d068a-bf45-48e3-80b4-ad2c438354e0"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("4d6dd566-14a4-46e8-9c1f-e7b064b41354"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("54084e95-a302-4c09-bbf2-5fb34f6b5b2e"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("63be61df-3336-4d71-aac4-c4e03f77337a"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("7217e917-a145-487f-b597-d2066d7f9ec9"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("88ee3df8-aeea-4440-b610-74f7ba55ac9e"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("8a716c82-f3de-472c-ab02-acf5e9fd51d6"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("8ca7c447-5702-4e45-b4af-406953ab030d"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("8e159166-e735-4db5-a32d-5931f8401483"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("955e64cf-01ad-42b1-9e2f-3176527c0eaa"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("9c12d917-5c56-4238-b96b-d1a3c831bf40"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("9ee16682-c880-4074-8bf7-c9299e690d76"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("9fad3be5-9e10-4d1b-b2de-55e585c7f98c"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("a2af6d40-1d8c-4940-bd0e-13629c85a480"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("a611af01-0c29-45af-9d1a-ad792d97e863"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("a8c0cdcd-7939-44f1-887a-1a9ebc70b9ad"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("aadad9a0-a847-4437-8c1a-2443ef5c4543"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("c5790423-d558-4347-bf0b-39f6addfe9fb"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("dfe0ea06-daa1-469d-b4cf-0774c3bac0c5"));

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "subject_id",
                keyValue: new Guid("f6dd951c-b8e4-4f41-b6e6-ad04707e6c61"));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 13, 57, 27, 762, DateTimeKind.Utc).AddTicks(8645));

            migrationBuilder.UpdateData(
                table: "TierMembership",
                keyColumn: "tier_id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "create_at",
                value: new DateTime(2026, 6, 21, 13, 57, 27, 762, DateTimeKind.Utc).AddTicks(8647));
        }
    }
}

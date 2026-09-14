using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorpusAndReferenceMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorpusId",
                table: "MushafEditions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Revision",
                table: "MushafEditions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuranCorpora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NameLatin = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RiwayaCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CountingSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAyahs = table.Column<int>(type: "integer", nullable: false),
                    TextualRevision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QualificationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuranCorpora", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorpusAyahs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorpusId = table.Column<int>(type: "integer", nullable: false),
                    SurahNumber = table.Column<int>(type: "integer", nullable: false),
                    AyahNumber = table.Column<int>(type: "integer", nullable: false),
                    NumberInQuran = table.Column<int>(type: "integer", nullable: false),
                    TextArabic = table.Column<string>(type: "text", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    PageNumberHint = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorpusAyahs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorpusAyahs_QuranCorpora_CorpusId",
                        column: x => x.CorpusId,
                        principalTable: "QuranCorpora",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceCorpusId = table.Column<int>(type: "integer", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetCorpusId = table.Column<int>(type: "integer", nullable: false),
                    TargetReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SurahNumber = table.Column<int>(type: "integer", nullable: false),
                    Cardinality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DifferenceDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProofNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceMappings_QuranCorpora_SourceCorpusId",
                        column: x => x.SourceCorpusId,
                        principalTable: "QuranCorpora",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferenceMappings_QuranCorpora_TargetCorpusId",
                        column: x => x.TargetCorpusId,
                        principalTable: "QuranCorpora",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MushafEditions_CorpusId",
                table: "MushafEditions",
                column: "CorpusId");

            migrationBuilder.CreateIndex(
                name: "IX_CorpusAyahs_CorpusId_NumberInQuran",
                table: "CorpusAyahs",
                columns: new[] { "CorpusId", "NumberInQuran" });

            migrationBuilder.CreateIndex(
                name: "IX_CorpusAyahs_CorpusId_SurahNumber_AyahNumber",
                table: "CorpusAyahs",
                columns: new[] { "CorpusId", "SurahNumber", "AyahNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuranCorpora_Code",
                table: "QuranCorpora",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceMappings_SourceCorpusId_SourceReference",
                table: "ReferenceMappings",
                columns: new[] { "SourceCorpusId", "SourceReference" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceMappings_TargetCorpusId_TargetReference",
                table: "ReferenceMappings",
                columns: new[] { "TargetCorpusId", "TargetReference" });

            migrationBuilder.AddForeignKey(
                name: "FK_MushafEditions_QuranCorpora_CorpusId",
                table: "MushafEditions",
                column: "CorpusId",
                principalTable: "QuranCorpora",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MushafEditions_QuranCorpora_CorpusId",
                table: "MushafEditions");

            migrationBuilder.DropTable(
                name: "CorpusAyahs");

            migrationBuilder.DropTable(
                name: "ReferenceMappings");

            migrationBuilder.DropTable(
                name: "QuranCorpora");

            migrationBuilder.DropIndex(
                name: "IX_MushafEditions_CorpusId",
                table: "MushafEditions");

            migrationBuilder.DropColumn(
                name: "CorpusId",
                table: "MushafEditions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "MushafEditions");
        }
    }
}

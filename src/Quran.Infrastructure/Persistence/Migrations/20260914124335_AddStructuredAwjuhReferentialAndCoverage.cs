using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredAwjuhReferentialAndCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RouteCode",
                table: "Awjuh",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewStatus",
                table: "Awjuh",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Awjuh",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Awjuh",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "ExcludedScope",
                table: "Awjuh",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReadingRuleId",
                table: "Awjuh",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Awjuh",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "Awjuh",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerUserId",
                table: "Awjuh",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleGroup",
                table: "Awjuh",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceBooks",
                table: "Awjuh",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceBranch",
                table: "Awjuh",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransmissionRouteId",
                table: "Awjuh",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Awjuh",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "DocumentaryCoverageEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransmissionRouteCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceWorkTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChapterOrSection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SurahNumber = table.Column<int>(type: "integer", nullable: true),
                    ScopeContext = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    AnalysisStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntriesCount = table.Column<int>(type: "integer", nullable: false),
                    ConstraintsCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentaryCoverageEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    Locator = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Sha256 = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransmissionRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Riwaya = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tariq = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubTariq = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverageStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentRouteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransmissionRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransmissionRoutes_TransmissionRoutes_ParentRouteId",
                        column: x => x.ParentRouteId,
                        principalTable: "TransmissionRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WajhApplicationScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WajhId = table.Column<int>(type: "integer", nullable: false),
                    SourceSurah = table.Column<int>(type: "integer", nullable: true),
                    SourceAyahStart = table.Column<int>(type: "integer", nullable: true),
                    SourceAyahEnd = table.Column<int>(type: "integer", nullable: true),
                    MarwMappingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AutomaticAssignment = table.Column<bool>(type: "boolean", nullable: false),
                    ContextDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WajhApplicationScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WajhApplicationScopes_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WajhComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WajhId = table.Column<int>(type: "integer", nullable: false),
                    ComponentKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ValueNumeric = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WajhComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WajhComponents_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WajhRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WajhId = table.Column<int>(type: "integer", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RouteCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefinitionSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    AffectsSemanticRecitation = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WajhRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WajhRevisions_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingRealizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReadingRuleId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ValueNumeric = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingRealizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingRealizations_ReadingRules_ReadingRuleId",
                        column: x => x.ReadingRuleId,
                        principalTable: "ReadingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CombinationConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransmissionRouteId = table.Column<int>(type: "integer", nullable: true),
                    RouteCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContextArabic = table.Column<string>(type: "text", nullable: false),
                    ConditionJson = table.Column<string>(type: "text", nullable: false),
                    EffectJson = table.Column<string>(type: "text", nullable: false),
                    ExplanationArabic = table.Column<string>(type: "text", nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReviewerUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AutomaticEnforcement = table.Column<bool>(type: "boolean", nullable: false),
                    MarwMappingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombinationConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombinationConstraints_TransmissionRoutes_TransmissionRoute~",
                        column: x => x.TransmissionRouteId,
                        principalTable: "TransmissionRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceCitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceSourceId = table.Column<int>(type: "integer", nullable: false),
                    WajhId = table.Column<int>(type: "integer", nullable: true),
                    CombinationConstraintId = table.Column<int>(type: "integer", nullable: true),
                    Locator = table.Column<string>(type: "text", nullable: true),
                    QuoteOrNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceCitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceCitations_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceCitations_CombinationConstraints_CombinationConstra~",
                        column: x => x.CombinationConstraintId,
                        principalTable: "CombinationConstraints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceCitations_ReferenceSources_ReferenceSourceId",
                        column: x => x.ReferenceSourceId,
                        principalTable: "ReferenceSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Awjuh_ReadingRuleId",
                table: "Awjuh",
                column: "ReadingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Awjuh_TransmissionRouteId",
                table: "Awjuh",
                column: "TransmissionRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_CombinationConstraints_Code",
                table: "CombinationConstraints",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombinationConstraints_TransmissionRouteId",
                table: "CombinationConstraints",
                column: "TransmissionRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentaryCoverageEntries_TransmissionRouteCode_ChapterOrS~",
                table: "DocumentaryCoverageEntries",
                columns: new[] { "TransmissionRouteCode", "ChapterOrSection" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCitations_CombinationConstraintId",
                table: "EvidenceCitations",
                column: "CombinationConstraintId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCitations_ReferenceSourceId",
                table: "EvidenceCitations",
                column: "ReferenceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceCitations_WajhId",
                table: "EvidenceCitations",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRealizations_Code",
                table: "ReadingRealizations",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRealizations_ReadingRuleId",
                table: "ReadingRealizations",
                column: "ReadingRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingRules_Code",
                table: "ReadingRules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceSources_SourceCode",
                table: "ReferenceSources",
                column: "SourceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransmissionRoutes_Code",
                table: "TransmissionRoutes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransmissionRoutes_ParentRouteId",
                table: "TransmissionRoutes",
                column: "ParentRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_WajhApplicationScopes_WajhId",
                table: "WajhApplicationScopes",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_WajhComponents_WajhId",
                table: "WajhComponents",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_WajhRevisions_WajhId_RevisionNumber",
                table: "WajhRevisions",
                columns: new[] { "WajhId", "RevisionNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Awjuh_ReadingRules_ReadingRuleId",
                table: "Awjuh",
                column: "ReadingRuleId",
                principalTable: "ReadingRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Awjuh_TransmissionRoutes_TransmissionRouteId",
                table: "Awjuh",
                column: "TransmissionRouteId",
                principalTable: "TransmissionRoutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Awjuh_ReadingRules_ReadingRuleId",
                table: "Awjuh");

            migrationBuilder.DropForeignKey(
                name: "FK_Awjuh_TransmissionRoutes_TransmissionRouteId",
                table: "Awjuh");

            migrationBuilder.DropTable(
                name: "DocumentaryCoverageEntries");

            migrationBuilder.DropTable(
                name: "EvidenceCitations");

            migrationBuilder.DropTable(
                name: "ReadingRealizations");

            migrationBuilder.DropTable(
                name: "WajhApplicationScopes");

            migrationBuilder.DropTable(
                name: "WajhComponents");

            migrationBuilder.DropTable(
                name: "WajhRevisions");

            migrationBuilder.DropTable(
                name: "CombinationConstraints");

            migrationBuilder.DropTable(
                name: "ReferenceSources");

            migrationBuilder.DropTable(
                name: "ReadingRules");

            migrationBuilder.DropTable(
                name: "TransmissionRoutes");

            migrationBuilder.DropIndex(
                name: "IX_Awjuh_ReadingRuleId",
                table: "Awjuh");

            migrationBuilder.DropIndex(
                name: "IX_Awjuh_TransmissionRouteId",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "ExcludedScope",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "ReadingRuleId",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "RuleGroup",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "SourceBooks",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "SourceBranch",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "TransmissionRouteId",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Awjuh");

            migrationBuilder.AlterColumn<string>(
                name: "RouteCode",
                table: "Awjuh",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ReviewStatus",
                table: "Awjuh",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}

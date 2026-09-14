using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Awjuh",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Awjuh", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MushafPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    SurahStartId = table.Column<int>(type: "integer", nullable: false),
                    AyahStartNumber = table.Column<int>(type: "integer", nullable: false),
                    SurahEndId = table.Column<int>(type: "integer", nullable: false),
                    AyahEndNumber = table.Column<int>(type: "integer", nullable: false),
                    ImagePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MushafPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reciters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NameLatin = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reciters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Riwayas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NameLatin = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Riwayas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Surahs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameLatin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameTranslation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AyahCount = table.Column<int>(type: "integer", nullable: false),
                    RevelationOrder = table.Column<int>(type: "integer", nullable: false),
                    RevelationType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surahs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Bitrate = table.Column<int>(type: "integer", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioAssets_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Ayahs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SurahId = table.Column<int>(type: "integer", nullable: false),
                    AyahNumber = table.Column<int>(type: "integer", nullable: false),
                    NumberInQuran = table.Column<int>(type: "integer", nullable: false),
                    TextWarsh = table.Column<string>(type: "text", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    LineStart = table.Column<int>(type: "integer", nullable: true),
                    LineEnd = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ayahs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ayahs_Surahs_SurahId",
                        column: x => x.SurahId,
                        principalTable: "Surahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recitations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyahId = table.Column<long>(type: "bigint", nullable: false),
                    ReciterId = table.Column<int>(type: "integer", nullable: false),
                    RiwayaId = table.Column<int>(type: "integer", nullable: false),
                    WajhId = table.Column<int>(type: "integer", nullable: true),
                    AudioAssetId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recitations_AudioAssets_AudioAssetId",
                        column: x => x.AudioAssetId,
                        principalTable: "AudioAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recitations_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Recitations_Ayahs_AyahId",
                        column: x => x.AyahId,
                        principalTable: "Ayahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recitations_Reciters_ReciterId",
                        column: x => x.ReciterId,
                        principalTable: "Reciters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recitations_Riwayas_RiwayaId",
                        column: x => x.RiwayaId,
                        principalTable: "Riwayas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ValidationReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecitationId = table.Column<long>(type: "bigint", nullable: false),
                    ValidatorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationReviews_Recitations_RecitationId",
                        column: x => x.RecitationId,
                        principalTable: "Recitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioAssets_Checksum",
                table: "AudioAssets",
                column: "Checksum");

            migrationBuilder.CreateIndex(
                name: "IX_AudioAssets_SourceId",
                table: "AudioAssets",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Awjuh_Code",
                table: "Awjuh",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ayahs_NumberInQuran",
                table: "Ayahs",
                column: "NumberInQuran");

            migrationBuilder.CreateIndex(
                name: "IX_Ayahs_SearchText",
                table: "Ayahs",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_Ayahs_SurahId_AyahNumber",
                table: "Ayahs",
                columns: new[] { "SurahId", "AyahNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MushafPages_PageNumber",
                table: "MushafPages",
                column: "PageNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_AudioAssetId",
                table: "Recitations",
                column: "AudioAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_AyahId",
                table: "Recitations",
                column: "AyahId");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_IsPublished",
                table: "Recitations",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_ReciterId",
                table: "Recitations",
                column: "ReciterId");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_RiwayaId",
                table: "Recitations",
                column: "RiwayaId");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_Status",
                table: "Recitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_WajhId",
                table: "Recitations",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_Surahs_Number",
                table: "Surahs",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationReviews_RecitationId",
                table: "ValidationReviews",
                column: "RecitationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MushafPages");

            migrationBuilder.DropTable(
                name: "ValidationReviews");

            migrationBuilder.DropTable(
                name: "Recitations");

            migrationBuilder.DropTable(
                name: "AudioAssets");

            migrationBuilder.DropTable(
                name: "Awjuh");

            migrationBuilder.DropTable(
                name: "Ayahs");

            migrationBuilder.DropTable(
                name: "Reciters");

            migrationBuilder.DropTable(
                name: "Riwayas");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "Surahs");
        }
    }
}

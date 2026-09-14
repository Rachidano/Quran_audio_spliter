using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMushafImagesSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MushafPages_PageNumber",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "AyahEndNumber",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "MushafPages");

            migrationBuilder.RenameColumn(
                name: "SurahStartId",
                table: "MushafPages",
                newName: "Width");

            migrationBuilder.RenameColumn(
                name: "SurahEndId",
                table: "MushafPages",
                newName: "MushafEditionId");

            migrationBuilder.RenameColumn(
                name: "AyahStartNumber",
                table: "MushafPages",
                newName: "Height");

            migrationBuilder.AddColumn<string>(
                name: "ChecksumSha256",
                table: "MushafPages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MushafPages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "MasterStorageKey",
                table: "MushafPages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WebStorageKey",
                table: "MushafPages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AudioSegmentImageRegions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AudioSegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MushafPageId = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Y = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Width = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Height = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSegmentImageRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioSegmentImageRegions_AudioSegments_AudioSegmentId",
                        column: x => x.AudioSegmentId,
                        principalTable: "AudioSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioSegmentImageRegions_MushafPages_MushafPageId",
                        column: x => x.MushafPageId,
                        principalTable: "MushafPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AyahImageRegions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyahId = table.Column<long>(type: "bigint", nullable: false),
                    MushafPageId = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Y = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Width = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Height = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AyahImageRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AyahImageRegions_Ayahs_AyahId",
                        column: x => x.AyahId,
                        principalTable: "Ayahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AyahImageRegions_MushafPages_MushafPageId",
                        column: x => x.MushafPageId,
                        principalTable: "MushafPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MushafEditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NameLatin = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RiwayaId = table.Column<int>(type: "integer", nullable: false),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MushafEditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MushafEditions_Riwayas_RiwayaId",
                        column: x => x.RiwayaId,
                        principalTable: "Riwayas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MushafPages_MushafEditionId_PageNumber",
                table: "MushafPages",
                columns: new[] { "MushafEditionId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegmentImageRegions_AudioSegmentId",
                table: "AudioSegmentImageRegions",
                column: "AudioSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegmentImageRegions_MushafPageId",
                table: "AudioSegmentImageRegions",
                column: "MushafPageId");

            migrationBuilder.CreateIndex(
                name: "IX_AyahImageRegions_AyahId",
                table: "AyahImageRegions",
                column: "AyahId");

            migrationBuilder.CreateIndex(
                name: "IX_AyahImageRegions_MushafPageId",
                table: "AyahImageRegions",
                column: "MushafPageId");

            migrationBuilder.CreateIndex(
                name: "IX_MushafEditions_Code",
                table: "MushafEditions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MushafEditions_RiwayaId",
                table: "MushafEditions",
                column: "RiwayaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MushafPages_MushafEditions_MushafEditionId",
                table: "MushafPages",
                column: "MushafEditionId",
                principalTable: "MushafEditions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MushafPages_MushafEditions_MushafEditionId",
                table: "MushafPages");

            migrationBuilder.DropTable(
                name: "AudioSegmentImageRegions");

            migrationBuilder.DropTable(
                name: "AyahImageRegions");

            migrationBuilder.DropTable(
                name: "MushafEditions");

            migrationBuilder.DropIndex(
                name: "IX_MushafPages_MushafEditionId_PageNumber",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "ChecksumSha256",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "MasterStorageKey",
                table: "MushafPages");

            migrationBuilder.DropColumn(
                name: "WebStorageKey",
                table: "MushafPages");

            migrationBuilder.RenameColumn(
                name: "Width",
                table: "MushafPages",
                newName: "SurahStartId");

            migrationBuilder.RenameColumn(
                name: "MushafEditionId",
                table: "MushafPages",
                newName: "SurahEndId");

            migrationBuilder.RenameColumn(
                name: "Height",
                table: "MushafPages",
                newName: "AyahStartNumber");

            migrationBuilder.AddColumn<int>(
                name: "AyahEndNumber",
                table: "MushafPages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "MushafPages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MushafPages_PageNumber",
                table: "MushafPages",
                column: "PageNumber",
                unique: true);
        }
    }
}

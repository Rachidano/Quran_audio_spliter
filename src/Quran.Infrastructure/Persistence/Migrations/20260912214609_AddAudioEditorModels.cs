using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioEditorModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AyahWords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AyahId = table.Column<long>(type: "bigint", nullable: false),
                    WordIndex = table.Column<int>(type: "integer", nullable: false),
                    TextWarsh = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SearchText = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AyahWords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AyahWords_Ayahs_AyahId",
                        column: x => x.AyahId,
                        principalTable: "Ayahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceAudios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SurahId = table.Column<int>(type: "integer", nullable: false),
                    ReciterId = table.Column<int>(type: "integer", nullable: false),
                    RiwayaId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Bitrate = table.Column<int>(type: "integer", nullable: true),
                    Codec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceAudios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceAudios_Reciters_ReciterId",
                        column: x => x.ReciterId,
                        principalTable: "Reciters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceAudios_Riwayas_RiwayaId",
                        column: x => x.RiwayaId,
                        principalTable: "Riwayas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SourceAudios_Surahs_SurahId",
                        column: x => x.SurahId,
                        principalTable: "Surahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudioSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AyahId = table.Column<long>(type: "bigint", nullable: false),
                    WajhId = table.Column<int>(type: "integer", nullable: true),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    StartWordIndex = table.Column<int>(type: "integer", nullable: true),
                    EndWordIndex = table.Column<int>(type: "integer", nullable: true),
                    SegmentType = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAudioAssetId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioSegments_AudioAssets_PublishedAudioAssetId",
                        column: x => x.PublishedAudioAssetId,
                        principalTable: "AudioAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AudioSegments_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AudioSegments_Ayahs_AyahId",
                        column: x => x.AyahId,
                        principalTable: "Ayahs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudioSegments_SourceAudios_SourceAudioId",
                        column: x => x.SourceAudioId,
                        principalTable: "SourceAudios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_AyahId",
                table: "AudioSegments",
                column: "AyahId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_PublishedAudioAssetId",
                table: "AudioSegments",
                column: "PublishedAudioAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_SourceAudioId",
                table: "AudioSegments",
                column: "SourceAudioId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_SourceAudioId_StartMs",
                table: "AudioSegments",
                columns: new[] { "SourceAudioId", "StartMs" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_Status",
                table: "AudioSegments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_WajhId",
                table: "AudioSegments",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_AyahWords_AyahId_WordIndex",
                table: "AyahWords",
                columns: new[] { "AyahId", "WordIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceAudios_ReciterId",
                table: "SourceAudios",
                column: "ReciterId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceAudios_RiwayaId",
                table: "SourceAudios",
                column: "RiwayaId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceAudios_StorageKey",
                table: "SourceAudios",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceAudios_SurahId",
                table: "SourceAudios",
                column: "SurahId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioSegments");

            migrationBuilder.DropTable(
                name: "AyahWords");

            migrationBuilder.DropTable(
                name: "SourceAudios");
        }
    }
}

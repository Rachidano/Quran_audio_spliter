using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSegmentCanonicalAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "AyahId",
                table: "AudioSegments",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "Edition",
                table: "AudioSegments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservationJson",
                table: "AudioSegments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "AudioSegments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepeatOfId",
                table: "AudioSegments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceHash",
                table: "AudioSegments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AudioSegments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AnnotationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceAudioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    CurrentSurahNumber = table.Column<int>(type: "integer", nullable: false),
                    CurrentAyahNumber = table.Column<int>(type: "integer", nullable: false),
                    LastSegmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRepeatMode = table.Column<bool>(type: "boolean", nullable: false),
                    RepeatTargetSegmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnotationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnotationSessions_AudioSegments_LastSegmentId",
                        column: x => x.LastSegmentId,
                        principalTable: "AudioSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnnotationSessions_SourceAudios_SourceAudioId",
                        column: x => x.SourceAudioId,
                        principalTable: "SourceAudios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OldValuesJson = table.Column<string>(type: "text", nullable: true),
                    NewValuesJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SegmentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioSegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SegmentType = table.Column<int>(type: "integer", nullable: false),
                    WajhId = table.Column<int>(type: "integer", nullable: true),
                    WajhAssignmentStatus = table.Column<string>(type: "text", nullable: true),
                    ProofNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentRevisions_AudioSegments_AudioSegmentId",
                        column: x => x.AudioSegmentId,
                        principalTable: "AudioSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SegmentRevisions_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SegmentTextSpans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AudioSegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorpusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SurahNumber = table.Column<int>(type: "integer", nullable: false),
                    AyahNumber = table.Column<int>(type: "integer", nullable: false),
                    StartWordIndex = table.Column<int>(type: "integer", nullable: true),
                    EndWordIndex = table.Column<int>(type: "integer", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentTextSpans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentTextSpans_AudioSegments_AudioSegmentId",
                        column: x => x.AudioSegmentId,
                        principalTable: "AudioSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_Reference",
                table: "AudioSegments",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_AudioSegments_RepeatOfId",
                table: "AudioSegments",
                column: "RepeatOfId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationSessions_LastSegmentId",
                table: "AnnotationSessions",
                column: "LastSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationSessions_SourceAudioId",
                table: "AnnotationSessions",
                column: "SourceAudioId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnotationSessions_UserId_SourceAudioId",
                table: "AnnotationSessions",
                columns: new[] { "UserId", "SourceAudioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SegmentRevisions_AudioSegmentId_RevisionNumber",
                table: "SegmentRevisions",
                columns: new[] { "AudioSegmentId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentRevisions_WajhId",
                table: "SegmentRevisions",
                column: "WajhId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentTextSpans_AudioSegmentId_Order",
                table: "SegmentTextSpans",
                columns: new[] { "AudioSegmentId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_AudioSegments_AudioSegments_RepeatOfId",
                table: "AudioSegments",
                column: "RepeatOfId",
                principalTable: "AudioSegments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioSegments_AudioSegments_RepeatOfId",
                table: "AudioSegments");

            migrationBuilder.DropTable(
                name: "AnnotationSessions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "SegmentRevisions");

            migrationBuilder.DropTable(
                name: "SegmentTextSpans");

            migrationBuilder.DropIndex(
                name: "IX_AudioSegments_Reference",
                table: "AudioSegments");

            migrationBuilder.DropIndex(
                name: "IX_AudioSegments_RepeatOfId",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "Edition",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "ObservationJson",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "RepeatOfId",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "SourceHash",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AudioSegments");

            migrationBuilder.AlterColumn<long>(
                name: "AyahId",
                table: "AudioSegments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}

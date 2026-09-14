using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegratedAwjuhAndMushafPassages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefinitionJson",
                table: "Awjuh",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "Awjuh",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RouteCode",
                table: "Awjuh",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceCitation",
                table: "Awjuh",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Awjuh",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MushafPassages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Edition = table.Column<string>(type: "text", nullable: false),
                    SourceKey = table.Column<string>(type: "text", nullable: false),
                    SourceHash = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    SegmentType = table.Column<int>(type: "integer", nullable: false),
                    RepeatOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    WajhId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    ObservationJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MushafPassages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MushafPassages_Awjuh_WajhId",
                        column: x => x.WajhId,
                        principalTable: "Awjuh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MushafPassages_MushafPassages_RepeatOfId",
                        column: x => x.RepeatOfId,
                        principalTable: "MushafPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MushafPassages_Edition_SourceHash_Reference",
                table: "MushafPassages",
                columns: new[] { "Edition", "SourceHash", "Reference" });

            migrationBuilder.CreateIndex(
                name: "IX_MushafPassages_RepeatOfId",
                table: "MushafPassages",
                column: "RepeatOfId");

            migrationBuilder.CreateIndex(
                name: "IX_MushafPassages_WajhId",
                table: "MushafPassages",
                column: "WajhId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MushafPassages");

            migrationBuilder.DropColumn(
                name: "DefinitionJson",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "RouteCode",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "SourceCitation",
                table: "Awjuh");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Awjuh");
        }
    }
}

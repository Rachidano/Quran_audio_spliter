using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quran.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioSegmentReviewWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "AudioSegments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "AudioSegments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AudioSegments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "AudioSegments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "AudioSegments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AudioSegments");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "AudioSegments");
        }
    }
}

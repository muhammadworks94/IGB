using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LessonDecisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAtUtc",
                table: "LessonBookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DecisionByUserId",
                table: "LessonBookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "LessonBookings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionAtUtc",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "DecisionByUserId",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "LessonBookings");
        }
    }
}

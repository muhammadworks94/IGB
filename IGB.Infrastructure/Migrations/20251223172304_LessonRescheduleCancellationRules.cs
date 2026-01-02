using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LessonRescheduleCancellationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "LessonBookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationNote",
                table: "LessonBookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancellationRequested",
                table: "LessonBookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAt",
                table: "LessonBookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CancellationRequestedByUserId",
                table: "LessonBookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "LessonBookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CancelledByUserId",
                table: "LessonBookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCount",
                table: "LessonBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LessonChangeLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonBookingId = table.Column<long>(type: "bigint", nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OldStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OldEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NewStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NewEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonChangeLogs_LessonBookings_LessonBookingId",
                        column: x => x.LessonBookingId,
                        principalTable: "LessonBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonChangeLogs_LessonBookingId_IsDeleted",
                table: "LessonChangeLogs",
                columns: new[] { "LessonBookingId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonChangeLogs");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancellationNote",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancellationRequested",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedByUserId",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "LessonBookings");

            migrationBuilder.DropColumn(
                name: "RescheduleCount",
                table: "LessonBookings");
        }
    }
}

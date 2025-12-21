using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LessonBookingPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LessonBookings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseBookingId = table.Column<long>(type: "bigint", nullable: true),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: true),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Option1 = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Option2 = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Option3 = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RescheduleRequested = table.Column<bool>(type: "bit", nullable: false),
                    RescheduleRequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RescheduleNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ZoomMeetingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZoomJoinUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ZoomPassword = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SessionStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SessionEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StudentAttended = table.Column<bool>(type: "bit", nullable: false),
                    TutorAttended = table.Column<bool>(type: "bit", nullable: false),
                    EndedByAdminUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonBookings_CourseBookings_CourseBookingId",
                        column: x => x.CourseBookingId,
                        principalTable: "CourseBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LessonBookings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonBookings_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonBookings_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonBookings_CourseBookingId",
                table: "LessonBookings",
                column: "CourseBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonBookings_CourseId_StudentUserId_IsDeleted",
                table: "LessonBookings",
                columns: new[] { "CourseId", "StudentUserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonBookings_StudentUserId",
                table: "LessonBookings",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonBookings_TutorUserId",
                table: "LessonBookings",
                column: "TutorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonBookings");
        }
    }
}

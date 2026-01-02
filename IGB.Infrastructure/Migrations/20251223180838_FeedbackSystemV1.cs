using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackSystemV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentFeedbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonBookingId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Participation = table.Column<int>(type: "int", nullable: false),
                    HomeworkCompletion = table.Column<int>(type: "int", nullable: false),
                    Attentiveness = table.Column<int>(type: "int", nullable: false),
                    Improvement = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FlaggedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FlaggedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFeedbacks_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentFeedbacks_LessonBookings_LessonBookingId",
                        column: x => x.LessonBookingId,
                        principalTable: "LessonBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentFeedbacks_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentFeedbacks_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TutorFeedbacks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonBookingId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    SubjectKnowledge = table.Column<int>(type: "int", nullable: false),
                    Communication = table.Column<int>(type: "int", nullable: false),
                    Punctuality = table.Column<int>(type: "int", nullable: false),
                    TeachingMethod = table.Column<int>(type: "int", nullable: false),
                    Friendliness = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FlaggedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FlaggedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorFeedbacks_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorFeedbacks_LessonBookings_LessonBookingId",
                        column: x => x.LessonBookingId,
                        principalTable: "LessonBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorFeedbacks_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorFeedbacks_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentFeedbackId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackAttachments_StudentFeedbacks_StudentFeedbackId",
                        column: x => x.StudentFeedbackId,
                        principalTable: "StudentFeedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackAttachments_StudentFeedbackId_IsDeleted",
                table: "FeedbackAttachments",
                columns: new[] { "StudentFeedbackId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeedbacks_CourseId",
                table: "StudentFeedbacks",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeedbacks_LessonBookingId_IsDeleted",
                table: "StudentFeedbacks",
                columns: new[] { "LessonBookingId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeedbacks_StudentUserId_CreatedAt_IsDeleted",
                table: "StudentFeedbacks",
                columns: new[] { "StudentUserId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeedbacks_TutorUserId",
                table: "StudentFeedbacks",
                column: "TutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorFeedbacks_CourseId",
                table: "TutorFeedbacks",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorFeedbacks_LessonBookingId_IsDeleted",
                table: "TutorFeedbacks",
                columns: new[] { "LessonBookingId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorFeedbacks_StudentUserId",
                table: "TutorFeedbacks",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorFeedbacks_TutorUserId_CreatedAt_IsDeleted",
                table: "TutorFeedbacks",
                columns: new[] { "TutorUserId", "CreatedAt", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackAttachments");

            migrationBuilder.DropTable(
                name: "TutorFeedbacks");

            migrationBuilder.DropTable(
                name: "StudentFeedbacks");
        }
    }
}

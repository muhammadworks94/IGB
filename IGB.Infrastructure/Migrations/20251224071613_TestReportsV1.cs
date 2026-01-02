using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TestReportsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalMarks = table.Column<int>(type: "int", nullable: false),
                    ObtainedMarks = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Strengths = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AreasForImprovement = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TutorComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TestFileUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TestFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TestFileContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestReports_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestReports_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestReports_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestReportTopics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestReportId = table.Column<long>(type: "bigint", nullable: false),
                    CourseTopicId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReportTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestReportTopics_CourseTopics_CourseTopicId",
                        column: x => x.CourseTopicId,
                        principalTable: "CourseTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestReportTopics_TestReports_TestReportId",
                        column: x => x.TestReportId,
                        principalTable: "TestReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_CourseId",
                table: "TestReports",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_StudentUserId_CourseId_TestDate_IsDeleted",
                table: "TestReports",
                columns: new[] { "StudentUserId", "CourseId", "TestDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_TutorUserId_CourseId_TestDate_IsDeleted",
                table: "TestReports",
                columns: new[] { "TutorUserId", "CourseId", "TestDate", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReportTopics_CourseTopicId",
                table: "TestReportTopics",
                column: "CourseTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TestReportTopics_TestReportId_CourseTopicId_IsDeleted",
                table: "TestReportTopics",
                columns: new[] { "TestReportId", "CourseTopicId", "IsDeleted" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestReportTopics");

            migrationBuilder.DropTable(
                name: "TestReports");
        }
    }
}

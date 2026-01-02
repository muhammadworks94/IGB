using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProgressTrackingV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuardianWards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuardianUserId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianWards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuardianWards_Users_GuardianUserId",
                        column: x => x.GuardianUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianWards_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonTopicCoverages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonBookingId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    CourseTopicId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonTopicCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonTopicCoverages_CourseTopics_CourseTopicId",
                        column: x => x.CourseTopicId,
                        principalTable: "CourseTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonTopicCoverages_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonTopicCoverages_LessonBookings_LessonBookingId",
                        column: x => x.LessonBookingId,
                        principalTable: "LessonBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonTopicCoverages_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonTopicCoverages_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentProgressNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProgressNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProgressNotes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProgressNotes_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProgressNotes_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianWards_GuardianUserId_StudentUserId_IsDeleted",
                table: "GuardianWards",
                columns: new[] { "GuardianUserId", "StudentUserId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuardianWards_StudentUserId",
                table: "GuardianWards",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTopicCoverages_CourseId",
                table: "LessonTopicCoverages",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTopicCoverages_CourseTopicId",
                table: "LessonTopicCoverages",
                column: "CourseTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTopicCoverages_LessonBookingId_CourseTopicId_IsDeleted",
                table: "LessonTopicCoverages",
                columns: new[] { "LessonBookingId", "CourseTopicId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonTopicCoverages_StudentUserId_CourseId_CreatedAt_IsDeleted",
                table: "LessonTopicCoverages",
                columns: new[] { "StudentUserId", "CourseId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonTopicCoverages_TutorUserId",
                table: "LessonTopicCoverages",
                column: "TutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressNotes_CourseId",
                table: "StudentProgressNotes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressNotes_StudentUserId_CourseId_CreatedAt_IsDeleted",
                table: "StudentProgressNotes",
                columns: new[] { "StudentUserId", "CourseId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgressNotes_TutorUserId",
                table: "StudentProgressNotes",
                column: "TutorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuardianWards");

            migrationBuilder.DropTable(
                name: "LessonTopicCoverages");

            migrationBuilder.DropTable(
                name: "StudentProgressNotes");
        }
    }
}

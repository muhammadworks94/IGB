using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CourseEnrollmentCreditsAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TutorUserId",
                table: "Courses",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseReviews_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseReviews_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DeltaCredits = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditLedgerEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_TutorUserId",
                table: "Courses",
                column: "TutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_CourseId_IsDeleted",
                table: "CourseReviews",
                columns: new[] { "CourseId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_CourseId_StudentUserId_IsDeleted",
                table: "CourseReviews",
                columns: new[] { "CourseId", "StudentUserId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_StudentUserId",
                table: "CourseReviews",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_UserId_IsDeleted",
                table: "CreditLedgerEntries",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Users_TutorUserId",
                table: "Courses",
                column: "TutorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Users_TutorUserId",
                table: "Courses");

            migrationBuilder.DropTable(
                name: "CourseReviews");

            migrationBuilder.DropTable(
                name: "CreditLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Courses_TutorUserId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TutorUserId",
                table: "Courses");
        }
    }
}

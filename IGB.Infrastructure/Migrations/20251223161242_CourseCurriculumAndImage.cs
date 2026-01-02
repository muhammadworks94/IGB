using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CourseCurriculumAndImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable first, backfill, then enforce FK
            migrationBuilder.AddColumn<long>(
                name: "CurriculumId",
                table: "Courses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Courses",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            // Backfill from Grades
            migrationBuilder.Sql(@"
UPDATE c
SET c.CurriculumId = g.CurriculumId
FROM Courses c
INNER JOIN Grades g ON g.Id = c.GradeId
WHERE c.CurriculumId IS NULL;
");

            // Safety fallback: pick first curriculum if any remaining NULL
            migrationBuilder.Sql(@"
UPDATE Courses
SET CurriculumId = (SELECT TOP 1 Id FROM Curricula ORDER BY Id)
WHERE CurriculumId IS NULL;
");

            migrationBuilder.AlterColumn<long>(
                name: "CurriculumId",
                table: "Courses",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_CurriculumId_IsDeleted",
                table: "Courses",
                columns: new[] { "CurriculumId", "IsDeleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Curricula_CurriculumId",
                table: "Courses",
                column: "CurriculumId",
                principalTable: "Curricula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Curricula_CurriculumId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_CurriculumId_IsDeleted",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "CurriculumId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Courses");
        }
    }
}

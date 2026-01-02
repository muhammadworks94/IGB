using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TutorAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorAvailabilityBlocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorAvailabilityBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorAvailabilityBlocks_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorAvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartMinutes = table.Column<int>(type: "int", nullable: false),
                    EndMinutes = table.Column<int>(type: "int", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorAvailabilityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorAvailabilityRules_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilityBlocks_TutorUserId_StartUtc_IsDeleted",
                table: "TutorAvailabilityBlocks",
                columns: new[] { "TutorUserId", "StartUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilityRules_TutorUserId_DayOfWeek_IsDeleted",
                table: "TutorAvailabilityRules",
                columns: new[] { "TutorUserId", "DayOfWeek", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorAvailabilityBlocks");

            migrationBuilder.DropTable(
                name: "TutorAvailabilityRules");
        }
    }
}

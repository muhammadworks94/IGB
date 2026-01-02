using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IGB.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreditsSystemV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseCreditLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    CreditsAllocated = table.Column<int>(type: "int", nullable: false),
                    CreditsUsed = table.Column<int>(type: "int", nullable: false),
                    CreditsRemaining = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCreditLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseCreditLedgers_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseCreditLedgers_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseLedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentUserId = table.Column<long>(type: "bigint", nullable: false),
                    CourseId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseLedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseLedgerTransactions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseLedgerTransactions_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditsBalances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TotalCredits = table.Column<int>(type: "int", nullable: false),
                    UsedCredits = table.Column<int>(type: "int", nullable: false),
                    RemainingCredits = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditsBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditsBalances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorEarningTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreditsEarned = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LessonBookingId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorEarningTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorEarningTransactions_Users_TutorUserId",
                        column: x => x.TutorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill balances and transactions from legacy CreditLedgerEntries
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CreditLedgerEntries')
BEGIN
    -- CreditsBalances per user
    INSERT INTO CreditsBalances (UserId, TotalCredits, UsedCredits, RemainingCredits, CreatedAt, UpdatedAt, IsDeleted)
    SELECT
        e.UserId,
        SUM(CASE WHEN e.DeltaCredits > 0 THEN e.DeltaCredits ELSE 0 END) AS TotalCredits,
        SUM(CASE WHEN e.DeltaCredits < 0 THEN -e.DeltaCredits ELSE 0 END) AS UsedCredits,
        SUM(e.DeltaCredits) AS RemainingCredits,
        MIN(e.CreatedAt) AS CreatedAt,
        NULL AS UpdatedAt,
        0 AS IsDeleted
    FROM CreditLedgerEntries e
    WHERE e.IsDeleted = 0
    GROUP BY e.UserId;

    -- CreditTransactions (adjustments), preserve ordering; BalanceAfter is running sum
    ;WITH ordered AS (
        SELECT
            e.Id,
            e.UserId,
            e.DeltaCredits AS Amount,
            e.Reason,
            e.ReferenceType,
            e.ReferenceId,
            e.CreatedByUserId,
            e.CreatedAt,
            SUM(e.DeltaCredits) OVER (PARTITION BY e.UserId ORDER BY e.CreatedAt, e.Id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS BalanceAfter
        FROM CreditLedgerEntries e
        WHERE e.IsDeleted = 0
    )
    INSERT INTO CreditTransactions (UserId, Amount, Type, ReferenceType, ReferenceId, Reason, Notes, BalanceAfter, CreatedByUserId, CreatedAt, UpdatedAt, IsDeleted)
    SELECT
        o.UserId,
        o.Amount,
        3 AS Type, -- Adjustment
        o.ReferenceType,
        o.ReferenceId,
        ISNULL(o.Reason, 'Legacy ledger'),
        'Migrated from CreditLedgerEntries',
        o.BalanceAfter,
        o.CreatedByUserId,
        o.CreatedAt,
        NULL,
        0
    FROM ordered o;
END
");

            // Backfill course ledgers for existing approved course bookings (allocate full course credit cost)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CourseBookings')
BEGIN
    INSERT INTO CourseCreditLedgers (StudentUserId, CourseId, CreditsAllocated, CreditsUsed, CreditsRemaining, CreatedAt, UpdatedAt, IsDeleted)
    SELECT
        b.StudentUserId,
        b.CourseId,
        c.CreditCost,
        0,
        c.CreditCost,
        MIN(b.DecisionAt) AS CreatedAt,
        NULL,
        0
    FROM CourseBookings b
    INNER JOIN Courses c ON c.Id = b.CourseId
    WHERE b.IsDeleted = 0 AND b.Status = 1 -- Approved
    GROUP BY b.StudentUserId, b.CourseId, c.CreditCost;
END
");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCreditLedgers_CourseId",
                table: "CourseCreditLedgers",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCreditLedgers_StudentUserId_CourseId_IsDeleted",
                table: "CourseCreditLedgers",
                columns: new[] { "StudentUserId", "CourseId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseLedgerTransactions_CourseId",
                table: "CourseLedgerTransactions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseLedgerTransactions_StudentUserId_CourseId_CreatedAt_IsDeleted",
                table: "CourseLedgerTransactions",
                columns: new[] { "StudentUserId", "CourseId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditsBalances_UserId_IsDeleted",
                table: "CreditsBalances",
                columns: new[] { "UserId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_UserId_CreatedAt_IsDeleted",
                table: "CreditTransactions",
                columns: new[] { "UserId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorEarningTransactions_TutorUserId_CreatedAt_IsDeleted",
                table: "TutorEarningTransactions",
                columns: new[] { "TutorUserId", "CreatedAt", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseCreditLedgers");

            migrationBuilder.DropTable(
                name: "CourseLedgerTransactions");

            migrationBuilder.DropTable(
                name: "CreditsBalances");

            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "TutorEarningTransactions");
        }
    }
}

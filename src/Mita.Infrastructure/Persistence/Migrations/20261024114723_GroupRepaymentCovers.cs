using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupRepaymentCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_repayment_covers",
                columns: table => new
                {
                    RepaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    GroupAmount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_repayment_covers", x => new { x.RepaymentId, x.ExpenseId });
                    table.CheckConstraint("CK_group_repayment_covers_amount_positive", "\"GroupAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_group_repayment_covers_group_expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "group_expenses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_repayment_covers_group_repayments_RepaymentId",
                        column: x => x.RepaymentId,
                        principalTable: "group_repayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_repayment_covers_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_repayment_covers_ExpenseId",
                table: "group_repayment_covers",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_group_repayment_covers_GroupId",
                table: "group_repayment_covers",
                column: "GroupId");

            migrationBuilder.Sql(
                """
                WITH shares AS (
                    SELECT s."MemberId" AS debtor, e."PayerId" AS creditor, e."Id" AS expense_id,
                        s."GroupAmount" AS amount,
                        row_number() OVER pair AS share_position,
                        sum(s."GroupAmount") OVER pair AS running
                    FROM group_expense_shares AS s
                    JOIN group_expenses AS e ON e."Id" = s."ExpenseId"
                    WHERE e."DeletedAt" IS NULL AND s."MemberId" <> e."PayerId"
                    WINDOW pair AS (PARTITION BY s."MemberId", e."PayerId" ORDER BY e."Date", e."CreatedAt", e."Id"
                        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
                ),
                repayments AS (
                    SELECT r."Id" AS repayment_id, r."GroupId" AS group_id, r."FromMemberId" AS debtor,
                        r."ToMemberId" AS creditor, r."GroupAmount" AS amount,
                        sum(r."GroupAmount") OVER (
                            PARTITION BY r."FromMemberId", r."ToMemberId" ORDER BY r."Date", r."CreatedAt", r."Id"
                            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running
                    FROM group_repayments AS r
                    WHERE r."DeletedAt" IS NULL
                ),
                overlapping AS (
                    SELECT r.repayment_id, s.expense_id, r.group_id, s.share_position,
                        least(s.running, r.running) - greatest(s.running - s.amount, r.running - r.amount) AS amount
                    FROM repayments AS r
                    JOIN shares AS s ON s.debtor = r.debtor AND s.creditor = r.creditor
                )
                INSERT INTO group_repayment_covers ("RepaymentId", "ExpenseId", "GroupId", "Position", "GroupAmount")
                SELECT repayment_id, expense_id, group_id,
                    (row_number() OVER (PARTITION BY repayment_id ORDER BY share_position) - 1)::int, amount
                FROM overlapping
                WHERE amount > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_repayment_covers");
        }
    }
}

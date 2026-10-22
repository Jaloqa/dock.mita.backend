using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Debts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "debts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Counterparty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountAmount = table.Column<long>(type: "bigint", nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Repaid = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debts", x => x.Id);
                    table.CheckConstraint("CK_debts_account_amount_positive", "\"AccountAmount\" > 0");
                    table.CheckConstraint("CK_debts_account_fields", "(\"AccountId\" IS NULL) = (\"AccountAmount\" IS NULL) AND (\"AccountId\" IS NULL) = (\"Rate\" IS NULL)");
                    table.CheckConstraint("CK_debts_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_debts_due_after_date", "\"DueDate\" >= \"Date\"");
                    table.CheckConstraint("CK_debts_repaid_range", "\"Repaid\" >= 0 AND \"Repaid\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_debts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_debts_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "debt_repayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    DebtAmount = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountAmount = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debt_repayments", x => x.Id);
                    table.CheckConstraint("CK_debt_repayments_account_amount_positive", "\"AccountAmount\" > 0");
                    table.CheckConstraint("CK_debt_repayments_account_fields", "(\"AccountId\" IS NULL) = (\"AccountAmount\" IS NULL)");
                    table.CheckConstraint("CK_debt_repayments_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_debt_repayments_debt_amount_positive", "\"DebtAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_debt_repayments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_debt_repayments_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_debt_repayments_debts_DebtId",
                        column: x => x.DebtId,
                        principalTable: "debts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_debt_repayments_AccountId",
                table: "debt_repayments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_debt_repayments_DebtId",
                table: "debt_repayments",
                column: "DebtId");

            migrationBuilder.CreateIndex(
                name: "IX_debt_repayments_UserId",
                table: "debt_repayments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_debts_AccountId",
                table: "debts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_debts_UserId_Date",
                table: "debts",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE VIEW account_movements AS
                SELECT "UserId", "FromAccountId" AS "AccountId", -"FromAmount" AS "Amount" FROM transfers
                UNION ALL
                SELECT "UserId", "ToAccountId", "ToAmount" FROM transfers
                UNION ALL
                SELECT "UserId", "AccountId", CASE WHEN "Kind" = 'Income' THEN "AccountAmount" ELSE -"AccountAmount" END
                FROM transactions
                UNION ALL
                SELECT "UserId", "AccountId", CASE WHEN "Direction" = 'Borrowed' THEN "AccountAmount" ELSE -"AccountAmount" END
                FROM debts WHERE "AccountId" IS NOT NULL
                UNION ALL
                SELECT r."UserId", r."AccountId", CASE WHEN d."Direction" = 'Lent' THEN r."AccountAmount" ELSE -r."AccountAmount" END
                FROM debt_repayments r JOIN debts d ON d."Id" = r."DebtId" WHERE r."AccountId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE VIEW account_movements AS
                SELECT "UserId", "FromAccountId" AS "AccountId", -"FromAmount" AS "Amount" FROM transfers
                UNION ALL
                SELECT "UserId", "ToAccountId", "ToAmount" FROM transfers
                UNION ALL
                SELECT "UserId", "AccountId", CASE WHEN "Kind" = 'Income' THEN "AccountAmount" ELSE -"AccountAmount" END
                FROM transactions;
                """);

            migrationBuilder.DropTable(
                name: "debt_repayments");

            migrationBuilder.DropTable(
                name: "debts");
        }
    }
}

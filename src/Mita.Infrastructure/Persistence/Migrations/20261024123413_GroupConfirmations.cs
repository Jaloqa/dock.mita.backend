using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group_confirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandledTerms = table.Column<string>(type: "jsonb", nullable: true),
                    ConfirmedTerms = table.Column<string>(type: "jsonb", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_confirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_confirmations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_confirmations_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "group_account_legs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountAmount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_account_legs", x => x.Id);
                    table.CheckConstraint("CK_group_account_legs_amount_not_zero", "\"AccountAmount\" <> 0");
                    table.ForeignKey(
                        name: "FK_group_account_legs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_account_legs_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_account_legs_group_confirmations_ConfirmationId",
                        column: x => x.ConfirmationId,
                        principalTable: "group_confirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_confirmation_transactions",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_confirmation_transactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_group_confirmation_transactions_group_confirmations_Confirm~",
                        column: x => x.ConfirmationId,
                        principalTable: "group_confirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_confirmation_transactions_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_account_legs_AccountId",
                table: "group_account_legs",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_group_account_legs_ConfirmationId",
                table: "group_account_legs",
                column: "ConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_group_account_legs_UserId",
                table: "group_account_legs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_group_confirmation_transactions_ConfirmationId",
                table: "group_confirmation_transactions",
                column: "ConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_group_confirmations_AccountId",
                table: "group_confirmations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_group_confirmations_UserId_Kind_EntryId",
                table: "group_confirmations",
                columns: new[] { "UserId", "Kind", "EntryId" },
                unique: true);

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
                FROM debt_repayments r JOIN debts d ON d."Id" = r."DebtId" WHERE r."AccountId" IS NOT NULL
                UNION ALL
                SELECT "UserId", "AccountId", "AccountAmount" FROM group_account_legs;
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
                FROM transactions
                UNION ALL
                SELECT "UserId", "AccountId", CASE WHEN "Direction" = 'Borrowed' THEN "AccountAmount" ELSE -"AccountAmount" END
                FROM debts WHERE "AccountId" IS NOT NULL
                UNION ALL
                SELECT r."UserId", r."AccountId", CASE WHEN d."Direction" = 'Lent' THEN r."AccountAmount" ELSE -r."AccountAmount" END
                FROM debt_repayments r JOIN debts d ON d."Id" = r."DebtId" WHERE r."AccountId" IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "group_account_legs");

            migrationBuilder.DropTable(
                name: "group_confirmation_transactions");

            migrationBuilder.DropTable(
                name: "group_confirmations");
        }
    }
}

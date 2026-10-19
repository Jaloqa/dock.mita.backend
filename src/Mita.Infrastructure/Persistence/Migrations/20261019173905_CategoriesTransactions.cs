using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoriesTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Color = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    System = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AccountAmount = table.Column<long>(type: "bigint", nullable: false),
                    BaseAmount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.CheckConstraint("CK_transactions_account_amount_positive", "\"AccountAmount\" > 0");
                    table.CheckConstraint("CK_transactions_amount_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_transactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_transactions_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_user_kind_other",
                table: "categories",
                columns: new[] { "UserId", "Kind" },
                unique: true,
                filter: "\"System\" = 'Other'");

            migrationBuilder.CreateIndex(
                name: "IX_categories_UserId_Kind",
                table: "categories",
                columns: new[] { "UserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId",
                table: "transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CategoryId",
                table: "transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_UserId_Date",
                table: "transactions",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_categories_user_kind_name" ON categories ("UserId", "Kind", lower("Name"));
                """);

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

            migrationBuilder.Sql(
                """
                CREATE FUNCTION pln_value(p_currency text, p_date date) RETURNS numeric LANGUAGE sql STABLE AS $$
                SELECT CASE
                    WHEN p_currency = 'PLN' THEN 1::numeric
                    ELSE COALESCE(
                        (SELECT "PlnValue" FROM exchange_rates
                         WHERE "Currency" = p_currency AND "Date" <= p_date
                         ORDER BY "Date" DESC LIMIT 1),
                        (SELECT "PlnValue" FROM exchange_rates
                         WHERE "Currency" = p_currency
                         ORDER BY "Date" LIMIT 1))
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO categories ("Id", "UserId", "Name", "Kind", "Color", "System", "Position", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."Id", v.name, v.kind, v.color, v.system, v.position, now(), now()
                FROM "AspNetUsers" u
                JOIN user_settings s ON s."UserId" = u."Id"
                JOIN (VALUES
                    ('En', 'Groceries', 'Expense', 'Chart2', NULL, 0),
                    ('En', 'Transport', 'Expense', 'Chart5', NULL, 1),
                    ('En', 'Housing', 'Expense', 'Chart1', NULL, 2),
                    ('En', 'Cafes', 'Expense', 'Chart4', NULL, 3),
                    ('En', 'Health', 'Expense', 'Chart3', NULL, 4),
                    ('En', 'Shopping', 'Expense', 'Chart4', NULL, 5),
                    ('En', 'Leisure', 'Expense', 'Chart3', NULL, 6),
                    ('En', 'Gifts', 'Expense', 'Chart2', NULL, 7),
                    ('En', 'Other', 'Expense', 'Chart6', 'Other', 8),
                    ('En', 'Transfers', 'Expense', 'Chart5', 'Transfer', 9),
                    ('En', 'Salary', 'Income', 'Chart2', NULL, 10),
                    ('En', 'Freelance', 'Income', 'Chart1', NULL, 11),
                    ('En', 'Gift', 'Income', 'Chart4', NULL, 12),
                    ('En', 'Interest', 'Income', 'Chart3', NULL, 13),
                    ('En', 'Refund', 'Income', 'Chart5', NULL, 14),
                    ('En', 'Sale', 'Income', 'Chart1', NULL, 15),
                    ('En', 'Bonus', 'Income', 'Chart4', NULL, 16),
                    ('En', 'Other', 'Income', 'Chart6', 'Other', 17),
                    ('Pl', 'Spożywcze', 'Expense', 'Chart2', NULL, 0),
                    ('Pl', 'Transport', 'Expense', 'Chart5', NULL, 1),
                    ('Pl', 'Mieszkanie', 'Expense', 'Chart1', NULL, 2),
                    ('Pl', 'Kawiarnie', 'Expense', 'Chart4', NULL, 3),
                    ('Pl', 'Zdrowie', 'Expense', 'Chart3', NULL, 4),
                    ('Pl', 'Zakupy', 'Expense', 'Chart4', NULL, 5),
                    ('Pl', 'Rozrywka', 'Expense', 'Chart3', NULL, 6),
                    ('Pl', 'Prezenty', 'Expense', 'Chart2', NULL, 7),
                    ('Pl', 'Inne', 'Expense', 'Chart6', 'Other', 8),
                    ('Pl', 'Przelewy', 'Expense', 'Chart5', 'Transfer', 9),
                    ('Pl', 'Wynagrodzenie', 'Income', 'Chart2', NULL, 10),
                    ('Pl', 'Freelance', 'Income', 'Chart1', NULL, 11),
                    ('Pl', 'Prezent', 'Income', 'Chart4', NULL, 12),
                    ('Pl', 'Odsetki', 'Income', 'Chart3', NULL, 13),
                    ('Pl', 'Zwrot', 'Income', 'Chart5', NULL, 14),
                    ('Pl', 'Sprzedaż', 'Income', 'Chart1', NULL, 15),
                    ('Pl', 'Premia', 'Income', 'Chart4', NULL, 16),
                    ('Pl', 'Inne', 'Income', 'Chart6', 'Other', 17),
                    ('Ru', 'Продукты', 'Expense', 'Chart2', NULL, 0),
                    ('Ru', 'Транспорт', 'Expense', 'Chart5', NULL, 1),
                    ('Ru', 'Жильё', 'Expense', 'Chart1', NULL, 2),
                    ('Ru', 'Кафе', 'Expense', 'Chart4', NULL, 3),
                    ('Ru', 'Здоровье', 'Expense', 'Chart3', NULL, 4),
                    ('Ru', 'Покупки', 'Expense', 'Chart4', NULL, 5),
                    ('Ru', 'Досуг', 'Expense', 'Chart3', NULL, 6),
                    ('Ru', 'Подарки', 'Expense', 'Chart2', NULL, 7),
                    ('Ru', 'Другое', 'Expense', 'Chart6', 'Other', 8),
                    ('Ru', 'Переводы', 'Expense', 'Chart5', 'Transfer', 9),
                    ('Ru', 'Зарплата', 'Income', 'Chart2', NULL, 10),
                    ('Ru', 'Фриланс', 'Income', 'Chart1', NULL, 11),
                    ('Ru', 'Подарок', 'Income', 'Chart4', NULL, 12),
                    ('Ru', 'Проценты', 'Income', 'Chart3', NULL, 13),
                    ('Ru', 'Возврат', 'Income', 'Chart5', NULL, 14),
                    ('Ru', 'Продажа', 'Income', 'Chart1', NULL, 15),
                    ('Ru', 'Бонус', 'Income', 'Chart4', NULL, 16),
                    ('Ru', 'Другое', 'Income', 'Chart6', 'Other', 17),
                    ('Uk', 'Продукти', 'Expense', 'Chart2', NULL, 0),
                    ('Uk', 'Транспорт', 'Expense', 'Chart5', NULL, 1),
                    ('Uk', 'Житло', 'Expense', 'Chart1', NULL, 2),
                    ('Uk', 'Кафе', 'Expense', 'Chart4', NULL, 3),
                    ('Uk', 'Здоров''я', 'Expense', 'Chart3', NULL, 4),
                    ('Uk', 'Покупки', 'Expense', 'Chart4', NULL, 5),
                    ('Uk', 'Дозвілля', 'Expense', 'Chart3', NULL, 6),
                    ('Uk', 'Подарунки', 'Expense', 'Chart2', NULL, 7),
                    ('Uk', 'Інше', 'Expense', 'Chart6', 'Other', 8),
                    ('Uk', 'Перекази', 'Expense', 'Chart5', 'Transfer', 9),
                    ('Uk', 'Зарплата', 'Income', 'Chart2', NULL, 10),
                    ('Uk', 'Фриланс', 'Income', 'Chart1', NULL, 11),
                    ('Uk', 'Подарунок', 'Income', 'Chart4', NULL, 12),
                    ('Uk', 'Відсотки', 'Income', 'Chart3', NULL, 13),
                    ('Uk', 'Повернення', 'Income', 'Chart5', NULL, 14),
                    ('Uk', 'Продаж', 'Income', 'Chart1', NULL, 15),
                    ('Uk', 'Бонус', 'Income', 'Chart4', NULL, 16),
                    ('Uk', 'Інше', 'Income', 'Chart6', 'Other', 17)
                ) v(locale, name, kind, color, system, position) ON v.locale = s."Locale"
                WHERE NOT EXISTS (SELECT 1 FROM categories c WHERE c."UserId" = u."Id");
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
                SELECT "UserId", "ToAccountId", "ToAmount" FROM transfers;
                """);

            migrationBuilder.Sql("DROP FUNCTION pln_value(text, date);");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryStandardKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StandardKey",
                table: "categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH starters (kind, standard_key, en, pl, ru, uk) AS (
                    VALUES
                        ('Expense', 'groceries', 'Groceries', 'Spożywcze', 'Продукты', 'Продукти'),
                        ('Expense', 'transport', 'Transport', 'Transport', 'Транспорт', 'Транспорт'),
                        ('Expense', 'housing', 'Housing', 'Mieszkanie', 'Жильё', 'Житло'),
                        ('Expense', 'cafes', 'Cafes', 'Kawiarnie', 'Кафе', 'Кафе'),
                        ('Expense', 'health', 'Health', 'Zdrowie', 'Здоровье', 'Здоров''я'),
                        ('Expense', 'shopping', 'Shopping', 'Zakupy', 'Покупки', 'Покупки'),
                        ('Expense', 'leisure', 'Leisure', 'Rozrywka', 'Досуг', 'Дозвілля'),
                        ('Expense', 'gifts', 'Gifts', 'Prezenty', 'Подарки', 'Подарунки'),
                        ('Expense', 'other', 'Other', 'Inne', 'Другое', 'Інше'),
                        ('Expense', 'transfer', 'Transfers', 'Przelewy', 'Переводы', 'Перекази'),
                        ('Income', 'salary', 'Salary', 'Wynagrodzenie', 'Зарплата', 'Зарплата'),
                        ('Income', 'freelance', 'Freelance', 'Freelance', 'Фриланс', 'Фриланс'),
                        ('Income', 'gift', 'Gift', 'Prezent', 'Подарок', 'Подарунок'),
                        ('Income', 'interest', 'Interest', 'Odsetki', 'Проценты', 'Відсотки'),
                        ('Income', 'refund', 'Refund', 'Zwrot', 'Возврат', 'Повернення'),
                        ('Income', 'sale', 'Sale', 'Sprzedaż', 'Продажа', 'Продаж'),
                        ('Income', 'bonus', 'Bonus', 'Premia', 'Бонус', 'Бонус'),
                        ('Income', 'other', 'Other', 'Inne', 'Другое', 'Інше')
                ),
                starter_names AS (
                    SELECT DISTINCT starters.kind, starters.standard_key, lower(names.value) AS name
                    FROM starters
                    CROSS JOIN LATERAL unnest(ARRAY[starters.en, starters.pl, starters.ru, starters.uk]) AS names (value)
                ),
                candidates AS (
                    SELECT category."Id" AS id,
                        category."UserId" AS user_id,
                        category."Kind" AS kind,
                        category."Position" AS place,
                        category."CreatedAt" AS created_at,
                        category."System" IS NULL AS matched_by_name,
                        CASE category."System"
                            WHEN 'Other' THEN 'other'
                            WHEN 'Transfer' THEN 'transfer'
                            ELSE starter_names.standard_key
                        END AS standard_key
                    FROM categories AS category
                    LEFT JOIN starter_names
                        ON category."System" IS NULL
                        AND starter_names.kind = category."Kind"
                        AND starter_names.name = lower(category."Name")
                ),
                ranked AS (
                    SELECT candidates.id,
                        candidates.standard_key,
                        row_number() OVER (
                            PARTITION BY candidates.user_id, candidates.kind, candidates.standard_key
                            ORDER BY candidates.matched_by_name, candidates.place, candidates.created_at, candidates.id
                        ) AS rank
                    FROM candidates
                    WHERE candidates.standard_key IS NOT NULL
                )
                UPDATE categories
                SET "StandardKey" = ranked.standard_key
                FROM ranked
                WHERE categories."Id" = ranked.id AND ranked.rank = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_categories_user_kind_standard_key",
                table: "categories",
                columns: new[] { "UserId", "Kind", "StandardKey" },
                unique: true,
                filter: "\"StandardKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_user_kind_standard_key",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "StandardKey",
                table: "categories");
        }
    }
}

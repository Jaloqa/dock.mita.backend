using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CurrencyPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "transfers",
                type: "numeric(24,12)",
                precision: 24,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "transactions",
                type: "numeric(24,12)",
                precision: 24,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "PlnValue",
                table: "exchange_rates",
                type: "numeric(24,12)",
                precision: 24,
                scale: 12,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION currency_exponent(p_currency text) RETURNS int LANGUAGE sql IMMUTABLE AS $$
                SELECT CASE
                    WHEN p_currency IN ('BIF', 'CLP', 'DJF', 'GNF', 'ISK', 'JPY', 'KMF', 'KRW',
                        'PYG', 'RWF', 'UGX', 'VND', 'VUV', 'XAF', 'XOF', 'XPF') THEN 0
                    WHEN p_currency IN ('BHD', 'IQD', 'JOD', 'KWD', 'LYD', 'OMR', 'TND') THEN 3
                    ELSE 2
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION round_rate(p_rate numeric) RETURNS numeric LANGUAGE plpgsql IMMUTABLE AS $$
                DECLARE
                    decimals int := 6;
                    scaled numeric := abs(p_rate);
                BEGIN
                    WHILE scaled <> 0 AND scaled < 0.1 AND decimals < 12 LOOP
                        decimals := decimals + 1;
                        scaled := scaled * 10;
                    END LOOP;
                    RETURN round(p_rate, decimals);
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION round_rate(numeric);");

            migrationBuilder.Sql("DROP FUNCTION currency_exponent(text);");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "transfers",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,12)",
                oldPrecision: 24,
                oldScale: 12);

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,12)",
                oldPrecision: 24,
                oldScale: 12);

            migrationBuilder.AlterColumn<decimal>(
                name: "PlnValue",
                table: "exchange_rates",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,12)",
                oldPrecision: 24,
                oldScale: 12);
        }
    }
}

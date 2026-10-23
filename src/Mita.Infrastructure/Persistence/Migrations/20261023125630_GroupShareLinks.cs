using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "groups",
                type: "character varying(43)",
                maxLength: 43,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_ShareToken",
                table: "groups",
                column: "ShareToken",
                unique: true,
                filter: "\"ShareToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_groups_ShareToken",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "groups");
        }
    }
}

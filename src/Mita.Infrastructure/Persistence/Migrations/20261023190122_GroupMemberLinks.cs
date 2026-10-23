using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupMemberLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InvitedAt",
                table: "group_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedEmail",
                table: "group_members",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "group_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_members_group_user",
                table: "group_members",
                columns: new[] { "GroupId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_invited_email",
                table: "group_members",
                column: "InvitedEmail",
                filter: "\"InvitedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_UserId",
                table: "group_members",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_group_members_AspNetUsers_UserId",
                table: "group_members",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_members_AspNetUsers_UserId",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_members_group_user",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_members_invited_email",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_group_members_UserId",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "InvitedAt",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "InvitedEmail",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "group_members");
        }
    }
}

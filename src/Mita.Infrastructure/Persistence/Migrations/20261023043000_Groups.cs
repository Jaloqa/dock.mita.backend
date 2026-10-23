using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mita.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Groups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_groups_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_categories_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_members_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_entry_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_entry_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_group_entry_events_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_group_entry_events_group_members_ActorMemberId",
                        column: x => x.ActorMemberId,
                        principalTable: "group_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_entry_events_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StandardCategory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    RateManual = table.Column<bool>(type: "boolean", nullable: false),
                    GroupAmount = table.Column<long>(type: "bigint", nullable: false),
                    Split = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_expenses", x => x.Id);
                    table.CheckConstraint("CK_group_expenses_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_group_expenses_category", "(\"StandardCategory\" IS NULL) <> (\"CategoryId\" IS NULL)");
                    table.CheckConstraint("CK_group_expenses_group_amount_positive", "\"GroupAmount\" > 0");
                    table.CheckConstraint("CK_group_expenses_rate_positive", "\"Rate\" > 0");
                    table.ForeignKey(
                        name: "FK_group_expenses_group_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "group_categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_expenses_group_members_PayerId",
                        column: x => x.PayerId,
                        principalTable: "group_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_expenses_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_repayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(24,12)", precision: 24, scale: 12, nullable: false),
                    RateManual = table.Column<bool>(type: "boolean", nullable: false),
                    GroupAmount = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_repayments", x => x.Id);
                    table.CheckConstraint("CK_group_repayments_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_group_repayments_group_amount_positive", "\"GroupAmount\" > 0");
                    table.CheckConstraint("CK_group_repayments_members_differ", "\"FromMemberId\" <> \"ToMemberId\"");
                    table.CheckConstraint("CK_group_repayments_rate_positive", "\"Rate\" > 0");
                    table.ForeignKey(
                        name: "FK_group_repayments_group_members_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "group_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_repayments_group_members_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "group_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_repayments_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_expense_shares",
                columns: table => new
                {
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    GroupAmount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_expense_shares", x => new { x.ExpenseId, x.MemberId });
                    table.CheckConstraint("CK_group_expense_shares_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_group_expense_shares_group_amount_not_negative", "\"GroupAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_group_expense_shares_group_expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "group_expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_expense_shares_group_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "group_members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_group_expense_shares_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_categories_GroupId",
                table: "group_categories",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_entry_events_ActorMemberId",
                table: "group_entry_events",
                column: "ActorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_group_entry_events_ActorUserId",
                table: "group_entry_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_group_entry_events_GroupId_Kind_EntryId_At",
                table: "group_entry_events",
                columns: new[] { "GroupId", "Kind", "EntryId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_group_expense_shares_GroupId",
                table: "group_expense_shares",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_expense_shares_MemberId",
                table: "group_expense_shares",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_group_expenses_CategoryId",
                table: "group_expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_group_expenses_GroupId_Date",
                table: "group_expenses",
                columns: new[] { "GroupId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_group_expenses_PayerId",
                table: "group_expenses",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_GroupId_Position",
                table: "group_members",
                columns: new[] { "GroupId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_repayments_FromMemberId",
                table: "group_repayments",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_group_repayments_GroupId_Date",
                table: "group_repayments",
                columns: new[] { "GroupId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_group_repayments_ToMemberId",
                table: "group_repayments",
                column: "ToMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_groups_OwnerId_CreatedAt",
                table: "groups",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_entry_events");

            migrationBuilder.DropTable(
                name: "group_expense_shares");

            migrationBuilder.DropTable(
                name: "group_repayments");

            migrationBuilder.DropTable(
                name: "group_expenses");

            migrationBuilder.DropTable(
                name: "group_categories");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "groups");
        }
    }
}

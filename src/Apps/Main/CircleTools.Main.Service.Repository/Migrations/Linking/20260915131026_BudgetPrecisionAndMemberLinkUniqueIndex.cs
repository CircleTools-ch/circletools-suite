using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Linking
{
    /// <inheritdoc />
    public partial class BudgetPrecisionAndMemberLinkUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_member_links_GroupId_TargetAccountId_State",
                schema: "linking",
                table: "member_links");

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetMinAmount",
                schema: "linking",
                table: "assignments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                schema: "linking",
                table: "assignments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_links_GroupId_TargetAccountId",
                schema: "linking",
                table: "member_links",
                columns: new[] { "GroupId", "TargetAccountId" },
                unique: true,
                filter: "\"State\" = 'Linked' AND \"TargetAccountId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_member_links_GroupId_TargetAccountId",
                schema: "linking",
                table: "member_links");

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetMinAmount",
                schema: "linking",
                table: "assignments",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BudgetAmount",
                schema: "linking",
                table: "assignments",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_links_GroupId_TargetAccountId_State",
                schema: "linking",
                table: "member_links",
                columns: new[] { "GroupId", "TargetAccountId", "State" });
        }
    }
}

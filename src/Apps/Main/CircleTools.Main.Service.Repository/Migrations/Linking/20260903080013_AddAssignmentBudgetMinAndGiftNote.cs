using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Linking
{
    /// <inheritdoc />
    public partial class AddAssignmentBudgetMinAndGiftNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetMinAmount",
                schema: "linking",
                table: "assignments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientGiftNote",
                schema: "linking",
                table: "assignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetMinAmount",
                schema: "linking",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "RecipientGiftNote",
                schema: "linking",
                table: "assignments");
        }
    }
}

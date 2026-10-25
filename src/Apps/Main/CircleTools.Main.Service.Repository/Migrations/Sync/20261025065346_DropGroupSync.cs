using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.Main.Service.Repository.Migrations.Sync
{
    /// <summary>
    /// Removes the group copies the account sync used to carry, and their un-claim markers (ADR-0086): online
    /// groups live in the Secret-Santa group store now, and the sync store keeps profile and preferences only.
    /// </summary>
    public partial class DropGroupSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.Sql("""DELETE FROM sync.aggregates WHERE "Kind" = 'group';""");
            migrationBuilder.Sql("""DELETE FROM sync.markers WHERE "Kind" = 'group' OR "MarkerType" = 'Unclaim';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One way: deleted group copies are not restored.
        }
    }
}

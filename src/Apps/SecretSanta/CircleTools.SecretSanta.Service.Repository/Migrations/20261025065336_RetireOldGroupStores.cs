using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CircleTools.SecretSanta.Service.Repository.Migrations
{
    /// <summary>
    /// Drops the stores this one replaced (ADR-0086): the linking schema (member links, published assignments,
    /// wishlist snapshots) and the shared schema (link-shared groups, reveal slices), each with its own migration
    /// history. Nothing moves: the apps were not live, so online groups start empty (decision D2).
    /// </summary>
    public partial class RetireOldGroupStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS linking CASCADE;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS shared CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One way: the retired stores' code is gone, so there is nothing to recreate them for.
        }
    }
}

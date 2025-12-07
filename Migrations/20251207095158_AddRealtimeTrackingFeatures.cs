using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace t12Project.Migrations
{
    /// <inheritdoc />
    public partial class AddRealtimeTrackingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LoadId",
                table: "LocationUpdates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationUpdates_LoadId",
                table: "LocationUpdates",
                column: "LoadId");

            migrationBuilder.AddForeignKey(
                name: "FK_LocationUpdates_Loads_LoadId",
                table: "LocationUpdates",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LocationUpdates_Loads_LoadId",
                table: "LocationUpdates");

            migrationBuilder.DropIndex(
                name: "IX_LocationUpdates_LoadId",
                table: "LocationUpdates");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LoadId",
                table: "LocationUpdates");
        }
    }
}

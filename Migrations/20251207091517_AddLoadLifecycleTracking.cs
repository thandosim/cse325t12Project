using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace t12Project.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadLifecycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcceptedAt",
                table: "Loads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedDriverId",
                table: "Loads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Loads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "Loads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedTimeOfArrivalMinutes",
                table: "Loads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PickedUpAt",
                table: "Loads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Bookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loads_AssignedDriverId",
                table: "Loads",
                column: "AssignedDriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loads_AspNetUsers_AssignedDriverId",
                table: "Loads",
                column: "AssignedDriverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loads_AspNetUsers_AssignedDriverId",
                table: "Loads");

            migrationBuilder.DropIndex(
                name: "IX_Loads_AssignedDriverId",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "AssignedDriverId",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "EstimatedTimeOfArrivalMinutes",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "PickedUpAt",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Bookings");
        }
    }
}

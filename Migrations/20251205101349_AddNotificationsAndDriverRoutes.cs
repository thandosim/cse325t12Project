using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace t12Project.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndDriverRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Loads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<decimal>(
                name: "DropoffLatitude",
                table: "Loads",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DropoffLongitude",
                table: "Loads",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLatitude",
                table: "Loads",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLongitude",
                table: "Loads",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DriverRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    StartLocation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EndLocation = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartLatitude = table.Column<decimal>(type: "numeric", nullable: false),
                    StartLongitude = table.Column<decimal>(type: "numeric", nullable: false),
                    EndLatitude = table.Column<decimal>(type: "numeric", nullable: false),
                    EndLongitude = table.Column<decimal>(type: "numeric", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EquipmentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MaxWeightLbs = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverRoutes_AspNetUsers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverRoutes_DriverId",
                table: "DriverRoutes",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverRoutes");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "DropoffLatitude",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "DropoffLongitude",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "Loads");
        }
    }
}

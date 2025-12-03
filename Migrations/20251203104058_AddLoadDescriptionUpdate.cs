using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace t12Project.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadDescriptionUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CargoType",
                table: "Loads",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Loads",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CargoType",
                table: "Loads");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Loads");
        }
    }
}

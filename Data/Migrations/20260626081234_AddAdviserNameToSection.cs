using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookHiveLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdviserNameToSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdviserName",
                table: "Sections",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdviserName",
                table: "Sections");
        }
    }
}

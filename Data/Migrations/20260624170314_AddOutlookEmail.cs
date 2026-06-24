using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookHiveLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlookEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OutlookEmail",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutlookEmail",
                table: "AspNetUsers");
        }
    }
}

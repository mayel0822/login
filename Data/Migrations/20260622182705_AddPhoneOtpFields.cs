using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookHiveLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneOtpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneOTPCode",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneOTPExpiration",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneOTPCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PhoneOTPExpiration",
                table: "AspNetUsers");
        }
    }
}

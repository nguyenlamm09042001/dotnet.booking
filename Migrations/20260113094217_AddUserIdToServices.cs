using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Migrations
{
    public partial class AddUserIdToServices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add column UserId vào Services
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 2) Create index
            migrationBuilder.CreateIndex(
                name: "IX_Services_UserId",
                table: "Services",
                column: "UserId");

            // 3) Add FK -> Users(Id)
            migrationBuilder.AddForeignKey(
                name: "FK_Services_Users_UserId",
                table: "Services",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Users_UserId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_UserId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Services");
        }
    }
}

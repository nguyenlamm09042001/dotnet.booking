using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceBusinessUserNav : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.CreateIndex(
            //     name: "IX_Services_UserId",
            //     table: "Services",
            //     column: "UserId");

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Services_Users_UserId",
            //     table: "Services",
            //     column: "UserId",
            //     principalTable: "Users",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Services_Users_UserId",
            //     table: "Services");

            // migrationBuilder.DropIndex(
            //     name: "IX_Services_UserId",
            //     table: "Services");
        }
    }
}

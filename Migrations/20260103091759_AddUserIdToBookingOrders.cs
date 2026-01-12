using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Migrations
{
    public partial class AddUserIdToBookingOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ UserId đã tồn tại trong DB => KHÔNG add nữa

            // ✅ Tạo index (nếu DB chưa có index này)
            migrationBuilder.CreateIndex(
                name: "IX_BookingOrders_UserId",
                table: "BookingOrders",
                column: "UserId");

            // ✅ Tạo FK (nếu DB chưa có FK này)
            migrationBuilder.AddForeignKey(
                name: "FK_BookingOrders_Users_UserId",
                table: "BookingOrders",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ✅ rollback chỉ cần drop FK + Index, KHÔNG drop cột
            migrationBuilder.DropForeignKey(
                name: "FK_BookingOrders_Users_UserId",
                table: "BookingOrders");

            migrationBuilder.DropIndex(
                name: "IX_BookingOrders_UserId",
                table: "BookingOrders");

            // ❌ KHÔNG DropColumn nữa vì cột vốn đã tồn tại
        }
    }
}

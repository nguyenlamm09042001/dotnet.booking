using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBusinessUserIdFromServices : Migration
    {
        /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "BusinessUserId",
        table: "Services");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "BusinessUserId",
        table: "Services",
        type: "int",
        nullable: false,
        defaultValue: 0);
}

    }
}

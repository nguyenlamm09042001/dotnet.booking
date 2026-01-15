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
     // no-op: column BusinessUserId does not exist in fresh DB

}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // no-op

}

    }
}

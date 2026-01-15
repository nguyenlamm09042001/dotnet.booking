using booking.Models;
using Microsoft.EntityFrameworkCore;

namespace booking.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.BusinessCategories.AnyAsync())
        {
            db.BusinessCategories.AddRange(
                new BusinessCategory { Code = "hair", Name = "Tóc" },
                new BusinessCategory { Code = "car", Name = "Xe" },
                new BusinessCategory { Code = "hotel", Name = "Khách sạn" }
            );

            await db.SaveChangesAsync();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using booking.Models;

namespace booking.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<BookingOrder> BookingOrders => Set<BookingOrder>();
    public DbSet<ServiceReview> ServiceReviews => Set<ServiceReview>();

    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffService> StaffServices => Set<StaffService>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================
        // Service decimal precision (fix warning truncate)
        // =========================
        modelBuilder.Entity<Service>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Service>()
            .Property(x => x.Rating)
            .HasPrecision(3, 1); // vd: 4.8

        // =========================
        // Staff tables keys (BẮT BUỘC)
        // =========================
        modelBuilder.Entity<StaffProfile>()
            .HasKey(x => x.UserId);

        modelBuilder.Entity<StaffService>()
            .HasKey(x => new { x.StaffUserId, x.ServiceId });

        // =========================
        // ServiceReview constraints
        // =========================
        modelBuilder.Entity<ServiceReview>(e =>
        {
            // 1 booking chỉ được review 1 lần
            e.HasIndex(x => x.BookingOrderId).IsUnique();

            // ✅ Chỉ cascade 1 đường: BookingOrder -> Review
            e.HasOne(x => x.BookingOrder)
                .WithMany() // không cần navigation
                .HasForeignKey(x => x.BookingOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ tránh multiple cascade paths
            e.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict); // NO ACTION

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict); // NO ACTION
        });
        modelBuilder.Entity<StaffProfile>(e =>
{
    e.HasKey(x => x.UserId);

    // ✅ UserId vừa là PK vừa là FK -> Users.Id
    e.Property(x => x.UserId).ValueGeneratedNever();

    e.HasOne(x => x.User)
        .WithMany()
        .HasForeignKey(x => x.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});

        modelBuilder.Entity<StaffService>()
            .HasKey(x => new { x.StaffUserId, x.ServiceId });
    }


}

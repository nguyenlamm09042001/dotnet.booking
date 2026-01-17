using Microsoft.EntityFrameworkCore;
using booking.Models;

namespace booking.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // =========================
    // CORE TABLES
    // =========================
    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<BookingOrder> BookingOrders => Set<BookingOrder>();
    public DbSet<ServiceReview> ServiceReviews => Set<ServiceReview>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<MarketingOrder> MarketingOrders => Set<MarketingOrder>();



    // =========================
    // BUSINESS CATEGORY
    // =========================
    public DbSet<BusinessCategory> BusinessCategories => Set<BusinessCategory>();
    public DbSet<BusinessCategoryLink> BusinessCategoryLinks => Set<BusinessCategoryLink>();

    // =========================
    // STAFF
    // =========================
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffService> StaffServices => Set<StaffService>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================
        // SERVICE (ALL CONFIG IN ONE PLACE)
        // =====================================================
        modelBuilder.Entity<Service>(e =>
        {
            // decimal precision
            e.Property(x => x.Price)
                .HasPrecision(18, 2);

            e.Property(x => x.Rating)
                .HasPrecision(3, 1);

            // SERVICE -> SERVICE CATEGORY
            e.HasOne(x => x.ServiceCategory)
                .WithMany(c => c.Services)
                .HasForeignKey(x => x.ServiceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // SERVICE -> USER (BusinessUser)
            e.HasOne(x => x.BusinessUser)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // STAFF PROFILE
        // =====================================================
        modelBuilder.Entity<StaffProfile>(e =>
        {
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserId)
                .ValueGeneratedNever();

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // STAFF SERVICE (many-to-many)
        // =====================================================
        modelBuilder.Entity<StaffService>()
            .HasKey(x => new { x.StaffUserId, x.ServiceId });

        // =====================================================
        // SERVICE REVIEW
        // =====================================================
        modelBuilder.Entity<ServiceReview>(e =>
        {
            e.HasIndex(x => x.BookingOrderId)
                .IsUnique();

            e.HasOne(x => x.BookingOrder)
                .WithMany()
                .HasForeignKey(x => x.BookingOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // BUSINESS CATEGORY
        // =====================================================
        modelBuilder.Entity<BusinessCategory>(e =>
        {
            e.HasIndex(x => x.Code)
                .IsUnique();

            e.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            e.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();
        });

        // =====================================================
        // SERVICE CATEGORY
        // =====================================================
        modelBuilder.Entity<ServiceCategory>(e =>
        {
            e.HasIndex(x => x.Code)
                .IsUnique();

            e.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            e.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();
        });

        // =====================================================
        // BUSINESS CATEGORY LINK (many-to-many)
        // =====================================================
        modelBuilder.Entity<BusinessCategoryLink>(e =>
        {
            e.HasKey(x => new { x.BusinessUserId, x.CategoryId });

            e.HasOne(x => x.BusinessUser)
                .WithMany()
                .HasForeignKey(x => x.BusinessUserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Category)
                .WithMany(x => x.BusinessLinks)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

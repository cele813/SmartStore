using Microsoft.EntityFrameworkCore;
using SmartStore.Domain;
namespace SmartStore.Infrastructure;

public sealed class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SaleOrder> SaleOrders => Set<SaleOrder>();
    public DbSet<SaleOrderItem> SaleOrderItems => Set<SaleOrderItem>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>().Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Supplier>().Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Entity<Supplier>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<Supplier>().Property(x => x.Phone).HasMaxLength(30);
        var p = b.Entity<Product>();
        p.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        p.HasIndex(x => x.Sku).IsUnique();
        p.Property(x => x.Name).HasMaxLength(200).IsRequired();
        p.Property(x => x.Price).HasPrecision(18, 2);
        p.Property<uint>("Version").IsRowVersion();
        p.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        p.ToTable("Products", t => { t.HasCheckConstraint("CK_Product_Stock", "\"Stock\" >= 0"); t.HasCheckConstraint("CK_Product_Price", "\"Price\" >= 0"); });
        p.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().Property(x => x.Reason).HasMaxLength(300).IsRequired();
        b.Entity<StockMovement>().ToTable("StockMovements", t => t.HasCheckConstraint("CK_Movement_Quantity", "\"Quantity\" <> 0"));
        b.Entity<SaleOrder>().Ignore(x => x.Total);
        b.Entity<SaleOrder>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SaleOrderId);
        b.Entity<SaleOrderItem>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SaleOrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Entity<SaleOrderItem>().ToTable("SaleOrderItems", t => { t.HasCheckConstraint("CK_Item_Quantity", "\"Quantity\" > 0"); t.HasCheckConstraint("CK_Item_Price", "\"UnitPrice\" >= 0"); });
    }
}

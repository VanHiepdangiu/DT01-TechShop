using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TechShop.Models;
namespace TechShop.Data;
public class ShopDbContext(DbContextOptions<ShopDbContext> options):IdentityDbContext<ShopUser,IdentityRole<int>,int>(options)
{
 public DbSet<Product>Products=>Set<Product>();public DbSet<Category>Categories=>Set<Category>();public DbSet<Brand>Brands=>Set<Brand>();
 public DbSet<Coupon>Coupons=>Set<Coupon>();public DbSet<Order>Orders=>Set<Order>();public DbSet<OrderItem>OrderItems=>Set<OrderItem>();
 public DbSet<ProductImage>ProductImages=>Set<ProductImage>();public DbSet<ProductSpec>ProductSpecs=>Set<ProductSpec>();public DbSet<Review>Reviews=>Set<Review>();
 public DbSet<Address>Addresses=>Set<Address>();public DbSet<ReturnRequest>ReturnRequests=>Set<ReturnRequest>();
 public DbSet<BankTransactionLog>BankTransactions=>Set<BankTransactionLog>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  base.OnModelCreating(b);
  b.Entity<Category>().HasIndex(x=>x.Name).IsUnique();b.Entity<Brand>().HasIndex(x=>x.Name).IsUnique();
  b.Entity<ShopUser>().HasIndex(x=>x.NormalizedEmail).IsUnique();
  b.Entity<Product>().HasIndex(x=>new{x.IsActive,x.CategoryId});
  b.Entity<Coupon>().HasIndex(x=>x.Code).IsUnique();
  b.Entity<Order>().HasIndex(x=>x.IdempotencyKey).IsUnique();b.Entity<Order>().HasIndex(x=>new{x.UserId,x.CreatedAt});
  b.Entity<Review>().HasIndex(x=>new{x.ProductId,x.UserId}).IsUnique();b.Entity<ReturnRequest>().HasIndex(x=>x.OrderId).IsUnique();
  b.Entity<BankTransactionLog>().HasIndex(x=>x.TransactionId).IsUnique();b.Entity<BankTransactionLog>().Property(x=>x.Amount).HasPrecision(18,2);
  b.Entity<OrderItem>().HasOne(x=>x.Product).WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Order>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Review>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Address>().HasOne<ShopUser>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<ReturnRequest>().HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Product>().ToTable(t=>{t.HasCheckConstraint("CK_Product_Stock","Stock >= 0");t.HasCheckConstraint("CK_Product_Price","Price > 0");});
  b.Entity<Coupon>().ToTable(t=>t.HasCheckConstraint("CK_Coupon_Usage","UsedCount >= 0 AND UsedCount <= UsageLimit"));
  foreach(var e in b.Model.GetEntityTypes())foreach(var p in e.GetProperties())if(p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?)){p.SetPrecision(18);p.SetScale(2);}
  // SQL Server: genuine DB-generated rowversion. SQLite: application-generated binary token.
  if(Database.IsSqlite())
  {b.Entity<Product>().Property(x=>x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();b.Entity<Coupon>().Property(x=>x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();b.Entity<Order>().Property(x=>x.RowVersion).IsConcurrencyToken().ValueGeneratedNever();}
 }
 void StampVersions(){if(!Database.IsSqlite())return;foreach(var e in ChangeTracker.Entries().Where(x=>x.State==EntityState.Added||x.State==EntityState.Modified)){if(e.Entity is Product||e.Entity is Coupon||e.Entity is Order)e.Property("RowVersion").CurrentValue=RandomNumberGenerator.GetBytes(8);}}
 public override int SaveChanges(bool acceptAllChangesOnSuccess){StampVersions();return base.SaveChanges(acceptAllChangesOnSuccess);}
 public override Task<int>SaveChangesAsync(bool acceptAllChangesOnSuccess,CancellationToken cancellationToken=default){StampVersions();return base.SaveChangesAsync(acceptAllChangesOnSuccess,cancellationToken);}
}

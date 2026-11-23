using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace TechShop.Models;

public class ShopUser : IdentityUser<int>
{
 [Required,StringLength(100)] public string FullName {get;set;}="";
 public bool IsActive {get;set;}=true;
}
public class Category
{
 public int Id {get;set;}
 [Required,StringLength(80)] public string Name {get;set;}="";
 public bool IsActive {get;set;}=true;
}
public class Brand
{
 public int Id {get;set;}
 [Required,StringLength(80)] public string Name {get;set;}="";
 public bool IsActive {get;set;}=true;
}
public class Product
{
 public int Id {get;set;}
 [Required,StringLength(150)] public string Name {get;set;}="";
 [Range(1,1000000000)] public decimal Price {get;set;}
 [Range(0,1000000000)] public decimal? SalePrice {get;set;}
 [Range(0,1000000)] public int Stock {get;set;}
 public int CategoryId {get;set;}
 public Category? Category {get;set;}
 public int? BrandId {get;set;}
 public Brand? Brand {get;set;}
 [StringLength(4000)] public string Description {get;set;}="";
 public bool IsActive {get;set;}=true;
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 [Timestamp] public byte[] RowVersion {get;set;}=Array.Empty<byte>();
 public List<ProductImage> Images {get;set;}=new();
 public List<ProductSpec> Specs {get;set;}=new();
 public List<Review> Reviews {get;set;}=new();
 [NotMapped] public decimal EffectivePrice => SalePrice.HasValue && SalePrice>0 && SalePrice<Price ? SalePrice.Value:Price;
}
public class ProductImage {public int Id{get;set;}public int ProductId{get;set;}[Required,StringLength(300)]public string Url{get;set;}="";public bool IsMain{get;set;}}
public class ProductSpec {public int Id{get;set;}public int ProductId{get;set;}[Required,StringLength(80)]public string SpecName{get;set;}="";[Required,StringLength(200)]public string SpecValue{get;set;}="";}
public class Review {public int Id{get;set;}public int ProductId{get;set;}public int UserId{get;set;}public ShopUser? User{get;set;}[Range(1,5)]public int Rating{get;set;}[Required,StringLength(1000)]public string Comment{get;set;}="";public DateTime CreatedAt{get;set;}=DateTime.UtcNow;}
public class Address {public int Id{get;set;}public int UserId{get;set;}[Required,StringLength(100)]public string Recipient{get;set;}="";[Required,StringLength(20)]public string Phone{get;set;}="";[Required,StringLength(300)]public string Detail{get;set;}="";}
public class Coupon
{
 public int Id{get;set;}[Required,StringLength(30)]public string Code{get;set;}="";
 [Range(1,100)]public int DiscountPercent{get;set;}public DateTime ExpiredAt{get;set;}public int UsageLimit{get;set;}public int UsedCount{get;set;}
 [Timestamp]public byte[] RowVersion{get;set;}=Array.Empty<byte>();
}
public class Order
{
 public int Id{get;set;}[NotMapped]public string Code=>$"TS{Id:000000}";
 public int UserId{get;set;}public ShopUser? User{get;set;}
 public Guid IdempotencyKey{get;set;}
 public string FullName{get;set;}="";public string Phone{get;set;}="";public string ShippingAddress{get;set;}="";
 public decimal Subtotal{get;set;}public decimal Discount{get;set;}public decimal ShippingFee{get;set;}public decimal Total{get;set;}
 public string? CouponCode{get;set;}public int? CouponId{get;set;}
 public string Status{get;set;}=OrderStates.New;public string PaymentMethod{get;set;}="COD";public string PaymentStatus{get;set;}="Unpaid";public string? PaymentTransactionId{get;set;}
 public bool StockRestored{get;set;}public string InternalNote{get;set;}="";
 public DateTime CreatedAt{get;set;}=DateTime.UtcNow;
 [Timestamp]public byte[] RowVersion{get;set;}=Array.Empty<byte>();
 public List<OrderItem>Items{get;set;}=new();public List<OrderHistory>History{get;set;}=new();
}
public class OrderItem {public int Id{get;set;}public int OrderId{get;set;}public Order? Order{get;set;}public int ProductId{get;set;}public Product? Product{get;set;}public string ProductName{get;set;}="";public decimal UnitPrice{get;set;}public int Quantity{get;set;}[NotMapped]public decimal LineTotal=>UnitPrice*Quantity;}
public class OrderHistory {public int Id{get;set;}public int OrderId{get;set;}public string Status{get;set;}="";public string Note{get;set;}="";public DateTime CreatedAt{get;set;}=DateTime.UtcNow;}
public class ReturnRequest {public int Id{get;set;}public int OrderId{get;set;}public int UserId{get;set;}[Required,StringLength(500)]public string Reason{get;set;}="";public string Status{get;set;}="Requested";public DateTime CreatedAt{get;set;}=DateTime.UtcNow;}
public static class OrderStates
{
 public const string New="New",PendingPayment="PendingPayment",Packing="Packing",Shipping="Shipping",Completed="Completed",Cancelled="Cancelled",Returned="Returned";
 public static string Label(string s)=>s switch{New=>"Mới",PendingPayment=>"Chờ thanh toán",Packing=>"Đóng gói",Shipping=>"Đang giao",Completed=>"Hoàn tất",Cancelled=>"Đã hủy",Returned=>"Đã hoàn hàng",_=>s};
 public static bool CanMove(string from,string to)=>from switch{New=>to==Packing||to==Cancelled,PendingPayment=>to==Cancelled,Packing=>to==Shipping,Shipping=>to==Completed,Completed=>to==Returned,_=>false};
}

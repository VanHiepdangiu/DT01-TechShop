using System.ComponentModel.DataAnnotations;
using TechShop.Services;
namespace TechShop.Models;
public class LoginModel{[Required,EmailAddress]public string Email{get;set;}="";[Required,DataType(DataType.Password)]public string Password{get;set;}="";public bool RememberMe{get;set;}public string? ReturnUrl{get;set;}}
public class RegisterModel
{
 [Required,StringLength(100)]public string FullName{get;set;}="";[Required,EmailAddress]public string Email{get;set;}="";
 [Required,StringLength(128,MinimumLength=8),RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9\s]).{8,128}$",ErrorMessage="Mật khẩu phải có chữ hoa, chữ thường, số và ký tự đặc biệt."),DataType(DataType.Password)]public string Password{get;set;}="";
 [Required,Compare(nameof(Password)),DataType(DataType.Password)]public string ConfirmPassword{get;set;}="";
}
public class AuthPage{public LoginModel Login{get;set;}=new();public RegisterModel Register{get;set;}=new();public bool RegisterTab{get;set;}}
public class CheckoutModel{[Required,StringLength(100)]public string FullName{get;set;}="";[Required,RegularExpression(@"^0\d{9}$")]public string Phone{get;set;}="";[Required,StringLength(300)]public string Address{get;set;}="";public string PaymentMethod{get;set;}="COD";public string? CouponCode{get;set;}public Guid Key{get;set;}=Guid.NewGuid();}
public class CatalogModel{public List<Product>Products{get;set;}=new();public List<Category>Categories{get;set;}=new();public List<Brand>Brands{get;set;}=new();public string? Q{get;set;}public int? CategoryId{get;set;}public int? BrandId{get;set;}public string? Sort{get;set;}public decimal? Min{get;set;}public decimal? Max{get;set;}public int Page{get;set;}public int Pages{get;set;}public int Total{get;set;}}
public record CartViewLine(Product Product,int Quantity){public decimal Amount=>Product.EffectivePrice*Quantity;}
public class ProductForm
{
 public int Id{get;set;}[Required,StringLength(150)]public string Name{get;set;}="";[Range(1,1000000000)]public decimal Price{get;set;}[Range(0,1000000000)]public decimal? SalePrice{get;set;}[Range(0,1000000)]public int Stock{get;set;}public int CategoryId{get;set;}public int? BrandId{get;set;}public bool IsActive{get;set;}=true;[StringLength(4000)]public string Description{get;set;}="";public string? RowVersion{get;set;}
 public string SpecsText{get;set;}="";
 // Thông số theo dòng khi biên tập sản phẩm
 public List<SpecInput> Specs{get;set;}=new();
 // Danh sách ảnh cần xoá khi lưu
 public List<int> RemoveImageIds{get;set;}=new();
 // Ảnh được đặt làm ảnh chính
 public int? MainImageId{get;set;}
}
public static class IdentityExtensions{public static int UserId(this System.Security.Claims.ClaimsPrincipal u)=>int.Parse(u.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechShop.Models;
namespace TechShop.Data;
public static class Seed
{
 public static async Task InitializeAsync(IServiceProvider sp)
 {
  var db=sp.GetRequiredService<ShopDbContext>();var users=sp.GetRequiredService<UserManager<ShopUser>>();var roles=sp.GetRequiredService<RoleManager<IdentityRole<int>>>();
  foreach(var r in new[]{"Admin","Staff","Customer"})if(!await roles.RoleExistsAsync(r))await roles.CreateAsync(new IdentityRole<int>(r));
  foreach(var (email,name,role,password) in new[]{("admin@techshop.local","Quản trị viên","Admin","Admin@123"),("staff@techshop.local","Nhân viên bán hàng","Staff","Staff@123"),("customer@techshop.local","Khách hàng mẫu","Customer","Customer@123")})
  {if(await users.FindByEmailAsync(email)==null){var user=new ShopUser{UserName=email,Email=email,FullName=name,EmailConfirmed=true};var result=await users.CreateAsync(user,password);if(!result.Succeeded)throw new InvalidOperationException(string.Join(";",result.Errors.Select(x=>x.Description)));await users.AddToRoleAsync(user,role);}}
  if(await db.Products.AnyAsync())return;
  var cats=new[]{new Category{Name="Laptop"},new Category{Name="Điện thoại"},new Category{Name="Phụ kiện"}};
  var brands=new[]{new Brand{Name="Apple"},new Brand{Name="Samsung"},new Brand{Name="ASUS"},new Brand{Name="Logitech"}};
  db.AddRange(cats);db.AddRange(brands);await db.SaveChangesAsync();
  var samples=new[]{("MacBook Air M3 13 inch",27990000m,12,0,0,"16 GB","512 GB","laptop"),("ASUS Vivobook 15 OLED",18990000m,8,0,2,"16 GB","512 GB","laptop"),("iPhone 15 128GB",17990000m,15,1,0,"6 GB","128 GB","phone"),("Samsung Galaxy S24",16990000m,10,1,1,"8 GB","256 GB","phone"),("Apple AirPods Pro",5490000m,4,2,0,"Bluetooth 5.3","USB-C","accessories"),("Logitech MX Master 3S",2290000m,1,2,3,"Bluetooth","8000 DPI","accessories"),("MacBook Pro M3 14 inch",39990000m,6,0,0,"16 GB","512 GB","laptop"),("Samsung Galaxy Tab S9",14990000m,5,1,1,"8 GB","128 GB","phone"),("ASUS ROG Gaming",32990000m,7,0,2,"32 GB","1 TB","laptop"),("Magic Keyboard",2990000m,11,2,0,"Wireless","USB-C","accessories"),("Samsung Galaxy A55",8990000m,20,1,1,"8 GB","256 GB","phone"),("Logitech Webcam C920",1590000m,9,2,3,"Full HD","USB-A","accessories")};
  foreach(var (name,price,stock,cat,brand,a,b,image) in samples)
  {
   db.Products.Add(new Product{Name=name,Price=price,SalePrice=price>10000000?price-500000:null,Stock=stock,CategoryId=cats[cat].Id,BrandId=brands[brand].Id,Description="Thiết bị công nghệ dành cho học tập và làm việc. Sản phẩm và hình minh họa dùng cho bài thực hành TechShop; không phải cửa hàng thanh toán thật.",Images=new(){new ProductImage{Url=$"/images/{image}.svg",IsMain=true}},Specs=new(){new ProductSpec{SpecName="Cấu hình",SpecValue=a},new ProductSpec{SpecName="Bộ nhớ / kết nối",SpecValue=b},new ProductSpec{SpecName="Bảo hành",SpecValue="12 tháng (dữ liệu minh họa)"}}});
  }
  db.Coupons.Add(new Coupon{Code="TECH10",DiscountPercent=10,UsageLimit=100,ExpiredAt=DateTime.UtcNow.AddMonths(6)});await db.SaveChangesAsync();
 }
}

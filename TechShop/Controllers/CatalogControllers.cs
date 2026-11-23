using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
namespace TechShop.Controllers;
public class HomeController(ShopDbContext db):Controller
{
 [AllowAnonymous]public async Task<IActionResult>Index()=>View(await db.Products.AsNoTracking().Include(x=>x.Images).Include(x=>x.Category).Where(x=>x.IsActive).OrderByDescending(x=>x.CreatedAt).Take(8).ToListAsync());
 public IActionResult Error(){Response.StatusCode=500;return View();}
}
public class ProductsController(ShopDbContext db):Controller
{
 [AllowAnonymous]public async Task<IActionResult>Index(string? q,int? categoryId,int? brandId,decimal? min,decimal? max,string? sort,int page=1)
 {
  var x=db.Products.AsNoTracking().Include(p=>p.Images).Include(p=>p.Category).Where(p=>p.IsActive);
  if(!string.IsNullOrWhiteSpace(q))x=x.Where(p=>p.Name.Contains(q));if(categoryId.HasValue)x=x.Where(p=>p.CategoryId==categoryId);if(brandId.HasValue)x=x.Where(p=>p.BrandId==brandId);
  if(min.HasValue)x=x.Where(p=>(p.SalePrice??p.Price)>=min);if(max.HasValue)x=x.Where(p=>(p.SalePrice??p.Price)<=max);
  // SQLite cannot ORDER BY decimal; SQL Server executes native decimal order.
  int total=await x.CountAsync();page=Math.Clamp(page,1,Math.Max(1,(total+11)/12));
  var sorted=sort switch{"priceAsc"=>x.OrderBy(p=>(double)(p.SalePrice??p.Price)),"priceDesc"=>x.OrderByDescending(p=>(double)(p.SalePrice??p.Price)),"name"=>x.OrderBy(p=>p.Name),_=>x.OrderByDescending(p=>p.Id)};
  return View(new CatalogModel{Products=await sorted.Skip((page-1)*12).Take(12).ToListAsync(),Categories=await db.Categories.Where(c=>c.IsActive).ToListAsync(),Brands=await db.Brands.Where(c=>c.IsActive).ToListAsync(),Q=q,CategoryId=categoryId,BrandId=brandId,Min=min,Max=max,Sort=sort,Page=page,Pages=Math.Max(1,(total+11)/12),Total=total});
 }
 [AllowAnonymous]public async Task<IActionResult>Details(int id){var x=await db.Products.Include(p=>p.Images).Include(p=>p.Specs).Include(p=>p.Reviews).ThenInclude(r=>r.User).Include(p=>p.Brand).SingleOrDefaultAsync(p=>p.Id==id&&p.IsActive);return x==null?NotFound():View(x);}
 [AllowAnonymous]public async Task<IActionResult>Compare(string? ids)
 {var all=(ids??"").Split(',').Select(x=>int.TryParse(x,out var a)?a:0).Where(x=>x>0).Distinct().ToArray();if(all.Length>3){TempData["Error"]="Chỉ so sánh tối đa 3 sản phẩm.";return RedirectToAction(nameof(Index));}return View(await db.Products.Include(x=>x.Specs).Include(x=>x.Images).Where(x=>all.Contains(x.Id)&&x.IsActive).ToListAsync());}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Review(int productId,int rating,string comment)
 {
  var uid=User.UserId();if(rating<1||rating>5||string.IsNullOrWhiteSpace(comment)||comment.Length>1000||!await db.Products.AnyAsync(x=>x.Id==productId&&x.IsActive))return BadRequest();
  if(!await db.Reviews.AnyAsync(x=>x.ProductId==productId&&x.UserId==uid)){db.Reviews.Add(new Review{ProductId=productId,UserId=uid,Rating=rating,Comment=comment.Trim()});await db.SaveChangesAsync();}return RedirectToAction(nameof(Details),new{id=productId});
 }
}

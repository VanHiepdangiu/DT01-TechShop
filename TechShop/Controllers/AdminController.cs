using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;
namespace TechShop.Controllers;
[Authorize(Roles="Admin,Staff")]
public class AdminController(ShopDbContext db,UserManager<ShopUser> users,IWebHostEnvironment env,IConfiguration config):Controller
{
 public IActionResult Index()=>View();
 // FR-1.4: nhập sản phẩm hàng loạt từ CSV; báo lỗi theo từng dòng và chỉ lưu khi mọi dòng hợp lệ.
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> Import(IFormFile? file)
 {
  if(file==null||file.Length==0){TempData["Error"]="Chưa chọn tệp CSV.";return RedirectToAction(nameof(Products));}
  if(file.Length>1024*1024){TempData["Error"]="Tệp CSV tối đa 1 MB.";return RedirectToAction(nameof(Products));}
  if(!string.Equals(Path.GetExtension(file.FileName),".csv",StringComparison.OrdinalIgnoreCase)){TempData["Error"]="Chỉ nhận tệp .csv (UTF-8).";return RedirectToAction(nameof(Products));}
  using var reader=new StreamReader(file.OpenReadStream(),System.Text.Encoding.UTF8,true);
  var report=await ProductImport.ImportAsync(db,await reader.ReadToEndAsync());
  if(report.Errors.Count>0){TempData["Error"]="Không nhập dòng nào. "+string.Join(" | ",report.Errors.Take(8))+(report.Errors.Count>8?$" … còn {report.Errors.Count-8} lỗi.":"");}
  else{TempData["Message"]=$"Đã nhập {report.Imported} sản phẩm từ CSV.";}
  return RedirectToAction(nameof(Products));
 }
 // FR-5.4: xuất danh mục sản phẩm ra Excel.
 [Authorize(Roles="Admin")]
 public async Task<IActionResult> ExportProducts()
 {
  var list=await db.Products.AsNoTracking().Include(x=>x.Category).Include(x=>x.Brand).OrderBy(x=>x.Id).ToListAsync();
  var bytes=XlsxWriter.Build("SanPham",new[]{"Mã","Tên sản phẩm","Danh mục","Hãng","Giá bán","Giá khuyến mãi","Tồn kho","Trạng thái","Ngày tạo"},
   list.Select(p=>new object?[]{p.Id,p.Name,p.Category?.Name??"",p.Brand?.Name??"",p.Price,p.SalePrice,p.Stock,p.IsActive?"Đang bán":"Ngừng bán",p.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")}).ToList());
  return File(bytes,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"TechShop-SanPham-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
 }
 // FR-5.4: xuất chi tiết đơn hoàn tất trong khoảng thời gian.
 [Authorize(Roles="Admin")]
 public async Task<IActionResult> ExportRevenue(DateTime? from,DateTime? to)
 {
  var start=from?.Date??DateTime.UtcNow.Date.AddDays(-29);var end=(to?.Date??DateTime.UtcNow.Date).AddDays(1);
  var orders=await db.Orders.AsNoTracking().Include(x=>x.Items).Where(x=>x.Status==OrderStates.Completed&&x.CreatedAt>=start&&x.CreatedAt<end).OrderBy(x=>x.CreatedAt).ToListAsync();
  var rows=new List<object?[]>{new object?[]{"TỔNG",$"{orders.Count} đơn",null,null,null,orders.Sum(x=>x.Total)}};
  rows.AddRange(orders.Select(o=>new object?[]{o.Code,o.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),o.FullName,o.PaymentMethod,o.Items.Sum(i=>i.Quantity),o.Total}));
  var bytes=XlsxWriter.Build("DoanhThu",new[]{"Mã đơn","Ngày hoàn tất","Khách hàng","Thanh toán","Số lượng","Tổng tiền"},rows);
  return File(bytes,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"TechShop-DoanhThu-{start:yyyyMMdd}-{end.AddDays(-1):yyyyMMdd}.xlsx");
 }
 [Authorize(Roles="Admin")]public async Task<IActionResult>Products(string? q,int? categoryId,int? brandId,string? status)
 {
  var x=db.Products.Include(p=>p.Category).Include(p=>p.Brand).AsQueryable();
  if(!string.IsNullOrWhiteSpace(q))x=x.Where(p=>p.Name.Contains(q));
  if(categoryId.HasValue)x=x.Where(p=>p.CategoryId==categoryId.Value);
  if(brandId.HasValue)x=x.Where(p=>p.BrandId==brandId.Value);
  if(status=="hidden")x=x.Where(p=>!p.IsActive);
  else if(status=="active")x=x.Where(p=>p.IsActive);
  else if(status=="low")x=x.Where(p=>p.Stock<=5);
  ViewBag.Total=await x.CountAsync();
  ViewBag.Categories=await db.Categories.AsNoTracking().OrderBy(c=>c.Name).ToListAsync();
  ViewBag.Brands=await db.Brands.AsNoTracking().OrderBy(b=>b.Name).ToListAsync();
  ViewBag.Q=q;ViewBag.CategoryId=categoryId;ViewBag.BrandId=brandId;ViewBag.Status=status;
  return View(await x.OrderByDescending(p=>p.Id).Take(200).ToListAsync());
 }
 // Trang quản lý sản phẩm dạng thẻ + nút "Thêm mới sản phẩm" nổi bật (tách khỏi form sửa).
 [Authorize(Roles="Admin")]public async Task<IActionResult>ProductManager(string? q,int? categoryId,int? brandId,string? status)
 {
  var x=db.Products.Include(p=>p.Category).Include(p=>p.Brand).Include(p=>p.Images).AsQueryable();
  if(!string.IsNullOrWhiteSpace(q))x=x.Where(p=>p.Name.Contains(q));
  if(categoryId.HasValue)x=x.Where(p=>p.CategoryId==categoryId.Value);
  if(brandId.HasValue)x=x.Where(p=>p.BrandId==brandId.Value);
  if(status=="hidden")x=x.Where(p=>!p.IsActive);
  else if(status=="active")x=x.Where(p=>p.IsActive);
  else if(status=="low")x=x.Where(p=>p.Stock<=5);
  ViewBag.Categories=await db.Categories.AsNoTracking().OrderBy(c=>c.Name).ToListAsync();
  ViewBag.Brands=await db.Brands.AsNoTracking().OrderBy(b=>b.Name).ToListAsync();
  ViewBag.Q=q;ViewBag.CategoryId=categoryId;ViewBag.BrandId=brandId;ViewBag.Status=status;
  ViewBag.StatTotal=await db.Products.CountAsync();
  ViewBag.StatActive=await db.Products.CountAsync(p=>p.IsActive);
  ViewBag.StatHidden=await db.Products.CountAsync(p=>!p.IsActive);
  ViewBag.StatLow=await db.Products.CountAsync(p=>p.Stock<=5);
  ViewBag.StatOut=await db.Products.CountAsync(p=>p.Stock==0);
  return View(await x.OrderByDescending(p=>p.Id).Take(120).ToListAsync());
 }
 // Trang riêng chỉ để THÊM MỚI sản phẩm (không lẫn với sửa).
 [Authorize(Roles="Admin")]public async Task<IActionResult>Create()
 {
  await Setup();
  ViewBag.Images=new List<ProductImage>();
  ViewBag.LastCreated=TempData["LastCreated"];
  return View("Edit",new ProductForm{IsActive=true,Stock=0});
 }
 // Nhận biểu mẫu THÊM MỚI gửi từ trang /Admin/Create.
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public Task<IActionResult>Create(ProductForm m,List<IFormFile>? photos,List<int>? removeImageIds,int? mainImageId,string? action)
 {m.Id=0;return Edit(m,photos,removeImageIds,mainImageId,action);}
 [Authorize(Roles="Admin")]public async Task<IActionResult>Edit(int? id)
 {await Setup();if(!id.HasValue)return View(new ProductForm());ViewBag.Images=await db.ProductImages.AsNoTracking().Where(i=>i.ProductId==id.Value).OrderByDescending(i=>i.IsMain).ThenBy(i=>i.Id).ToListAsync();var x=await db.Products.Include(x=>x.Specs).Include(x=>x.Images).SingleOrDefaultAsync(x=>x.Id==id);return x==null?NotFound():View(new ProductForm{Id=x.Id,Name=x.Name,Price=x.Price,SalePrice=x.SalePrice,Stock=x.Stock,CategoryId=x.CategoryId,BrandId=x.BrandId,IsActive=x.IsActive,Description=x.Description,RowVersion=Convert.ToBase64String(x.RowVersion),SpecsText=string.Join("\n",x.Specs.Select(s=>s.SpecName+"="+s.SpecValue)),Specs=x.Specs.Select(s=>new SpecInput{Name=s.SpecName,Value=s.SpecValue}).ToList(),MainImageId=x.Images.OrderByDescending(i=>i.IsMain).ThenBy(i=>i.Id).Select(i=>i.Id).FirstOrDefault()==0?null:x.Images.OrderByDescending(i=>i.IsMain).ThenBy(i=>i.Id).Select(i=>i.Id).FirstOrDefault()});}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Edit(ProductForm m,List<IFormFile>? photos,List<int>? removeImageIds,int? mainImageId,string? action)
 {
  if(m.SalePrice.HasValue&&(m.SalePrice<=0||m.SalePrice>m.Price))ModelState.AddModelError("SalePrice","Giá khuyến mại phải lớn hơn 0 và không vượt giá gốc.");
  if(!await db.Categories.AnyAsync(x=>x.Id==m.CategoryId))ModelState.AddModelError("CategoryId","Danh mục không tồn tại.");
  if(m.BrandId.HasValue&&!await db.Brands.AnyAsync(x=>x.Id==m.BrandId))ModelState.AddModelError("BrandId","Hãng không tồn tại.");
  photos=photos?.Where(f=>f.Length>0).ToList();
  if(photos?.Count>ImageFiles.MaxPerRequest||photos?.Any(f=>!ImageFiles.IsValid(f))==true)ModelState.AddModelError("","Chỉ nhận tối đa 5 ảnh JPG/PNG/WebP, mỗi ảnh dưới 2 MB, đúng định dạng.");
  if(!ModelState.IsValid){await Setup();return View(m);}
  Product p;if(m.Id==0){p=new Product();db.Add(p);}else{p=(await db.Products.Include(x=>x.Specs).Include(x=>x.Images).SingleOrDefaultAsync(x=>x.Id==m.Id))!;if(p==null)return NotFound();try{db.Entry(p).Property(x=>x.RowVersion).OriginalValue=Convert.FromBase64String(m.RowVersion??"");}catch{ModelState.AddModelError("","Phiên bản sản phẩm không hợp lệ.");await Setup();return View(m);}}
  p.Name=m.Name.Trim();p.Price=m.Price;p.SalePrice=m.SalePrice;p.Stock=m.Stock;p.CategoryId=m.CategoryId;p.BrandId=m.BrandId;p.Description=m.Description;p.IsActive=m.IsActive;
  // ---- Thông số kỹ thuật: ưu tiên các dòng có cấu trúc, nếu không có thì đọc từ SpecsText ----
  var specPairs=new List<(string Name,string Value)>();
  if(m.Specs!=null&&m.Specs.Any(x=>!string.IsNullOrWhiteSpace(x.Name)))
   foreach(var sp in m.Specs.Where(x=>!string.IsNullOrWhiteSpace(x.Name)).Take(20))
    specPairs.Add((Clip(sp.Name.Trim(),80),Clip((sp.Value??"").Trim(),200)));
  else
   foreach(var line in (m.SpecsText??"").Split('\n',StringSplitOptions.RemoveEmptyEntries).Take(20))
   {var pair=line.Split('=',2);if(pair.Length==2&&pair[0].Trim().Length>0)specPairs.Add((Clip(pair[0].Trim(),80),Clip(pair[1].Trim(),200)));}
  p.Specs.Clear();
  foreach(var (sn,sv) in specPairs)p.Specs.Add(new ProductSpec{SpecName=sn,SpecValue=sv});
  // ---- Ảnh: xoá ảnh được chọn, thêm ảnh mới, đặt ảnh chính ----
  var toRemove=(removeImageIds??new List<int>()).ToHashSet();
  var removed=p.Images.Where(i=>toRemove.Contains(i.Id)).ToList();
  var orphanFiles=new List<string>();
  var savedFiles=new List<string>();
  foreach(var img in removed)
  {orphanFiles.Add(ImageFiles.PathOf(img.Url));p.Images.Remove(img);}
  if(mainImageId.HasValue&&p.Images.Any(i=>i.Id==mainImageId.Value))
   foreach(var img in p.Images)img.IsMain=(img.Id==mainImageId.Value);
  if(photos!=null)
   foreach(var f in photos)
   {
    var url=await ImageFiles.SaveAsync(f,env.WebRootPath);
    savedFiles.Add(ImageFiles.PathOf(url));
    var ni=new ProductImage{Url=url,IsMain=p.Images.Count==0};p.Images.Add(ni);
   }
  EnsureSingleMain(p.Images);
  if(m.Id==0&&p.Images.Count==0)p.Images.Add(new ProductImage{Url="/images/laptop.svg",IsMain=true});
  try{await db.SaveChangesAsync();}
  catch(DbUpdateConcurrencyException){ModelState.AddModelError("","Sản phẩm vừa thay đổi do giao dịch khác. Vui lòng mở lại form, không ghi đè tồn kho.");await Setup();Response.StatusCode=409;return View(m);}
  if(action=="saveNew"){var b=await BuildNewFromAsync(p);TempData["Message"]=$"Đã lưu sản phẩm. Biểu mẫu đã sẵn sàng cho sản phẩm tiếp theo (đã sao chép danh mục, hãng, thông số).";return View("Edit",b);}
  foreach(var fp in orphanFiles)ImageFiles.DeletePhysical(fp,env.WebRootPath);
  TempData["Message"]=m.Id==0?$"Đã thêm sản phẩm “{p.Name}”.":$"Đã lưu thay đổi sản phẩm “{p.Name}”.";
  TempData["LastCreated"]=m.Id==0?p.Name:null;
  return RedirectToAction(nameof(ProductManager));
 }
 // Chuẩn bị biểu mẫu trống để THÊM TIẾP, giữ lại danh mục/hãng/thông số vừa nhập.
 async Task<ProductForm> BuildNewFromAsync(Product saved)
 {
  await Setup();
  ViewBag.Images=new List<ProductImage>();
  return new ProductForm{Id=0,CategoryId=saved.CategoryId,BrandId=saved.BrandId,IsActive=true,
   Stock=0,Specs=saved.Specs.Select(s=>new SpecInput{Name=s.SpecName,Value=s.SpecValue}).ToList(),
   SpecsText=string.Join("\n",saved.Specs.Select(s=>s.SpecName+"="+s.SpecValue))};
 }
 static string Clip(string v,int max)=>v.Length<=max?v:v[..max];
 // Bảo đảm luôn có đúng một ảnh chính khi còn ảnh.
 static void EnsureSingleMain(ICollection<ProductImage> images)
 {
  if(images.Count==0)return;
  var first=images.FirstOrDefault(i=>i.IsMain)??images.First();
  foreach(var i in images)i.IsMain=(i==first);
 }
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Hide(int id){var p=await db.Products.FindAsync(id);if(p!=null){p.IsActive=false;try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){TempData["Error"]="Sản phẩm vừa thay đổi; hãy thử lại.";}}return RedirectToAction(nameof(Products));}
 [Authorize(Roles="Admin")]public async Task<IActionResult>Settings(){ViewBag.Categories=await db.Categories.ToListAsync();ViewBag.Brands=await db.Brands.ToListAsync();ViewBag.Coupons=await db.Coupons.ToListAsync();return View();}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Category(int id,string name,bool active=true){if(string.IsNullOrWhiteSpace(name)||name.Length>80)return BadRequest();var x=id==0?new Category():await db.Categories.FindAsync(id);if(x==null)return NotFound();x.Name=name.Trim();x.IsActive=active;if(id==0)db.Add(x);try{await db.SaveChangesAsync();}catch(DbUpdateException){TempData["Error"]="Tên danh mục bị trùng.";}return RedirectToAction(nameof(Settings));}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Brand(int id,string name,bool active=true){if(string.IsNullOrWhiteSpace(name)||name.Length>80)return BadRequest();var x=id==0?new Brand():await db.Brands.FindAsync(id);if(x==null)return NotFound();x.Name=name.Trim();x.IsActive=active;if(id==0)db.Add(x);try{await db.SaveChangesAsync();}catch(DbUpdateException){TempData["Error"]="Tên hãng bị trùng.";}return RedirectToAction(nameof(Settings));}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Coupon(string code,int percent,int limit,DateTime expired){if(string.IsNullOrWhiteSpace(code)||code.Length>30||percent<1||percent>100||limit<1||expired<=DateTime.UtcNow)return BadRequest();db.Coupons.Add(new Coupon{Code=code.Trim().ToUpperInvariant(),DiscountPercent=percent,UsageLimit=limit,ExpiredAt=expired});try{await db.SaveChangesAsync();}catch(DbUpdateException){TempData["Error"]="Mã đã tồn tại.";}return RedirectToAction(nameof(Settings));}
 [Authorize(Roles="Admin")]public async Task<IActionResult>Users(){var list=await db.Users.OrderBy(x=>x.Email).ToListAsync();var roles=new Dictionary<int,string>();foreach(var u in list)roles[u.Id]=string.Join(", ",await users.GetRolesAsync(u));ViewBag.Roles=roles;return View(list);}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Role(int id,string role){if(!new[]{"Admin","Staff","Customer"}.Contains(role)||id==User.UserId())return BadRequest();var u=await users.FindByIdAsync(id.ToString());if(u==null)return NotFound();await users.RemoveFromRolesAsync(u,await users.GetRolesAsync(u));await users.AddToRoleAsync(u,role);await users.UpdateSecurityStampAsync(u);return RedirectToAction(nameof(Users));}
 // Nhân viên/kế toán xác nhận đã nhận tiền chuyển khoản sau khi đối chiếu sao kê.
 [Authorize(Roles="Admin,Staff"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult>ConfirmTransfer(int id,string? note)
 {
  var o=await db.Orders.Include(x=>x.History).SingleOrDefaultAsync(x=>x.Id==id);
  if(o==null)return NotFound();
  if(o.PaymentMethod!="BankTransfer")
  {TempData["Error"]="Đơn này không dùng phương thức chuyển khoản.";return RedirectToAction("Details","Orders",new{id});}
  if(o.PaymentStatus=="Paid")
  {TempData["Message"]="Đơn đã được xác nhận thanh toán trước đó.";return RedirectToAction("Details","Orders",new{id});}
  o.PaymentStatus="Paid";
  o.PaymentTransactionId="BT"+DateTime.UtcNow.ToString("yyMMddHHmmss");
  if(o.Status==OrderStates.PendingPayment)o.Status=OrderStates.New;
  var extra=string.IsNullOrWhiteSpace(note)?"":" Mã giao dịch sao kê: "+note.Trim();
  o.History.Add(new OrderHistory{Status=o.Status,Note=$"Tài khoản #{User.UserId()}: xác nhận đã nhận tiền chuyển khoản.{extra}"});
  try{await db.SaveChangesAsync();}
  catch(DbUpdateConcurrencyException)
  {TempData["Error"]="Đơn vừa thay đổi. Hãy tải lại trang.";return RedirectToAction("Details","Orders",new{id});}
  TempData["Message"]="Đã xác nhận thanh toán chuyển khoản.";
  return RedirectToAction("Details","Orders",new{id});
 }
 // Trang đối soát giao dịch ngân hàng (webhook báo có).
 [Authorize(Roles="Admin,Staff")]
 public async Task<IActionResult>BankTransactions(string? status)
 {
  var q=db.BankTransactions.AsNoTracking();
  if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.Status==status);
  ViewBag.Status=status;
  ViewBag.WebhookEnabled=config.GetValue<bool?>("BankWebhook:Enabled")??false;
  ViewBag.Simulator=config.GetValue<bool?>("BankWebhook:AllowSimulator")??false;
  return View(await q.OrderByDescending(x=>x.Id).Take(200).ToListAsync());
 }
 [Authorize(Roles="Admin")]public async Task<IActionResult>Returns()=>View(await db.ReturnRequests.OrderByDescending(x=>x.CreatedAt).ToListAsync());
 async Task Setup(){ViewBag.Categories=await db.Categories.ToListAsync();ViewBag.Brands=await db.Brands.ToListAsync();}
}
public record StatusUpdate(string Status,string? Note,string? RowVersion);
[ApiController,Route("api")]
public class ShopApiController(ShopDbContext db,OrderService orders):ControllerBase
{
 [AllowAnonymous,HttpGet("products")]public async Task<IActionResult>Products(string? q,int page=1){var x=db.Products.AsNoTracking().Where(x=>x.IsActive);if(!string.IsNullOrWhiteSpace(q))x=x.Where(x=>x.Name.Contains(q));return Ok(new{total=await x.CountAsync(),items=await x.OrderByDescending(x=>x.Id).Skip((Math.Max(1,page)-1)*12).Take(12).Select(x=>new{x.Id,x.Name,x.Price,x.SalePrice,x.Stock}).ToListAsync()});}
 [Authorize(Roles="Admin"),HttpGet("reports/revenue")]public async Task<IActionResult>Revenue(DateTime? from,DateTime? to){var start=from?.Date??DateTime.UtcNow.Date.AddDays(-29);var end=(to?.Date??DateTime.UtcNow.Date).AddDays(1);var list=await db.Orders.AsNoTracking().Where(x=>x.Status==OrderStates.Completed&&x.CreatedAt>=start&&x.CreatedAt<end).ToListAsync();return Ok(new{total=list.Sum(x=>x.Total),orders=list.Count,points=list.GroupBy(x=>x.CreatedAt.Date).OrderBy(x=>x.Key).Select(g=>new{date=g.Key.ToString("yyyy-MM-dd"),total=g.Sum(x=>x.Total)})});}
 [Authorize(Roles="Admin"),HttpGet("reports/top-products")]public async Task<IActionResult>Top(){var rows=await db.OrderItems.AsNoTracking().Where(x=>x.Order!.Status==OrderStates.Completed).ToListAsync();return Ok(rows.GroupBy(x=>x.ProductId).Select(g=>new{name=g.First().ProductName,quantity=g.Sum(x=>x.Quantity),total=g.Sum(x=>x.Quantity*x.UnitPrice)}).OrderByDescending(x=>x.quantity).Take(10));}
 [Authorize(Roles="Admin"),HttpGet("reports/low-stock")]public async Task<IActionResult>Low()=>Ok(await db.Products.AsNoTracking().Where(x=>x.IsActive&&x.Stock<=5).OrderBy(x=>x.Stock).Select(x=>new{x.Id,x.Name,x.Stock}).ToListAsync());
 [Authorize(Roles="Admin,Staff"),HttpGet("orders")]public async Task<IActionResult>Orders(string? status){var x=db.Orders.AsNoTracking();if(!string.IsNullOrEmpty(status))x=x.Where(x=>x.Status==status);return Ok(await x.OrderByDescending(x=>x.CreatedAt).Take(200).Select(x=>new{x.Id,code="TS"+x.Id,x.FullName,x.Total,x.Status,x.CreatedAt,x.RowVersion}).ToListAsync());}
 [Authorize(Roles="Admin,Staff"),HttpPut("orders/{id:int}/status"),ValidateAntiForgeryToken]public async Task<IActionResult>Status(int id,StatusUpdate m){var x=await db.Orders.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);if(x==null)return NotFound();if(string.IsNullOrEmpty(m.RowVersion)||Convert.ToBase64String(x.RowVersion)!=m.RowVersion)return Conflict(new{message="Đơn vừa thay đổi. Hãy tải lại."});var r=await orders.ChangeAsync(id,m.Status,User.UserId(),true,m.Note,m.RowVersion);return r.Success?Ok(new{message=r.Message}):Conflict(new{message=r.Message});}
}

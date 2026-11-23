using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;
using TechShop.Data;
using TechShop.Models;
namespace TechShop.Services;
public record CartLine(int ProductId,int Quantity);
// No client price, subtotal, discount amount, shipping fee or total is accepted.
public record CheckoutCommand(int UserId,Guid Key,string FullName,string Phone,string Address,IReadOnlyList<CartLine> Lines,string? CouponCode=null,string PaymentMethod="COD");
public record CheckoutResult(bool Success,string Message,int? OrderId=null);
public interface ICheckoutObserver {Task AfterReadAsync();Task AfterStockSavedAsync();}
public class CheckoutService(ShopDbContext db,ICheckoutObserver? observer=null)
{
 public static bool IsContention(Exception ex)=>ex is DbUpdateConcurrencyException || ex is SqliteException{SqliteErrorCode:5 or 6} || ex is SqlException{Number:1205} || ex.InnerException is SqliteException{SqliteErrorCode:5 or 6} || ex.InnerException is SqlException{Number:1205};
 public async Task<CheckoutResult>PlaceAsync(CheckoutCommand c)
 {
  if(c.Key==Guid.Empty||string.IsNullOrWhiteSpace(c.FullName)||c.FullName.Length>100||string.IsNullOrWhiteSpace(c.Address)||c.Address.Length>300||!Regex.IsMatch(c.Phone??"",@"^0\d{9}$"))return new(false,"Vui lòng nhập đủ họ tên, địa chỉ và số điện thoại 10 chữ số.");
  if(c.Lines.Count==0||c.Lines.Count>100||c.Lines.Any(x=>x.Quantity<1||x.Quantity>99))return new(false,"Giỏ hàng trống hoặc số lượng không hợp lệ (1–99).");
  if(c.PaymentMethod!="COD"&&c.PaymentMethod!="VNPay")return new(false,"Phương thức thanh toán không hợp lệ.");
  var lines=c.Lines.GroupBy(x=>x.ProductId).Select(g=>new CartLine(g.Key,g.Sum(x=>x.Quantity))).OrderBy(x=>x.ProductId).ToList();
  if(lines.Any(x=>x.Quantity>99))return new(false,"Tổng số lượng mỗi sản phẩm không được quá 99.");
  var existing=await db.Orders.AsNoTracking().FirstOrDefaultAsync(x=>x.IdempotencyKey==c.Key);
  if(existing!=null)return existing.UserId==c.UserId?new(true,"Đơn hàng đã được ghi nhận trước đó.",existing.Id):new(false,"Yêu cầu đặt hàng không hợp lệ.");
  if(!await db.Users.AnyAsync(x=>x.Id==c.UserId&&x.IsActive))return new(false,"Tài khoản không hợp lệ.");
  // The SQLite fallback has a deferred transaction for testing the same two-reader race.
  if(db.Database.IsSqlite()) await db.Database.OpenConnectionAsync();
  await using var rawTx = db.Database.IsSqlite() ? ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred:true) : null;
  await using var tx = db.Database.IsSqlite()
   ? (await db.Database.UseTransactionAsync(rawTx!))!
   : await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  try
  {
   var ids=lines.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)&&x.IsActive).OrderBy(x=>x.Id).ToListAsync();
   if(products.Count!=lines.Count)return new(false,"Sản phẩm không tồn tại hoặc đã ngừng bán.");
   foreach(var l in lines)if(products.Single(x=>x.Id==l.ProductId).Stock<l.Quantity)return new(false,"Sản phẩm đã hết hàng hoặc tồn kho không đủ. Vui lòng cập nhật giỏ hàng.");
   if(observer!=null)await observer.AfterReadAsync();
   var subtotal=lines.Sum(l=>products.Single(x=>x.Id==l.ProductId).EffectivePrice*l.Quantity);
   Coupon? coupon=null;var discount=0m;
   if(!string.IsNullOrWhiteSpace(c.CouponCode))
   {
    var code=c.CouponCode.Trim().ToUpperInvariant();coupon=await db.Coupons.SingleOrDefaultAsync(x=>x.Code==code);
    if(coupon==null||coupon.ExpiredAt<DateTime.UtcNow||coupon.UsedCount>=coupon.UsageLimit||coupon.DiscountPercent<1||coupon.DiscountPercent>100)return new(false,"Mã giảm giá hết hạn, không tồn tại hoặc đã hết lượt.");
    discount=decimal.Round(subtotal*coupon.DiscountPercent/100m,0,MidpointRounding.AwayFromZero);coupon.UsedCount++;
   }
   var shipping=subtotal>=10000000m?0m:30000m;
   foreach(var l in lines)products.Single(x=>x.Id==l.ProductId).Stock-=l.Quantity;
   // EF UPDATE ... WHERE Id=@id AND RowVersion=@original; a loser throws, never retries blindly.
   await db.SaveChangesAsync();if(observer!=null)await observer.AfterStockSavedAsync();
   var order=new Order{UserId=c.UserId,IdempotencyKey=c.Key,FullName=c.FullName.Trim(),Phone=c.Phone!,ShippingAddress=c.Address.Trim(),Subtotal=subtotal,Discount=discount,ShippingFee=shipping,Total=subtotal-discount+shipping,CouponCode=coupon?.Code,CouponId=coupon?.Id,PaymentMethod=c.PaymentMethod,Status=c.PaymentMethod=="VNPay"?OrderStates.PendingPayment:OrderStates.New};
   foreach(var l in lines){var p=products.Single(x=>x.Id==l.ProductId);order.Items.Add(new OrderItem{ProductId=p.Id,ProductName=p.Name,Quantity=l.Quantity,UnitPrice=p.EffectivePrice});}
   order.History.Add(new OrderHistory{Status=order.Status,Note="Đặt hàng; số tiền và tồn kho được xác định tại máy chủ."});db.Orders.Add(order);await db.SaveChangesAsync();await tx.CommitAsync();return new(true,"Đặt hàng thành công.",order.Id);
  }
  catch(Exception ex)when(IsContention(ex))
  {await tx.RollbackAsync();db.ChangeTracker.Clear();return new(false,"Tồn kho vừa thay đổi do giao dịch khác. Đơn này chưa được tạo; vui lòng tải lại giỏ hàng.");}
  catch(DbUpdateException)
  {await tx.RollbackAsync();db.ChangeTracker.Clear();var found=await db.Orders.AsNoTracking().FirstOrDefaultAsync(x=>x.IdempotencyKey==c.Key&&x.UserId==c.UserId);if(found!=null)return new(true,"Đơn đã được ghi nhận.",found.Id);throw;}
 }
}
public static class PasswordPolicy
{
 public static bool Valid(string? x)=>x!=null&&x.Length>=8&&x.Length<=128&&x.Any(char.IsUpper)&&x.Any(char.IsLower)&&x.Any(char.IsDigit)&&x.Any(c=>!char.IsLetterOrDigit(c)&&!char.IsWhiteSpace(c));
}
public class OrderService(ShopDbContext db)
{
 public async Task<CheckoutResult>ChangeAsync(int id,string target,int actorId,bool staff,string? note=null,string? expectedVersion=null)
 {
  await using var tx=await db.Database.BeginTransactionAsync();
  try
  {
   var o=await db.Orders.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id);
   if(o==null||(!staff&&o.UserId!=actorId))return new(false,"Không tìm thấy đơn hàng.");
   if(expectedVersion!=null&&Convert.ToBase64String(o.RowVersion)!=expectedVersion)return new(false,"Đơn vừa thay đổi. Hãy tải lại trước khi cập nhật.");
   if(!staff&&target!=OrderStates.Cancelled)return new(false,"Bạn không có quyền thay đổi trạng thái này.");
   if(!OrderStates.CanMove(o.Status,target))return new(false,"Không được chuyển trạng thái sai thứ tự hoặc hoàn kho lần nữa.");
   if(o.PaymentStatus=="Paid"&&target==OrderStates.Cancelled)return new(false,"Đơn đã thanh toán cần quy trình hoàn tiền; không tự hủy.");
   if(target==OrderStates.Returned&&!await db.ReturnRequests.AnyAsync(x=>x.OrderId==id&&x.Status=="Requested"))return new(false,"Chưa có yêu cầu đổi trả hợp lệ.");
   if(target==OrderStates.Cancelled||target==OrderStates.Returned)
   {
    if(o.StockRestored)return new(false,"Đơn đã hoàn kho.");
    var ids=o.Items.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)).ToListAsync();
    foreach(var item in o.Items)products.Single(x=>x.Id==item.ProductId).Stock+=item.Quantity;o.StockRestored=true;
    if(o.CouponId.HasValue){var coupon=await db.Coupons.FindAsync(o.CouponId.Value);if(coupon!=null&&coupon.UsedCount>0)coupon.UsedCount--;}
    if(target==OrderStates.Returned){var req=await db.ReturnRequests.SingleAsync(x=>x.OrderId==id);req.Status="Approved";}
   }
   o.Status=target;if(staff&&note!=null)o.InternalNote=note.Length>1000?note[..1000]:note;
   o.History.Add(new OrderHistory{Status=target,Note=$"Tài khoản #{actorId}: "+(target==OrderStates.Returned?"Duyệt trả hàng và hoàn kho":"Cập nhật trạng thái")});
   await db.SaveChangesAsync();await tx.CommitAsync();return new(true,"Đã cập nhật đơn.",o.Id);
  }catch(Exception ex)when(CheckoutService.IsContention(ex)){await tx.RollbackAsync();db.ChangeTracker.Clear();return new(false,"Đơn hoặc tồn kho vừa thay đổi. Vui lòng tải lại.");}
 }
}

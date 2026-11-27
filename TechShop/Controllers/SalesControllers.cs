using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;
namespace TechShop.Controllers;
[Authorize]
public class CartController(ShopDbContext db):Controller
{
 public async Task<IActionResult>Index(){var lines=CartSession.Read(HttpContext.Session);var ids=lines.Select(x=>x.ProductId).ToArray();var products=await db.Products.Include(x=>x.Images).Where(x=>ids.Contains(x.Id)).ToListAsync();return View(lines.Where(x=>products.Any(p=>p.Id==x.ProductId)).Select(x=>new CartViewLine(products.Single(p=>p.Id==x.ProductId),x.Quantity)).ToList());}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Update(int productId,int quantity,bool add=false)
 {
  if(quantity<0||quantity>99)return BadRequest(new{message="Số lượng phải từ 0 đến 99."});
  var p=await db.Products.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==productId&&x.IsActive);if(p==null)return NotFound(new{message="Sản phẩm không còn bán."});
  var lines=CartSession.Read(HttpContext.Session);var old=lines.FirstOrDefault(x=>x.ProductId==productId);int desired=add?quantity+(old?.Quantity??0):quantity;
  if(desired>p.Stock||desired>99)return Conflict(new{message="Số lượng vượt tồn kho hiện tại. Giỏ hàng chưa được thay đổi."});
  lines.RemoveAll(x=>x.ProductId==productId);if(desired>0)lines.Add(new CartLine(productId,desired));CartSession.Write(HttpContext.Session,lines);
  return Json(new{message="Đã cập nhật giỏ hàng.",count=lines.Sum(x=>x.Quantity),quantity=desired});
 }
}
[Authorize]
public class CheckoutController(ShopDbContext db,CheckoutService checkout,VnPayService vnpay,BankTransferService bank,ShopMailer mail,ILogger<CheckoutController> logger):Controller
{
 public async Task<IActionResult>Index(){if(!CartSession.Read(HttpContext.Session).Any())return RedirectToAction("Index","Cart");var user=await db.Users.FindAsync(User.UserId());var model=new CheckoutModel{FullName=user?.FullName??""};await Setup();return View(model);}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Index(CheckoutModel m)
 {
  if(m.PaymentMethod=="VNPay"&&!vnpay.Enabled)ModelState.AddModelError("PaymentMethod","VNPay sandbox chưa cấu hình; hãy chọn COD.");
  if(m.PaymentMethod=="BankTransfer"&&!bank.Enabled)ModelState.AddModelError("PaymentMethod","Chuyển khoản chưa được cấu hình; hãy chọn COD.");
  if(!ModelState.IsValid){await Setup();return View(m);}
  var r=await checkout.PlaceAsync(new CheckoutCommand(User.UserId(),m.Key,m.FullName,m.Phone,m.Address,CartSession.Read(HttpContext.Session),m.CouponCode,m.PaymentMethod));
  if(!r.Success){ModelState.AddModelError("",r.Message);await Setup();Response.StatusCode=409;return View(m);}
  CartSession.Clear(HttpContext.Session);
  var order=await db.Orders.AsNoTracking().SingleAsync(x=>x.Id==r.OrderId);
  try{var email=(await db.Users.FindAsync(order.UserId))?.Email;if(email!=null)await mail.SendAsync(email,"Xác nhận đơn "+order.Code,$"{order.Code}\nTổng tiền {order.Total:N0} VND\nPhương thức {order.PaymentMethod}\nTrạng thái {OrderStates.Label(order.Status)}");}catch(Exception ex){logger.LogError(ex,"Order {Id} committed but email not delivered",order.Id);}
  if(order.PaymentMethod=="VNPay")return RedirectToAction("Pay","Orders",new{id=order.Id});
  if(order.PaymentMethod=="BankTransfer")return RedirectToAction("BankTransfer","Orders",new{id=order.Id});
  return RedirectToAction("Details","Orders",new{id=order.Id});
 }
 async Task Setup(){var lines=CartSession.Read(HttpContext.Session);var ids=lines.Select(x=>x.ProductId).ToArray();var ps=await db.Products.Where(x=>ids.Contains(x.Id)).ToListAsync();ViewBag.Lines=lines.Where(x=>ps.Any(p=>p.Id==x.ProductId)).Select(x=>new CartViewLine(ps.Single(p=>p.Id==x.ProductId),x.Quantity)).ToList();ViewBag.Addresses=await db.Addresses.Where(x=>x.UserId==User.UserId()).ToListAsync();ViewBag.VnPay=vnpay.Enabled;ViewBag.Bank=bank.Enabled;ViewBag.BankName=bank.Account.BankName;}
}
[Authorize]
public class OrdersController(ShopDbContext db,OrderService service,VnPayService vnpay,BankTransferService bank):Controller
{
 bool Staff=>User.IsInRole("Admin")||User.IsInRole("Staff");
 public async Task<IActionResult>Index()=>View(await db.Orders.Where(x=>x.UserId==User.UserId()).OrderByDescending(x=>x.CreatedAt).ToListAsync());
 public async Task<IActionResult>Details(int id){var x=await db.Orders.Include(x=>x.Items).Include(x=>x.History).SingleOrDefaultAsync(x=>x.Id==id&&(x.UserId==User.UserId()||Staff));return x==null?NotFound():View(x);}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Cancel(int id){var r=await service.ChangeAsync(id,OrderStates.Cancelled,User.UserId(),false);TempData[r.Success?"Message":"Error"]=r.Message;return RedirectToAction(nameof(Details),new{id});}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>RequestReturn(int id,string reason)
 {var x=await db.Orders.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId()&&x.Status==OrderStates.Completed);if(x==null)return NotFound();if(!string.IsNullOrWhiteSpace(reason)&&reason.Length<=500&&!await db.ReturnRequests.AnyAsync(x=>x.OrderId==id)){db.ReturnRequests.Add(new ReturnRequest{OrderId=id,UserId=User.UserId(),Reason=reason});await db.SaveChangesAsync();TempData["Message"]="Đã gửi yêu cầu đổi trả.";}return RedirectToAction(nameof(Details),new{id});}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Change(int id,string status,string? note){if(!Staff)return Forbid();var r=await service.ChangeAsync(id,status,User.UserId(),true,note);TempData[r.Success?"Message":"Error"]=r.Message;return RedirectToAction(nameof(Details),new{id});}
 public async Task<IActionResult>Invoice(int id){var x=await db.Orders.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&(x.UserId==User.UserId()||Staff));return x==null?NotFound():View(x);}
 // Trang hướng dẫn chuyển khoản: mã QR, thông tin tài khoản, nội dung chuyển tiền.
 public async Task<IActionResult>BankTransfer(int id)
 {
  var o=await db.Orders.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId());
  if(o==null)return NotFound();
  if(o.PaymentMethod!="BankTransfer")return RedirectToAction(nameof(Details),new{id});
  ViewBag.Enabled=bank.Enabled;ViewBag.Account=bank.Account;ViewBag.Qr=bank.QrUrl(o);
  ViewBag.Reference=BankTransferService.Reference(o);ViewBag.Instructions=bank.ManualInstructions(o);
  return View(o);
 }
 // Khách báo đã chuyển khoản: ghi nhận để nhân viên đối chiếu sao kê.
 [HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult>ReportTransfer(int id,string? note)
 {
  var o=await db.Orders.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId());
  if(o==null)return NotFound();
  if(o.PaymentMethod!="BankTransfer")return BadRequest();
  if(o.PaymentStatus=="Paid"){TempData["Message"]="Đơn này đã được xác nhận thanh toán.";return RedirectToAction(nameof(Details),new{id});}
  if(o.PaymentStatus!="AwaitingConfirmation")
  {
   o.PaymentStatus="AwaitingConfirmation";
   var extra=string.IsNullOrWhiteSpace(note)?"":(" Ghi chú của khách: "+(note.Length>200?note[..200]:note));
   o.History.Add(new OrderHistory{Status=o.Status,Note="Khách báo đã chuyển khoản, chờ đối chiếu sao kê."+extra});
   await db.SaveChangesAsync();
  }
  TempData["Message"]="Đã ghi nhận. Nhân viên sẽ đối chiếu sao kê và xác nhận trong thời gian sớm nhất.";
  return RedirectToAction(nameof(Details),new{id});
 }
 public async Task<IActionResult>Pay(int id){var o=await db.Orders.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId()&&x.Status==OrderStates.PendingPayment);if(o==null)return NotFound();if(!vnpay.Enabled){TempData["Error"]="VNPay sandbox chưa được cấu hình, không có thanh toán thật xảy ra.";return RedirectToAction(nameof(Details),new{id});}return Redirect(vnpay.Create(o,HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()??"127.0.0.1"));}
 [AllowAnonymous,HttpGet]public async Task<IActionResult>PaymentReturn()
 {
  var q=Request.Query.ToDictionary(x=>x.Key,x=>x.Value.ToString());if(!vnpay.Verify(q))return BadRequest("Chữ ký VNPay không hợp lệ.");
  if(!q.TryGetValue("vnp_TxnRef",out var s)||!int.TryParse(s,out var id))return BadRequest();var o=await db.Orders.SingleOrDefaultAsync(x=>x.Id==id);
  if(o==null||!q.TryGetValue("vnp_Amount",out var amt)||!long.TryParse(amt,out var a)||a!=(long)(o.Total*100))return BadRequest("Số tiền không khớp đơn.");
  if(o.Status!=OrderStates.PendingPayment){return Content("Kết quả đã được xử lý hoặc đơn không còn chờ thanh toán.");}
  if(q.GetValueOrDefault("vnp_ResponseCode")=="00"&&q.GetValueOrDefault("vnp_TransactionStatus")=="00")
  {o.PaymentStatus="Paid";o.PaymentTransactionId=q.GetValueOrDefault("vnp_TransactionNo");o.Status=OrderStates.New;o.History.Add(new OrderHistory{Status=o.Status,Note="VNPay sandbox: chữ ký và số tiền đã được xác thực."});try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict("Kết quả vừa được xử lý. Hãy xem lại đơn hàng.");}}
  return Content(o.PaymentStatus=="Paid"?"Đã ghi nhận thanh toán sandbox. Hãy mở Đơn hàng của tôi để xem.":"Thanh toán chưa thành công; đơn giữ Chờ thanh toán để thử lại.");
 }
}

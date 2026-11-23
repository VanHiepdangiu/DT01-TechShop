using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;
namespace TechShop.Controllers;
public class AccountController(UserManager<ShopUser> users,SignInManager<ShopUser> signin,ShopMailer mail,ShopDbContext db,ILogger<AccountController> logger):Controller
{
 [AllowAnonymous,HttpGet]public IActionResult Index(string? returnUrl=null,bool register=false)=>User.Identity?.IsAuthenticated==true?RedirectToAction("Index","Home"):View(new AuthPage{Login=new LoginModel{ReturnUrl=returnUrl},RegisterTab=register});
 [AllowAnonymous,HttpGet]public IActionResult Login(string? returnUrl=null)=>RedirectToAction(nameof(Index),new{returnUrl});
 [AllowAnonymous,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Login([Bind(Prefix="Login")]LoginModel m)
 {
  if(!ModelState.IsValid)return View("Index",new AuthPage{Login=m});
  var u=await users.FindByEmailAsync(m.Email);
  var result=u!=null&&u.IsActive?await signin.PasswordSignInAsync(u,m.Password,m.RememberMe,lockoutOnFailure:true):Microsoft.AspNetCore.Identity.SignInResult.Failed;
  if(!result.Succeeded){ModelState.AddModelError("Login.Email",result.IsLockedOut?"Tài khoản tạm khóa do đăng nhập sai nhiều lần.":"Email hoặc mật khẩu không đúng.");return View("Index",new AuthPage{Login=m});}
  logger.LogInformation("Account {Id} signed in.",u!.Id);return Url.IsLocalUrl(m.ReturnUrl)?LocalRedirect(m.ReturnUrl!):RedirectToAction("Index","Home");
 }
 [AllowAnonymous,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Register([Bind(Prefix="Register")]RegisterModel m)
 {
  if(!ModelState.IsValid)return View("Index",new AuthPage{Register=m,RegisterTab=true});
  var u=new ShopUser{UserName=m.Email.Trim(),Email=m.Email.Trim(),FullName=m.FullName.Trim()};var r=await users.CreateAsync(u,m.Password);
  if(!r.Succeeded){foreach(var e in r.Errors)ModelState.AddModelError("Register.Email",e.Code=="DuplicateUserName"||e.Code=="DuplicateEmail"?"Email đã được sử dụng.":"Thông tin không hợp lệ: "+e.Description);return View("Index",new AuthPage{Register=m,RegisterTab=true});}
  await users.AddToRoleAsync(u,"Customer");TempData["Message"]="Đăng ký thành công. Mời đăng nhập để vào hệ thống.";return RedirectToAction(nameof(Index));
 }
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Logout(){await signin.SignOutAsync();HttpContext.Session.Clear();return RedirectToAction(nameof(Index));}
 [AllowAnonymous,HttpGet]public IActionResult Forgot()=>View();
 [AllowAnonymous,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Forgot(string email)
 {
  var u=await users.FindByEmailAsync(email??"");
  if(u!=null){var token=await users.GeneratePasswordResetTokenAsync(u);var url=Url.Action(nameof(Reset),"Account",new{email=u.Email,token},Request.Scheme)!;try{await mail.SendAsync(u.Email!,"TechShop – Đặt lại mật khẩu",url);}catch(Exception ex){logger.LogError(ex,"Reset email delivery failed");}}
  ViewBag.Message="Nếu email hợp lệ, hướng dẫn đã được chuyển tới dịch vụ email. Bản demo chưa cấu hình SMTP chỉ lưu thư tại Outbox cục bộ.";return View();
 }
 [AllowAnonymous,HttpGet]public IActionResult Reset(string email,string token){ViewBag.Email=email;ViewBag.Token=token;return View();}
 [AllowAnonymous,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Reset(string email,string token,string password,string confirmPassword)
 {
  ViewBag.Email=email;ViewBag.Token=token;
  if(password!=confirmPassword||!PasswordPolicy.Valid(password)){ModelState.AddModelError("","Mật khẩu phải từ 8 ký tự, đủ hoa/thường/số/ký tự đặc biệt và xác nhận khớp.");return View();}
  var u=await users.FindByEmailAsync(email);if(u==null||!(await users.ResetPasswordAsync(u,token,password)).Succeeded){ModelState.AddModelError("","Liên kết không hợp lệ hoặc đã hết hạn.");return View();}await signin.SignOutAsync();TempData["Message"]="Đã đổi mật khẩu, hãy đăng nhập lại.";return RedirectToAction(nameof(Index));
 }
 [AllowAnonymous]public IActionResult Denied(){Response.StatusCode=403;return View();}
 [Authorize,HttpGet]public async Task<IActionResult>Profile(){ViewBag.Addresses=await db.Addresses.Where(x=>x.UserId==User.UserId()).ToListAsync();return View((await users.GetUserAsync(User))!);}
 [Authorize,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>Profile(string fullName,string? phone)
 {var u=(await users.GetUserAsync(User))!;if(!string.IsNullOrWhiteSpace(fullName)&&fullName.Length<=100){u.FullName=fullName.Trim();u.PhoneNumber=phone;await users.UpdateAsync(u);}return RedirectToAction(nameof(Profile));}
 [Authorize,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>AddAddress([Bind("Recipient,Phone,Detail")]Address a){a.UserId=User.UserId();if(ModelState.IsValid){db.Add(a);await db.SaveChangesAsync();}return RedirectToAction(nameof(Profile));}
 [Authorize,HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult>DeleteAddress(int id){var a=await db.Addresses.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==User.UserId());if(a!=null){db.Remove(a);await db.SaveChangesAsync();}return RedirectToAction(nameof(Profile));}
}

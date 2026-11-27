// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ShopDbContext>(o=>{if(builder.Configuration["Database:Provider"]=="Sqlite")o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));else o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));});
builder.Services.AddIdentity<ShopUser,IdentityRole<int>>(o=>{o.Password.RequiredLength=8;o.Password.RequireUppercase=true;o.Password.RequireLowercase=true;o.Password.RequireDigit=true;o.Password.RequireNonAlphanumeric=true;o.User.RequireUniqueEmail=true;o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(10);}).AddEntityFrameworkStores<ShopDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o=>{o.LoginPath="/Account";o.AccessDeniedPath="/Account/Denied";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.SlidingExpiration=true;o.ExpireTimeSpan=TimeSpan.FromHours(8);o.Events.OnRedirectToLogin=c=>{if(c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=401;return Task.CompletedTask;}c.Response.Redirect(c.RedirectUri);return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{if(c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=403;return Task.CompletedTask;}c.Response.Redirect(c.RedirectUri);return Task.CompletedTask;};});
builder.Services.AddAuthorization(o=>o.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddDistributedMemoryCache();builder.Services.AddSession(o=>{o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;o.IdleTimeout=TimeSpan.FromHours(2);});
builder.Services.AddAntiforgery(o=>o.HeaderName="X-CSRF-TOKEN");builder.Services.AddScoped<CheckoutService>();builder.Services.AddScoped<OrderService>();builder.Services.AddScoped<ShopMailer>();builder.Services.AddSingleton<VnPayService>();builder.Services.AddSingleton<BankTransferService>();
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
var app=builder.Build();
if(args.Contains("--schema")){using var scope=app.Services.CreateScope();Console.WriteLine(scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.GenerateCreateScript());return;}
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
if(builder.Configuration.GetValue<bool>("RequireHttps"))app.UseHttpsRedirection();
app.Use(async(c,n)=>{c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";await n();});
app.UseStaticFiles();app.UseRouting();app.UseSession();app.UseAuthentication();
// A configurable storefront gate: default follows the requested login-first experience.
// Set Access:RequireLoginForCatalog=false to match the teacher's public guest catalog.
app.Use(async(c,n)=>{var p=c.Request.Path.Value??"";bool catalog=c.Request.Method=="GET"&&(p=="/"||p=="/Home"||p=="/Home/Index"||p.StartsWith("/Products",StringComparison.OrdinalIgnoreCase)||p=="/api/products");if(catalog&&builder.Configuration.GetValue<bool>("Access:RequireLoginForCatalog")&&c.User.Identity?.IsAuthenticated!=true){if(p.StartsWith("/api"))c.Response.StatusCode=401;else c.Response.Redirect("/Account?ReturnUrl="+Uri.EscapeDataString(p+c.Request.QueryString));return;}await n();});
app.UseAuthorization();
// Swagger is role protected. Do not leak order reports or admin schema to anonymous visitors.
app.UseWhen(c=>c.Request.Path.StartsWithSegments("/swagger"),branch=>branch.Use(async(c,n)=>{if(!c.User.IsInRole("Admin")){c.Response.StatusCode=403;return;}await n();}));
app.UseSwagger();app.UseSwaggerUI();
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
using(var scope=app.Services.CreateScope())
{
 var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
 if(db.Database.IsSqlite())await db.Database.EnsureCreatedAsync();else await db.Database.MigrateAsync();
 await Seed.InitializeAsync(scope.ServiceProvider);
}
await app.RunAsync();
public partial class Program { }

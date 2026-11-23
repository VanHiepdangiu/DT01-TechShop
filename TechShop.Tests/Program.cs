using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;
using TechShop.Services;

// Relational integration tests. --sql "connection string" uses a DEDICATED throw-away database.
// SQL mode NEVER deletes the database; use a fresh test DB, not a real store.
var sql = args.Length == 2 && args[0] == "--sql" ? args[1] : null;
if(sql!=null){var builder=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sql);if(!builder.InitialCatalog.StartsWith("TechShopTests_",StringComparison.Ordinal)){Console.Error.WriteLine("Safety: SQL test database name must start with TechShopTests_. Do not use store data.");Environment.ExitCode=2;return;}}
int passed = 0, failed = 0;
var checks = new List<(string, Func<Task>)>();
DbContextOptions<ShopDbContext> Options(string name) => sql != null
    ? new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(sql).Options
    : new DbContextOptionsBuilder<ShopDbContext>().UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(),name+".db")};Default Timeout=2").Options;
void Assert(bool value,string message){if(!value)throw new Exception(message);}
async Task<(DbContextOptions<ShopDbContext>,int,int)> Fixture(int stock=3,bool active=true)
{
    var opt=Options("techshop-"+Guid.NewGuid().ToString("N"));
    await using var db=new ShopDbContext(opt); await db.Database.EnsureCreatedAsync();
    var uid=Guid.NewGuid().ToString("N");
    var user=new ShopUser{Email=uid+"@test.local",NormalizedEmail=uid.ToUpper()+"@TEST.LOCAL",FullName="Test customer",PasswordHash="TEST-NOT-AN-ACCOUNT"};
    var cat=new Category{Name="Test "+uid};db.AddRange(user,cat);await db.SaveChangesAsync();
    var product=new Product{Name="Last product "+uid,CategoryId=cat.Id,Price=1250000m,Stock=stock,IsActive=active};
    db.Add(product);await db.SaveChangesAsync();return(opt,user.Id,product.Id);
}
CheckoutCommand Cmd(int uid,int pid,int qty=1,Guid? key=null)=>new(uid,key??Guid.NewGuid(),"Customer","0988089376","123 Test street",new[]{new CartLine(pid,qty)});
checks.Add(("Empty cart is rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p) with {Lines=Array.Empty<CartLine>()});Assert(!r.Success,"Empty cart accepted");}));
checks.Add(("Quantity zero is rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p,0))).Success,"Zero accepted");}));
checks.Add(("Negative quantity is rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p,-1))).Success,"Negative accepted");}));
checks.Add(("Quantity above 99 is rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p,100))).Success,"100 accepted");}));
checks.Add(("Missing product is rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,int.MaxValue))).Success,"Unknown accepted");}));
checks.Add(("Inactive product is rejected",async()=>{var(o,u,p)=await Fixture(active:false);await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p))).Success,"Hidden accepted");}));
checks.Add(("Insufficient stock creates no order",async()=>{var(o,u,p)=await Fixture(1);await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p,2));Assert(!r.Success&&!await db.Orders.AnyAsync(x=>x.UserId==u),"Invalid order committed");}));
checks.Add(("Server computes amount from database",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p,2));Assert(r.Success,"Checkout failed");var order=await db.Orders.Include(x=>x.Items).SingleAsync(x=>x.Id==r.OrderId);Assert(order.Subtotal==2500000m&&order.Total==2530000m&&order.Items.Single().UnitPrice==1250000m,"Wrong server amount");}));
checks.Add(("Checkout decrements stock",async()=>{var(o,u,p)=await Fixture(3);await using var db=new ShopDbContext(o);await new CheckoutService(db).PlaceAsync(Cmd(u,p,2));Assert((await db.Products.FindAsync(p))!.Stock==1,"Wrong remaining stock");}));
checks.Add(("Free delivery threshold is server-owned",async()=>{var(o,u,p)=await Fixture(20);await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p,8));var order=await db.Orders.SingleAsync(x=>x.Id==r.OrderId);Assert(r.Success&&order.ShippingFee==0&&order.Total==10000000,"Shipping rule wrong");}));
checks.Add(("Duplicate lines merged before stock validation",async()=>{var(o,u,p)=await Fixture(3);await using var db=new ShopDbContext(o);var cmd=Cmd(u,p) with {Lines=new[]{new CartLine(p,2),new CartLine(p,2)}};Assert(!(await new CheckoutService(db).PlaceAsync(cmd)).Success,"Merged quantity oversold");}));
checks.Add(("Duplicate lines use one snapshot item",async()=>{var(o,u,p)=await Fixture(3);await using var db=new ShopDbContext(o);var cmd=Cmd(u,p) with {Lines=new[]{new CartLine(p,1),new CartLine(p,1)}};var r=await new CheckoutService(db).PlaceAsync(cmd);var a=await db.Orders.Include(x=>x.Items).SingleAsync(x=>x.Id==r.OrderId);Assert(a.Items.Count==1&&a.Items.Single().Quantity==2,"Duplicates not merged");}));
checks.Add(("Repeated checkout key is idempotent",async()=>{var(o,u,p)=await Fixture(3);var cmd=Cmd(u,p);await using(var db=new ShopDbContext(o)){Assert((await new CheckoutService(db).PlaceAsync(cmd)).Success,"First failed");}await using var db2=new ShopDbContext(o);var r=await new CheckoutService(db2).PlaceAsync(cmd);Assert(r.Success&&await db2.Orders.CountAsync(x=>x.UserId==u)==1&&(await db2.Products.FindAsync(p))!.Stock==2,"Duplicate charge/stock decrement");}));
checks.Add(("Failure after stock save rolls back all rows",async()=>{var(o,u,p)=await Fixture(3);await using(var db=new ShopDbContext(o)){try{await new CheckoutService(db,new FailAfterStock()).PlaceAsync(Cmd(u,p));}catch(InvalidOperationException){}}await using var fresh=new ShopDbContext(o);Assert((await fresh.Products.FindAsync(p))!.Stock==3&&!await fresh.Orders.AnyAsync(x=>x.UserId==u),"Transaction not rolled back");}));
checks.Add(("Stale product version raises concurrency exception",async()=>{var(o,u,p)=await Fixture(2);await using var a=new ShopDbContext(o);await using var b=new ShopDbContext(o);var pa=await a.Products.FindAsync(p);var pb=await b.Products.FindAsync(p);pa!.Stock=1;await a.SaveChangesAsync();pb!.Stock=1;bool conflict=false;try{await b.SaveChangesAsync();}catch(DbUpdateConcurrencyException){conflict=true;}Assert(conflict,"Stale write silently succeeded");}));
checks.Add(("Two buyers of last unit: exactly one winner",async()=>{var(o,u,p)=await Fixture(1);var gate=new TwoBuyerGate();await using var a=new ShopDbContext(o);await using var b=new ShopDbContext(o);var results=await Task.WhenAll(new CheckoutService(a,gate).PlaceAsync(Cmd(u,p)),new CheckoutService(b,gate).PlaceAsync(Cmd(u,p)));await using var fresh=new ShopDbContext(o);Assert(results.Count(x=>x.Success)==1,"Winner count != 1");Assert((await fresh.Products.FindAsync(p))!.Stock==0,"Stock not zero");Assert(await fresh.Orders.CountAsync(x=>x.UserId==u)==1,"Expected one committed order");}));
checks.Add(("Missing customer name rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p) with {FullName=""})).Success,"No recipient");}));
checks.Add(("Invalid phone rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p) with {Phone="abc"})).Success,"Invalid phone accepted");}));
checks.Add(("Missing address rejected",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);Assert(!(await new CheckoutService(db).PlaceAsync(Cmd(u,p) with {Address=""})).Success,"No address");}));
checks.Add(("Password policy rejects missing character groups",()=>{Assert(!PasswordPolicy.Valid("abcdefgh")&&!PasswordPolicy.Valid("ABCDEFG1!")&&!PasswordPolicy.Valid("Abcdefgh!")&&!PasswordPolicy.Valid("Abcdefg1")&&!PasswordPolicy.Valid("Aa1!aaa"),"Weak password accepted");Assert(PasswordPolicy.Valid("Tech@123456"),"Strong password rejected");return Task.CompletedTask;}));
checks.Add(("Price snapshot survives later catalog price change",async()=>{var(o,u,p)=await Fixture();await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p));var prod=await db.Products.FindAsync(p);prod!.Price=999m;await db.SaveChangesAsync();var order=await db.Orders.Include(x=>x.Items).SingleAsync(x=>x.Id==r.OrderId);Assert(order.Items.Single().UnitPrice==1250000m,"Order uses mutable catalog price");}));
checks.Add(("Mixed basket insufficient item leaves others untouched",async()=>{var(o,u,p)=await Fixture(2);await using var db=new ShopDbContext(o);var r=await new CheckoutService(db).PlaceAsync(Cmd(u,p) with {Lines=new[]{new CartLine(p,1),new CartLine(int.MaxValue,1)}});Assert(!r.Success&&(await db.Products.FindAsync(p))!.Stock==2,"Partial stock update");}));
Console.WriteLine(sql==null?"Provider: SQLite relational test (NOT proof of SQL Server rowversion)":"Provider: SQL SERVER real rowversion integration");
foreach(var (name,test) in checks){try{await test();passed++;Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
Console.WriteLine($"RESULT: {passed} passed, {failed} failed");Environment.ExitCode=failed==0?0:1;
class FailAfterStock:ICheckoutObserver{public Task AfterReadAsync()=>Task.CompletedTask;public Task AfterStockSavedAsync()=>throw new InvalidOperationException("Injected failure");}
class TwoBuyerGate:ICheckoutObserver{int count;readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);public async Task AfterReadAsync(){if(Interlocked.Increment(ref count)==2)ready.TrySetResult();await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));}public Task AfterStockSavedAsync()=>Task.CompletedTask;}

using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using TechShop.Models;
namespace TechShop.Services;
public class ShopMailer(IConfiguration config,IWebHostEnvironment env,ILogger<ShopMailer> log)
{
 public async Task SendAsync(string to,string subject,string body)
 {
  if(!config.GetValue<bool>("Smtp:Enabled"))
  {
   var folder=Path.Combine(env.ContentRootPath,"App_Data","Outbox");Directory.CreateDirectory(folder);
   // This private local outbox is NOT an email delivery; never expose reset links through static files.
   await File.WriteAllTextAsync(Path.Combine(folder,Guid.NewGuid()+".txt"),$"To: {to}\nSubject: {subject}\n\n{body}");
   log.LogInformation("SMTP disabled: mail written to private local outbox, not delivered.");return;
  }
  using var msg=new MailMessage(config["Smtp:From"]!,to,subject,body);
  using var smtp=new SmtpClient(config["Smtp:Host"],config.GetValue<int>("Smtp:Port",587)){EnableSsl=true,Credentials=new NetworkCredential(config["Smtp:User"],config["Smtp:Password"])};
  await smtp.SendMailAsync(msg);
 }
}
public class VnPayService(IConfiguration config)
{
 public bool Enabled=>config.GetValue<bool>("VnPay:Enabled")&&!string.IsNullOrEmpty(config["VnPay:HashSecret"])&&!string.IsNullOrEmpty(config["VnPay:TmnCode"]);
 public static string Canonical(IEnumerable<KeyValuePair<string,string>> values)=>string.Join("&",values.Where(x=>!string.IsNullOrEmpty(x.Value)&&x.Key!="vnp_SecureHash"&&x.Key!="vnp_SecureHashType").OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>WebUtility.UrlEncode(x.Key)+"="+WebUtility.UrlEncode(x.Value)));
 public static string Sign(string text,string secret)=>Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
 public bool Verify(IReadOnlyDictionary<string,string> query)
 {
  if(!Enabled||!query.TryGetValue("vnp_SecureHash",out var received)||received.Length!=128)return false;
  try{var expected=Sign(Canonical(query.Where(x=>x.Key.StartsWith("vnp_",StringComparison.Ordinal))),config["VnPay:HashSecret"]!);return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(received),Convert.FromHexString(expected));}catch(FormatException){return false;}
 }
 public string Create(Order order,string ip)
 {
  if(!Enabled)throw new InvalidOperationException("Chưa cấu hình VNPay sandbox; hãy dùng COD hoặc cấu hình merchant test.");
  var data=new Dictionary<string,string>{{"vnp_Version","2.1.0"},{"vnp_Command","pay"},{"vnp_TmnCode",config["VnPay:TmnCode"]!},{"vnp_Amount",((long)(order.Total*100)).ToString(System.Globalization.CultureInfo.InvariantCulture)},{"vnp_CurrCode","VND"},{"vnp_TxnRef",order.Id.ToString()},{"vnp_OrderInfo","Thanh toan TechShop "+order.Code},{"vnp_OrderType","other"},{"vnp_Locale","vn"},{"vnp_ReturnUrl",config["VnPay:ReturnUrl"]!},{"vnp_IpAddr",ip},{"vnp_CreateDate",DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss")},{"vnp_ExpireDate",DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss")}};
  var q=Canonical(data);return config["VnPay:Url"]+"?"+q+"&vnp_SecureHash="+Sign(q,config["VnPay:HashSecret"]!);
 }
}
public static class CartSession
{
 const string Key="TechShop.Cart";
 public static List<CartLine> Read(ISession session){try{return System.Text.Json.JsonSerializer.Deserialize<List<CartLine>>(session.GetString(Key)??"[]")??new();}catch{return new();}}
 public static void Write(ISession session,IEnumerable<CartLine>lines)=>session.SetString(Key,System.Text.Json.JsonSerializer.Serialize(lines));
 public static void Clear(ISession session)=>session.Remove(Key);
}

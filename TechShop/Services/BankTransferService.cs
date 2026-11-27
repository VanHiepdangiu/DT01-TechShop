// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Chuyển khoản ngân hàng: sinh mã đối chiếu, kiểm tra và xác nhận thanh toán.
using System.Text;
using TechShop.Models;

namespace TechShop.Services;

/// <summary>Thông tin tài khoản nhận tiền, đọc từ cấu hình BankTransfer.</summary>
public record BankAccount(string BankId, string BankName, string AccountNo, string AccountName,
                          string? Branch = null)
{
    public bool Configured => !string.IsNullOrWhiteSpace(BankId)
                              && !string.IsNullOrWhiteSpace(AccountNo)
                              && !string.IsNullOrWhiteSpace(AccountName);
}

/// <summary>
/// Nghiệp vụ chuyển khoản: mỗi đơn có một mã đối chiếu duy nhất để ngân hàng hiển thị
/// trong nội dung chuyển tiền; kế toán đối chiếu mã này với sao kê để xác nhận đơn.
/// </summary>
public class BankTransferService(IConfiguration config)
{
    public BankAccount Account { get; } = config.GetSection("BankTransfer").Get<BankAccount>()
                                          ?? new BankAccount("", "", "", "");

    public bool Enabled => config.GetValue<bool?>("BankTransfer:Enabled") ?? Account.Configured;

    /// <summary>
    /// Mã đối chiếu dạng TS + 6 số đơn, ví dụ TS000128. Không chứa dấu và khoảng trắng
    /// để ngân hàng không làm sai lệch nội dung chuyển khoản.
    /// </summary>
    public static string Reference(Order order) => $"TS{order.Id:000000}";

    /// <summary>Nội dung chuyển tiền khuyến nghị, ngắn gọn để không bị ngân hàng cắt.</summary>
    public string TransferContent(Order order) => Reference(order);

    /// <summary>
    /// Đường dẫn ảnh VietQR theo chuẩn EMVCo, hiển thị được trên mọi ứng dụng ngân hàng
    /// hỗ trợ VietQR. Ảnh được tạo từ máy chủ của vietqr.io nên cần mạng khi hiển thị.
    /// </summary>
    public string QrUrl(Order order, int template = 0)
    {
        if (!Account.Configured) return "";
        var amount = (long)order.Total;
        var addInfo = Uri.EscapeDataString(TransferContent(order));
        var name = Uri.EscapeDataString(Account.AccountName);
        var tpl = template == 1 ? "qr_only" : "compact2";
        return $"https://img.vietqr.io/image/{Account.BankId}-{Account.AccountNo}-{tpl}.png"
               + $"?amount={amount}&addInfo={addInfo}&accountName={name}";
    }

    /// <summary>
    /// Kiểm tra nội dung chuyển khoản do người dùng hoặc kế toán nhập vào có chứa
    /// đúng mã đối chiếu của đơn hay không. So khớp không phân biệt hoa thường.
    /// </summary>
    public static bool MatchesReference(Order order, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var normalized = new string(content.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return normalized.Contains(Reference(order), StringComparison.Ordinal);
    }

    /// <summary>
    /// Sinh nội dung chuyển khoản có mã đơn để dán vào ứng dụng ngân hàng khi
    /// khách không dùng được mã QR.
    /// </summary>
    public string ManualInstructions(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ngân hàng: " + Account.BankName);
        sb.AppendLine("Số tài khoản: " + Account.AccountNo);
        sb.AppendLine("Chủ tài khoản: " + Account.AccountName);
        if (!string.IsNullOrWhiteSpace(Account.Branch)) sb.AppendLine("Chi nhánh: " + Account.Branch);
        sb.AppendLine("Số tiền: " + order.Total.ToString("N0") + " ₫");
        sb.AppendLine("Nội dung chuyển khoản: " + TransferContent(order));
        return sb.ToString().TrimEnd();
    }
}

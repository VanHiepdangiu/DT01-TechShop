// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Tiện ích ảnh sản phẩm: kiểm tra tệp, lưu tệp, xoá tệp an toàn trong wwwroot/uploads.
using System.Text;

namespace TechShop.Services;

/// <summary>Một dòng thông số kỹ thuật trong biểu mẫu biên tập sản phẩm.</summary>
public class SpecInput
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public static class ImageFiles
{
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    public const long MaxBytes = 2 * 1024 * 1024;
    public const int MaxPerRequest = 5;

    /// <summary>Kiểm tra phần mở rộng, dung lượng và chữ ký nhị phân của ảnh.</summary>
    public static bool IsValid(IFormFile f)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (f.Length < 12 || f.Length > MaxBytes || !AllowedExtensions.Contains(ext)) return false;
        using var s = f.OpenReadStream();
        var b = new byte[12];
        s.ReadExactly(b);
        return ext switch
        {
            ".jpg" or ".jpeg" => b[0] == 255 && b[1] == 216 && b[2] == 255,
            ".png" => b[0] == 137 && b[1] == 80 && b[2] == 78 && b[3] == 71,
            ".webp" => Encoding.ASCII.GetString(b, 0, 4) == "RIFF" && Encoding.ASCII.GetString(b, 8, 4) == "WEBP",
            _ => false
        };
    }

    /// <summary>Lưu tệp vào wwwroot/uploads với tên duy nhất, trả về URL tương đối.</summary>
    public static async Task<string> SaveAsync(IFormFile f, string webRoot)
    {
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(f.FileName).ToLowerInvariant();
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);
        await using var stream = File.Create(Path.Combine(dir, name));
        await f.CopyToAsync(stream);
        return "/uploads/" + name;
    }

    /// <summary>
    /// Xoá tệp ảnh nằm trong thư mục uploads. Không xoá ảnh mặc định trong /images
    /// và chặn đường dẫn thoát khỏi thư mục (path traversal).
    /// </summary>
    /// <summary>
    /// Trả về đường dẫn tuyệt đối của ảnh nằm trong wwwroot/uploads.
    /// Trả chuỗi rỗng với ảnh mặc định (/images/...) hoặc đường dẫn không an toàn.
    /// </summary>
    public static string PathOf(string? url, string webRoot = "")
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        const string prefix = "/uploads/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
        var fileName = Path.GetFileName(url);
        return string.IsNullOrEmpty(fileName) ? "" : fileName;
    }

    /// <summary>Xoá tệp vật lý trong thư mục uploads; bỏ qua ảnh ở /images và đường dẫn thoát thư mục.</summary>
    public static void DeletePhysical(string fileName, string webRoot = "")
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        if (string.IsNullOrWhiteSpace(webRoot)) return;
        var dir = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
        var full = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(fileName)));
        if (!full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return;
        if (File.Exists(full)) File.Delete(full);
    }
}

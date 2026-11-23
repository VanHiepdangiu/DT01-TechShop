// DT01 TechShop — kiểm thử nhập CSV (FR-1.4) và xuất Excel (FR-5.4).
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechShop.Models;
using TechShop.Services;
using Xunit;

namespace TechShop.UnitTests;

public class BulkToolsTests
{
    [Fact]
    public void ParseCsv_HandlesQuotedFieldsAndCommas()
    {
        var rows = ProductImport.ParseCsv("Name,Price,SalePrice,Stock,Category,Brand,Description\n" +
                                          "\"Laptop Pro, 14 inch\",1000,,2,Laptop,ASUS,\"Mô tả, có dấu phẩy\"\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Laptop Pro, 14 inch", rows[1][0]);
        Assert.Equal("Mô tả, có dấu phẩy", rows[1][6]);
    }

    [Fact]
    public async Task ImportAsync_ValidRows_AreSaved()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var csv = "Ten,Price,SalePrice,Stock,Category,Brand,Description\n" +
                  "Sản phẩm A,1000000,900000,5,Unit-test category,,\n" +
                  "Sản phẩm B,2000000,,3,Unit-test category,,\n";
        var report = await ProductImport.ImportAsync(db, csv);
        Assert.Empty(report.Errors);
        Assert.Equal(2, report.Imported);
        Assert.Equal(3, await db.Products.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_InvalidPrice_RejectsWholeFile()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var csv = "Ten,Price,SalePrice,Stock,Category\n" +
                  "Sản phẩm hợp lệ,1000,,1,Unit-test category\n" +
                  "Sản phẩm lỗi,-5,,1,Unit-test category\n";
        var report = await ProductImport.ImportAsync(db, csv);
        Assert.Equal(0, report.Imported);
        Assert.Contains(report.Errors, e => e.Contains("Dòng 3"));
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_UnknownCategory_ReportsLine()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var report = await ProductImport.ImportAsync(db, "Ten,Price,SalePrice,Stock,Category\nSP,1000,,1,Không tồn tại\n");
        Assert.Equal(0, report.Imported);
        Assert.Contains("danh mục", report.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_TooManyRows_IsRejected()
    {
        await using var fx = await SqliteShopFixture.CreateAsync();
        await using var db = fx.NewDb();
        var csv = new StringBuilder("Ten,Price,SalePrice,Stock,Category\n");
        for (int i = 0; i < ProductImport.MaxRows + 1; i++) csv.Append($"SP {i},1000,,1,Unit-test category\n");
        var report = await ProductImport.ImportAsync(db, csv.ToString());
        Assert.Equal(0, report.Imported);
        Assert.Contains("tối đa", report.Errors[0]);
    }

    [Fact]
    public void XlsxWriter_ProducesWorkbookContainingData()
    {
        var bytes = XlsxWriter.Build("SanPham", new[] { "Mã", "Tên" },
            new List<object?[]> { new object?[] { 1, "MacBook Air <M3>" } });
        Assert.True(bytes.Length > 500);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet!.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        Assert.Contains("Mã", xml);
        Assert.Contains("MacBook Air &lt;M3&gt;", xml);
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
    }
}

// DT01 TechShop — Nguyễn Văn Hiệp — MSSV 2410900035 — K24CNT1
// Nhập sản phẩm từ CSV (FR-1.4) và xuất báo cáo Excel (FR-5.4) — không dùng thư viện ngoài.
using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TechShop.Data;
using TechShop.Models;

namespace TechShop.Services;

public record CsvProductRow(int Line, string Name, decimal Price, decimal? SalePrice, int Stock,
    string Category, string? Brand, string Description);

public record ImportReport(int Imported, int Skipped, IReadOnlyList<string> Errors);

/// <summary>Đọc CSV có hỗ trợ dấu ngoặc kép; mỗi dòng dữ liệu là một sản phẩm.</summary>
public static class ProductImport
{
    public const int MaxRows = 500;

    // Cột: Name,Price,SalePrice(để trống được),Stock,Category,Brand(để trống được),Description
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var current = new List<string>();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case ',': current.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    current.Add(field.ToString()); field.Clear();
                    if (current.Any(v => v.Trim().Length > 0)) rows.Add(current.ToArray());
                    current.Clear();
                    break;
                default: field.Append(c); break;
            }
        }
        current.Add(field.ToString());
        if (current.Any(v => v.Trim().Length > 0)) rows.Add(current.ToArray());
        return rows;
    }

    public static async Task<ImportReport> ImportAsync(ShopDbContext db, string text)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return new ImportReport(0, 0, new[] { "Tệp rỗng." });

        var rows = ParseCsv(text);
        if (rows.Count == 0) return new ImportReport(0, 0, new[] { "Tệp không có dòng dữ liệu." });

        int start = 0;
        var head = rows[0][0].Trim().ToLowerInvariant();
        if (head is "name" or "ten" or "tên" or "tensanpham" or "tên sản phẩm") start = 1;
        if (rows.Count - start > MaxRows)
            return new ImportReport(0, 0, new[] { $"Mỗi lần chỉ nhập tối đa {MaxRows} dòng." });

        var categories = await db.Categories.ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id);
        var brands = await db.Brands.ToDictionaryAsync(x => x.Name.ToLower(), x => x.Id);
        var parsed = new List<CsvProductRow>();

        for (int i = start; i < rows.Count; i++)
        {
            int line = i + 1;
            var c = rows[i];
            if (c.Length < 5) { errors.Add($"Dòng {line}: cần ít nhất 5 cột (Tên, Giá, Giá KM, Tồn, Danh mục)."); continue; }

            string name = c[0].Trim();
            if (name.Length == 0 || name.Length > 150) { errors.Add($"Dòng {line}: tên sản phẩm phải từ 1 đến 150 ký tự."); continue; }
            if (!decimal.TryParse(c[1].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price) || price <= 0)
            { errors.Add($"Dòng {line}: giá bán không hợp lệ."); continue; }

            decimal? sale = null;
            var saleText = c[2].Trim();
            if (saleText.Length > 0)
            {
                if (!decimal.TryParse(saleText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var s) || s <= 0 || s >= price)
                { errors.Add($"Dòng {line}: giá khuyến mãi phải lớn hơn 0 và nhỏ hơn giá bán."); continue; }
                sale = s;
            }

            if (!int.TryParse(c[3].Trim(), out var stock) || stock < 0)
            { errors.Add($"Dòng {line}: tồn kho phải là số nguyên không âm."); continue; }

            string category = c[4].Trim();
            if (!categories.ContainsKey(category.ToLower()))
            { errors.Add($"Dòng {line}: danh mục “{category}” chưa tồn tại."); continue; }

            string? brand = c.Length > 5 && c[5].Trim().Length > 0 ? c[5].Trim() : null;
            if (brand != null && !brands.ContainsKey(brand.ToLower()))
            { errors.Add($"Dòng {line}: hãng “{brand}” chưa tồn tại."); continue; }

            string description = c.Length > 6 ? c[6].Trim() : "";
            parsed.Add(new CsvProductRow(line, name, price, sale, stock, category, brand,
                description.Length > 4000 ? description[..4000] : description));
        }

        if (errors.Count > 0) return new ImportReport(0, rows.Count - start, errors);

        foreach (var r in parsed)
        {
            db.Products.Add(new Product
            {
                Name = r.Name,
                Price = r.Price,
                SalePrice = r.SalePrice,
                Stock = r.Stock,
                CategoryId = categories[r.Category.ToLower()],
                BrandId = r.Brand == null ? null : brands[r.Brand.ToLower()],
                Description = r.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Images = { new ProductImage { Url = "/images/laptop.svg", IsMain = true } }
            });
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            db.ChangeTracker.Clear();
            return new ImportReport(0, parsed.Count, new[] { "Không lưu được dữ liệu: " + (ex.InnerException?.Message ?? ex.Message) });
        }
        return new ImportReport(parsed.Count, 0, Array.Empty<string>());
    }
}

/// <summary>Xuất .xlsx tối giản (OpenXML) không cần thư viện ngoài.</summary>
public static class XlsxWriter
{
    public static byte[] Build(string sheetName, string[] headers, IReadOnlyList<object?[]> rows)
    {
        var name = SafeSheetName(sheetName);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "</Types>");

            Write(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");

            Write(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{SecurityElement.Escape(name)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

            Write(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");

            Write(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Times New Roman\"/></font>" +
                "<font><b/><sz val=\"11\"/><name val=\"Times New Roman\"/></font></fonts>" +
                "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                "</styleSheet>");

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            sb.Append("<row r=\"1\">");
            for (int c = 0; c < headers.Length; c++) sb.Append(Cell(c, 1, headers[c], style: 1));
            sb.Append("</row>");
            int r2 = 2;
            foreach (var row in rows)
            {
                sb.Append($"<row r=\"{r2}\">");
                for (int c = 0; c < row.Length; c++) sb.Append(Cell(c, r2, row[c]));
                sb.Append("</row>");
                r2++;
            }
            sb.Append("</sheetData></worksheet>");
            Write(zip, "xl/worksheets/sheet1.xml", sb.ToString());
        }
        return ms.ToArray();
    }

    static string Cell(int col, int row, object? value, int style = 0)
    {
        var reference = Column(col) + row;
        var styleAttr = style == 0 ? "" : $" s=\"{style}\"";
        if (value is null) return $"<c r=\"{reference}\"{styleAttr}/>";
        switch (value)
        {
            case decimal d:
                return $"<c r=\"{reference}\"{styleAttr}><v>{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>";
            case int i:
                return $"<c r=\"{reference}\"{styleAttr}><v>{i}</v></c>";
            case long l:
                return $"<c r=\"{reference}\"{styleAttr}><v>{l}</v></c>";
            case double db:
                return $"<c r=\"{reference}\"{styleAttr}><v>{db.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>";
            default:
                var text = SecurityElement.Escape(value.ToString() ?? "");
                return $"<c r=\"{reference}\"{styleAttr} t=\"inlineStr\"><is><t xml:space=\"preserve\">{text}</t></is></c>";
        }
    }

    static string Column(int index)
    {
        var name = "";
        index++;
        while (index > 0)
        {
            int rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - 1) / 26;
        }
        return name;
    }

    static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(ch => "\\/*?:[]".IndexOf(ch) < 0).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Sheet1";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}

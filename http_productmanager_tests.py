"""Kiểm thử trang Quản lý sản phẩm (thẻ) + trang Thêm mới sản phẩm riêng."""
import sys, re, html, uuid
import requests

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5095"
ok = []

PNG = (b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00"
       b"\x1f\x15\xc4\x89\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82")


def ck(n, c, d=""):
    ok.append(bool(c))
    print(("PASS" if c else "FAIL"), n, "" if c else str(d)[:200])


def page(r):
    return html.unescape(r.text)


def tok(r):
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text).group(1))


def login(e, p):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login", data={"__RequestVerificationToken": tok(r),
                                          "Login.Email": e, "Login.Password": p}, timeout=30)
    return s


a = login("admin@techshop.local", "Admin@123")

# 1. Trang quản lý sản phẩm dạng thẻ
r = a.get(BASE + "/Admin/ProductManager", timeout=30)
h = page(r)
ck("Trang ProductManager trả 200", r.status_code == 200, r.status_code)
ck("Có nút 'Thêm mới sản phẩm'", "Thêm mới sản phẩm" in h)
ck("Nút trỏ /Admin/Create", 'href="/Admin/Create"' in h)
ck("Có thẻ sản phẩm (product-card)", "product-card" in h)
ck("Có 5 ô thống kê", len(re.findall(r'class="stat-value[^"]*"', h)) == 5, len(re.findall(r'class="stat-value[^"]*"', h)))
ck("Có bộ lọc danh mục/hãng/trạng thái", 'name="categoryId"' in h and 'name="brandId"' in h and 'name="status"' in h)
ck("Hiển thị ảnh sản phẩm", 'class="product-thumb"' in h)

# 2. Lọc theo trạng thái + tìm kiếm
r = a.get(BASE + "/Admin/ProductManager?status=active", timeout=30)
ck("Lọc 'Đang bán' chạy được", r.status_code == 200 and "product-card" in page(r))
r = a.get(BASE + "/Admin/ProductManager?q=zzzzkhongco", timeout=30)
h = page(r)
ck("Tìm không có kết quả -> trạng thái rỗng", "Chưa có sản phẩm nào khớp" in h)

# 3. Trang Thêm mới riêng
r = a.get(BASE + "/Admin/Create", timeout=30)
h = page(r)
ck("Trang /Admin/Create trả 200", r.status_code == 200, r.status_code)
ck("Có dòng 'chế độ THÊM MỚI'", "chế độ THÊM MỚI" in h)
ck("Tiêu đề 'Thêm sản phẩm mới'", "Thêm sản phẩm mới" in h)
ck("Không có mục 'Sửa sản phẩm'", "Sửa sản phẩm #" not in h)
ck("Có đủ 4 nhóm trường", all(k in h for k in ["1. Thông tin bắt buộc", "2. Nội dung mô tả", "3. Hình ảnh", "4. Trạng thái"]))
ck("Có nút 'Lưu & thêm tiếp'", "thêm tiếp" in h.lower())
body_form = h.split("</script>")[0]
ck("Không có ô chọn ảnh chính (vì chưa có ảnh)", 'type="radio" name="mainImageId"' not in body_form)

# 4. Thêm sản phẩm thật từ trang Create -> quay về ProductManager
name = "SP Trang Moi " + uuid.uuid4().hex[:5]
data = {"__RequestVerificationToken": tok(r), "Id": "0", "Name": name, "Price": "12000000",
        "Stock": "8", "CategoryId": "1", "BrandId": "1", "Description": "sản phẩm kiểm thử trang thêm mới",
        "Specs[0].Name": "RAM", "Specs[0].Value": "16 GB", "IsActive": "true", "action": "save"}
r = a.post(BASE + "/Admin/Create", data=data, files=[("photos", ("p.png", PNG, "image/png"))],
           allow_redirects=True, timeout=60)
h = page(r)
ck("Thêm sản phẩm từ trang Create thành công", "Đã thêm sản phẩm" in h, h[:200])
ck("Chuyển về trang Quản lý sản phẩm", "Quản lý sản phẩm" in h and "product-card" in h)
ck("Có thông báo + nút 'Thêm sản phẩm khác'", "Thêm sản phẩm khác" in h)
ck("Sản phẩm mới xuất hiện trong danh sách", name in h)

# 5. Sản phẩm hiển thị ở cửa hàng
r = a.get(BASE + "/api/products?page=1", timeout=30)
names = [i["name"] for i in r.json()["items"]]
ck("Sản phẩm mới có trong API cửa hàng", name in names, names[:4])

# 6. Nhân viên bị chặn
s = login("staff@techshop.local", "Staff@123")
r = s.get(BASE + "/Admin/Create", allow_redirects=False, timeout=30)
ck("Staff bị chặn trang thêm mới", r.status_code in (302, 403), r.status_code)
r = s.get(BASE + "/Admin/ProductManager", allow_redirects=False, timeout=30)
ck("Staff bị chặn trang quản lý sản phẩm", r.status_code in (302, 403), r.status_code)

print("PRODUCTMANAGER RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

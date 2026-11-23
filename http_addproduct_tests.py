"""Kiểm thử chức năng THÊM SẢN PHẨM qua HTTP (form /Admin/Edit)."""
import requests, re, html, sys, uuid, io

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5092"
ok = []


def ck(name, cond, detail=""):
    ok.append(bool(cond))
    print(("PASS" if cond else "FAIL"), name, "" if cond else str(detail)[:300])


def page(r):
    return html.unescape(r.text)


def token(r):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text)
    return html.unescape(m.group(1))


def login(email, pw):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login",
           data={"__RequestVerificationToken": token(r), "Login.Email": email, "Login.Password": pw},
           timeout=30)
    return s


admin = login("admin@techshop.local", "Admin@123")

# 1. Form thêm mới mở được
r = admin.get(BASE + "/Admin/Edit", timeout=30)
h = page(r)
ck("Mở form thêm sản phẩm (GET /Admin/Edit)", r.status_code == 200, r.status_code)
ck("Tiêu đề 'Thêm sản phẩm mới'", "Thêm sản phẩm mới" in h)
ck("Có đủ 4 nhóm trường", all(k in h for k in ("Thông tin bắt buộc", "Nội dung mô tả", "Hình ảnh", "Trạng thái")))
ck("Có ô tải nhiều ảnh", 'name="photos"' in h and "multiple" in h)
ck("Có ô thông số kỹ thuật", 'name="SpecsText"' in h)

# 2. Thêm sản phẩm hợp lệ (LiPad Test) + upload 1 ảnh PNG
name = "LiPad Test " + uuid.uuid4().hex[:6]
png = (b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00"
       b"\x1f\x15\xc4\x89\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82")
data = {
    "__RequestVerificationToken": token(r),
    "Id": "0",
    "Name": name,
    "Price": "12990000",
    "SalePrice": "11990000",
    "Stock": "7",
    "CategoryId": "1",
    "BrandId": "1",
    "Description": "Sản phẩm tạo bởi test tự động",
    "SpecsText": "RAM=8 GB\nSSD=256 GB",
    "IsActive": "true",
}
files = {"photos": ("anh.png", png, "image/png")}
r = admin.post(BASE + "/Admin/Edit", data=data, files=files, allow_redirects=True, timeout=60)
h = page(r)
ck("POST thêm sản phẩm trả 200", r.status_code == 200, r.status_code)
ck("Thông báo 'Đã lưu sản phẩm'", "Đã lưu sản phẩm" in h, h[:200])
ck("Sản phẩm mới có trong danh sách", name in h)

# 3. Kiểm tra dữ liệu thật qua API catalog và trang chi tiết
r = admin.get(BASE + "/api/products?page=1", timeout=30)
items = r.json()["items"] if r.status_code == 200 else []
hit = [x for x in items if x["name"] == name]
ck("API /api/products trả sản phẩm mới", len(hit) == 1, items[:2])
if hit:
    pid = hit[0]["id"]
    ck("Giá đúng 12.990.000", float(hit[0]["price"]) == 12990000.0, hit[0]["price"])
    ck("Tồn kho đúng 7", hit[0]["stock"] == 7, hit[0]["stock"])
    r2 = admin.get(BASE + f"/Products/Details/{pid}", timeout=30)
    h2 = page(r2)
    ck("Trang chi tiết hiển thị sản phẩm", r2.status_code == 200 and name in h2)
    ck("Trang chi tiết có thông số RAM=8 GB", "RAM" in h2 and "8 GB" in h2)

# 4. Giá khuyến mãi lớn hơn giá bán -> bị chặn
r = admin.get(BASE + "/Admin/Edit", timeout=30)
bad = dict(data)
bad["__RequestVerificationToken"] = token(r)
bad["Name"] = "SP Lỗi Giá " + uuid.uuid4().hex[:4]
bad["Price"] = "5000000"
bad["SalePrice"] = "9000000"
r = admin.post(BASE + "/Admin/Edit", data=bad, allow_redirects=True, timeout=60)
h = page(r)
ck("Chặn giá khuyến mãi > giá bán", "Giá khuyến mại phải lớn hơn 0 và không vượt giá gốc" in h, h[:300])

# 5. Thiếu tên -> bị chặn bởi validation
r = admin.get(BASE + "/Admin/Edit", timeout=30)
bad2 = dict(data)
bad2["__RequestVerificationToken"] = token(r)
bad2["Name"] = ""
r = admin.post(BASE + "/Admin/Edit", data=bad2, allow_redirects=True, timeout=60)
ck("Chặn khi thiếu tên sản phẩm", r.status_code == 200 and "Thêm sản phẩm mới" in page(r))

# 6. Tệp không phải ảnh -> bị chặn
r = admin.get(BASE + "/Admin/Edit", timeout=30)
bad3 = dict(data)
bad3["__RequestVerificationToken"] = token(r)
bad3["Name"] = "SP Ảnh Lỗi " + uuid.uuid4().hex[:4]
r = admin.post(BASE + "/Admin/Edit", data=bad3,
               files={"photos": ("doc.txt", b"hello world this is not an image", "text/plain")},
               allow_redirects=True, timeout=60)
ck("Chặn tệp không phải ảnh", "Chỉ nhận tối đa 5 ảnh" in page(r), page(r)[:300])

# 7. Staff không được thêm sản phẩm
staff = login("staff@techshop.local", "Staff@123")
r = staff.get(BASE + "/Admin/Edit", allow_redirects=False, timeout=30)
ck("Staff bị chặn form thêm sản phẩm", r.status_code in (302, 403), r.status_code)

print("ADDPRODUCT RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

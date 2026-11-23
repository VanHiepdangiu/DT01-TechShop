# Kiểm thử thật 2 chức năng mới: nhập CSV (FR-1.4) và xuất Excel (FR-5.4)
import requests, re, html, io, zipfile, sys

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5090"
ok = []


def ck(name, cond, detail=""):
    ok.append(bool(cond))
    print(("PASS" if cond else "FAIL"), name, "" if cond else detail)


def page(resp):
    # ASP.NET Core HtmlEncoder mã hoá ký tự tiếng Việt thành entity; giải mã trước khi so khớp.
    return html.unescape(resp.text)


def token(text):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text)
    return html.unescape(m.group(1))


def login(email, pw):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login",
           data={"__RequestVerificationToken": token(r.text), "Login.Email": email, "Login.Password": pw},
           timeout=30)
    return s


admin = login("admin@techshop.local", "Admin@123")

# --- 1. Nhập CSV hợp lệ
r = admin.get(BASE + "/Admin/Products", timeout=30)
t = token(r.text)
ck("Trang quản lý sản phẩm có form nhập CSV", 'action="/Admin/Import"' in r.text)
csv_ok = ("Ten,Price,SalePrice,Stock,Category,Brand,Description\n"
          "Test CSV Laptop A,15000000,14000000,4,Laptop,ASUS,Máy test nhập CSV\n"
          "Test CSV Phone B,9000000,,7,Điện thoại,Samsung,\n").encode("utf-8")
files = {"file": ("sanpham.csv", csv_ok, "text/csv")}
r = admin.post(BASE + "/Admin/Import",
               data={"__RequestVerificationToken": t}, files=files,
               allow_redirects=True, timeout=60)
ck("POST /Admin/Import trả về trang quản lý", r.status_code == 200 and "Quản lý sản phẩm" in page(r), r.status_code)
ck("Thông báo nhập thành công 2 sản phẩm", "Đã nhập 2 sản phẩm" in page(r), page(r)[:300])
ck("Sản phẩm mới xuất hiện trong danh sách", "Test CSV Laptop A" in page(r) and "Test CSV Phone B" in page(r))

# --- 2. Nhập CSV lỗi (giá âm) → không lưu dòng nào, báo số dòng
r = admin.get(BASE + "/Admin/Products", timeout=30)
t = token(r.text)
bad = ("Ten,Price,SalePrice,Stock,Category\n"
       "Test CSV Bad,1000,,1,Laptop\n"
       "Test CSV Bad2,-99,,1,Laptop\n").encode("utf-8")
r = admin.post(BASE + "/Admin/Import", data={"__RequestVerificationToken": t},
               files={"file": ("bad.csv", bad, "text/csv")}, allow_redirects=True, timeout=60)
ck("CSV lỗi bị từ chối toàn bộ", "Không nhập dòng nào" in page(r) and "Dòng 3" in page(r), page(r)[:300])
ck("Sản phẩm lỗi không được lưu", "Test CSV Bad2" not in page(r))

# --- 3. Từ chối tệp không phải .csv
r = admin.get(BASE + "/Admin/Products", timeout=30)
t = token(r.text)
r = admin.post(BASE + "/Admin/Import", data={"__RequestVerificationToken": t},
               files={"file": ("anh.png", b"\x89PNG\r\n\x1a\n", "image/png")},
               allow_redirects=True, timeout=60)
ck("Từ chối tệp .png", "Chỉ nhận tệp .csv" in page(r), page(r)[:300])

# --- 4. Xuất Excel danh mục sản phẩm
r = admin.get(BASE + "/Admin/ExportProducts", timeout=60)
ck("ExportProducts trả 200", r.status_code == 200, r.status_code)
ck("Content-Type xlsx", "spreadsheetml.sheet" in r.headers.get("Content-Type", ""), r.headers.get("Content-Type"))
ck("Có tên tệp .xlsx", ".xlsx" in r.headers.get("Content-Disposition", ""), r.headers.get("Content-Disposition"))
z = zipfile.ZipFile(io.BytesIO(r.content))
ck("Workbook có sheet1.xml", "xl/worksheets/sheet1.xml" in z.namelist())
sheet = z.read("xl/worksheets/sheet1.xml").decode("utf-8")
ck("Sheet chứa sản phẩm vừa nhập", "Test CSV Laptop A" in sheet, sheet[:200])
ck("Sheet có tiêu đề cột", "Tồn kho" in sheet)

# --- 5. Xuất doanh thu
r = admin.get(BASE + "/Admin/ExportRevenue", timeout=60)
ck("ExportRevenue trả 200", r.status_code == 200, r.status_code)
z2 = zipfile.ZipFile(io.BytesIO(r.content))
ck("Báo cáo doanh thu có dữ liệu", "xl/worksheets/sheet1.xml" in z2.namelist())

# --- 6. Nhân viên (Staff) không được nhập/xuất
staff = login("staff@techshop.local", "Staff@123")
r = staff.get(BASE + "/Admin/ExportProducts", allow_redirects=False, timeout=30)
ck("Staff bị chặn ExportProducts", r.status_code in (302, 403), r.status_code)

print("BULK RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

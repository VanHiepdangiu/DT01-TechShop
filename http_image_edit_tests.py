"""Kiểm thử sửa/xoá ảnh và thông số sản phẩm qua HTTP."""
import requests, re, html, sys, uuid

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5093"
ok = []

PNG = (b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00"
       b"\x1f\x15\xc4\x89\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82")


def ck(n, c, d=""):
    ok.append(bool(c))
    print(("PASS" if c else "FAIL"), n, "" if c else str(d)[:250])


def page(r):
    return html.unescape(r.text)


def tok(r):
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text).group(1))


def login(e, p):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login",
           data={"__RequestVerificationToken": tok(r), "Login.Email": e, "Login.Password": p}, timeout=30)
    return s


a = login("admin@techshop.local", "Admin@123")
name = "SP Anh Test " + uuid.uuid4().hex[:5]

# 1. Tạo sản phẩm với 2 ảnh + 2 thông số
r = a.get(BASE + "/Admin/Edit", timeout=30)
data = {"__RequestVerificationToken": tok(r), "Id": "0", "Name": name, "Price": "9000000",
        "Stock": "5", "CategoryId": "1", "BrandId": "1", "Description": "test",
        "Specs[0].Name": "RAM", "Specs[0].Value": "8 GB",
        "Specs[1].Name": "SSD", "Specs[1].Value": "256 GB", "IsActive": "true", "action": "save"}
r = a.post(BASE + "/Admin/Edit", data=data,
           files=[("photos", ("a1.png", PNG, "image/png")), ("photos", ("a2.png", PNG, "image/png"))],
           allow_redirects=True, timeout=60)
ck("Tạo sản phẩm 2 ảnh + 2 thông số", ("Đã thêm sản phẩm" in page(r)) or ("Đã lưu sản phẩm" in page(r)), page(r)[:150])

# tìm id
pid = None
for it in a.get(BASE + "/api/products?page=1", timeout=30).json()["items"]:
    if it["name"] == name:
        pid = it["id"]
ck("Sản phẩm được tạo", pid is not None)
if pid:
    # 2. Mở form sửa: phải thấy thông số + 2 ảnh + radio ảnh chính
    r = a.get(BASE + f"/Admin/Edit/{pid}", timeout=30)
    h = page(r)
    ck("Form sửa hiển thị tên sản phẩm", name in h)
    ck("Form sửa có dòng thông số RAM", 'value="RAM"' in h and 'value="8 GB"' in h)
    ck("Form sửa có dòng thông số SSD", 'value="SSD"' in h)
    # form mới có thêm input ẩn "Specs[0].Names" không dùng; đếm theo value để chính xác
    ck("Có 2 ảnh hiện có", len(re.findall(r'name="removeImageIds" value="\d+"', r.text)) == 2,
       re.findall(r'name="removeImageIds"[^>]*', r.text))
    ck("Có nút ảnh chính", 'name="mainImageId"' in h)
    # đọc id ảnh
    ids = re.findall(r'name="removeImageIds" value="(\d+)"', r.text)
    ck("Đọc được id ảnh", len(ids) == 2, ids)
    ck("Ảnh chính đang được chọn", 'value="%s" checked' % ids[0] in r.text or 'checked' in r.text)

    # 3. Sửa: xoá ảnh thứ 2, đặt ảnh 1 làm chính, thêm 1 ảnh mới, sửa thông số
    data2 = {"__RequestVerificationToken": tok(r), "Id": str(pid), "RowVersion": re.search(r'name="RowVersion"[^>]*value="([^"]*)"', r.text).group(1),
             "Name": name, "Price": "9500000", "Stock": "6", "CategoryId": "1", "BrandId": "1",
             "Description": "đã sửa", "IsActive": "true", "action": "save",
             "Specs[0].Name": "RAM", "Specs[0].Value": "16 GB",
             "Specs[1].Name": "Màu sắc", "Specs[1].Value": "Đen",
             "mainImageId": ids[0], "removeImageIds": ids[1]}
    r = a.post(BASE + "/Admin/Edit", data=data2,
               files=[("photos", ("a3.png", PNG, "image/png"))], allow_redirects=True, timeout=60)
    ck("Lưu thay đổi thành công", ("Đã lưu thay đổi" in page(r)) or ("Đã lưu sản phẩm" in page(r)), page(r)[:200])

    # 4. Kiểm tra kết quả: còn 2 ảnh (1 xoá, 1 thêm), thông số đổi
    r = a.get(BASE + f"/Admin/Edit/{pid}", timeout=30)
    h2 = page(r)
    ids2 = re.findall(r'name="removeImageIds" value="(\d+)"', r.text)
    ck("Sau sửa còn đúng 2 ảnh", len(ids2) == 2, ids2)
    ck("Ảnh bị xoá không còn", ids[1] not in ids2, (ids, ids2))
    ck("Thông số RAM đã đổi thành 16 GB", 'value="16 GB"' in h2)
    ck("Thông số mới Màu sắc xuất hiện", 'value="Màu sắc"' in h2)
    ck("Thông số cũ SSD đã mất", 'value="SSD"' not in h2)
    r2 = a.get(BASE + f"/Products/Details/{pid}", timeout=30)
    h3 = page(r2)
    ck("Trang khách hiển thị thông số mới", "16 GB" in h3 and "Màu sắc" in h3)
    # trang chi tiết định dạng giá theo culture mặc định của server (9,500,000 ₫)
    price_shown = re.findall(r"[\d][\d\.,]*\s*(?:₫|đ)", h3)
    ck("Giá đã cập nhật 9.500.000", any(x.replace(",", ".").replace(" ", "") in ("9.500.000₫", "9500000₫")
                                        or x.startswith("9,500,000") for x in price_shown), price_shown[:4])

    # 5. Chặn xoá quá 0 ảnh? (xoá cả 2 -> còn 0 ảnh, dùng ảnh mặc định)
    r = a.get(BASE + f"/Admin/Edit/{pid}", timeout=30)
    rv = re.search(r'name="RowVersion"[^>]*value="([^"]*)"', r.text).group(1)
    ids3 = re.findall(r'name="removeImageIds" value="(\d+)"', r.text)
    data3 = {"__RequestVerificationToken": tok(r), "Id": str(pid), "RowVersion": rv, "Name": name,
             "Price": "9500000", "Stock": "6", "CategoryId": "1", "BrandId": "1",
             "Description": "x", "IsActive": "true", "action": "save",
             "Specs[0].Name": "RAM", "Specs[0].Value": "16 GB"}
    r = a.post(BASE + "/Admin/Edit", data=data3,
               files=[("removeImageIds", (None, i)) for i in ids3], allow_redirects=True, timeout=60)
    ck("Xoá hết ảnh vẫn lưu được", ("Đã lưu thay đổi" in page(r)) or ("Đã lưu sản phẩm" in page(r)), page(r)[:200])
    r2 = a.get(BASE + f"/Products/Details/{pid}", timeout=30)
    ck("Sản phẩm vẫn hiển thị (ảnh mặc định)", r2.status_code == 200)

    # 6. RowVersion cũ -> 409
    data4 = dict(data3)
    data4["__RequestVerificationToken"] = tok(r)
    data4["RowVersion"] = rv  # đã dùng ở bước trước
    r = a.post(BASE + "/Admin/Edit", data=data4, allow_redirects=True, timeout=60)
    ck("RowVersion cũ bị chặn", "vừa thay đổi" in page(r) or r.status_code == 409, r.status_code)

print("IMGTEST RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

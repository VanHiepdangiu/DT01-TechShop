"""Kiểm thử chức năng CHUYỂN KHOẢN NGÂN HÀNG qua HTTP."""
import re
import sys
import uuid

import requests
import html

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5100"
ok = []


def ck(n, c, d=""):
    ok.append(bool(c))
    print(("PASS" if c else "FAIL"), n, "" if c else str(d)[:220])


def page(r):
    return html.unescape(r.text)


def tok(r):
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text).group(1))


def login(email, pwd):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login",
           data={"__RequestVerificationToken": tok(r), "Login.Email": email, "Login.Password": pwd},
           timeout=30)
    return s


# 1. Trang đặt hàng có lựa chọn chuyển khoản
cust = login("customer@techshop.local", "Customer@123")
r = cust.get(BASE + "/Products/Details/4", timeout=30)
cust.post(BASE + "/Cart/Update", data={"productId": 4, "quantity": 1},
          headers={"X-CSRF-TOKEN": tok(r)}, timeout=30)
r = cust.get(BASE + "/Checkout", timeout=30)
h = page(r)
ck("Trang đặt hàng có lựa chọn chuyển khoản", 'value="BankTransfer"' in h)
ck("Nhãn hiển thị tên ngân hàng", "Chuyển khoản ngân hàng" in h)
ck("VNPay vẫn còn", 'value="VNPay"' in h)
ck("COD vẫn còn", 'value="COD"' in h)

# 2. Đặt hàng bằng chuyển khoản
data = {"__RequestVerificationToken": tok(r), "Key": str(uuid.uuid4()),
        "FullName": "Khach Chuyen Khoan", "Phone": "0988089376",
        "Address": "Số 1 Đường Test, Hà Nội", "PaymentMethod": "BankTransfer"}
r = cust.post(BASE + "/Checkout", data=data, allow_redirects=True, timeout=60)
h = page(r)
ck("Đặt hàng chuyển khoản thành công", "Chuyển khoản cho đơn" in h, r.url)
ck("Chuyển tới trang hướng dẫn chuyển khoản", "/Orders/BankTransfer/" in r.url, r.url)

order_id = re.search(r"/Orders/BankTransfer/(\d+)", r.url)
oid = int(order_id.group(1)) if order_id else None
ck("Lấy được mã đơn", oid is not None)

# 3. Trang hướng dẫn có đủ thông tin
ck("Có mã QR VietQR", "img.vietqr.io" in h)
ck("Có số tài khoản", "0123456789" in h)
ck("Có tên chủ tài khoản", "NGUYEN VAN HIEP" in h)
ck("Có tên ngân hàng", "Vietcombank" in h)
ref = "TS%06d" % oid if oid else ""
ck("Có mã đối chiếu đúng định dạng", ref in h, ref)
ck("Có nút sao chép mã", "Sao chép" in h)
ck("Có số tiền", "₫" in h)
ck("Có nút báo đã chuyển khoản", "Tôi đã chuyển khoản" in h)
ck("Có hướng dẫn thủ công", "Nội dung chuyển khoản" in h)

# 4. Đơn ở trạng thái chờ thanh toán
r = cust.get(BASE + "/Orders/Details/%d" % oid, timeout=30)
h = page(r)
ck("Đơn ở trạng thái Chờ thanh toán", "Chờ thanh toán" in h, h[:150])
ck("Hiển thị phương thức chuyển khoản", "Chuyển khoản ngân hàng" in h)
ck("Có nút hướng dẫn chuyển khoản", "Hướng dẫn chuyển khoản" in h)
ck("Chưa hiện nút thanh toán VNPay", "Thanh toán sandbox" not in h)

# 5. Khách báo đã chuyển khoản
r = cust.get(BASE + "/Orders/BankTransfer/%d" % oid, timeout=30)
r = cust.post(BASE + "/Orders/ReportTransfer",
              data={"__RequestVerificationToken": tok(r), "id": oid, "note": "FT26112345678"},
              allow_redirects=True, timeout=60)
h = page(r)
ck("Báo chuyển khoản thành công", "Đã ghi nhận" in h or "đối chiếu sao kê" in h, h[:200])
ck("Trạng thái chuyển sang Chờ đối chiếu", "Chờ đối chiếu chuyển khoản" in h, h[:200])
ck("Lịch sử có ghi nhận báo chuyển khoản", "Khách báo đã chuyển khoản" in h)

# 6. Khách không tự xác nhận được
ck("Khách không có nút xác nhận của nhân viên", "Xác nhận đã nhận tiền" not in h)

# 7. Nhân viên xác nhận đã nhận tiền
staff = login("staff@techshop.local", "Staff@123")
r = staff.get(BASE + "/Orders/Details/%d" % oid, timeout=30)
h = page(r)
ck("Nhân viên thấy nút xác nhận chuyển khoản", "Xác nhận đã nhận tiền chuyển khoản" in h, h[:200])
r = staff.post(BASE + "/Admin/ConfirmTransfer",
               data={"__RequestVerificationToken": tok(r), "id": oid, "note": "FT26112345678"},
               allow_redirects=True, timeout=60)
h = page(r)
ck("Xác nhận thanh toán thành công", "Đã xác nhận thanh toán" in h, h[:200])
ck("Trạng thái thanh toán Đã thanh toán", "Đã thanh toán" in h)
ck("Đơn chuyển sang trạng thái Mới", "Mới" in h)
ck("Lịch sử ghi xác nhận", "xác nhận đã nhận tiền chuyển khoản" in h)

# 8. Xác nhận lặp không lỗi
r = staff.post(BASE + "/Admin/ConfirmTransfer",
               data={"__RequestVerificationToken": tok(r), "id": oid},
               allow_redirects=True, timeout=60)
h = page(r)
ck("Xác nhận lặp không gây lỗi", "trước đó" in h or "Đã thanh toán" in h, h[:200])

# 9. Khách hàng không gọi được API xác nhận
r = cust.post(BASE + "/Admin/ConfirmTransfer", data={"id": oid}, allow_redirects=False, timeout=30)
ck("Khách hàng bị chặn xác nhận thanh toán", r.status_code in (302, 403), r.status_code)

# 10. Đơn COD không bị ảnh hưởng
r = cust.get(BASE + "/Products/Details/11", timeout=30)
cust.post(BASE + "/Cart/Update", data={"productId": 11, "quantity": 1},
          headers={"X-CSRF-TOKEN": tok(r)}, timeout=30)
r = cust.get(BASE + "/Checkout", timeout=30)
r = cust.post(BASE + "/Checkout", data={"__RequestVerificationToken": tok(r), "Key": str(uuid.uuid4()),
                                        "FullName": "Khach COD", "Phone": "0988089376",
                                        "Address": "Số 2 Đường Test", "PaymentMethod": "COD"},
              allow_redirects=True, timeout=60)
h = page(r)
ck("Đơn COD vẫn tạo bình thường", "Đơn hàng" in h and "Chờ thanh toán" not in h[:3000], r.url)
ck("Đơn COD không chuyển sang trang ngân hàng", "/Orders/BankTransfer/" not in r.url)

print("BANKTRANSFER RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

"""Kiểm thử WEBHOOK ngân hàng báo có: xác thực chữ ký, đối chiếu mã đơn, chống trùng, lệch tiền."""
import hashlib
import hmac
import html
import re
import sys
import uuid
from datetime import datetime, timezone

import requests
import json

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5102"
SECRET = "demo-webhook-secret-doi-khi-trien-khai-that"
ok = []


def ck(n, c, d=""):
    ok.append(bool(c))
    print(("PASS" if c else "FAIL"), n, "" if c else str(d)[:250])


def page(r):
    return html.unescape(r.text)


def tok(r):
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text).group(1))


def login(email, pwd):
    s = requests.Session()
    r = s.get(BASE + "/Account", timeout=30)
    s.post(BASE + "/Account/Login",
           data={"__RequestVerificationToken": tok(r), "Login.Email": email, "Login.Password": pwd}, timeout=30)
    return s


def sign(tid, content, amount, when):
    # C# dùng amount.ToString("0.##") với culture bất biến -> không có số mũ, không có số 0 thừa
    amt = ("%.2f" % float(amount)).rstrip("0").rstrip(".")
    canon = "amount=%s&content=%s&transactionId=%s&transactionAt=%s" % (amt, content, tid, when)
    return hmac.new(SECRET.encode(), canon.encode(), hashlib.sha256).hexdigest()


def hook(tid, content, amount, when, signature=None, key="amount"):
    payload = {key: (float(amount) if key == "amount" else amount),
               "content": content, "transactionId": tid, "transactionAt": when}
    if key != "amount":
        payload["amount"] = float(amount)
    if signature is None:
        signature = sign(tid, content, amount, when)
    return requests.post(BASE + "/api/bank/webhook", json=payload,
                         headers={"X-Bank-Signature": signature}, timeout=30)


def mkorder(sess, pid=4):
    r = sess.get(BASE + "/Products/Details/%d" % pid, timeout=30)
    sess.post(BASE + "/Cart/Update", data={"productId": pid, "quantity": 1},
              headers={"X-CSRF-TOKEN": tok(r)}, timeout=30)
    r = sess.get(BASE + "/Checkout", timeout=30)
    r = sess.post(BASE + "/Checkout",
                  data={"__RequestVerificationToken": tok(r), "Key": str(uuid.uuid4()),
                        "FullName": "Khach Webhook", "Phone": "0988089376",
                        "Address": "Số 9 Đường Webhook, Hà Nội", "PaymentMethod": "BankTransfer"},
                  allow_redirects=True, timeout=60)
    m = re.search(r"/Orders/BankTransfer/(\d+)", r.url)
    return int(m.group(1)) if m else None


# ------------------------------------------------------------ chuẩn bị
cust = login("customer@techshop.local", "Customer@123")
oid = mkorder(cust, 4)
ck("Tạo đơn chuyển khoản để test", oid is not None)
ref = "TS%06d" % oid

r = requests.get(BASE + "/api/bank/status", timeout=30)
ck("Endpoint trạng thái webhook hoạt động", r.status_code == 200 and r.json().get("enabled") is True, r.text[:120])

# ------------------------------------------------------------ 1. chữ ký sai
now = datetime.now(timezone.utc).isoformat()
r = hook("TID-BAD-1", "CK " + ref, 1000, now, signature="0" * 64)
ck("Chữ ký sai bị từ chối 401", r.status_code == 401, (r.status_code, r.text[:120]))
ck("Thông báo lỗi chữ ký rõ ràng", "invalid_signature" in r.text, r.text[:120])

r = requests.post(BASE + "/api/bank/webhook", json={"transactionId": "X", "content": "y", "amount": 1},
                  timeout=30)
ck("Thiếu header chữ ký bị từ chối", r.status_code == 401, r.status_code)

# ------------------------------------------------------------ 2. nội dung không có mã đơn
r = hook("TID-NOREF", "Chuyen tien mua hang", 500000, now)
ck("Nội dung không có mã đơn trả 202 unmatched", r.status_code == 202 and r.json()["status"] == "unmatched",
   (r.status_code, r.text[:150]))

# ------------------------------------------------------------ 3. mã đơn không tồn tại
r = hook("TID-NOORDER", "CK TS999999", 500000, now)
ck("Mã đơn không tồn tại trả unmatched", r.json()["status"] == "unmatched", r.text[:150])

# ------------------------------------------------------------ 4. lệch số tiền
total = 0
r = cust.get(BASE + "/Orders/Details/%d" % oid, timeout=30)
m = re.search(r"([\d\.]+)\s*₫", page(r))
r = hook("TID-WRONGAMT", "CK " + ref, 1000, now)
ck("Số tiền lệch trả 422", r.status_code == 422 and r.json()["status"] == "amount_mismatch",
   (r.status_code, r.text[:180]))
r = cust.get(BASE + "/Orders/Details/%d" % oid, timeout=30)
ck("Đơn vẫn Chờ thanh toán khi lệch tiền", "Chờ thanh toán" in page(r), page(r)[:150])

# lấy đúng tổng tiền từ trang chuyển khoản: tìm ô <th>Số tiền</th> rồi tới số đầu tiên trong <td>
r = cust.get(BASE + "/Orders/BankTransfer/%d" % oid, timeout=30)
h = page(r)
pos = h.find("<th>Số tiền</th>")
seg = h[pos:pos + 300] if pos >= 0 else ""
m = re.search(r"([\d]{1,3}(?:[.,][\d]{3})+)", seg)
amt = int(re.sub(r"[^0-9]", "", m.group(1))) if m else 0
ck("Đọc được tổng tiền của đơn", amt > 0, seg[:150])

# ------------------------------------------------------------ 5. webhook đúng -> tự xác nhận
r = hook("TID-OK-1", "CK " + ref, amt, now)
ck("Webhook đúng xác nhận thanh toán 200", r.status_code == 200 and r.json()["status"] == "matched",
   (r.status_code, r.text[:180]))
ck("Trả về đúng mã đơn", r.json().get("orderId") == oid, r.json())

r = cust.get(BASE + "/Orders/Details/%d" % oid, timeout=30)
h = page(r)
ck("Đơn chuyển sang Đã thanh toán", "Đã thanh toán" in h, h[:200])
ck("Đơn chuyển sang trạng thái Mới", ">Mới<" in h or "Mới" in h, h[:200])
ck("Lịch sử ghi webhook ngân hàng", "Webhook ngân hàng" in h, h[:300])

# ------------------------------------------------------------ 6. chống trùng
r = hook("TID-OK-1", "CK " + ref, amt, now)
ck("Gửi lại cùng mã giao dịch bị chặn trùng", r.json()["status"] == "duplicate",
   (r.status_code, r.text[:180]))
r = hook("TID-OK-2", "CK " + ref, amt, now)
ck("Giao dịch mới cho đơn đã trả tiền -> duplicate", r.json()["status"] == "duplicate", r.text[:180])

# ------------------------------------------------------------ 7. đơn COD không bị ảnh hưởng
r = cust.get(BASE + "/Products/Details/11", timeout=30)
cust.post(BASE + "/Cart/Update", data={"productId": 11, "quantity": 1},
          headers={"X-CSRF-TOKEN": tok(r)}, timeout=30)
r = cust.get(BASE + "/Checkout", timeout=30)
r = cust.post(BASE + "/Checkout",
              data={"__RequestVerificationToken": tok(r), "Key": str(uuid.uuid4()),
                    "FullName": "Khach COD", "Phone": "0988089376",
                    "Address": "Số 3 Test", "PaymentMethod": "COD"}, allow_redirects=True, timeout=60)
cod_id = int(re.search(r"/Orders/Details/(\d+)", r.url).group(1))
r = hook("TID-COD", "CK TS%06d" % cod_id, 100000, now)
ck("Webhook cho đơn COD bị từ chối unmatched", r.json()["status"] == "unmatched", r.text[:180])
r = cust.get(BASE + "/Orders/Details/%d" % cod_id, timeout=30)
ck("Đơn COD không bị đánh dấu đã thanh toán", "Chưa thanh toán" in page(r), page(r)[:150])

# ------------------------------------------------------------ 8. phân quyền
anon = requests.Session()
r = anon.get(BASE + "/Admin/BankTransactions", allow_redirects=False, timeout=30)
ck("Chưa đăng nhập không xem được nhật ký", r.status_code in (302, 401, 403), r.status_code)
r = anon.get(BASE + "/api/bank/transactions", allow_redirects=False, timeout=30)
ck("API nhật ký chặn người chưa đăng nhập", r.status_code in (302, 401, 403), r.status_code)

staff = login("staff@techshop.local", "Staff@123")
r = staff.get(BASE + "/Admin/BankTransactions", timeout=30)
ck("Nhân viên xem được trang đối soát", r.status_code == 200 and "Giao dịch ngân hàng" in page(r), r.status_code)
r = staff.get(BASE + "/api/bank/transactions", timeout=30)
data = r.json()
ck("API nhật ký trả danh sách", r.status_code == 200 and len(data) >= 4, len(data) if r.ok else r.status_code)
ck("Có bản ghi matched", any(x["status"] == "matched" for x in data))
ck("Có bản ghi unmatched", any(x["status"] == "unmatched" for x in data))
ck("Có bản ghi duplicate", any(x["status"] == "duplicate" for x in data))
ck("Có bản ghi invalid (chữ ký sai)", any(x["status"] == "invalid" for x in data))

# ------------------------------------------------------------ 9. trình giả lập (admin)
admin = login("admin@techshop.local", "Admin@123")
oid2 = mkorder(admin, 5) if False else mkorder(cust, 5)
r = admin.get(BASE + "/Admin/BankTransactions", timeout=30)
h = page(r)
ck("Admin thấy form giả lập ngân hàng", "Giả lập ngân hàng báo có" in h, h[:200])
ck("Trang có nút báo có", "Báo có" in h)
r = admin.post(BASE + "/api/bank/simulate", params={"orderId": oid2},
               headers={"X-CSRF-TOKEN": tok(r)}, timeout=60)
ck("Giả lập báo có thành công", r.status_code == 200 and r.json().get("status") == "matched",
   (r.status_code, r.text[:200]))
r = cust.get(BASE + "/Orders/Details/%d" % oid2, timeout=30)
ck("Đơn thứ hai được xác nhận qua giả lập", "Đã thanh toán" in page(r), page(r)[:150])

r = cust.post(BASE + "/api/bank/simulate", params={"orderId": oid2}, timeout=30)
ck("Khách không dùng được trình giả lập", r.status_code in (302, 401, 403), r.status_code)

print("WEBHOOK RESULT: %d passed, %d failed" % (sum(ok), len(ok) - sum(ok)))

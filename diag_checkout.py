import requests, re, html

BASE = "http://127.0.0.1:5093"


def tok(r):
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text).group(1))


s = requests.Session()
r = s.get(BASE + "/Account")
s.post(BASE + "/Account/Login", data={"__RequestVerificationToken": tok(r),
                                      "Login.Email": "customer@techshop.local",
                                      "Login.Password": "Customer@123"})
r = s.get(BASE + "/Products/Details/6")
print("details:", r.status_code)
rc = s.post(BASE + "/Cart/Update", data={"productId": 6, "quantity": 1},
            headers={"X-CSRF-TOKEN": tok(r)})
print("cart update:", rc.status_code, rc.text[:120])
r = s.get(BASE + "/Checkout", allow_redirects=False)
print("checkout:", r.status_code, r.headers.get("Location"))
if r.status_code == 200:
    print("len html:", len(r.text))
    print("co Key input:", "Key" in r.text)
    m = re.search(r'<input[^>]*name="Key"[^>]*>', r.text)
    print("input Key:", m.group(0) if m else "KHONG THAY")
    print("form:", "checkout-form" in r.text)
else:
    body = s.get(BASE + "/Cart").text
    print("gio hang:", "trong" if "trống" in body.lower() else "co hang")

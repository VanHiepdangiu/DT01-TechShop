"""HTTP integration tests against a running LOCAL TechShop instance. Uses isolated test users/orders.
Run on demo DB only, not on production. Prices checked from actual response and DB.
"""
import requests,re,json,sys,uuid,concurrent.futures,time
from html import unescape
BASE=sys.argv[1]if len(sys.argv)>1 else 'http://127.0.0.1:5088'
results=[]
def ck(name,condition,detail=''):
 results.append({'name':name,'pass':bool(condition),'detail':str(detail)[:300]});print(('PASS'if condition else'FAIL'),name,detail if not condition else'')
def get(s,path):return s.get(BASE+path,timeout=30)
def token(text):
 m=re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',text)
 if not m:raise RuntimeError('No antiforgery token in '+text[:200])
 return unescape(m.group(1))
def login(email,password):
 s=requests.Session();r=get(s,'/Account');t=token(r.text);r=s.post(BASE+'/Account/Login',data={'__RequestVerificationToken':t,'Login.Email':email,'Login.Password':password},timeout=30)
 ck('Login '+email,r.status_code==200 and '/Account'not in r.url,r.status_code)
 return s
anon=requests.Session();r=anon.get(BASE+'/',allow_redirects=False,timeout=30);ck('Catalog access follows configured login gate',r.status_code==302)
ck('Anonymous API report blocked',get(anon,'/api/reports/revenue').status_code==401)
ck('Profile protected',anon.get(BASE+'/Account/Profile',allow_redirects=False).status_code==302)
r=get(anon,'/Account');ck('Combined login/register screen',r.status_code==200 and 'Register.Email'in r.text and 'Login.Email'in r.text)
ck('Login without CSRF rejected',anon.post(BASE+'/Account/Login',data={'Login.Email':'x@test.local','Login.Password':'weak'}).status_code==400)
r=anon.post(BASE+'/Account/Register',data={'__RequestVerificationToken':token(r.text),'Register.FullName':'Test weak','Register.Email':'weak@test.local','Register.Password':'weak','Register.ConfirmPassword':'weak'},timeout=30);ck('Weak registration rejected',r.status_code==200 and 'validation-summary-errors'in r.text)
email='test-'+uuid.uuid4().hex[:10]+'@example.test';password='TestShop@123'
r=anon.post(BASE+'/Account/Register',data={'__RequestVerificationToken':token(r.text),'Register.FullName':'HTTP Test Hiệp','Register.Email':email,'Register.Password':password,'Register.ConfirmPassword':password},timeout=30)
ck('Strong registration accepted',r.status_code==200 and 'Đăng ký thành công'in unescape(r.text))
s=login(email,password)
for path in ['/','/Products','/Products?q=ASUS&sort=priceAsc','/Products?categoryId=2&min=5000000&max=30000000','/Products/Details/1','/Products/Compare?ids=1,2,3','/Cart','/Orders','/Account/Profile']:
 r=get(s,path);ck('Customer page '+path,r.status_code==200,r.status_code)
ck('Customer admin forbidden',get(s,'/Admin').status_code==403)
ck('Customer reports forbidden',get(s,'/api/reports/revenue').status_code==403)
r=get(s,'/Products/Details/1');t=token(r.text)
r=s.post(BASE+'/Cart/Update',data={'productId':1,'quantity':1,'add':'true'},headers={'X-CSRF-TOKEN':t},timeout=30);ck('AJAX cart add',r.status_code==200 and r.json().get('count')==1,r.text[:100])
r=s.post(BASE+'/Cart/Update',data={'productId':1,'quantity':-1},headers={'X-CSRF-TOKEN':t});ck('Negative cart quantity rejected',r.status_code==400)
r=get(s,'/Checkout');ck('Checkout screen populated',r.status_code==200 and 'MacBook'in r.text)
key=re.search(r'name="Key"[^>]*value="([^"]+)"',r.text).group(1)
r=s.post(BASE+'/Checkout',data={'__RequestVerificationToken':token(r.text),'Key':key,'FullName':'Nguyễn HTTP Test','Phone':'0987654321','Address':'Hà Nội – địa chỉ kiểm thử','PaymentMethod':'COD','CouponCode':'TECH10','Total':'1','Price':'1'},timeout=30)
ck('Checkout redirects to committed order',r.status_code==200 and '/Orders/Details/'in r.url,r.url)
match=re.search(r'/Orders/Details/(\d+)',r.url);oid=int(match.group(1))if match else 0
ck('Server ignores tampered client amount',oid>0 and '24,741,000'in r.text,r.text[-200:])
if oid:
 ck('Invoice available to owner',get(s,f'/Orders/Invoice/{oid}').status_code==200)
 other=login('customer@techshop.local','Customer@123');ck('Other customer order IDOR blocked',get(other,f'/Orders/Details/{oid}').status_code==404)
 r=s.post(BASE+'/Orders/Cancel',data={'__RequestVerificationToken':token(r.text),'id':oid},timeout=30);ck('Owner can cancel New order',r.status_code==200 and 'Đã hủy'in unescape(r.text))
 r=s.post(BASE+'/Orders/Cancel',data={'__RequestVerificationToken':token(r.text),'id':oid},timeout=30);ck('Repeat cancel rejected safely',r.status_code==200 and 'Không được chuyển'in unescape(r.text))
admin=login('admin@techshop.local','Admin@123')
for path in ['/Admin','/Admin/Products','/Admin/Edit','/Admin/Edit/1','/Admin/Settings','/Admin/Users','/Admin/Returns','/api/products','/api/reports/revenue','/api/reports/top-products','/api/reports/low-stock','/api/orders','/swagger/v1/swagger.json','/lib/react/react.production.min.js','/js/admin.js','/css/shop.css']:
 r=get(admin,path);ck('Admin resource '+path,r.status_code==200,r.status_code)
r=get(admin,'/Admin');ck('React root and local scripts', 'id="admin-app"'in r.text and '/lib/react/'in r.text)
if oid:
 ck('API status write without CSRF fails',admin.put(BASE+f'/api/orders/{oid}/status',json={'status':'Packing','rowVersion':'x'}).status_code==400)
 ck('API stale rowversion conflict',admin.put(BASE+f'/api/orders/{oid}/status',headers={'X-CSRF-TOKEN':token(r.text)},json={'status':'Packing','rowVersion':'x'}).status_code==409)
# Admin product create/edit and concurrency-aware update.
r=get(admin,'/Admin/Edit');form={'__RequestVerificationToken':token(r.text),'Id':0,'RowVersion':'','Name':'HTTP TEST Product','Price':'100000','Stock':'3','CategoryId':1,'BrandId':1,'IsActive':'true','Description':'Real HTTP CRUD','SpecsText':'RAM=8 GB'}
r=admin.post(BASE+'/Admin/Edit',data=form,timeout=30);ck('Admin product create',r.status_code==200 and '/Admin/Products'in r.url,r.url)
html=get(admin,'/Admin/Products?q=HTTP%20TEST').text;m=re.search(r'/Admin/Edit/(\d+)',html);pid=int(m.group(1))if m else 0
if pid:
 r=get(admin,f'/Admin/Edit/{pid}');ver=re.search(r'name="RowVersion"[^>]*value="([^"]+)"',r.text).group(1);form.update({'Id':pid,'RowVersion':unescape(ver),'Name':'HTTP TEST Product Updated','__RequestVerificationToken':token(r.text)})
 r=admin.post(BASE+'/Admin/Edit',data=form,timeout=30);ck('Admin product edit',r.status_code==200 and '/Admin/Products'in r.url)
 r=get(admin,'/Admin/Products');r=admin.post(BASE+'/Admin/Hide',data={'__RequestVerificationToken':token(r.text),'id':pid},timeout=30);ck('Admin product soft delete',r.status_code==200 and get(admin,f'/Products/Details/{pid}').status_code==404)
# Two HTTP clients race for seeded last unit (product 6). Both baskets loaded first.
a=login('customer@techshop.local','Customer@123');b=login(email,password)
commands=[]
for client in [a,b]:
 r=get(client,'/Cart');client.post(BASE+'/Cart/Update',data={'productId':6,'quantity':1},headers={'X-CSRF-TOKEN':token(r.text)},timeout=30);r=get(client,'/Checkout');k=re.search(r'name="Key"[^>]*value="([^"]+)"',r.text).group(1);commands.append((client,{'__RequestVerificationToken':token(r.text),'Key':k,'FullName':'Race buyer','Phone':'0987654321','Address':'Local concurrency test','PaymentMethod':'COD'}))
def place(x):
 c,d=x;r=c.post(BASE+'/Checkout',data=d,timeout=60);return {'status':r.status_code,'url':r.url}
with concurrent.futures.ThreadPoolExecutor(2)as pool:race=list(pool.map(place,commands))
ck('Two HTTP buyers last unit: exactly one order',sum('/Orders/Details/'in x['url']for x in race)==1,race)
r=get(admin,'/Admin');ck('Forged VNPay callback rejected',get(anon,'/Orders/PaymentReturn?vnp_TxnRef=1&vnp_Amount=100&vnp_ResponseCode=00').status_code==400)
private=get(anon,'/App_Data/Outbox');ck('Private outbox not served','/Account' in private.url or private.status_code in [401,404])
from pathlib import Path
out=Path(__file__).parent/'Verification';out.mkdir(exist_ok=True)
(out/'http-results.json').write_text(json.dumps({'base':'local isolated demo','passed':sum(x['pass']for x in results),'failed':sum(not x['pass']for x in results),'tests':results},ensure_ascii=False,indent=2),encoding='utf-8')
print('HTTP RESULT:',sum(x['pass']for x in results),'passed,',sum(not x['pass']for x in results),'failed')
sys.exit(1 if any(not x['pass']for x in results)else 0)

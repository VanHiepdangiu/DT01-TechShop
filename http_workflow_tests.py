# Additional authenticated HTTP state-machine checks, then a measured 1,000-row catalog query.
import requests,re,html,json,sqlite3,os,time,statistics
from pathlib import Path
BASE='http://127.0.0.1:5089';root=Path(__file__).parent
checks=[]
def ck(n,v):checks.append({'name':n,'pass':bool(v)});print('PASS'if v else'FAIL',n)
def token(r):return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',r.text)[1])
def login(email,p):
 s=requests.Session();r=s.get(BASE+'/Account');s.post(BASE+'/Account/Login',data={'__RequestVerificationToken':token(r),'Login.Email':email,'Login.Password':p});return s
s=login('customer@techshop.local','Customer@123');r=s.get(BASE+'/Products/Details/2');s.post(BASE+'/Cart/Update',data={'productId':2,'quantity':1},headers={'X-CSRF-TOKEN':token(r)})
r=s.get(BASE+'/Checkout');key=re.search(r'name="Key"[^>]*value="([^"]+)"',r.text)[1]
r=s.post(BASE+'/Checkout',data={'__RequestVerificationToken':token(r),'Key':key,'FullName':'Test workflow','Phone':'0987654321','Address':'Test address','PaymentMethod':'COD'});oid=int(r.url.rsplit('/',1)[1]);ck('Fresh COD workflow order created','/Orders/Details/'in r.url)
a=login('admin@techshop.local','Admin@123');t=token(a.get(BASE+'/Admin'));headers={'X-CSRF-TOKEN':t}
rows=a.get(BASE+'/api/orders').json();o=next(x for x in rows if x['id']==oid);old=o['rowVersion']
for target in ['Packing','Shipping','Completed']:
 o=next(x for x in a.get(BASE+'/api/orders').json()if x['id']==oid)
 r=a.put(BASE+f'/api/orders/{oid}/status',json={'status':target,'rowVersion':o['rowVersion'],'note':'HTTP transition'},headers=headers)
 ck('Admin API legal transition '+target,r.status_code==200)
r=a.put(BASE+f'/api/orders/{oid}/status',json={'status':'Cancelled','rowVersion':old},headers=headers);ck('API rejects previously used rowversion',r.status_code==409)
rep=a.get(BASE+'/api/reports/revenue').json();ck('Completed order contributes real revenue',rep['orders']>=1 and rep['total']>=18490000 and len(rep['points'])>=1)
ck('Top products aggregates completed items',any(x['quantity']>=1 for x in a.get(BASE+'/api/reports/top-products').json()))
r=s.get(BASE+'/Orders/Details/'+str(oid));r=s.post(BASE+'/Orders/RequestReturn',data={'__RequestVerificationToken':token(r),'id':oid,'reason':'HTTP real return test'});ck('Customer requests return after completion',r.status_code==200)
o=next(x for x in a.get(BASE+'/api/orders').json()if x['id']==oid)
r=a.put(BASE+f'/api/orders/{oid}/status',json={'status':'Returned','rowVersion':o['rowVersion'],'note':'Approved return'},headers=headers);ck('Admin return restores inventory atomically',r.status_code==200)
rep=a.get(BASE+'/api/reports/revenue').json();ck('Returned order removed from completed revenue',rep['orders']==0 and rep['total']==0)
# Seed only this isolated verification SQLite DB. Never targets the delivered/SQL database.
c=sqlite3.connect(root/'TechShop/App_Data/techshop-verify2.db');before=c.execute('select count(*) from Products').fetchone()[0]
for i in range(max(0,1000-before)):
 c.execute('insert into Products(Name,Price,Stock,CategoryId,BrandId,Description,IsActive,CreatedAt,RowVersion)values(?,?,?,?,?,?,?,?,?)',(f'Benchmark laptop {i:04}',1000000+i,5,1,1,'Measurement-only fixture',1,'2026-01-01 00:00:00',os.urandom(8)))
c.commit();count=c.execute('select count(*) from Products').fetchone()[0];c.close()
s.get(BASE+'/Products?sort=priceAsc');samples=[]
for i in range(20):
 start=time.perf_counter();r=s.get(BASE+'/Products?sort=priceAsc',timeout=10);samples.append((time.perf_counter()-start)*1000);assert r.status_code==200
perf={'provider':'SQLite relational','rows':count,'requests':20,'warmup_requests':1,'endpoint':'GET /Products?sort=priceAsc','method':'authenticated requests.Session, loopback HTTP; includes HTTP+server rendering, excludes browser JS/render','milliseconds':samples,'median_ms':statistics.median(samples),'max_ms':max(samples),'mean_ms':statistics.mean(samples),'under_3_seconds_all':all(x<3000 for x in samples),'sql_server_measured':False}
(root/'Verification/catalog-performance.json').write_text(json.dumps(perf,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'Verification/http-workflow-results.json').write_text(json.dumps({'passed':sum(x['pass']for x in checks),'failed':sum(not x['pass']for x in checks),'tests':checks},ensure_ascii=False,indent=2),encoding='utf-8')
print('Workflow',sum(x['pass']for x in checks),'passed;',sum(not x['pass']for x in checks),'failed');print('Performance',count,'rows,20requests','median_ms',perf['median_ms'],'max_ms',perf['max_ms'])
raise SystemExit(1 if any(not x['pass']for x in checks)else 0)

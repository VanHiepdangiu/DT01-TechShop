# DT01 – TechShop
**Nguyễn Văn Hiệp – 2410900035 – K24CNT1**

## Nhóm thực hiện
Quy mô **6 sinh viên**; mức độ **Trung bình**; lớp **K24CNT1**. Số thứ tự nhóm chưa được cung cấp (không đồng nhất quy mô 6 người với số nhóm 05).

| STT | Họ và tên | MSSV | Vai trò |
|---|---|---|---|
| 1 | Trần Phan Đức Anh | 2410900006 | Trưởng nhóm |
| 2 | Kim Việt Quang | 2410900062 | Thành viên |
| 3 | Nguyễn Văn Hiệp | 2410900035 | Thành viên |
| 4 | Lê Huy Hoàng | 2410900037 | Thành viên |
| 5 | Nguyễn Văn Đạo | 2410900019 | Thành viên |
| 6 | Lương Mạnh Quân | 2410900063 | Thành viên |

Phân công chuyên môn trong tài liệu là đề xuất để nhóm thống nhất; không phải xác nhận đóng góp đã hoàn thành.


Website học tập ASP.NET Core MVC (.NET 8), ASP.NET Core Identity, EF Core Code First/Migrations, SQL Server; dashboard React và Web API. Có chế độ SQLite để xem nhanh khi chưa có SQL Server, không phải EF InMemory.

## 1. Mở nhanh trên Windows
1. Giải nén toàn bộ ZIP, không chạy ngay bên trong tệp ZIP. Nên đặt tại `D:\BaiTap\TechShop`.
2. Máy cần **.NET 8 SDK** hoặc Visual Studio 2022 có workload *ASP.NET and web development*. Lần đầu cần mạng để NuGet tải các thư viện.
3. Nếu có SQL Server LocalDB: bấm **CHAY_SQLSERVER.cmd**. Chương trình tự áp dụng Migrations và tạo dữ liệu mẫu, không phải mở file EXE trong bin.
4. Chưa có SQL Server: bấm **CHAY_DEMO_KHONG_CAN_SQLSERVER.cmd**. Đây là chế độ SQLite lưu dữ liệu thật, chỉ để xem/test nhanh.
5. Giữ cửa sổ lệnh mở, đợi dòng `Now listening on`, truy cập **http://localhost:5088**. Nếu trang mở sớm, tải lại sau khi server đã sẵn sàng.
6. Có thể mở `TechShop.sln` trong Visual Studio, chọn **TechShop** làm Startup Project. Không chọn “Current Document”. Mặc định chạy SQL Server.

## 2. Tài khoản minh họa (chỉ dùng học tập)
| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | admin@techshop.local | Admin@123 |
| Staff | staff@techshop.local | Staff@123 |
| Customer | customer@techshop.local | Customer@123 |

Đây là dữ liệu seed công khai, không phải tài khoản thật. Không triển khai nguyên trạng ra Internet; thay tài khoản seed/mật khẩu, dùng HTTPS và thiết lập secrets trước khi triển khai. Đăng ký mới chỉ nhận vai trò Customer, không nhận role do client gửi. Mật khẩu ≥8 ký tự, đủ hoa/thường/số/ký tự đặc biệt; khóa tạm sau 5 lần đăng nhập sai.

## 2b. Thêm sản phẩm mới (dành cho Admin)
1. Đăng nhập `admin@techshop.local / Admin@123`.
2. Vào **Quản trị → Sản phẩm → “+ Thêm sản phẩm”** (hoặc menu **“+ Thêm sản phẩm”** ở Trung tâm điều hành).
3. Điền biểu mẫu gồm 4 nhóm:
   - **Thông tin bắt buộc**: tên (5–150 ký tự), danh mục, thương hiệu, giá bán > 0, giá khuyến mãi (nếu có, phải nhỏ hơn giá bán), tồn kho ≥ 0.
   - **Nội dung mô tả**: mô tả (≤4000 ký tự) và thông số kỹ thuật (mỗi dòng `Tên=Giá trị`, tối đa 20 dòng).
   - **Hình ảnh**: tải lên tối đa 5 ảnh/lần (JPG/PNG/WebP, mỗi ảnh dưới 2 MB); ảnh đầu tiên là ảnh chính.
   - **Trạng thái**: “Đang bán” để hiển thị ngay trên cửa hàng.
4. Bấm **Thêm sản phẩm** → hệ thống lưu và quay về danh sách, sản phẩm xuất hiện ngay trên trang khách hàng.
5. Muốn thêm hàng loạt: dùng **Nhập CSV** ở trang Quản lý sản phẩm (tối đa 500 dòng/lần).

### Trang Quản lý sản phẩm (/Admin/ProductManager)
Trang quản trị dạng thẻ: 5 ô thống kê (tổng, đang bán, đang ẩn, sắp hết, hết hàng), lọc theo
tên / danh mục / thương hiệu / trạng thái, mỗi thẻ có ảnh, giá và nút Sửa – Xem – Ẩn.
Nút **"+ Thêm mới sản phẩm"** mở trang thêm riêng `/Admin/Create` (không lẫn với form sửa);
lưu xong quay lại trang này kèm thông báo và nút *Thêm sản phẩm khác*.

### Sửa ảnh và thông số của sản phẩm đã có
1. Vào **Quản lý sản phẩm → Sửa** ở dòng sản phẩm cần chỉnh.
2. **Ảnh**: mỗi ảnh hiện có kèm 2 lựa chọn — *Ảnh chính* (chọn 1 ảnh) và *Xoá* (tích để bỏ khi lưu).
   Tải thêm ảnh mới ở ô “Thêm ảnh mới” (tối đa 5 ảnh/lần).
3. **Thông số**: mỗi dòng là một cặp Tên – Giá trị; dùng **+ Thêm dòng thông số** để thêm, nút **✕** để xoá dòng.
   Nếu cần dán nhanh, mở mục “Dán nhanh nhiều thông số” và nhập mỗi dòng `Tên=Giá trị`.
4. Bấm **Lưu thay đổi**. Ảnh bị xoá sẽ được gỡ khỏi CSDL và xoá tệp trong `wwwroot/uploads`;
   nếu xoá hết ảnh, hệ thống dùng ảnh mặc định.

Lưu ý kiểm soát: giá khuyến mãi ≥ giá bán, thiếu tên, tệp không phải ảnh đều bị máy chủ từ chối;
nếu sản phẩm vừa bị người khác sửa, hệ thống không ghi đè mà yêu cầu mở lại biểu mẫu (RowVersion).

## 3. Các chức năng
- Đăng nhập/đăng ký chung màn hình, chuyển tab; Identity hash mật khẩu, cookie, quên/đặt lại mật khẩu; hồ sơ và sổ địa chỉ.
- Catalog: tìm tên, lọc danh mục/hãng/khoảng giá, sắp xếp, phân trang; chi tiết nhiều ảnh và thông số; đánh giá; so sánh tối đa 3 sản phẩm.
- Giỏ hàng AJAX, kiểm tra số lượng; coupon `TECH10`; checkout COD hoặc VNPay sandbox khi có cấu hình.
- Đơn hàng của tôi, lịch sử trạng thái, in phiếu/lưu PDF từ hộp thoại in; hủy khi chưa xử lý, yêu cầu đổi trả sau hoàn tất.
- Admin: **thêm sản phẩm mới** (biểu mẫu 4 nhóm trường: thông tin bắt buộc, mô tả + thông số, ảnh, trạng thái), sửa/ẩn sản phẩm; **sửa ảnh (xoá ảnh cũ, đặt ảnh chính, thêm ảnh mới) và sửa thông số theo từng dòng** khi biên tập; upload ảnh có kiểm tra định dạng/dung lượng, danh mục, hãng, coupon; người dùng/phân quyền; duyệt trả hàng.
- Admin: **nhập sản phẩm hàng loạt từ CSV** (báo lỗi theo từng dòng, chỉ lưu khi toàn bộ hợp lệ, tối đa 500 dòng) và **xuất Excel .xlsx** cho danh mục sản phẩm và doanh thu theo khoảng ngày.
- Staff: xử lý trạng thái đơn, đóng gói/giao/hoàn tất, in phiếu; không quản lý người dùng hoặc báo cáo doanh thu.
- Dashboard React: doanh thu theo khoảng ngày, biểu đồ, top hàng bán, tồn kho thấp, cập nhật đơn bằng API.
- Swagger: `/swagger`, chỉ Admin. Các API xác thực/phân quyền ở server; thao tác ghi có CSRF.
- Ảnh SVG trong seed là **hình minh họa tự dựng**, không phải ảnh sản phẩm do hãng cung cấp. Không có giao dịch/thanh toán thật.

### Chế độ truy cập catalog
Theo yêu cầu đăng nhập trước khi vào hệ thống, mặc định `Access:RequireLoginForCatalog=true`. Mẫu thầy yêu cầu Guest duyệt catalog: đặt giá trị này **false** trong `TechShop/appsettings.json` để bật chế độ đó. Khi false, chỉ trang chủ/catalog/chi tiết/so sánh và GET `/api/products` công khai; giỏ hàng, đơn và quản trị vẫn yêu cầu đăng nhập. Các tài liệu giữ tiêu chí Guest của mẫu và nêu quyết định này cần xác nhận khi nghiệm thu.

## 4. SQL Server
Mặc định: `(localdb)\MSSQLLocalDB`, CSDL `TechShopDT01`, Windows Authentication. Với SQL Express, sửa `ConnectionStrings:SqlServer`, ví dụ:
```text
Server=.\SQLEXPRESS;Database=TechShopDT01;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False
```
Không dùng SQL password thật trong mã nguồn. Có thể đặt biến môi trường `ConnectionStrings__SqlServer` hoặc user-secrets.

- Khởi động chế độ SQL Server sẽ gọi `Database.MigrateAsync()` để tạo schema theo `TechShop/Migrations`, rồi seed.
- **Không chạy `SQL/CreateDatabase.sql` rồi lại chạy Migrations trên cùng CSDL.** Script SQL là bản schema tham khảo cho CSDL rỗng; script đó không thêm lịch sử EF Migrations.
- Nếu đã tạo CSDL bằng schema script, dùng một CSDL tên mới/rỗng cho chế độ Migrations, không tự xóa dữ liệu quan trọng.
- `SQL/BackupRestore.sql` là hướng dẫn backup/restore thủ công, cần chỉnh vị trí lưu và quyền. Chưa được chạy trên SQL Server trong môi trường kiểm tra.
- Khi thay đổi model, dùng EF Core CLI phiên bản 8 tương ứng để tạo migration mới. Không tự sửa migration đã áp dụng ở máy khác.

## 5. Cách chống bán trùng sản phẩm cuối
1. Client chỉ gửi ID sản phẩm, số lượng, thông tin giao hàng, mã giảm giá, phương thức và idempotency key. **Không nhận giá/tổng tiền/phí giao từ client**.
2. Server đọc giá hiện hành, sản phẩm còn bán, tồn kho, hạn/lượt coupon và tự tính tiền. Miễn phí giao với tiền hàng từ 10 triệu, còn lại 30.000đ; coupon giảm trên tiền hàng.
3. Một transaction bao trùm trừ kho, tiêu thụ coupon, tạo Order/OrderItem và lịch sử. Có lỗi thì rollback.
4. SQL Server sinh `rowversion` thật cho Product/Coupon/Order. EF UPDATE có điều kiện version gốc. Giao dịch thua trả thông báo xung đột, không retry mù/quay vòng tự đặt đơn khi tồn kho đã thay đổi.
5. Client cần tải lại giỏ để đặt lại. Idempotency key có UNIQUE constraint để chống tạo trùng khi retry; chỉ trả lại đơn cho đúng chủ tài khoản. API cập nhật trạng thái nhận version, xung đột trả 409.
6. Hủy/trả hàng kiểm tra trạng thái và cờ `StockRestored` trong transaction, không hoàn kho/coupon hai lần. Đơn đã thanh toán không tự hủy; hoàn tiền qua cổng là nghiệp vụ ngoài phạm vi bản demo.

**SQLite không có rowversion SQL Server**: fallback dùng token byte do ứng dụng sinh và xử lý write-contention SQLite. Test SQLite chứng minh logic relational/transaction nhưng không được coi là đã kiểm chứng SQL Server.

## 6. VNPay sandbox và email
Mặc định **đều tắt** vì chưa có tài khoản dịch vụ; website COD hoạt động độc lập.

### VNPay
- Dùng merchant sandbox hợp lệ; đặt `VnPay:Enabled=true`, TmnCode, HashSecret, ReturnUrl đúng host đang chạy.
- Dùng user-secrets/biến môi trường `VnPay__HashSecret`, không commit khóa thật.
- URL mặc định là sandbox. Return URL cần khớp cấu hình merchant.
- Callback xác thực HMAC-SHA512, số tiền phải bằng tổng đơn ×100, kiểm tra trạng thái và version để chống xử lý lặp.
- Đã unit test ký/xác thực/tampering; **chưa gửi thanh toán sandbox thật**. Bản này nhận kết quả qua ReturnUrl đã ký; chưa có endpoint IPN riêng, không dùng cho thanh toán production. Đơn pending không tự hết hạn/giải phóng kho; có thể hủy đơn chưa thanh toán để hoàn kho.

### SMTP
- Đặt `Smtp:Enabled=true`, Host/Port/User/From; password/app-password dùng secrets/biến `Smtp__Password`.
- Khi chưa cấu hình: thư xác nhận/reset được lưu **Outbox cục bộ trong App_Data**, không phải đã gửi tới email. Thư không phục vụ qua web.
- Chưa kiểm thử gửi thư ra nhà cung cấp. Không công khai thư reset, token hoặc thư mục Outbox.

## 7. React / Vite
- Trang MVC dùng React18 UMD lưu cục bộ, hoạt động không cần Node/npm khi chạy web. Không tải React từ CDN lúc sử dụng.
- Bản nguồn Vite ở `TechShop/ClientApp`; xem README tại đó. **Chưa chạy thành công Vite build**, và chưa kiểm thử bằng trình duyệt tự động. Bản web được kiểm thử bằng HTTP; JavaScript đã kiểm tra cú pháp.
- Vite là bản nguồn tùy chọn, không cần chạy để mở website hiện tại. Khi build/tích hợp bundle phải giữ cookie cùng origin và CSRF meta do MVC cấp. Không dùng token tĩnh từ file index.html.

## 8. Chạy kiểm thử
```text
dotnet build TechShop.sln
dotnet test TechShop.UnitTests/TechShop.UnitTests.csproj
dotnet run --project TechShop.Tests/TechShop.Tests.csproj
```
Kết quả đã thực thi (cập nhật đợt tuần 3–4): **61 xUnit qua** (55 test cũ + 6 test mới cho nhập CSV/xuất Excel), **22 integration relational qua**, **57 kiểm tra HTTP qua**, **16 kiểm tra HTTP cho nhập CSV/xuất Excel qua**, **17 kiểm tra HTTP cho chức năng thêm sản phẩm qua**, **20 kiểm tra HTTP cho sửa/xoá ảnh và thông số qua**, **10 kiểm tra luồng đơn/doanh thu/đổi trả qua**. Build cuối: **0 lỗi**. Xem `Verification/KET_QUA_KIEM_THU.md`, `http_bulk_tests.py` và JSON chi tiết.

Có bài thử SQL Server thật trong console suite:
```text
dotnet run --project TechShop.Tests/TechShop.Tests.csproj -- --sql "Server=(localdb)\MSSQLLocalDB;Database=TechShopTests_Concurrency;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False"
```
Chỉ nhận tên DB bắt đầu `TechShopTests_`. Tạo test data, không dùng dữ liệu cửa hàng/không xóa DB; dùng DB test mới để tránh dữ liệu cũ. **Chưa chạy lệnh SQL này tại môi trường kiểm tra** vì không có SQL Server.

Các Python HTTP test dành cho CSDL demo kiểm thử riêng, không chạy trên CSDL đang dùng thật; cần Python requests. `http_workflow_tests.py` chứa dữ liệu benchmark và đường dẫn tương đối đến DB verification, chỉ chạy theo hướng dẫn kiểm thử.

## 9. Tài liệu Word
Trong `Documents/`:
1. `Nhom1_DeCuong.docx` – Đề cương chi tiết đề tài.
2. `Nhom1_KeHoach-8Tuan.docx` – Kế hoạch thực hiện 8 tuần.
3. `Nhom1_SRS.docx` – Đặc tả yêu cầu phần mềm (SRS).
4. `Nhom1_TaiLieuKyThuat_Tuan5.docx` – Tài liệu kỹ thuật đợt tuần 3–4.
5. `Nhom1_BaoCaoTuan5.docx` – Báo cáo tiến độ tuần 3–4.

Đã dựng từ ba PDF mẫu; TNR13, A4, bảng và thông tin sinh viên. Đã cập nhật đủ 5 thành viên, trưởng nhóm Trần Phan Đức Anh và mức độ Trung bình. Chỉ số thứ tự nhóm và ngày thực hiện chưa được cung cấp nên vẫn chờ xác nhận. Kế hoạch là dự kiến, không ghi giả tiến độ. Đã kiểm cấu trúc DOCX/CRC và nội dung; **chưa render Word để xác nhận phân trang**.

## 10. Phạm vi chưa hoàn tất kiểm chứng
- SQL Server runtime/rowversion và backup/restore thật.
- Giao dịch VNPay sandbox thật, SMTP ra nhà cung cấp.
- Vite build, kiểm thử giao diện trên trình duyệt/điện thoại thật, kiểm tra phân trang Word.
- Nhập CSV (FR-1.4) và xuất Excel (FR-5.4) **đã làm và đã kiểm thử** (`Services/BulkTools.cs`, `AdminController.Import/ExportProducts/ExportRevenue`, 6 unit test + 16 kiểm tra HTTP). Phần chưa kiểm chứng còn lại: mở tệp .xlsx bằng Microsoft Excel thật trên máy Windows, vì môi trường kiểm tra chỉ xác thực cấu trúc ZIP/OpenXML. Không phải cửa hàng production, không lưu thông tin thẻ/PAN/CVV.

Mã nguồn và CSDL mẫu chỉ phục vụ học tập. Trước khi đưa ra Internet cần rà soát bảo mật, cấu hình HTTPS, thay seed credentials, đặt host đúng và hoàn thiện cơ chế thanh toán/IPN/hoàn tiền/đơn hết hạn.

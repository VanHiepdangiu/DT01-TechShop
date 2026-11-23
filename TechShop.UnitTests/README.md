# Kiểm thử xUnit TechShop

Chạy tại thư mục chứa TechShop.sln (cần .NET 8 SDK):

```text
dotnet test TechShop.UnitTests/TechShop.UnitTests.csproj
```

Bộ kiểm thử tham chiếu trực tiếp mã nguồn web, không chép lại logic. Lần kiểm tra đã thực thi: **55 passed, 0 failed**. Các nhóm bao phủ mật khẩu, luồng trạng thái đơn, HMAC/chữ ký VNPay, chống sửa số tiền, coupon, quyền sở hữu đơn, idempotency, hoàn kho đúng một lần và rollback.

Các kiểm thử DB dùng SQLite relational với connection riêng từng test. Đây là CSDL có transaction và ràng buộc thật, không phải provider EF InMemory. Tuy nhiên, **không thay thế kiểm thử trên SQL Server**. Không có kết nối tới VNPay hay SMTP ngoài; khóa `test-key`/merchant `TESTONLY` trong test chỉ là dữ liệu công khai dùng kiểm thử thuật toán.

Kết quả tổng hợp của các bộ test được đặt tại `Verification/`. Các test xUnit không đọc chuỗi kết nối ứng dụng, không nhận `--sql` và không xóa CSDL người dùng.

Máy ít tài nguyên có thể build/test tuần tự:

```text
dotnet build TechShop.sln -m:1 -p:UseSharedCompilation=false
dotnet test TechShop.UnitTests/TechShop.UnitTests.csproj --no-build
```

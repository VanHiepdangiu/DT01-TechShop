# Kết quả kiểm thử bản bàn giao TechShop

## Kết quả đã thực thi
- Build solution cuối: **0 warnings, 0 errors**.
- xUnit: **55 passed, 0 failed, 0 skipped**.
- Console integration: **22 passed, 0 failed**, provider SQLite relational.
- HTTP: **57 passed, 0 failed**; chi tiết `http-results.json`.
- Luồng đơn: **10 passed, 0 failed**; chi tiết `http-workflow-results.json`.
- JavaScript: kiểm tra cú pháp Node cho shop.js, admin.js và ClientApp/src/main.js đã qua. Không tương đương kiểm thử hành vi trong trình duyệt.
- Migrations và SQL script được sinh từ EF Core SQL Server model, đã compile; chưa áp dụng lên instance SQL Server thật.

## Những tình huống đã được kiểm tra
Giá lấy từ DB bất chấp client gửi giá 1; hết kho không tạo đơn; lỗi sau khi save stock rollback toàn bộ; stale token không ghi đè; hai yêu cầu mua sản phẩm cuối chỉ một đơn thành công; idempotency và quyền chủ đơn; coupon đúng/sai/hết hạn/hết lượt; hủy hoàn kho/coupon đúng một lần; chủ tài khoản khác bị chặn xem đơn; CSRF/ràng buộc role ở server; CRUD sản phẩm; New→Packing→Shipping→Completed; số liệu doanh thu/top hàng phản ánh đơn Completed; yêu cầu trả hàng và Returned loại khỏi doanh thu; kiểm tra HMAC/callback VNPay bị sửa hoặc giả chữ ký.

## Đo hiệu năng
Thực hiện 1 lần warm-up và 20 yêu cầu HTTP có đăng nhập với catalog đủ **1.000 sản phẩm**, sắp giá tăng, phân trang. Dùng SQLite, kết nối loopback, đo thời gian HTTP + render Razor, **không gồm render/JavaScript trình duyệt**. Số đo thô, median/max nằm tại `catalog-performance.json`. Không suy diễn kết quả này thành hiệu năng SQL Server hoặc khi triển khai Internet.

## Chưa thực thi / không tuyên bố đã đạt
- Chạy app và test concurrency trên SQL Server thật; backup/restore SQL Server thật.
- Thanh toán VNPay sandbox có merchant thật, SMTP tới email thật.
- Vite production build, kiểm thử trình duyệt/điện thoại, render/kiểm tra phân trang Word.

Có code và hướng dẫn cấu hình/tái kiểm tra; đây không phải bằng chứng các kết nối ngoài đã chạy. Hình sản phẩm là SVG minh họa. Không có ảnh chụp màn hình trình duyệt giả hoặc phản hồi API giả.

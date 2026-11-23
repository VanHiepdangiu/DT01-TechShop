-- TechShopDT01 — Nguyễn Văn Hiệp — 2410900035
-- THAM KHẢO, CHƯA THỰC THI: sửa thư mục lưu/đọc và xác nhận quyền của SQL Server.
-- Tạo thư mục C:\TechShopBackups trước khi chạy. Không chạy RESTORE trên DB đang dùng.
-- 1. SAO LƯU (chạy riêng lệnh sau khi chỉnh đường dẫn):
-- BACKUP DATABASE [TechShopDT01] TO DISK=N'C:\TechShopBackups\TechShopDT01.bak' WITH CHECKSUM, INIT;
-- 2. KIỂM TRA BẢN BACKUP:
-- RESTORE VERIFYONLY FROM DISK=N'C:\TechShopBackups\TechShopDT01.bak' WITH CHECKSUM;
-- 3. XEM LOGICAL FILE NAMES rồi RESTORE vào CSDL thử MỚI, không ghi đè dữ liệu gốc:
-- RESTORE FILELISTONLY FROM DISK=N'C:\TechShopBackups\TechShopDT01.bak';
-- Dùng SSMS Restore Database, chọn database đích mới, map file data/log sang tên/path mới.
-- Sau restore kiểm tra số Product/Order/OrderItem, FK, tài khoản và thử chạy app trên DB sao chép.
-- Không dùng WITH REPLACE/SINGLE_USER nếu chưa có bản backup và xác nhận riêng.

-- Kiểm tra kiểu rowversion sau khi Migrations đã chạy:
SELECT OBJECT_NAME(c.object_id) AS TableName,c.name AS ColumnName,t.name AS DataType
FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id
WHERE OBJECT_NAME(c.object_id) IN ('Products','Coupons','Orders') AND c.name='RowVersion';

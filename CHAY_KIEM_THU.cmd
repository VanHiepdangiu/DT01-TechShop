@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo TechShop - Nguyễn Văn Hiệp - 2410900035 - Kiểm thử SQLite riêng, không dùng DB cửa hàng.
dotnet build TechShop.sln -m:1 -p:UseSharedCompilation=false
if errorlevel 1 goto error
dotnet test TechShop.UnitTests\TechShop.UnitTests.csproj --no-build
if errorlevel 1 goto error
dotnet run --project TechShop.Tests\TechShop.Tests.csproj --no-build
if errorlevel 1 goto error
echo Đã chạy xong. SQL Server, VNPay live và SMTP cần kiểm tra riêng theo README.
pause
exit /b 0
:error
echo Kiểm thử thất bại. Giữ lại nội dung lỗi và gửi ảnh để được hỗ trợ.
pause
exit /b 1

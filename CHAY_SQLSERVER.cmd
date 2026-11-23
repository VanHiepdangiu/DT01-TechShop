@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Chưa có .NET SDK. Cài .NET 8 SDK từ https://dotnet.microsoft.com/download/dotnet/8.0 rồi mở lại.
 pause
 exit /b 1
)
set ASPNETCORE_ENVIRONMENT=Development
set Database__Provider=SqlServer
if not exist "TechShop\App_Data" mkdir "TechShop\App_Data"
echo TechShop - Nguyễn Văn Hiệp - 2410900035
echo Chế độ SQL Server: mặc định LocalDB của Visual Studio 2022.
echo Nếu dùng SQL Server Express: sửa ConnectionStrings:SqlServer trong TechShop\appsettings.json.
echo Migrations sẽ tự tạo CSDL ở lần đầu, không cần chạy CreateDatabase.sql riêng.
echo Giữ cửa sổ này mở. Khi xuất hiện Now listening, truy cập http://localhost:5088
start "TechShop Browser" powershell -NoProfile -Command "$u='http://localhost:5088/Account';for($i=0;$i -lt 120;$i++){try{$r=Invoke-WebRequest $u -UseBasicParsing -TimeoutSec 2;if($r.StatusCode -eq 200){Start-Process 'http://localhost:5088';exit}}catch{};Start-Sleep -Seconds 1}"
dotnet run --project "TechShop\TechShop.csproj" --no-launch-profile --urls http://localhost:5088
echo Nếu lỗi kết nối: kiểm tra SQL Server đang chạy hoặc thử CHAY_DEMO_KHONG_CAN_SQLSERVER.cmd.
pause

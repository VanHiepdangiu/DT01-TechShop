@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Cần cài .NET 8 SDK từ https://dotnet.microsoft.com/download/dotnet/8.0.
 pause
 exit /b 1
)
set ASPNETCORE_ENVIRONMENT=Development
set Database__Provider=Sqlite
if not exist "TechShop\App_Data" mkdir "TechShop\App_Data"
echo TechShop - Nguyễn Văn Hiệp - 2410900035
echo Chế độ DEMO SQLite lưu dữ liệu thật trên máy. Không phải InMemory.
echo Chế độ này không chứng minh rowversion của SQL Server. Dùng CHAY_SQLSERVER.cmd khi nộp/demo đúng công nghệ.
echo Giữ cửa sổ này mở; mở http://localhost:5088 sau khi có Now listening.
start "TechShop Browser" powershell -NoProfile -Command "$u='http://localhost:5088/Account';for($i=0;$i -lt 120;$i++){try{$r=Invoke-WebRequest $u -UseBasicParsing -TimeoutSec 2;if($r.StatusCode -eq 200){Start-Process 'http://localhost:5088';exit}}catch{};Start-Sleep -Seconds 1}"
dotnet run --project "TechShop\TechShop.csproj" --no-launch-profile --urls http://localhost:5088
pause

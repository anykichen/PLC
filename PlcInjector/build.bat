@echo off
chcp 65001 >nul
echo ============================================================
echo   PLC Injector Pro v1.3  —  Build Script
echo ============================================================
echo.

:: Check .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 .NET SDK
    echo 请从 https://dotnet.microsoft.com/download 下载安装 .NET 8 SDK
    pause & exit /b 1
)

echo [1/3] 还原 NuGet 包...
dotnet restore PlcInjector.csproj
if errorlevel 1 ( echo 还原失败 & pause & exit /b 1 )

echo.
echo [2/3] 安装 Playwright 浏览器（首次需要）...
dotnet build PlcInjector.csproj -c Release -o build\ >nul 2>&1
build\playwright.ps1 install chromium >nul 2>&1
echo     浏览器已就绪（或已跳过）

echo.
echo [3/3] 编译发布版本...
dotnet publish PlcInjector.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o dist\
if errorlevel 1 ( echo 编译失败 & pause & exit /b 1 )

echo.
echo ============================================================
echo   编译成功！输出目录: dist\PlcInjector.exe
echo ============================================================
echo.
start "" "dist\"
pause

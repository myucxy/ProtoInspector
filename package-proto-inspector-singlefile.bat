@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%ProtoInspector\ProtoInspector.csproj"
set "PACKAGE_DIR=%ROOT%dist\ProtoInspectorPackage"
set "WORKSPACE_SRC=%ROOT%ProtocolWorkspace"
set "WORKSPACE_DST=%PACKAGE_DIR%\ProtocolWorkspace"
set "PROTOC_SRC=%ROOT%protoc.exe"
set "USAGE_SRC=%ROOT%ProtoInspectorPackageUsage.txt"

if not exist "%PROJECT%" (
  echo [ERROR] ProtoInspector project not found: %PROJECT%
  exit /b 1
)

if not exist "%PROTOC_SRC%" (
  echo [ERROR] protoc.exe not found: %PROTOC_SRC%
  exit /b 1
)

if not exist "%USAGE_SRC%" (
  echo [ERROR] Usage guide not found: %USAGE_SRC%
  exit /b 1
)

if exist "%PACKAGE_DIR%" (
  echo [0/4] Cleaning old package...
  rmdir /S /Q "%PACKAGE_DIR%"
)

echo [1/4] Publishing single-file executable...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%PACKAGE_DIR%"
if errorlevel 1 exit /b 1

echo [2/4] Preparing workspace directories...
if not exist "%WORKSPACE_DST%\proto" mkdir "%WORKSPACE_DST%\proto"
if not exist "%WORKSPACE_DST%\generated" mkdir "%WORKSPACE_DST%\generated"
if not exist "%WORKSPACE_DST%\bytes" mkdir "%WORKSPACE_DST%\bytes"

echo [3/4] Copying protocol workspace...
xcopy "%WORKSPACE_SRC%\proto\*" "%WORKSPACE_DST%\proto\" /E /I /Y >nul
if errorlevel 1 exit /b 1
xcopy "%WORKSPACE_SRC%\generated\*" "%WORKSPACE_DST%\generated\" /E /I /Y >nul
if errorlevel 1 exit /b 1
xcopy "%WORKSPACE_SRC%\bytes\*" "%WORKSPACE_DST%\bytes\" /E /I /Y >nul
if errorlevel 1 exit /b 1
copy /Y "%WORKSPACE_SRC%\README.txt" "%WORKSPACE_DST%\README.txt" >nul
copy /Y "%PROTOC_SRC%" "%PACKAGE_DIR%\protoc.exe" >nul

echo [4/4] Copying usage guide...
copy /Y "%USAGE_SRC%" "%PACKAGE_DIR%\使用说明.txt" >nul
if errorlevel 1 exit /b 1

echo.
echo Package completed:
echo %PACKAGE_DIR%
exit /b 0

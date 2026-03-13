@echo off
echo --- FoxyClient Standalone Builder ---

echo [1/3] Preparing Payload...
set "BUILD_DIR=temp_setup"
if exist "%BUILD_DIR%" rd /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"

copy "FoxyClient.exe" "%BUILD_DIR%\"
copy "FoxyClient.php" "%BUILD_DIR%\"
copy "FoxyClient.sign" "%BUILD_DIR%\"
xcopy "FoxyClient" "%BUILD_DIR%\FoxyClient\" /E /I /Y
xcopy "temurin-jre" "%BUILD_DIR%\temurin-jre\" /E /I /Y

echo [2/3] Zipping Assets...
powershell -Command "Expand-Archive -Path '%BUILD_DIR%' -DestinationPath '.' -Force" 2>nul
:: Wait, Expand-Archive is for unzipping. Compress-Archive is for zipping.
powershell -Command "Compress-Archive -Path '%BUILD_DIR%\*' -DestinationPath 'Setup.zip' -Force"

echo [3/3] Compiling Standalone Setup...
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /out:Setup.exe /platform:x86 /target:winexe /win32icon:app.ico /r:Microsoft.CSharp.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /res:Setup.zip Installer.cs

echo Cleaning up...
rd /s /q "%BUILD_DIR%"
del Setup.zip

echo Done! Setup.exe is now a standalone installer.
pause

@echo off
del FoxyClient.exe
echo --- FoxyClient Standalone Builder ---

echo [1/3] Preparing Payload...
if not exist "root-ca.crt" (
    echo ERROR: root-ca.crt was not found.
    exit /b 1
)
set "BUILD_DIR=temp_setup"
set "BUILD_DIR_OTA=temp_setup-ota"
if exist "%BUILD_DIR%" rd /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"
if exist "%BUILD_DIR_OTA%" rd /s /q "%BUILD_DIR_OTA%"
mkdir "%BUILD_DIR_OTA%"

copy "FoxyClient\FoxyClient.exe" "%BUILD_DIR%\"
copy "FoxyClient\FoxyClient.php" "%BUILD_DIR%\"
copy "FoxyClient\FoxyClient.sign" "%BUILD_DIR%\"
copy "root-ca.crt" "%BUILD_DIR%\"
if errorlevel 1 goto build_failed
xcopy "FoxyClient\FoxyClient" "%BUILD_DIR%\FoxyClient\" /E /I /Y
if errorlevel 1 goto build_failed
copy "FoxyClient\FoxyClient.php" "%BUILD_DIR_OTA%\"
copy "FoxyClient\FoxyClient.sign" "%BUILD_DIR_OTA%\"
copy "root-ca.crt" "%BUILD_DIR_OTA%\"
if errorlevel 1 goto build_failed

echo [2/3] Zipping Assets...
powershell -Command "Compress-Archive -Path '%BUILD_DIR%\*' -DestinationPath 'Setup.zip' -Force"
powershell -Command "Compress-Archive -Path '%BUILD_DIR_OTA%\*' -DestinationPath 'FoxyClient_OTA.zip' -Force"
if not exist "Setup.zip" goto build_failed
if not exist "FoxyClient_OTA.zip" goto build_failed

echo [3/3] Compiling Standalone Setup...
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /out:Setup.exe /platform:x86 /target:winexe /win32manifest:Installer.exe.manifest /win32icon:app.ico /r:Microsoft.CSharp.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /res:Setup.zip Installer.cs
if errorlevel 1 goto build_failed

echo Cleaning up...
rd /s /q "%BUILD_DIR%"
rd /s /q "%BUILD_DIR_OTA%"
copy "Setup.zip" "FoxyClient.zip"
if errorlevel 1 goto build_failed
del Setup.zip

move Setup.exe FoxyClient.exe
if errorlevel 1 goto build_failed

echo Done! FoxyClient.exe is now a standalone installer.
pause
exit /b 0

:build_failed
echo ERROR: Build failed.
if exist "%BUILD_DIR%" rd /s /q "%BUILD_DIR%"
if exist "%BUILD_DIR_OTA%" rd /s /q "%BUILD_DIR_OTA%"
exit /b 1

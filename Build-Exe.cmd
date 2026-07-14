@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe not found.
  exit /b 1
)
if not exist "%~dp0dist" mkdir "%~dp0dist"
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%~dp0dist\ResourceHackerAITranslator.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "%~dp0TranslatorBridge.cs"
if errorlevel 1 exit /b %errorlevel%
copy /y "%~dp0translator.config.json" "%~dp0dist\translator.config.json" >nul
echo Built "%~dp0dist\ResourceHackerAITranslator.exe"

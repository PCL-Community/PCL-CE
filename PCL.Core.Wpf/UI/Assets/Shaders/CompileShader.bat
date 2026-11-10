@echo off
setlocal EnableDelayedExpansion

set SHADER_FILE=AdaptiveBlur.hlsl
set OUTPUT_FILE=AdaptiveBlur.ps
set FXC_PATH=

echo Searching for FXC compiler...

rem Search common Windows SDK installation paths
for /d %%i in ("C:\Program Files (x86)\Windows Kits\10\bin\*") do (
    if exist "%%i\x64\fxc.exe" (
        set "FXC_PATH=%%i\x64\fxc.exe"
        echo Found FXC compiler: %%i\x64\fxc.exe
        goto found
    )
)

for /d %%i in ("C:\Program Files\Windows Kits\10\bin\*") do (
    if exist "%%i\x64\fxc.exe" (
        set "FXC_PATH=%%i\x64\fxc.exe"
        echo Found FXC compiler: %%i\x64\fxc.exe
        goto found
    )
)

rem Try to find from PATH environment variable
where fxc.exe >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    set "FXC_PATH=fxc.exe"
    echo Found FXC compiler in PATH environment variable
    goto found
)

echo ERROR: FXC compiler not found
echo Please make sure Windows SDK is installed
echo Recommended version: Windows 10 SDK
echo Download: https://developer.microsoft.com/windows/downloads/windows-sdk/
goto error

:found
echo Compiling adaptive sampling blur shader...
echo Source file: %SHADER_FILE%
echo Output file: %OUTPUT_FILE%
echo Target platform: Pixel Shader 3.0

"!FXC_PATH!" /T ps_3_0 /E PixelShaderFunction /Fo %OUTPUT_FILE% %SHADER_FILE%

if !ERRORLEVEL! EQU 0 (
    echo.
    echo [SUCCESS] Shader compiled successfully: %OUTPUT_FILE%
    if exist %OUTPUT_FILE% (
        for %%F in (%OUTPUT_FILE%) do echo File size: %%~zF bytes
    )
    echo.
    echo Compilation complete! Ready for use in WPF.
) else (
    echo.
    echo [ERROR] Compilation failed, error code: !ERRORLEVEL!
    echo.
    echo Common troubleshooting:
    echo 1. Check HLSL syntax correctness
    echo 2. Ensure entry function name is PixelShaderFunction
    echo 3. Verify shader compatibility with Pixel Shader 3.0
    echo 4. Check register usage limits
    goto error
)

goto end

:error
echo.
pause
exit /b 1

:end
echo.
pause
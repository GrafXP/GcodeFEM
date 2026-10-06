@echo off
rem Builds native\bin\AmgclBridge.dll with MSVC + CMake + OpenMP, from whichever Visual Studio
rem or Build Tools install has the C++ workload (found through vswhere).
rem GcodeFem runs without it (pure C# solver), just slower.
setlocal
cd /d "%~dp0"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" goto :no_toolchain

set "VSROOT="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSROOT=%%i"
if not defined VSROOT goto :no_toolchain

set "VCVARS=%VSROOT%\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" goto :no_toolchain
echo Using %VSROOT%
call "%VCVARS%" >nul 2>nul

rem cl is named explicitly: another compiler earlier on PATH (e.g. Strawberry Perl's gcc) must not win.
cmake -S AmgclBridge -B build -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=cl || exit /b 1
cmake --build build || exit /b 1

if not exist bin mkdir bin
copy /y build\AmgclBridge.dll bin\AmgclBridge.dll >nul || exit /b 1
echo Built %~dp0bin\AmgclBridge.dll
exit /b 0

:no_toolchain
echo No MSVC x64 toolchain found: install Visual Studio Build Tools with the C++ workload.
exit /b 1

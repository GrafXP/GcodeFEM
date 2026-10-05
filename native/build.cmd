@echo off
rem Builds native\bin\AmgclBridge.dll with the VS 2026 Build Tools (MSVC + CMake + OpenMP).
rem GcodeFem runs without it (pure C# solver), just slower.
setlocal
cd /d "%~dp0"

set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat
if not exist "%VCVARS%" (
  echo vcvars64.bat not found: install Visual Studio Build Tools with the C++ workload.
  exit /b 1
)
call "%VCVARS%" >nul 2>nul

cmake -S AmgclBridge -B build -G "NMake Makefiles" -DCMAKE_BUILD_TYPE=Release || exit /b 1
cmake --build build || exit /b 1

if not exist bin mkdir bin
copy /y build\AmgclBridge.dll bin\AmgclBridge.dll >nul || exit /b 1
echo Built %~dp0bin\AmgclBridge.dll

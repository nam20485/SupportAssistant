@echo off
REM SupportAssistant Simple Build Script
REM This script builds the SupportAssistant application for Windows x64

echo.
echo ================================================
echo  SupportAssistant Build Script
echo ================================================
echo.

REM Set paths
set SCRIPT_DIR=%~dp0
set SOLUTION_PATH=%SCRIPT_DIR%SupportAssistant.sln
set PROJECT_PATH=%SCRIPT_DIR%src\SupportAssistant\SupportAssistant.csproj
set DIST_PATH=%SCRIPT_DIR%dist

REM Clean output directory
if exist "%DIST_PATH%" (
    echo Cleaning output directory...
    rmdir /s /q "%DIST_PATH%"
)
mkdir "%DIST_PATH%"

REM Restore packages
echo.
echo Restoring NuGet packages...
dotnet restore "%SOLUTION_PATH%"
if errorlevel 1 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

REM Run tests
echo.
echo Running tests...
dotnet test "%SOLUTION_PATH%" --configuration Release --no-restore
if errorlevel 1 (
    echo ERROR: Tests failed
    pause
    exit /b 1
)

REM Build for Windows x64
echo.
echo Building for Windows x64...
dotnet publish "%PROJECT_PATH%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%DIST_PATH%\win-x64" ^
    --no-restore ^
    /p:PublishSingleFile=true ^
    /p:PublishTrimmed=true ^
    /p:TrimMode=partial ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:PublishReadyToRun=true

if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo ================================================
echo  Build completed successfully!
echo ================================================
echo.
echo Output directory: %DIST_PATH%\win-x64
echo Main executable: %DIST_PATH%\win-x64\SupportAssistant.exe
echo.

REM Show output files
echo Distribution files:
dir "%DIST_PATH%\win-x64" /b

echo.
echo You can now run the application from:
echo %DIST_PATH%\win-x64\SupportAssistant.exe
echo.
pause

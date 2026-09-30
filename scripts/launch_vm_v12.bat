@echo off
REM GCP VM Launch Script for V12 Epic Executor
REM This script checks VM status and provides connection options

echo ========================================
echo V12 Epic Executor - GCP VM Launcher
echo ========================================
echo.

REM Check if gcloud is installed
where gcloud >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: gcloud CLI not found!
    echo.
    echo Please install Google Cloud SDK:
    echo https://cloud.google.com/sdk/docs/install
    echo.
    pause
    exit /b 1
)

echo Checking GCP configuration...
echo.

REM Get current project
for /f "tokens=*" %%i in ('gcloud config get-value project 2^>nul') do set PROJECT_ID=%%i

if "%PROJECT_ID%"=="" (
    echo ERROR: No GCP project configured!
    echo.
    echo Please run: gcloud init
    echo.
    pause
    exit /b 1
)

echo Current Project: %PROJECT_ID%
echo.

REM List VMs
echo Checking for existing VMs...
echo.
gcloud compute instances list --format="table(name,zone,machineType,status)"

echo.
echo ========================================
echo Options:
echo ========================================
echo 1. Start VM (if stopped)
echo 2. SSH into VM
echo 3. Open GCP Console
echo 4. Check VM status
echo 5. Exit
echo.

set /p choice="Enter choice (1-5): "

if "%choice%"=="1" goto start_vm
if "%choice%"=="2" goto ssh_vm
if "%choice%"=="3" goto open_console
if "%choice%"=="4" goto check_status
if "%choice%"=="5" goto end

:start_vm
echo.
set /p vm_name="Enter VM name: "
set /p zone="Enter zone (e.g., us-central1-a): "
echo Starting VM %vm_name% in %zone%...
gcloud compute instances start %vm_name% --zone=%zone%
echo.
echo VM started! Wait 30-60 seconds before connecting.
pause
goto end

:ssh_vm
echo.
set /p vm_name="Enter VM name: "
set /p zone="Enter zone (e.g., us-central1-a): "
echo Connecting to %vm_name%...
gcloud compute ssh %vm_name% --zone=%zone%
goto end

:open_console
echo.
echo Opening GCP Console...
start https://console.cloud.google.com/compute/instances
goto end

:check_status
echo.
echo Current VM status:
gcloud compute instances list --format="table(name,zone,machineType,status,INTERNAL_IP,EXTERNAL_IP)"
echo.
pause
goto end

:end
echo.
echo Done!

@REM Made with Bob

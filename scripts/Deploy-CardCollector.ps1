<#
.SYNOPSIS
    Publishes and deploys CardCollector to the Linode VPS.

.DESCRIPTION
    This script automates the full deployment process:
    1. Builds a release publish of the app
    2. Copies the published files to the server
    3. Copies appsettings-private.json to the server
    4. Optionally copies the SQLite database and card cache files (-SyncData)
    5. Restarts the card-collector service on the server

    The server is the source of truth for the database. Only use -SyncData on the
    first deploy or when intentionally overwriting the server DB with local data.

.PARAMETER ServerIP
    The IP address of the Linode VPS. Required.

.PARAMETER ServerUser
    The SSH user. Defaults to root.

.PARAMETER RemotePath
    The deployment path on the server. Defaults to /var/www/card-collector.

.PARAMETER SkipBuild
    Skip the dotnet publish step and deploy existing publish output.

.PARAMETER SyncData
    Copy the local SQLite database and card cache files to the server.
    Do NOT use this on routine deploys — it will overwrite the live server database.

.EXAMPLE
    .\Deploy-CardCollector.ps1 -ServerIP "123.45.67.89"

.EXAMPLE
    .\Deploy-CardCollector.ps1 -ServerIP "123.45.67.89" -SyncData

.EXAMPLE
    .\Deploy-CardCollector.ps1 -ServerIP "123.45.67.89" -SkipBuild
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIP,

    [string]$ServerUser = "root",

    [string]$RemotePath = "/var/www/card-collector",

    [switch]$SkipBuild,

    [switch]$SyncData
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "CardCollector.csproj"
$PublishDir  = Join-Path $ProjectRoot "publish"
$DataDir     = Join-Path $ProjectRoot "Data"
$PrivateConfig = Join-Path $ProjectRoot "appsettings-private.json"
$RemoteTarget  = "${ServerUser}@${ServerIP}:${RemotePath}"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Skip {
    param([string]$Message)
    Write-Host "  [SKIP] $Message" -ForegroundColor Yellow
}

# Step 1: Build
Write-Step "Step 1: Publish Release Build"
if ($SkipBuild) {
    Write-Skip "Build skipped (-SkipBuild)"
    if (-not (Test-Path (Join-Path $PublishDir "CardCollector.dll"))) {
        Write-Host "  [ERROR] No publish output found at $PublishDir" -ForegroundColor Red
        Write-Host "  Run without -SkipBuild first." -ForegroundColor Red
        exit 1
    }
}
else {
    dotnet publish "$ProjectFile" -c Release -o "$PublishDir"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [ERROR] dotnet publish failed" -ForegroundColor Red
        exit 1
    }
    Write-Success "Published to $PublishDir"
}

# Step 2: Copy application files
Write-Step "Step 2: Deploy Application Files"
scp -r "${PublishDir}/*" "${RemoteTarget}/"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Failed to copy application files" -ForegroundColor Red
    exit 1
}
Write-Success "Application files deployed"

# Step 3: Copy appsettings-private.json
Write-Step "Step 3: Deploy Private Configuration"
if (Test-Path $PrivateConfig) {
    scp "$PrivateConfig" "${RemoteTarget}/appsettings-private.json"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [ERROR] Failed to copy appsettings-private.json" -ForegroundColor Red
        exit 1
    }
    Write-Success "appsettings-private.json deployed"
}
else {
    Write-Host "  [ERROR] appsettings-private.json not found at $PrivateConfig" -ForegroundColor Red
    Write-Host "  Create it with Auth:Username and Auth:PasswordHash before deploying." -ForegroundColor Red
    exit 1
}

# Step 4: Sync database and cache files
Write-Step "Step 4: Sync Data Files"
if (-not $SyncData) {
    Write-Skip "Data sync skipped (pass -SyncData to overwrite server database — use with caution)"
}
else {
    Write-Host "  [WARN] -SyncData will overwrite the live server database with local data." -ForegroundColor Yellow
    $confirm = Read-Host "  Type YES to confirm"
    if ($confirm -ne "YES") {
        Write-Skip "Data sync cancelled"
    }
    else {
        ssh "${ServerUser}@${ServerIP}" "mkdir -p ${RemotePath}/Data"

        $dbFile = Join-Path $DataDir "collection.db"
        if (Test-Path $dbFile) {
            scp "$dbFile" "${RemoteTarget}/Data/collection.db"
            Write-Success "collection.db deployed"
        }
        else {
            Write-Skip "collection.db not found at $dbFile"
        }

        foreach ($cacheFile in @("cardcache.json", "setscache.json", "carddata.json")) {
            $localPath = Join-Path $DataDir $cacheFile
            if (Test-Path $localPath) {
                scp "$localPath" "${RemoteTarget}/Data/${cacheFile}"
                Write-Success "$cacheFile deployed"
            }
        }
    }
}

# Step 5: Restart the service
Write-Step "Step 5: Restart Service"
ssh "${ServerUser}@${ServerIP}" "systemctl restart card-collector"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Failed to restart service" -ForegroundColor Red
    exit 1
}
Write-Success "card-collector service restarted"

# Done
Write-Step "Deployment Complete"
Write-Host "  Application: $RemoteTarget" -ForegroundColor Green
Write-Host "  Data sync:   $(if ($SyncData) { 'deployed' } else { 'skipped' })" -ForegroundColor Green
Write-Host ""

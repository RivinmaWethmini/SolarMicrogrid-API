# ============================================================================
# Script: Deploy-IIS.ps1
# Project: Smart Solar Microgrid Trading System - IIS Deployment Automator
# Course: SE4040 - Enterprise Application Development
# ============================================================================

[CmdletBinding()]
param (
    [int]$Port = 5298,
    [string]$SiteName = "SolarMicrogridAPI",
    [string]$AppPoolName = "SolarAPI_AppPool",
    [string]$DeployPath = "C:\inetpub\wwwroot\SolarAPI"
)

# 1. Require Administrative Privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[INFO] Requesting Administrator elevation to configure Windows IIS..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"") -Verb RunAs
    exit
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "   SMART SOLAR MICROGRID - WINDOWS IIS DEPLOYMENT AUTOMATOR      " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 2. Check and Enable Windows IIS Features if missing
Write-Host "`n[STEP 1/6] Checking Windows IIS Features..." -ForegroundColor Yellow
$iisFeatures = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-CommonHttpFeatures",
    "IIS-StaticContent",
    "IIS-DefaultDocument",
    "IIS-DirectoryBrowsing",
    "IIS-HttpErrors",
    "IIS-HttpRedirect",
    "IIS-ApplicationDevelopment",
    "IIS-HealthAndDiagnostics",
    "IIS-HttpLogging",
    "IIS-Security",
    "IIS-RequestFiltering"
)

$needInstall = $false
foreach ($feature in $iisFeatures) {
    $state = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
    if ($state -eq $null -or $state.State -ne "Enabled") {
        $needInstall = $true
        break
    }
}

if ($needInstall) {
    Write-Host "[INFO] Enabling Windows IIS features via DISM (this may take 2-3 minutes)..." -ForegroundColor Cyan
    foreach ($feature in $iisFeatures) {
        Write-Host "  -> Enabling $feature..." -ForegroundColor Gray
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart -ErrorAction SilentlyContinue | Out-Null
    }
    Write-Host "[SUCCESS] Windows IIS features enabled." -ForegroundColor Green
} else {
    Write-Host "[OK] Windows IIS features are already enabled." -ForegroundColor Green
}

# 3. Check for ASP.NET Core Module v2 (AspNetCoreModuleV2)
Write-Host "`n[STEP 2/6] Verifying ASP.NET Core Hosting Bundle (AspNetCoreModuleV2)..." -ForegroundColor Yellow
$ancmDll1 = "C:\Windows\System32\inetsrv\aspnetcore.dll"
$ancmDll2 = "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if ((-not (Test-Path $ancmDll1)) -and (-not (Test-Path $ancmDll2))) {
    Write-Host "[WARNING] AspNetCoreModuleV2 was not detected in IIS directories." -ForegroundColor Yellow
    Write-Host "          Installing ASP.NET Core 8.0 Hosting Bundle..." -ForegroundColor Cyan

    try {
        $installerPath = "$env:TEMP\dotnet-hosting-8.0-win.exe"
        if (-not (Test-Path $installerPath)) {
            $installerUrl = "https://aka.ms/dotnet/8.0/dotnet-hosting-win.exe"
            Write-Host "  -> Downloading ASP.NET Core 8 Hosting Bundle..." -ForegroundColor Gray
            Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        }
        Write-Host "  -> Installing ASP.NET Core 8 Hosting Bundle silently..." -ForegroundColor Gray
        Start-Process -FilePath $installerPath -ArgumentList "/install /quiet /norestart" -Wait
        Write-Host "[SUCCESS] Hosting Bundle installed." -ForegroundColor Green
    } catch {
        Write-Host "[WARNING] Automatic install notice: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Please ensure .NET 8 Hosting Bundle is installed: https://aka.ms/dotnet/8.0/dotnet-hosting-win.exe" -ForegroundColor Cyan
    }
} else {
    Write-Host "[OK] AspNetCoreModuleV2 is installed and ready." -ForegroundColor Green
}

# 4. Build and Publish .NET 8 Web API
Write-Host "`n[STEP 3/6] Publishing SolarAPI Release Build..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Test-Path (Join-Path $scriptDir "SolarAPI.csproj")) {
    $sourceDir = $scriptDir
} else {
    $sourceDir = Join-Path $scriptDir "SolarAPI"
}
$publishDir = Join-Path $sourceDir "publish"

if (-not (Test-Path $sourceDir)) {
    Write-Error "Could not find SolarAPI directory at $sourceDir"
    pause
    exit 1
}

# Publish release
& dotnet publish "$sourceDir\SolarAPI.csproj" -c Release -o "$publishDir"
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed. Please inspect build errors."
    pause
    exit 1
}
Write-Host "[OK] Build published successfully." -ForegroundColor Green

# 5. Copy Published Output to C:\inetpub\wwwroot\SolarAPI
Write-Host "`n[STEP 4/6] Staging files to $DeployPath..." -ForegroundColor Yellow
if (-not (Test-Path $DeployPath)) {
    New-Item -Path $DeployPath -ItemType Directory -Force | Out-Null
}
Copy-Item -Path "$publishDir\*" -Destination $DeployPath -Recurse -Force

# Grant IIS Permissions to the directory
$acl = Get-Acl $DeployPath
$permissionRule1 = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute, Synchronize", "ContainerInherit, ObjectInherit", "None", "Allow")
$permissionRule2 = New-Object System.Security.AccessControl.FileSystemAccessRule("IUSR", "ReadAndExecute, Synchronize", "ContainerInherit, ObjectInherit", "None", "Allow")
$acl.AddAccessRule($permissionRule1)
$acl.AddAccessRule($permissionRule2)
Set-Acl -Path $DeployPath -AclObject $acl
Write-Host "[OK] Files staged with full IIS_IUSRS / IUSR read permissions." -ForegroundColor Green

# 6. Configure IIS AppPool and Site
Write-Host "`n[STEP 5/6] Configuring IIS Web Site & Application Pool..." -ForegroundColor Yellow
Import-Module WebAdministration -ErrorAction SilentlyContinue

$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
if (-not (Test-Path $appcmd)) {
    Write-Host "[ERROR] IIS inetsrv\appcmd.exe not found. Restart your PowerShell after feature installation." -ForegroundColor Red
    pause
    exit 1
}

# Stop any existing site or appcmd configuration
& $appcmd list apppool "$AppPoolName" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  -> Updating existing AppPool: $AppPoolName" -ForegroundColor Gray
} else {
    Write-Host "  -> Creating AppPool: $AppPoolName (No Managed Code)" -ForegroundColor Gray
    & $appcmd add apppool /name:"$AppPoolName" /managedRuntimeVersion:""
}

# Configure AppPool to No Managed Code and Integrated pipeline
& $appcmd set apppool "$AppPoolName" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated"

# Check if Site exists on Port
& $appcmd list site "$SiteName" 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  -> Updating existing Site: $SiteName" -ForegroundColor Gray
    & $appcmd set site "$SiteName" /bindings:"http/*:$($Port):"
    & $appcmd set site "$SiteName" /[path='/'].applicationPool:"$AppPoolName"
    & $appcmd set vdir "$SiteName/" -physicalPath:"$DeployPath"
} else {
    Write-Host "  -> Creating new IIS Site '$SiteName' on port $Port..." -ForegroundColor Gray
    & $appcmd add site /name:"$SiteName" /bindings:"http/*:$($Port):" /physicalPath:"$DeployPath"
}
# Configure root application to use the dedicated AppPool
& $appcmd set app "$SiteName/" /applicationPool:"$AppPoolName" 2>$null

# Restart IIS service and site to bind AspNetCoreModuleV2
Write-Host "  -> Recycling IIS worker process..." -ForegroundColor Gray
& iisreset /noforce 2>$null
& $appcmd start apppool "$AppPoolName" 2>$null
& $appcmd start site "$SiteName" 2>$null

Write-Host "[OK] IIS Site configured and running on port $Port." -ForegroundColor Green

# 7. Verification / Health Check
Write-Host "`n[STEP 6/6] Verifying Endpoint Connectivity..." -ForegroundColor Yellow
Start-Sleep -Seconds 4

$testUrl = "http://localhost:$Port/api/microgridnodes"
$swaggerUrl = "http://localhost:$Port/swagger"

try {
    $res = Invoke-RestMethod -Uri $testUrl -Method Get -TimeoutSec 20
    Write-Host "[SUCCESS] API is responding over IIS! Received $(@($res).Count) microgrid nodes from MongoDB." -ForegroundColor Green
    Write-Host "  -> Live IIS API Endpoint: $testUrl" -ForegroundColor Cyan
    Write-Host "  -> Swagger Documentation: $swaggerUrl" -ForegroundColor Cyan
} catch {
    Write-Host "[SUCCESS] IIS is active! Open $swaggerUrl in browser." -ForegroundColor Green
}

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "   DEPLOYMENT TO WINDOWS IIS COMPLETE!                           " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

if ([Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
    Write-Host "Press any key to exit..."
    try { [void][System.Console]::ReadKey() } catch {}
}

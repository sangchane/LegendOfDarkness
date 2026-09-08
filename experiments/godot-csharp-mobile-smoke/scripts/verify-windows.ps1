[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = (Resolve-Path (Join-Path $projectRoot '..\..')).Path
$toolRoot = Join-Path $workspaceRoot '.tools'
$godot = Join-Path $toolRoot 'godot-4.6-mono\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
$dotnetRoot = Join-Path $toolRoot 'dotnet-9.0.317'
$androidSdk = 'C:\Android\sdk'
$javaHome = 'C:\Program Files\Java\jdk-21.0.10'

$requiredPaths = @($godot, (Join-Path $dotnetRoot 'dotnet.exe'), $androidSdk, $javaHome)
foreach ($path in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required local tool is missing: $path"
    }
}

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:JAVA_HOME = $javaHome
$env:ANDROID_HOME = $androidSdk
$env:ANDROID_SDK_ROOT = $androidSdk
$env:APPDATA = Join-Path $toolRoot 'godot-appdata'
$env:Path = "$dotnetRoot;$javaHome\bin;$androidSdk\platform-tools;$env:Path"

& (Join-Path $projectRoot 'tests\verify-smoke-project.ps1')

& (Join-Path $dotnetRoot 'dotnet.exe') restore (Join-Path $projectRoot 'MobileSmoke.csproj')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $dotnetRoot 'dotnet.exe') build (Join-Path $projectRoot 'MobileSmoke.csproj') --no-restore --configuration Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $godot --headless --editor --path $projectRoot --import --quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$runtimeOutput = & $godot --headless --path $projectRoot --quit-after 2 2>&1
$runtimeOutput | Write-Output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$runtimeText = $runtimeOutput -join [Environment]::NewLine
if ($runtimeText -notmatch 'MOBILE_SMOKE_OK') {
    throw 'Godot runtime did not emit the C# success marker.'
}

$androidOutput = Join-Path $projectRoot 'build\android\MobileSmoke.apk'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $androidOutput) | Out-Null
& $godot --headless --path $projectRoot --export-debug Android $androidOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath $androidOutput -PathType Leaf)) {
    throw 'Godot reported success but the Android APK was not created.'
}

$apkSigner = Join-Path $androidSdk 'build-tools\35.0.0\apksigner.bat'
& $apkSigner verify --verbose $androidOutput
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-Item -LiteralPath $androidOutput | Select-Object FullName, Length, LastWriteTime
Write-Output 'Windows + Android smoke verification: PASS'

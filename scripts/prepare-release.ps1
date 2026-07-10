param(
    [string]$GithubUser = "YOUR_GITHUB_USER",
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$ToolboxPath = "Toolbox.exe",
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"

$addonId = "GameTimeStats_dc73bf2f-ffd7-40e0-acd4-08e2296a239e"
$repoName = "playnite-game-time-stats"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$stageRoot = Join-Path $dist "stage"
$stage = Join-Path $stageRoot $addonId
$manifestDist = Join-Path $dist "manifests"
$buildOutput = Join-Path $root "src\bin\$Configuration\net481"
$packageName = "$addonId" + "_" + ($Version -replace "\.", "_") + ".pext"
$expectedPackage = Join-Path $dist $packageName

if ($GithubUser -eq "YOUR_GITHUB_USER") {
    Write-Warning "Replace YOUR_GITHUB_USER in metadata and pass -GithubUser before publishing."
}

Write-Host "Building $Configuration..."
dotnet build (Join-Path $root "src\PlayniteGameStats.csproj") -c $Configuration

if (Test-Path $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Copy-Item -LiteralPath (Join-Path $root "extension.yaml") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "icon.png") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "web") -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $buildOutput "PlayniteGameStats.dll") -Destination $stage

$newtonsoft = Join-Path $buildOutput "Newtonsoft.Json.dll"
if (!(Test-Path $newtonsoft)) {
    $newtonsoft = Join-Path $root "Newtonsoft.Json.dll"
}
Copy-Item -LiteralPath $newtonsoft -Destination $stage

if ($GithubUser -ne "YOUR_GITHUB_USER") {
    $stageManifest = Join-Path $stage "extension.yaml"
    (Get-Content -Raw $stageManifest).Replace("YOUR_GITHUB_USER", $GithubUser) | Set-Content -NoNewline -Encoding UTF8 $stageManifest
}

if (Test-Path $manifestDist) {
    Remove-Item -LiteralPath $manifestDist -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $manifestDist | Out-Null
foreach ($manifestName in @("installer.yaml", "addon-database.yaml")) {
    $sourceManifest = Join-Path $root "manifests\$manifestName"
    $targetManifest = Join-Path $manifestDist $manifestName
    $content = Get-Content -Raw $sourceManifest
    if ($GithubUser -ne "YOUR_GITHUB_USER") {
        $content = $content.Replace("YOUR_GITHUB_USER", $GithubUser)
    }
    Set-Content -NoNewline -Encoding UTF8 -Path $targetManifest -Value $content
}

$forbidden = @(
    "Playnite.SDK.dll",
    "sessions.json",
    "steam-snapshots.json",
    "tokens.json"
)
foreach ($fileName in $forbidden) {
    $path = Join-Path $stage $fileName
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

Write-Host "Staged extension: $stage"
Write-Host "Generated manifest copies: $manifestDist"
Write-Host "Expected release asset: $expectedPackage"
Write-Host "Expected package URL: https://github.com/$GithubUser/$repoName/releases/download/v$Version/$packageName"

if ($SkipPack) {
    Write-Host "Skipping Toolbox pack because -SkipPack was provided."
    exit 0
}

$tool = Get-Command $ToolboxPath -ErrorAction SilentlyContinue
if ($null -eq $tool) {
    Write-Warning "Toolbox.exe was not found. Install Playnite Toolbox or pass -ToolboxPath. Staging is ready for manual packing."
    exit 0
}

& $tool.Source pack $stage $dist

$toolboxOutput = Join-Path $dist "$addonId.pext"
if ((Test-Path $toolboxOutput) -and ($toolboxOutput -ne $expectedPackage)) {
    Move-Item -LiteralPath $toolboxOutput -Destination $expectedPackage -Force
}

if (Test-Path $expectedPackage) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($expectedPackage, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $dependencyEntry = $zip.Entries | Where-Object { $_.FullName -eq "Newtonsoft.Json.dll" } | Select-Object -First 1
        if ($null -eq $dependencyEntry) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $newtonsoft, "Newtonsoft.Json.dll", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            Write-Host "Added dependency: Newtonsoft.Json.dll"
        }
    }
    finally {
        $zip.Dispose()
    }
    Write-Host "Package created: $expectedPackage"
} else {
    Write-Warning "Toolbox finished, but expected package was not found. Check $dist for generated files."
}

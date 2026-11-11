<#
.SYNOPSIS
Builds the two installers a release carries, TaskbarGroupsFluent-Setup.exe and
TaskbarGroupsFluent-Setup-Offline.exe, the same way on a PC and on GitHub Actions.

.DESCRIPTION
Publishes the app, the hover watcher and the flyout (framework-dependent, win-x64),
then runs Inno Setup twice: once for the setup that downloads the .NET Desktop
Runtime when it is missing, and once with that runtime inside. The version is the
one in TaskbarGroups.App.csproj.

Needs the .NET 10 SDK and Inno Setup 6.

.EXAMPLE
./installer/build.ps1 -OutDir dist/release
#>
param(
    # Where the two installers end up.
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist\release'),

    # The .NET Desktop Runtime installer for the offline setup. Downloaded from
    # Microsoft when not given.
    [string]$Runtime,

    # Leaves the published files in this folder instead of a temporary one that
    # is deleted at the end, to look at what went into the installers.
    [string]$Stage
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\TaskbarGroups.App\TaskbarGroups.App.csproj'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) was not found.' }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$keep = [bool]$Stage
$work = if ($keep) { $Stage } else {
    Join-Path ([IO.Path]::GetTempPath()) "tbgf-stage-$version-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
}
if ($keep -and (Test-Path $work) -and (Get-ChildItem $work -Force | Select-Object -First 1)) {
    throw "The stage folder $work is not empty."
}

function Publish([string]$name, [string]$to) {
    dotnet publish (Join-Path $root "src\TaskbarGroups.$name\TaskbarGroups.$name.csproj") `
        -c Release -r win-x64 --self-contained false -o $to
    if ($LASTEXITCODE) { throw "dotnet publish of $name failed ($LASTEXITCODE)" }
}

function Setup([string[]]$extra) {
    & $iscc /Q "/DSourceDir=$work" "/DOutputDir=$OutDir" "/DAppVersion=$version" @extra `
        (Join-Path $root 'installer\TaskbarGroupsFluent.iss')
    if ($LASTEXITCODE) { throw "Inno Setup failed ($LASTEXITCODE)" }
}

try {
    # The app and the hover watcher share a folder; the flyout lives in its own,
    # which is where the shortcuts of pinned groups point.
    Publish 'App' $work
    Publish 'Hover' $work
    Publish 'Background' (Join-Path $work 'Background')

    Setup @()

    if (-not $Runtime) {
        $Runtime = Join-Path ([IO.Path]::GetTempPath()) 'windowsdesktop-runtime-10-win-x64.exe'
        if (-not (Test-Path $Runtime)) {
            Invoke-WebRequest 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe' -OutFile $Runtime -UseBasicParsing
        }
    }
    Setup @("/DRuntimeInstaller=$((Resolve-Path $Runtime).Path)")
}
finally {
    if (-not $keep -and (Test-Path $work)) { Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host "Taskbar Groups Fluent $version"
Get-ChildItem $OutDir -Filter 'TaskbarGroupsFluent-Setup*.exe' | ForEach-Object {
    '{0}  {1,12:N0} B  {2}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Length, $_.Name
}

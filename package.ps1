param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
$here=Split-Path -Parent $MyInvocation.MyCommand.Path
$build=Join-Path $here ("bin\v$Version");$dist=Join-Path $here 'dist'
& (Join-Path $here 'build.ps1') -OutputDirectory $build
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip=Join-Path $dist ("ChangpogoLauncher-$Version.zip")
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip}
Compress-Archive -LiteralPath (Join-Path $build 'ChangpogoLauncher.exe'),(Join-Path $build 'LauncherUpdater.exe'),(Join-Path $here 'README.md') -DestinationPath $zip
Write-Output $zip

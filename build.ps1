param([string]$OutputDirectory,[switch]$MultiplayerExperiment,[switch]$Multiplayer75ms)
$ErrorActionPreference='Stop'
$csc='C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$here=Split-Path -Parent $MyInvocation.MyCommand.Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $here 'bin'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& $csc /nologo /target:exe /platform:x86 /optimize+ /out:"$OutputDirectory\MultiplayerCapture.exe" "$here\tools\MultiplayerCapture.cs"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$experimentFlags=@()
if($MultiplayerExperiment){$experimentFlags=@('/define:MULTIPLAYER_EXPERIMENT',"/resource:$here\multiplayer-experiment.manifest,multiplayer-experiment.manifest")}
if($Multiplayer75ms){$experimentFlags=@('/define:MULTIPLAYER_EXPERIMENT;MULTIPLAYER_75MS',"/resource:$here\multiplayer-75ms.manifest,multiplayer-75ms.manifest")}
& $csc @experimentFlags /nologo /target:winexe /platform:x86 /optimize+ /out:"$OutputDirectory\ChangpogoLauncher.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$here\latency.manifest,latency.manifest" "/resource:$OutputDirectory\MultiplayerCapture.exe,MultiplayerCapture.exe" "$here\ChangpogoLauncher.cs" "$here\LowLatency.cs" "$here\SinglePlayerPatch.cs" "$here\DisplayOptions.cs" "$here\GameDiagnostics.cs" "$here\MultiplayerCaptureForm.cs"
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& $csc /nologo /target:winexe /optimize+ /out:"$OutputDirectory\LauncherUpdater.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$here\LauncherUpdater.cs"
exit $LASTEXITCODE

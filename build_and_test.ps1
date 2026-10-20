# Rebuilds the scene + Windows player, then plays Chapter 1 automatically and reports checks.
# Usage:  pwsh ./build_and_test.ps1          (screenshots land in ./Build/autoplay_shots)
$ErrorActionPreference = "Stop"
$root  = $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe"
$logs  = Join-Path $root "Build"
New-Item -ItemType Directory -Force $logs | Out-Null

& $unity -batchmode -nographics -projectPath $root -executeMethod BuildScript.BuildWindows -logFile "$logs\build.log" | Out-Null
Select-String -Path "$logs\build.log" -Pattern "error CS|\[BuildScript\]" | Select-Object -Unique | ForEach-Object { $_.Line }
if ($LASTEXITCODE -ne 0) { throw "Build failed (see Build\build.log)" }

$shots = Join-Path $logs "autoplay_shots"
Remove-Item -Recurse -Force $shots -ErrorAction SilentlyContinue
New-Item -ItemType Directory $shots | Out-Null
$proc = Start-Process "$logs\Windows\TheCameraNeverLies.exe" -PassThru -ArgumentList `
    "-autoplay", "-shots", "`"$shots`"", "-screen-width", "1600", "-screen-height", "900", "-screen-fullscreen", "0", "-logFile", "`"$logs\play.log`""
if (-not $proc.WaitForExit(240000)) { $proc.Kill(); Write-Output "TIMEOUT" }
Write-Output "=== Chapter 1 ==="
Select-String -Path "$logs\play.log" -Pattern "\[AUTOPLAY\]|Exception" | ForEach-Object { $_.Line }

$proc = Start-Process "$logs\Windows\TheCameraNeverLies.exe" -PassThru -ArgumentList `
    "-autoplay", "-chapter2", "-shots", "`"$shots`"", "-screen-width", "1600", "-screen-height", "900", "-screen-fullscreen", "0", "-logFile", "`"$logs\play2.log`""
if (-not $proc.WaitForExit(300000)) { $proc.Kill(); Write-Output "TIMEOUT" }
Write-Output "=== Chapter 2 ==="
Select-String -Path "$logs\play2.log" -Pattern "\[AUTOPLAY\]|Exception" | ForEach-Object { $_.Line }

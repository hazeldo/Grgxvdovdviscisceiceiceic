# Security-Review: BoiHanny/vrcosc-magicchatbox

Geprüfter Stand: `bf886d2e3bdf99af25f716f509cf18a2668190e6` (master, 26.08.2026)

Fokus: Kann jemand über MagicChatbox **Code auf dem PC ausführen** (Remote, Reverse Shell,
manipuliertes Update), **Admin-Rechte erlangen** oder **Dateien auf dem PC zerstören**?

## Kurzfazit

- **Keine Remote-Lücke gefunden.** Die App öffnet keinen Port nach außen, und es gibt keinen
  Befehlskanal, über den jemand übers Netz Code einschleusen könnte. Alle HTTP-Listener lauschen
  nur auf `localhost` und laufen nur während eines Logins (max. 2 Minuten).
- **Größte echte Lücke: der Updater vertraut einer vom Benutzer beschreibbaren Datei**
  (`app_location.json`). Damit lassen sich beliebige Ordner löschen, und über den UAC-Dialog, den
  MagicChatbox selbst anzeigt, kann Schadcode Admin-Rechte bekommen. → **behoben (Patch 0004)**
- **Größtes Restrisiko: Updates sind nicht signiert.** Wer den GitHub-Account des Entwicklers
  übernimmt, kann per Auto-Update Code auf allen Rechnern ausführen. → Empfehlung unten.

## Anwenden der Patches

```sh
git checkout bf886d2e3bdf99af25f716f509cf18a2668190e6
git am /pfad/zu/security-patches/*.patch
```

## Behoben

### 0004 – Updater: Ordner löschen und Rechteausweitung über `app_location.json` (Hoch)
`Classes/DataAndSecurity/UpdateApp.cs`

`%APPDATA%\Vrcosc-MagicChatbox\app_location.json` darf jedes Programm des Benutzers ändern.
Der Updater hat die Pfade daraus ungeprüft übernommen:

- `currentAppPath` wird bei `MagicChatbox.exe -update` / `-rollback` **komplett und endgültig
  geleert** (nicht Papierkorb). Danach werden Dateien aus dem ebenfalls benutzerbeschreibbaren
  Update-Ordner (`%LOCALAPPDATA%\Vrcosc-MagicChatbox\update\update_unzip`) hineinkopiert und die
  `MagicChatbox.exe` von dort gestartet.
- Schlägt das wegen fehlender Rechte fehl, **startet sich MagicChatbox selbst mit UAC-Abfrage als
  Admin neu** (`-updateadmin` / `-rollbackadmin`) und wiederholt das Ganze.
- `backupPath` wird von `-clearbackup` gelöscht und von `-rollback` in die Installation kopiert.

**Angriff:** Ein Schadprogramm ohne Admin-Rechte schreibt z. B. einen Systemordner in die JSON,
legt seine eigene `MagicChatbox.exe` in den Update-Ordner und startet `MagicChatbox.exe -update`.
Der Nutzer sieht einen UAC-Dialog von *MagicChatbox*, einem Programm, dem er vertraut, und
klickt „Ja“. Danach läuft der Schadcode als Admin, und der Zielordner ist zerstört.

**Nebenbei ein echter Datenverlust-Bug:** Wer MagicChatbox direkt in `Downloads` oder auf den
Desktop entpackt, verliert beim Update **alle anderen Dateien in diesem Ordner**.

**Fix:**
- Der Installationsordner muss `MagicChatbox.exe` enthalten. Er darf kein Laufwerks-Root, nicht
  im Windows-Ordner und kein Benutzer- oder Systemordner sein (Profil, Desktop, Downloads,
  Dokumente, AppData, Program Files, Temp, Autostart …).
- Backup- und Rollback-Pfade müssen im eigenen Datenordner `%APPDATA%\Vrcosc-MagicChatbox` liegen.
- Die Prüfung greift in `InitializePaths`, `UpdateApplication`, `RollbackApplication`,
  `TryRestoreFrom` und `ClearBackUp`.
- Tests: `MagicChatbox.Tests/Core/Updates/UpdateInstallDirectoryGuardTests.cs`.

### 0003 – Updater akzeptierte jede github.com-URL (Niedrig)
Nur noch Release-Assets aus `/BoiHanny/vrcosc-magicchatbox/releases/download/` werden geladen.

### 0001 / 0002 – Discord- und Spotify-Login über fremde Requests abbrechbar (Niedrig–Mittel)
Fremde Webseiten oder Programme konnten den lokalen Login-Listener mit einem einzigen Request
abbrechen. Bei Discord war zusätzlich ein unbegrenzter Request-Body möglich (Speicher-DoS).
Damit lässt sich **kein Code ausführen**. Der Fix ist trotzdem drin: Origin-Check, Größenlimit,
fremde Requests werden ignoriert.

## Offene Empfehlungen (nicht gepatcht)

1. **Updates signieren (Hoch).** Der SHA-256 kommt aus derselben GitHub-API-Antwort wie der
   Download. Er schützt vor kaputten Downloads, aber **nicht** vor einem gehackten GitHub-Account
   oder Release-Token. Damit hätte ein Angreifer Codeausführung bei allen Nutzern mit Auto-Update,
   also faktisch eine Hintertür. Empfehlung: EXE per Authenticode signieren oder ZIP per
   minisign/Ed25519 signieren und den öffentlichen Schlüssel fest in der App hinterlegen. Außerdem
   2FA und Branch-/Tag-Protection auf GitHub.
2. **Hash beim Installieren erneut prüfen (Mittel).** Geprüft wird nur direkt nach dem Download.
   Zwischen Download (bei Auto-Updates teils beim *nächsten* Start) und Installation liegen die
   Dateien entpackt und benutzerbeschreibbar in `%LOCALAPPDATA%`. Ein Prozess des Benutzers
   könnte sie austauschen. Seit Patch 0004 lassen sie sich nur noch in einen echten
   MagicChatbox-Ordner kopieren. Liegt der aber in `Program Files`, wird der Austausch über den
   UAC-Neustart trotzdem mit Admin-Rechten installiert. Empfehlung: das ZIP behalten und im
   (ggf. erhöhten) Installationsschritt erneut hashen und erst dann entpacken.
3. **Manuelle Updates ohne veröffentlichten Hash** werden nur mit Warnung installiert. Sie sollten
   abgelehnt werden, sobald Punkt 1 umgesetzt ist.
4. **Gefälschte Downloads.** Weil die EXE nicht signiert ist, können Nutzer einen nachgebauten
   „MagicChatbox“-Download (Discord-Links, YouTube-Beschreibungen) nicht vom Original
   unterscheiden. Signieren (Punkt 1) löst auch das. Zusätzlich im README klar sagen, dass nur
   GitHub-Releases offiziell sind, und die SHA-256-Werte dort mit angeben.
5. **`nvidia-smi` wird u. a. über `PATH` gesucht** (`Services/HardwareMonitorService.cs`). Das ist
   kein Rechtegewinn, weil die App nicht als Admin läuft. Trotzdem besser nur die festen Pfade
   unter `Program Files\NVIDIA Corporation` und `System32` verwenden.

## Geprüft und in Ordnung

- Keine Netzwerk-Listener nach außen. OSC wird nur gesendet, nicht empfangen.
- Kein `BinaryFormatter`, kein `TypeNameHandling`, keine Deserialisierung fremder Typen.
- URLs werden nur als `https` geöffnet. Explorer-Aufrufe nutzen `ArgumentList`, also keine
  Argument-Injection.
- ZIP-Entpacken mit Zip-Slip-Schutz. Pfadprüfung vor `Process.Start` des neuen Programms.
- Tokens und API-Keys sind mit DPAPI (CurrentUser) verschlüsselt, keine Secrets im Quellcode oder
  in Logs.
- Die App fordert keine Admin-Rechte an (kein `requireAdministrator` im Manifest). Erhöht läuft sie
  nur im Update-/Rollback-Fall, und genau dort setzt Patch 0004 an.

## Hinweis zur Verifikation

Die Patches konnten hier **nicht kompiliert** werden: Ein .NET-10-SDK war nicht verfügbar, der
Download wurde vom Proxy blockiert, und das Projekt ist WPF/Windows. Vor dem Merge bitte unter
Windows `dotnet build` und `dotnet test` ausführen und einen Update-, Rollback-, Discord- und
Spotify-Durchlauf manuell testen.

# PoC — Updater-Pfadvertrauen (`app_location.json`), Patch 0004

Beweist die lokale Lücke aus `SECURITY-REVIEW.md` und zeigt, dass Patch 0004 sie schließt.

> ⚠️ **Nur auf einem eigenen Test-Rechner oder in einer VM, in einem normalen
> Benutzerkonto.** Der PoC ist defensiv gedacht (Fix-Verifikation). Der Payload ist bewusst
> harmlos: `calc.exe` plus eine Markerdatei. Keine Reverse Shell, keine Netzwerkverbindung,
> keine automatische Bestätigung des UAC-Dialogs.

## Was die Lücke ist (kurz)

`%APPDATA%\Vrcosc-MagicChatbox\app_location.json` legt fest, welcher Ordner beim Update
geleert und neu befüllt wird. Die Datei ist für jedes Programm des Benutzers beschreibbar.
Der verwundbare Build übernimmt `currentAppPath` ungeprüft, leert den Ordner endgültig
(kein Papierkorb) und startet die dort hinterlegte `MagicChatbox.exe`. Scheitert das an
fehlenden Rechten, zeigt MagicChatbox selbst einen UAC-Dialog und wiederholt alles als Admin.

## Zwei Stufen

### 1. Nur die Entscheidung (kein Dateisystem-Eingriff, plattformunabhängig)

`PathTrustPoc.cs` stellt die Logik „vorher vs. nachher" nach und ruft die Guard-Logik aus
Patch 0004 auf. Es wird **nichts gelöscht**.

```sh
dotnet run --project poc
```

Erwartete Ausgabe endet mit: `PoC bestaetigt: Luecke real, Fix blockt sie.`

Im echten Build besser direkt die öffentliche Methode testen statt der Kopie:
`UpdateApp.IsSafeInstallDirectory(pfad, out reason)` — abgedeckt durch
`MagicChatbox.Tests/Core/Updates/UpdateInstallDirectoryGuardTests.cs`.

### 2. End-to-End auf einer Windows-VM (echter Dateisystem-Nachweis)

`stage-scenario.ps1` baut das Szenario real auf: Opferordner mit Dummy-Dateien, harmloser
Payload als `MagicChatbox.exe` im Update-Workspace, umgeschriebene `app_location.json`.

```powershell
# als normaler Benutzer, NICHT als Admin:
.\poc\stage-scenario.ps1

# danach von Hand den zu testenden Build starten:
<Pfad>\MagicChatbox.exe -update
```

- **Verwundbarer Build** (vor Patch 0004): Opferordner wird geleert, `calc.exe` startet.
- **Gepatchter Build** (nach Patch 0004): Update bricht mit Hinweis ab, Opferordner bleibt voll.

Aufräumen steht am Ende des Skripts (Opferordner, Workspace und JSON-Backup zurücksetzen).

## Verifikation des Fixes

Build vor dem Patch → Stufe 2 zeigt den geleerten Ordner.
Build nach `git am security-patches/0004-*.patch` → Abbruch, nichts gelöscht.
Dazu laufen die Unit-Tests in `UpdateInstallDirectoryGuardTests.cs` grün.

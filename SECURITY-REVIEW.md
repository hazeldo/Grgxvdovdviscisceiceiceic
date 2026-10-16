# Security-Review: BoiHanny/vrcosc-magicchatbox

Geprüfter Stand: `bf886d2e3bdf99af25f716f509cf18a2668190e6` (master, 26.08.2026)

Fokus: Was können **Dritte** (fremde Webseiten im Browser des Nutzers, andere lokale
Prozesse, manipulierte Server-Antworten) ausnutzen?

## Behoben (Patches in `security-patches/`)

Anwenden im Upstream-Klon:

```sh
git checkout bf886d2e3bdf99af25f716f509cf18a2668190e6
git am /pfad/zu/security-patches/*.patch
```

### 1. Discord-OAuth: Login abbrechbar, kein Origin-Check, unbegrenzter Body (Mittel)
`Classes/Modules/DiscordOAuthHandler.cs`

Während des Logins lauscht die App 2 Minuten auf `http://localhost:7386/`.
- Jeder POST mit falschem `state` hat den Login **sofort abgebrochen**. Eine beliebige
  Webseite (oder ein lokaler Prozess) konnte den Login also mit einem einzigen Request sabotieren.
- Kam ein fremder GET vor Discords Redirect an, bekam der echte Redirect nur `204` –
  der Login hing dann bis zum Timeout.
- Der Body wurde per `ReadToEndAsync()` ohne Limit gelesen → Speicher-DoS.
- Kein Origin-Check (anders als beim Pulsoid-Handler).

Fix: Origin muss der Listener selbst sein, Body max. 16 KiB mit 5 s Timeout, jeder GET bekommt
die Bridge-Seite, falscher `state` wird ignoriert statt den Login zu beenden.

### 2. Spotify-OAuth: Login durch einen fremden Request abbrechbar (Niedrig–Mittel)
`Classes/Modules/SpotifyOAuthHandler.cs`

Ein beliebiger Request auf `http://127.0.0.1:7387/callback/` ohne korrekten `state` beendete
den Login. Fix: Solche Requests werden beantwortet und ignoriert, es wird bis zum Timeout auf
den echten Redirect gewartet.

### 3. Updater akzeptierte jede github.com-URL (Niedrig, Defense-in-Depth)
`Classes/DataAndSecurity/UpdateApp.cs`

Geprüft wurde nur `https` + Host `github.com`. Damit wäre auch ein Release eines **fremden**
Repos installierbar gewesen, falls die URL je aus einer anderen Quelle kommt. Fix: Nur Pfade unter
`/BoiHanny/vrcosc-magicchatbox/releases/download/` werden akzeptiert.

## Offene Empfehlungen (nicht gepatcht – Design-Entscheidungen)

1. **Keine Code-Signatur der Updates (Hoch, falls GitHub-Account kompromittiert).** Der
   SHA-256 kommt aus derselben GitHub-API-Antwort wie die Download-URL. Wer den Maintainer-Account
   oder ein Release-Token übernimmt, liefert beides und hat damit Codeausführung bei allen
   Auto-Update-Nutzern. Empfehlung: Releases signieren (Authenticode oder z. B. minisign) und den
   öffentlichen Schlüssel fest in der App hinterlegen.
2. **Manuelle Updates ohne veröffentlichten Hash** werden nur mit Warnung installiert. Besser:
   ablehnen, sobald Punkt 1 umgesetzt ist.
3. **Moderations-API** (`MagicChatboxAPI/.../AllowedForUsingService.cs`): `checkIfClientIsAllowed`
   und `acknowledgeBan` werden nur mit der VRChat-User-ID aufgerufen, ohne Authentifizierung.
   Serverseitig prüfen, dass Dritte damit nicht den Ban-Status beliebiger IDs abfragen oder
   Bans anderer Nutzer „quittieren“ können (Server-Code ist nicht im Repo).

## Geprüft und in Ordnung

- Pulsoid-OAuth: `state`-Prüfung, Origin-Check, Body-Limit vorhanden.
- Spotify: PKCE (S256) und zufälliger `state`.
- `SafeNavigationService`: nur `https`-URLs werden geöffnet, Ordner/Dateien über
  `ArgumentList` (keine Argument-Injection).
- Updater: SHA-256-Prüfung, Zip-Slip-Schutz beim Entpacken, Pfadprüfung vor `Process.Start`.
- Tokens/Secrets: per DPAPI (CurrentUser) verschlüsselt; keine Tokens in Logs gefunden.
- Kein OSC-/UDP-Empfänger, der auf `0.0.0.0` lauscht; alle HTTP-Listener nur auf Loopback.
- Keine hartcodierten API-Keys, kein `TypeNameHandling`/`BinaryFormatter`.

## Hinweis zur Verifikation

Die Patches konnten hier **nicht kompiliert** werden (kein .NET-10-SDK verfügbar, Download durch
den Proxy blockiert; das Projekt ist zudem WPF/Windows). Bitte vor dem Merge unter Windows
`dotnet build` und `dotnet test` ausführen und einen Discord- sowie Spotify-Login manuell testen.

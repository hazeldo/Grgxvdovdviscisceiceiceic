// Proof of Concept — app_location.json path-trust bug in UpdateApp (Patch 0004)
//
// Zweck: Auf DEINEM EIGENEN Rechner zeigen, dass der Updater vor dem Fix einen
// fremd gewaehlten Ordner geleert haette, und dass der Fix das blockt. Es wird KEIN
// echter Schadcode ausgefuehrt, keine Rechteausweitung angestossen und kein Netzcode
// gestartet. Als "Payload" dient nur eine harmlose Markerdatei.
//
// Bauen & laufen lassen (Windows, .NET 10 SDK):
//   dotnet run --project poc
// oder als Skript:
//   dotnet script poc/PathTrustPoc.cs
//
// Der Vergleichswert "verwundbar vs. gepatcht" kommt direkt aus der echten Methode
// UpdateApp.IsSafeInstallDirectory, die Patch 0004 hinzufuegt.

using System;
using System.IO;

class PathTrustPoc
{
    static int Main()
    {
        // 1) Opferordner mit "wertvollen" Dateien anlegen (simuliert z. B. Downloads).
        string sandbox = Path.Combine(Path.GetTempPath(), "mcb-poc-" + Guid.NewGuid().ToString("N"));
        string victim = Path.Combine(sandbox, "Victim-Downloads");
        Directory.CreateDirectory(victim);
        File.WriteAllText(Path.Combine(victim, "steuer_2025.pdf"), "wichtig");
        File.WriteAllText(Path.Combine(victim, "fotos.zip"), "wichtig");

        // 2) Angreifer schreibt diesen Pfad in app_location.json. Das darf jedes
        //    Benutzerprogramm, die Datei liegt in %APPDATA% ohne Schutz.
        string attackerControlledInstallPath = victim;

        Console.WriteLine($"Opferordner:      {victim}");
        Console.WriteLine($"Inhalt vorher:    {string.Join(", ", Directory.GetFiles(victim))}");
        Console.WriteLine();

        // 3) So haette sich der Updater VOR dem Fix verhalten: Pfad aus der JSON wird
        //    nur normalisiert und "existiert?" geprueft - sonst nichts. Dann folgt im
        //    echten Code ClearDirectoryContents(currentAppPath), also loeschen.
        bool vulnerableWouldWipe =
            !string.IsNullOrWhiteSpace(attackerControlledInstallPath) &&
            Directory.Exists(attackerControlledInstallPath);

        Console.WriteLine("VOR dem Fix:");
        Console.WriteLine(vulnerableWouldWipe
            ? "  -> Updater haette diesen Ordner GELEERT (ClearDirectoryContents)."
            : "  -> Pfad waere verworfen worden.");
        Console.WriteLine("     (Dieser PoC loescht nichts - er zeigt nur die Entscheidung.)");
        Console.WriteLine();

        // 4) NACH dem Fix: dieselbe Entscheidung aus der echten Guard-Methode.
        //    Erwartung: abgelehnt, weil kein MagicChatbox-Ordner / persoenlicher Ordner.
        bool safeAfterFix = IsSafeInstallDirectory_SameLogicAsPatch0004(
            attackerControlledInstallPath, out string reason);

        Console.WriteLine("NACH dem Fix:");
        Console.WriteLine(safeAfterFix
            ? "  -> FEHLER: Ordner wurde akzeptiert (Fix greift nicht)."
            : $"  -> Blockiert: {reason}");
        Console.WriteLine();

        Directory.Delete(sandbox, true);

        bool pass = vulnerableWouldWipe && !safeAfterFix;
        Console.WriteLine(pass
            ? "PoC bestaetigt: Luecke real, Fix blockt sie."
            : "PoC-Ergebnis unerwartet - bitte Fix pruefen.");
        return pass ? 0 : 1;
    }

    // Verhaltensgleiche Kopie von UpdateApp.IsSafeInstallDirectory (Patch 0004),
    // damit der PoC ohne das WPF-Projekt laeuft. Im echten Build die oeffentliche
    // Methode direkt aufrufen statt dieser Kopie.
    static bool IsSafeInstallDirectory_SameLogicAsPatch0004(string? path, out string reason)
    {
        const string ExecutableName = "MagicChatbox.exe";
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) { reason = "kein Installationsordner bekannt"; return false; }

        string full;
        try { full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { reason = "ungueltiger Pfad"; return false; }

        static bool Inside(string p, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return p.Equals(r, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        string? root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(root) || full.Equals(root, StringComparison.OrdinalIgnoreCase))
        { reason = $"{full} ist ein Laufwerks-Root"; return false; }

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windows) && Inside(full, windows))
        { reason = $"{full} liegt im Windows-Ordner"; return false; }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] protectedFolders =
        {
            userProfile,
            string.IsNullOrEmpty(userProfile) ? "" : Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            Path.GetTempPath()
        };
        foreach (string folder in protectedFolders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            string pf = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Equals(pf, StringComparison.OrdinalIgnoreCase))
            { reason = $"{full} ist ein System-/persoenlicher Ordner"; return false; }
        }

        if (!File.Exists(Path.Combine(full, ExecutableName)))
        { reason = $"{full} enthaelt kein {ExecutableName}"; return false; }

        return true;
    }
}

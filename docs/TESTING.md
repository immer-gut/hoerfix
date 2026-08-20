# Hoerfix Testing

Diese Datei sammelt die wichtigsten lokalen Befehle fuer Build, Release-Paket und kurze manuelle Checks.

## Build

```powershell
dotnet build
```

Erwartung: Build endet mit `0 Warnung(en), 0 Fehler`.

## Release-Paket

Vor dem Release-Build pruefen, ob keine alte Setup-EXE aus `dist` laeuft:

```powershell
Get-Process | Where-Object { $_.ProcessName -like '*Hoerfix*' } | Select-Object Id,ProcessName,Path
```

Release-Artefakte erzeugen:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
```

Erwartete Dateien:

```text
dist\Hoerfix-Setup.exe
dist\Hoerfix-win-x64.zip
```

## Manueller Profil-Check

1. Hoerfix starten.
2. Ein anderes Profil als `Standard` auswaehlen oder mit `Speichern als` anlegen.
3. Hoerfix beenden.
4. Hoerfix erneut starten.
5. Erwartung: Das zuletzt verwendete Profil ist direkt ausgewaehlt.

Der zuletzt verwendete Profilname liegt unter:

```text
%APPDATA%\Hoerhilfe\state.json
```

Wenn das gespeicherte Profil geloescht wurde oder die Datei fehlt, startet Hoerfix mit `Standard` oder dem ersten verfuegbaren Profil.

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
```

Erwartung: `Hoerfix-Setup.exe` ist die einzige Datei, die normale Nutzer aus
dem GitHub Release herunterladen muessen. Das Setup installiert Hoerfix ohne
Adminrechte fuer den aktuellen Windows-Benutzer.

Manueller Setup-Check:

1. `dist\Hoerfix-Setup.exe` starten.
2. `Installieren` klicken.
3. Erwartung: Hoerfix liegt unter `%LOCALAPPDATA%\Programs\Hoerfix`.
4. Erwartung: Startmenue-Verknuepfung ist vorhanden.
5. Erwartung: Hoerfix erscheint unter Windows `Installierte Apps` und kann dort entfernt werden.
6. Erwartung: Im Setup-Fenster sind Titel, Checkboxen, Hinweistext und Buttons vollstaendig sichtbar.

## Manueller Systemton-Check

Fuer Systemton mit Browser, Film oder Musik einen virtuellen Audioadapter installieren, z.B. VB-CABLE:

```text
https://vb-audio.com/Cable/
```

Kurztest:

1. VB-CABLE installieren, als Administrator ausfuehren und Windows neu starten.
2. Chrome oder den Player starten und kurz Ton abspielen, damit die App in den Windows-Soundeinstellungen sichtbar ist.
3. Windows 11: `Einstellungen` -> `System` -> `Sound` -> `Lautstaerke-Mixer`.
4. Unter `Apps` bei `Google Chrome` die Ausgabe auf `CABLE Input (VB-Audio Virtual Cable)` stellen.
5. Windows 10: `Einstellungen` -> `System` -> `Sound` -> `Erweiterte Soundoptionen` -> `App-Lautstaerke- und Geraeteeinstellungen`; dort Chrome auf `CABLE Input` stellen.
6. Hoerfix starten und `Systemton (Film/Browser)` waehlen.
7. Quelle: `CABLE Input`.
8. Ausgabe: Kopfhoerer oder anderes echtes Ausgabegeraet.
9. `Starten` klicken und leise testen.

Erwartung: Der Browser-/Player-Ton kommt verstaerkt ueber die gewaehlte Hoerfix-Ausgabe. Quelle und Ausgabe duerfen fuer diesen Test nicht dasselbe Geraet sein.

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

## Manueller Hoerkurven-Wizard-Check

1. Hoerfix starten und ein Ausgabegeraet auswaehlen.
2. `Wizard starten` klicken.
3. Erwartung: Vor dem ersten Messton kommt die Ansage `Linkes Ohr start` nur links oder, falls Windows Speech nicht verfuegbar ist, ein kurzer Start-Klang nur links.
4. Erwartung: `Gehoert (Space)` ist waehrend der Ansage gesperrt und wird erst zum Messton aktiv.
5. Linke Frequenzen durchgehen.
6. Erwartung: Beim Wechsel auf rechts kommt `Rechtes Ohr start` nur rechts oder der Start-Klang nur rechts.
7. Rechte Frequenzen durchgehen.
8. Erwartung: Am Ende meldet Hoerfix, dass beide Ohren gespeichert sind.

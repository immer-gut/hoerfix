# Hoerfix

Kleines Windows-Tool zur Hoerunterstuetzung am PC.

## Funktionen

- Hoerkurve getrennt fuer linkes und rechtes Ohr aufnehmen
- schlecht hoerbare Frequenzen pro Ohr automatisch verstaerken
- Mikrofon oder Systemton von Filmen/Browsern verarbeiten
- mehrere Profile speichern, auswaehlen und loeschen
- zuletzt verwendetes Profil beim Start automatisch laden
- Modus, Quelle, Ausgabe, Theme, Gesamt-Gain und Rauschschwelle pro Profil merken
- Hell-/Dunkelmodus
- Minimieren in den Windows-Systray
- einfache Setup-Skripte fuer lokale Installation

## Start

Release-EXE:

```text
Hoerfix.exe
```

Setup-EXE:

```text
Hoerfix-Setup.exe
```

Setup-Bundle:

```text
Hoerfix-Setup-bundle.zip
```

Script-Setup:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Hoerfix.ps1
```

## Nutzung

1. Kopfhoerer anschliessen und Windows-Lautstaerke niedrig einstellen.
2. Profil waehlen oder mit `Speichern als` ein neues Profil anlegen.
3. Ausgabe-Geraet auswaehlen.
4. Im `Hoerkurven-Wizard` auf `Wizard starten` klicken.
5. Sobald der Ton im aktiven Ohr hoerbar ist, `Space` druecken.
6. Danach `Systemton (Film/Browser)` oder `Mikrofon` waehlen und `Starten` klicken.

Tooltips erklaeren die Regler und Buttons direkt in der App.

## Profile

Profile liegen unter:

```text
%APPDATA%\Hoerhilfe\Profiles
```

## Dokumentation

- [Projekt-Notizen](docs/PROJECT_NOTES.md)
- [Architektur](docs/ARCHITECTURE.md)
- [Test- und Release-Anweisungen](docs/TESTING.md)

## Sicherheit

Hoerfix ist keine medizinisch kalibrierte Hoergeraete-Software. Bei Schmerz,
Druckgefuehl, Pfeifen oder Rueckkopplung sofort stoppen. Immer leise starten.

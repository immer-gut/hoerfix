# Hoerfix

Kleines Windows-Tool zur Hoerunterstuetzung am PC.

Fuer den Systemton-Betrieb mit Filmen, Browsern oder Musik wird ein virtueller
Audioadapter benoetigt, damit die Originalausgabe getrennt in Hoerfix eingespeist
und der verstaerkte Ton auf den Kopfhoerer ausgegeben werden kann. Eine einfache
Option ist [VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/).

## Funktionen

- Hoerkurve getrennt fuer linkes und rechtes Ohr aufnehmen
- kurze Ansage auf dem aktiven Ohr vor dem Hoerkurven-Messton, sonst Start-Klang als Fallback
- schlecht hoerbare Frequenzen pro Ohr automatisch verstaerken
- Mikrofon oder Systemton von Filmen/Browsern verarbeiten
- mehrere Profile speichern, auswaehlen und loeschen
- zuletzt verwendetes Profil beim Start automatisch laden
- Modus, Quelle, Ausgabe, Theme, Gesamt-Gain und Rauschschwelle pro Profil merken
- Hell-/Dunkelmodus
- Minimieren in den Windows-Systray
- einfache Setup-Skripte fuer lokale Installation

## Start

Fuer normale Nutzer:

1. Auf GitHub den neuesten Release oeffnen.
2. `Hoerfix-Setup.exe` herunterladen.
3. Die Datei starten und auf `Installieren` klicken.
4. Hoerfix startet danach ueber das Startmenue oder die Desktop-Verknuepfung.

Der Installer installiert Hoerfix fuer den aktuellen Windows-Benutzer. Es sind
keine Administratorrechte noetig. Ein spaeteres Entfernen ist ueber Windows
`Installierte Apps` moeglich.

Direkter Release-Link: <https://github.com/immer-gut/hoerfix/releases/latest>

## Nutzung

1. Kopfhoerer anschliessen und Windows-Lautstaerke niedrig einstellen.
2. Profil waehlen oder mit `Speichern als` ein neues Profil anlegen.
3. Ausgabe-Geraet auswaehlen.
4. Im `Hoerkurven-Wizard` auf `Wizard starten` klicken.
5. Hoerfix sagt zuerst auf dem aktiven Ohr `Linkes Ohr start` oder `Rechtes Ohr start` an. Wenn Windows keine passende Stimme bereitstellt, kommt stattdessen ein kurzer Start-Klang auf diesem Ohr.
6. Sobald der anschliessende Messton im aktiven Ohr hoerbar ist, `Space` druecken.
7. Danach `Systemton (Film/Browser)` oder `Mikrofon` waehlen und `Starten` klicken.

Tooltips erklaeren die Regler und Buttons direkt in der App.

## Systemton mit virtuellem Audioadapter

Kurzablauf mit VB-CABLE:

1. [VB-CABLE](https://vb-audio.com/Cable/) herunterladen, ZIP entpacken, Setup als Administrator ausfuehren und Windows neu starten.
2. Chrome/Player starten und kurz Ton abspielen, damit Windows die App im Lautstaerke-Mixer anzeigt.
3. Windows 11: `Einstellungen` -> `System` -> `Sound` -> `Lautstaerke-Mixer` oeffnen.
4. Bei `Apps` fuer `Google Chrome` als Ausgabegeraet `CABLE Input (VB-Audio Virtual Cable)` auswaehlen.
5. Windows 10: `Einstellungen` -> `System` -> `Sound` -> `Erweiterte Soundoptionen` -> `App-Lautstaerke- und Geraeteeinstellungen` oeffnen und dort Chrome auf `CABLE Input` stellen.
6. In Hoerfix den Modus `Systemton (Film/Browser)` waehlen.
7. Als Quelle das virtuelle Wiedergabegeraet `CABLE Input` waehlen.
8. Als Ausgabe den Kopfhoerer oder das gewuenschte echte Ausgabegeraet waehlen.
9. Mit `Starten` die Live-Unterstuetzung aktivieren.

Quelle und Ausgabe sollten nicht identisch sein, sonst kann der bereits
verstaerkte Ton erneut aufgenommen werden.

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

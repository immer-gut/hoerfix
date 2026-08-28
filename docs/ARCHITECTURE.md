# Hoerfix Architecture

## Application

Hoerfix is a .NET 9 Windows Forms application. Most UI and logic currently live in `Form1.cs`.

## Audio Flow

- `WasapiLoopbackCapture` records system audio from a selected render device.
- `WasapiCapture` records microphone audio.
- `BufferedWaveProvider` feeds the output pipeline.
- `WasapiOut` plays processed audio to the selected output device.
- `EqualizerProcessor` applies per-channel peaking EQ filters derived from profile bands.

For browser, film, or music audio, the recommended routing uses a virtual audio
adapter such as VB-CABLE (`https://vb-audio.com/Cable/`). The player outputs to
the virtual playback device (`CABLE Input`), Hoerfix captures that render device
through loopback, and Hoerfix plays the processed signal to the real headphones
or speakers.

## Profiles

Profiles are JSON files under:

```text
%APPDATA%\Hoerhilfe\Profiles
```

Each profile stores hearing bands, master gain, noise gate, source mode, source device id,
output device id, and theme.

The last selected profile name is stored separately in:

```text
%APPDATA%\Hoerhilfe\state.json
```

On startup, Hoerfix tries to load that profile first. If the state file is missing,
invalid, or points to a deleted profile, startup falls back to the default profile
or the first available profile.

## UI State

The UI locks conflicting controls while audio processing or the measurement wizard is active.
Minimizing hides the form in the Windows system tray; the tray menu can reopen or exit the app.

The hearing-curve wizard plays a short Windows speech prompt on the active stereo
channel before the rising test tone starts. If Windows speech is unavailable, it
uses a generated per-ear chime instead. During the prompt, the heard button and
Space action are disabled so the prompt is not stored as a hearing threshold.

## Installer

The release script builds a self-contained per-user Windows setup executable.
The installer embeds the self-contained `Hoerfix.exe`, copies it to
`%LOCALAPPDATA%\Programs\Hoerfix`, creates shortcuts, writes a small uninstall
script, and registers the uninstall command for Windows "Installierte Apps".

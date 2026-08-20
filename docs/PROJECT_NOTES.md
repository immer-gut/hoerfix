# Hoerfix Project Notes

Hoerfix is a small Windows desktop hearing-support tool for local PC audio.

## Current Scope

- Measure a per-ear hearing curve with a wizard.
- Store multiple named profiles.
- Load the last used profile on startup when it still exists.
- Remember profile-specific mode, source device, output device, theme, gain, and noise gate.
- Process microphone or system audio with per-channel EQ.
- Minimize to the Windows system tray.
- Provide a light/dark UI.

## Important Limits

- Hoerfix is not a medical hearing aid and is not calibrated as a clinical audiometer.
- System audio support depends on Windows loopback capture for the selected render device.
- Source and output should usually be different devices in system-audio mode to avoid feedback or recapture.

## Release Notes

Release builds use single-file framework-dependent Windows publish output.
Setup is provided as PowerShell scripts that copy `Hoerfix.exe` into the user profile and create shortcuts.
The release script also produces a normal setup bundle ZIP as a fallback when Windows blocks the packed standalone setup EXE.

# Hoerfix Project Notes

Hoerfix is a small Windows desktop hearing-support tool for local PC audio.

## Current Scope

- Measure a per-ear hearing curve with a wizard.
- Announce the active ear briefly before the hearing-curve tone starts, with a per-ear chime fallback if Windows speech is unavailable.
- Store multiple named profiles.
- Load the last used profile on startup when it still exists.
- Remember profile-specific mode, source device, output device, theme, gain, and noise gate.
- Process microphone or system audio with per-channel EQ.
- Minimize to the Windows system tray.
- Provide a light/dark UI.

## Important Limits

- Hoerfix is not a medical hearing aid and is not calibrated as a clinical audiometer.
- System audio support depends on Windows loopback capture for the selected render device.
- Clean system-audio use requires a virtual audio adapter, for example VB-CABLE:
  https://vb-audio.com/Cable/
- Source and output should usually be different devices in system-audio mode to avoid feedback or recapture.

## Release Notes

Release builds produce one primary download for normal users:

```text
dist\Hoerfix-Setup.exe
```

The setup is a self-contained Windows installer. It installs Hoerfix for the
current Windows user, creates Start menu/Desktop shortcuts, registers an
uninstall entry under Windows "Installierte Apps", and can update an existing
per-user installation.

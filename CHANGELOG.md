# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project uses [Semantic Versioning](https://semver.org/).

## [2.0.0] - 2026-10-26

The first public release. Versions 1.x were a private PowerShell script; 2.0
is the same idea rewritten as a single exe.

### Added
- English interface, with Russian as the second language. The language
  follows Windows unless chosen in the settings.
- A settings window. Before, every value was a line in the script.
- A notification before each break (30 seconds by default).
- Optional short breaks by the 20-20-20 rule, in a small window that does
  not take the keyboard focus.
- *Pause reminders* for 30 minutes, 1 hour, 2 hours or until tomorrow.
- A setting that turns off skipping a break.
- A log, `augensicher.log`, and *Support info*, which writes one file to send
  to whoever is helping.
- An *About* window with the licence and the disclaimer.
- A portable mode: `settings.ini` next to the exe keeps everything in that
  folder.
- Tests for the work-time logic, the settings file and the translations.

### Changed
- One `Augensicher.exe` instead of a PowerShell script. No console window
  flashes at start-up.
- Sharp text at 125% and 150% display scaling.

### Fixed
- *Start with Windows* did nothing on Windows 11 24H2 and later, which can
  come without VBScript: the startup entry was a `.vbs` file. It is now an
  ordinary shortcut written through the Windows shell.
- Starting the program twice ran two copies: two icons and two breaks in a
  row. A second start now tells the running copy to show a notification and
  exits.
- An error left no trace, because the script had no window. Errors now go to
  the log and the program keeps running.

## [1.4] - 2026-08-28

Private PowerShell version: work time counts only while the person is at the
computer; locked screen, display off and sleep count as away; full-screen
windows count as work; snooze button; break on every monitor.

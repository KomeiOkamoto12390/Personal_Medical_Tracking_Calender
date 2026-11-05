# Changelog

All notable changes to Medical Calendar are listed here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] — 2026-11-05

First production release.

### Added
- **Today** page: daily log with Medications, Symptoms, Vitals & measurements, Appointments and free-text Notes.
- Built-in categorised item lists with type-to-filter search; custom items are remembered under "My items" and can be removed.
- Edit and delete entries; entries are kept in time order.
- **Calendar** page: month view with colour-coded entry labels and an editable day panel.
- **Export** page: PDF report (summary per category and a day-by-day log) and CSV, filtered by time period and category, with an optional name on the report. Japanese text supported in PDFs.
- **Installer and automatic updates** (Velopack). `MedicalCalendar-Install.exe` installs for the current user with no admin prompt. Installed copies check GitHub Releases on start, download updates in the background (small delta packages) and offer **Restart to update**, or apply them when the app closes. Data is stored separately and is never touched by an update or uninstall. A safety copy is saved to `backups\` before each update.
- Automatic daily backups of the data file (newest 30 kept).
- Recovery from an unreadable data file: the original is kept, and the newest readable backup is loaded.
- Single-instance protection so two windows can't overwrite each other's saves.
- Error log for unexpected errors; the app shows a message and keeps running instead of closing.
- Keyboard shortcuts for moving between days and months, and for adding and cancelling.
- App icon, version shown in the sidebar, window opens maximized.
- Automated tests (`dotnet test`).
- MIT License.

### Known issues (to be fixed in 1.0.1)
- The default entry time isn't refreshed while the app stays open, and the Today page doesn't move to the next day at midnight. Check the time before adding.
- Entry times are stored in the Windows display format; if you change Windows' regional time format, older entries may sort out of order.
- If the very first save after upgrading from the 0.1 prototype fails, prototype notes can be left only in `entries.json.v1.bak`.
- "All time" export ends at today, so future-dated entries (e.g. upcoming appointments) are left out. Use a custom end date to include them.
### Data
- Data is stored in `%LOCALAPPDATA%\MedicalCalendar\data.json` (format version 2). Notes from the earlier prototype (`entries.json`) are migrated automatically.

## [0.1.0] — 2026-11-05

- Prototype: calendar with a free-text note per day.

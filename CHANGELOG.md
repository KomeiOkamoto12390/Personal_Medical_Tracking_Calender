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
- Automatic daily backups of the data file (newest 30 kept).
- Recovery from an unreadable data file: the original is kept, and the newest readable backup is loaded.
- Single-instance protection so two windows can't overwrite each other's saves.
- Error log for unexpected errors; the app shows a message and keeps running instead of closing.
- Keyboard shortcuts for moving between days and months, and for adding and cancelling.

### Data
- Data is stored in `%LOCALAPPDATA%\MedicalCalendar\data.json` (format version 2). Notes from the earlier prototype (`entries.json`) are migrated automatically.

## [0.1.0] — 2026-11-05

- Prototype: calendar with a free-text note per day.

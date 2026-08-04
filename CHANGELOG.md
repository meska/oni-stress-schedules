# Changelog

## Unreleased

## 2.3.1 - 2026-08-05

### Fixed

- Prevented the Schedules screen from crashing when ONI retains a destroyed
  Unity duplicant reference that its standard null-reference cleanup misses.

## 2.3.0 - 2026-08-03

### Added

- Added a private Workshop self-updater that bypasses ONI's stale legacy cache,
  validates the downloaded package, preserves `config.json`, and schedules the
  replacement safely for the next restart.

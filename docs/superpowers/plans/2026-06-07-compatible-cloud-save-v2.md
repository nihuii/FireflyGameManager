# Compatible Cloud Save V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the major unfinished local library, safe save synchronization, WebDAV V2, automatic sync, conflict, security, logging, and UI features while preserving legacy data.

**Architecture:** Add backward-compatible V2 services and SQLite migrations around the existing application. Keep legacy WebDAV operations available, introduce a per-game V2 cloud service, and coordinate automatic save sync through a local state/log repository. Extend existing ViewModels and shared WPF styles rather than adding a second visual system.

**Tech Stack:** C# 14, .NET 10 Windows, WPF, Microsoft.Data.Sqlite, HttpClient WebDAV, System.IO.Compression, SHA-256, Windows DPAPI.

---

### Task 1: Compatible SQLite V2 schema and play sessions

**Files:**
- Modify: `GameManager.App/Models/Game.cs`
- Create: `GameManager.App/Models/PlaySession.cs`
- Create: `GameManager.App/Models/SyncRecord.cs`
- Create: `GameManager.App/Models/SaveSyncState.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing tests proving old databases are backed up and migrated, legacy playtime becomes one session, and new launches create sessions.
- [x] Add optional game V2 fields without breaking existing constructors.
- [x] Add V2 tables and idempotent migration.
- [x] Persist play sessions and derive total playtime safely.
- [x] Run all tests and build.

### Task 2: DPAPI WebDAV credentials

**Files:**
- Create: `GameManager.App/Services/ISecretProtector.cs`
- Create: `GameManager.App/Services/DpapiSecretProtector.cs`
- Modify: `GameManager.App/Services/JsonWebDavSettingsStore.cs`
- Modify: `GameManager.App/Views/WebDavSettingsView.xaml`
- Modify: `GameManager.App/Views/WebDavSettingsView.xaml.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing tests proving saved JSON contains no plaintext password and legacy plaintext migrates.
- [x] Encrypt/decrypt through an injectable protector.
- [x] Replace visible password editor with a masked control.
- [x] Run all tests and build.

### Task 3: Manifest, protected restore, and retention

**Files:**
- Create: `GameManager.App/Models/SaveManifest.cs`
- Create: `GameManager.App/Services/ISaveManifestService.cs`
- Create: `GameManager.App/Services/SaveManifestService.cs`
- Modify: `GameManager.App/Services/ISaveBackupService.cs`
- Modify: `GameManager.App/Services/LocalSaveBackupService.cs`
- Modify: `GameManager.App/Models/AppSettings.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing tests for deterministic manifests, restore-before-backup, and retention cleanup.
- [x] Generate SHA-256 manifests for save directories and ZIP archives.
- [x] Automatically back up existing saves before every restore.
- [x] Enforce configurable backup retention.
- [x] Run all tests and build.

### Task 4: V2 WebDAV per-game protocol and compatibility migration

**Files:**
- Create: `GameManager.App/Models/GameCloudMetadata.cs`
- Create: `GameManager.App/Models/MachineGamePath.cs`
- Create: `GameManager.App/Services/IWebDavGameSyncService.cs`
- Create: `GameManager.App/Services/WebDavGameSyncService.cs`
- Create: `GameManager.App/Services/MachineIdentityService.cs`
- Modify: `GameManager.App/Services/WebDavFullSyncService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing HTTP tests for V2 metadata, machine paths, cover, sessions, manifest, and latest ZIP paths.
- [x] Upload per-game V2 data while continuing legacy sync.
- [x] Import V2 global metadata without overwriting local machine paths.
- [x] Treat first sync with two unknown save versions as conflict.
- [x] Run all tests and build.

### Task 5: Automatic synchronization, conflict resolution, and logs

**Files:**
- Create: `GameManager.App/Services/ISaveSyncCoordinator.cs`
- Create: `GameManager.App/Services/SaveSyncCoordinator.cs`
- Create: `GameManager.App/Services/ISyncLogService.cs`
- Create: `GameManager.App/Services/SqliteSyncLogService.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/ViewModels/WebDavSettingsViewModel.cs`
- Modify: `GameManager.App/Models/AppSettings.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing tests for pre-launch check, post-exit upload, conflict preservation, resolution, retry records, and logs.
- [x] Coordinate hashes and WebDAV operations without blocking local launch on network failure.
- [x] Expose conflict-resolution commands and recent sync records.
- [x] Run all tests and build.

### Task 6: Missing library/detail/settings UI

**Files:**
- Modify: `GameManager.App/ViewModels/GameLibraryViewModel.cs`
- Modify: `GameManager.App/Views/GameLibraryView.xaml`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`
- Modify: `GameManager.App/Views/WebDavSettingsView.xaml`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [x] Add failing tests for search, device-specific launch settings, directory/edit actions, sync status, conflict controls, and log list.
- [x] Add compact controls using shared dynamic styles.
- [x] Keep advanced sync controls secondary and preserve current navigation.
- [x] Run all tests and build.
- [x] Launch the WPF application and visually verify library, detail, sync, and conflict layouts.

---

## Final Compatibility Review

- [x] Allow games without configured save directories and do not block first launch.
- [x] Keep global metadata timestamps unchanged for device-only path and launch-setting edits.
- [x] Preserve backup history after game rename.
- [x] Roll back failed restores and refresh backup history after successful restore.
- [x] Safely clean temporary and invalid downloaded files without masking operation results.
- [x] Treat first-sync cloud-only saves as cloud-newer and verify Keep Both downloads.
- [x] Register single-game V2 uploads in the game index.
- [x] Prevent stale devices from overwriting newer cloud covers.
- [x] Sanitize local cloud-cover filenames and write downloads atomically.
- [x] Isolate startup pull failures per game and apply cover conflict/removal policy.
- [x] Sync administrator launch and local save-sync settings through machine paths.
- [x] Stop full sync after failed downloads, sync metadata for every game, and upload saves only for enabled games.
- [x] Create nested WebDAV directories sequentially and treat missing legacy remote data as an empty first sync.
- [x] Inject sync logging so lightweight view-model tests stay isolated while the real application keeps SQLite log persistence.
- [x] Verify 151 registered regression tests, clean build, and real WPF rendering at 150% DPI.

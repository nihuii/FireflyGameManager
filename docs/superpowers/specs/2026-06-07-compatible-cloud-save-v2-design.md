# Firefly Compatible Cloud Save V2 Design

## Goal

Upgrade the existing local-first game manager into a safe multi-device cloud-save manager without losing or overwriting existing local SQLite data, local ZIP backups, or legacy WebDAV files.

## Compatibility Rules

- Existing `app.db` is upgraded in place after creating a one-time pre-V2 backup.
- Existing `metadata/app.db` and `save-backups/**` WebDAV paths remain readable and are never deleted automatically.
- V2 data is written under `v2/` using per-game and per-device paths.
- First V2 sync establishes a baseline. If local and remote saves both exist without a common baseline, the result is a conflict and neither side is overwritten.
- Restore, cloud download, and conflict resolution always create a local backup first.

## Local Data

The `games` table gains working-directory, monitored-process, and per-game sync fields. New tables store play sessions, sync records, save sync state, migration records, and local deletion tombstones. Legacy total playtime is converted into one deterministic legacy session, while future launches create individual sessions. Tombstones prevent a locally deleted cloud game from reappearing during the next pull.

## Cloud Data

```text
v2/
  games-index.json
  games/{gameId}/
    metadata.json
    cover/{fileName}
    paths/{machineId}.json
    play-sessions/{sessionId}.json
    saves/latest.zip
    saves/save-manifest.json
```

Absolute executable, installation, and save paths are device-specific and never merged into another machine's local paths.

## Save Synchronization

A manifest contains a deterministic combined SHA-256 hash plus file hashes, sizes, and relative paths. The last synchronized hash is stored locally.

- Local changed only: upload.
- Remote changed only: create local backup, then download and restore.
- Both changed: create a pending conflict and wait for user selection.
- Equal hashes: mark synchronized without transfer.

Automatic checks can run before launch and after exit. A newer cloud save requires confirmation before launch. Failures are logged, retained as retry-pending state, and never prevent local-only operation. Save archives upload before their manifests, and restored archives are verified against the remote manifest before being marked synchronized.

On application startup, configured WebDAV accounts perform a read-only V2 metadata pull. This merges the cloud game list, covers, play sessions, and the current machine's path file without uploading or restoring saves.

## Security

The WebDAV application password is stored using Windows DPAPI. Loading a legacy plaintext configuration migrates it immediately to encrypted storage. The password editor uses a masked password control.

## UI

The game library gains search. Game details gain edit and directory actions, sync status, and compact conflict actions. The sync page gains a restrained recent-log list. All controls use existing dynamic theme resources, shared card styles, and shared button styles.

## Verification

Each batch adds tests before implementation. Final verification includes the complete console regression suite, a clean build, database migration tests, WebDAV protocol tests, and actual WPF screenshots of library, detail, sync, and conflict states.

## Implemented Safety Guarantees

- The save directory is optional. A missing directory does not block first launch, create an empty upload, or trigger a false first-sync conflict.
- Device-only edits do not advance global metadata timestamps. Game name and cover remain the global conflict boundary.
- Local backup history follows the stable game ID across renames. Restore creates a protection archive, rolls back after extraction failure, and refreshes the visible history.
- Every cloud archive is verified against its manifest before restore or Keep Both completion. Invalid and temporary downloads are cleaned without hiding the original result.
- V2 single-game uploads merge their ID into `games-index.json`. Stale devices cannot overwrite newer cover bytes.
- Cloud cover and save downloads use safe local names and sibling temporary files followed by atomic replacement.
- Startup metadata pull isolates failures per game. Newer local covers are preserved, while a winning remote cover removal clears the local cover reference.
- Machine path files include administrator launch and the per-device save-sync toggle while legacy files remain readable.
- Full sync requires successful remote downloads before any upload. It publishes metadata for every game and save archives only for games with save sync enabled.
- Manual sync, V2 sync, and connection testing create nested WebDAV directories in parent-first order. Missing legacy remote data is a valid empty first-sync state.
- Sync logging is injected into the shell. Lightweight/test shells use memory-only logs, while the real WPF shell keeps SQLite-backed recent sync records.
- The final sync page uses the shared modern scrollbar and was visually verified with the running WPF application at 150% DPI.

## Final Verification Result

- Console regression suite: 151 tests passed.
- Build: 0 warnings, 0 errors.
- Static checks: no duplicate test registrations and no `git diff --check` errors.
- Visual QA: custom title bar, rounded frame, navigation, library, and sync page rendered correctly; the remaining system scrollbar was replaced with the shared modern style.

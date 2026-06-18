# Stage 2/3 Completion Design

## Goal

Close the synchronization-safety, batch-matching, collection-editing, and search-quality gaps documented in `阶段2、3未实现功能.md` without regressing the existing Bangumi import and WebDAV V2 flows.

## Scope

### Stage 2: external metadata conflict safety

- A pending `ExternalMetadataConflict` blocks every V2 external-metadata upload for that game.
- External metadata is uploaded through one dedicated path. `UploadGameAsync` no longer writes `external-metadata.json` as a side effect.
- Resolving a conflict with local data, cloud data, or unlinking clears the conflict. The next full sync uploads the selected resolved state when a linked snapshot remains.
- Upload, download, skipped-conflict, success, and failure outcomes are recorded with sync type `external-metadata`.
- Log messages contain only game ID/name, provider, subject ID, direction, and compact outcome. They never contain access tokens, passwords, comments, private notes, or long summaries.

### Stage 3: batch metadata matching

- Batch search keeps up to five candidates per game.
- A candidate is automatically selected only when the normalized local title exactly matches a candidate display name, localized name, original name, or alias.
- Other candidates remain available for manual selection and are labeled as requiring confirmation.
- Each failed or unmatched row can be retried independently. A full-batch retry remains available.
- Applying a candidate uses `LookupDetailsAsync(gameName)` so Bangumi authenticated lookup and legacy-large fallback are preserved.
- Existing-game defaults come from `MetadataImportOptions.ForExistingGame`. Name and cover are not overwritten by default; summary, date, developer, publisher, and tags are imported. Explicit batch options may enable name and cover.
- Cover import uses the existing remote-image cache and remains non-fatal when download fails.

### Stage 3: collection fields

- `BangumiCollectionState` adds remote `IsPrivate` plus local-only `PrivateNote` and `ProgressPercent` fields.
- `ProgressPercent` is clamped to 0-100. It is local-only because the Bangumi game collection API does not expose a game-progress field.
- `PrivateNote` is local-only and stays in the private collection cache, which existing export and WebDAV safeguards already exclude.
- The collection form remains visible for linked Bangumi games when the account is disconnected or requires reconnection. Remote actions are disabled and a reconnect message is shown.
- Save requests always include rating and comment, including rating `0` and an empty comment, so remote values can be cleared.
- The supported remote privacy flag is sent and read as `private`.

### Stage 3: search quality

- Search results expose aliases and developer auxiliary information when the upstream response provides them.
- Ranking considers display name, localized name, original name, and aliases.
- Title normalization uses Unicode FormKC and retains Unicode letters and digits.
- Token matching is order-independent: all normalized query tokens must occur across candidate title tokens for a strong token match.
- Exact normalized matches remain stronger than token matches, prefix matches, and substring matches.

## Architecture

- `ExternalMetadataSyncPolicy` owns the upload-block decision and compact log description.
- `WebDavFullSyncService` orchestrates external snapshot download, merge, conflict logging, upload gating, and outcome logging.
- `MetadataMatchScorer` owns title normalization, tokenization, exact-match confidence, and candidate ranking.
- `ManageGameItemViewModel` owns candidate collection, selected candidate, confidence state, and row status.
- `MetadataImportCoordinator` owns details lookup, field merging, optional image caching, and persistence. Add/edit and batch flows use the same coordinator behavior.
- SQLite migrations add collection fields with backward-compatible defaults.

## UI Behavior

- The management page keeps the existing card layout. Each card gains a compact candidate selector, auxiliary text, and retry action.
- High-confidence rows are preselected but are never applied until the user checks the row and invokes apply.
- Low-confidence rows show “需手动选择” and remain unselected.
- The collection section adds privacy, private note, and progress controls. Reconnect state is shown inline instead of hiding the section.

## Error Handling

- One game's search, lookup, cover, or persistence failure does not stop the rest of the batch.
- Cancellation stops pending batch work without applying unconfirmed rows.
- External metadata conflicts never fail unrelated game metadata, play-session, or save-backup synchronization.
- Logs use compact sanitized messages produced by policy/helpers rather than raw exception payloads when secrets may be present.

## Verification

- RED/GREEN tests cover conflict upload blocking, all three resolution paths, next-sync upload, and sanitized complete logs.
- RED/GREEN tests cover exact-only auto-selection, manual candidate switching, row retry, import options, cover caching, and lookup fallback.
- RED/GREEN tests cover privacy, local note/progress persistence, reconnect visibility, and remote clearing of rating/comment.
- RED/GREEN tests cover aliases, developer auxiliary text, and order-independent token ranking.
- The full console test suite, no-restore build, and `git diff --check` must pass.

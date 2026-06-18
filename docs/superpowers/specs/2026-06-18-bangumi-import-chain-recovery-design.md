# Bangumi Import Chain Recovery Design

## Problem

When Bangumi subject details return a transient `404`, the edit view falls back to search-result data. That fallback currently clears the online association (`IsLinked=false`, empty `SubjectUrl`). Legacy search can also return an `http://lain.bgm.tv` cover URL, while the image cache accepts HTTPS only. The resulting row is persisted but has no summary, cannot refresh, cannot open its source, cannot unlink, and does not update the cover.

The affected local database confirms this exact state for subject `172612`. The official V0 subject endpoint currently returns the complete record, so the subject itself is valid.

## Approved Approach

Use a self-healing import chain:

1. Retry a subject detail request once when the first response is `404`.
2. Treat a search-result fallback with a valid provider and subject ID as a real online association. Generate the canonical Bangumi subject URL and clearly label the preview as partial.
3. Upgrade known Bangumi image hosts from HTTP or protocol-relative URLs to HTTPS before the image cache sees them.
4. Repair only legacy degraded rows matching this signature: provider is Bangumi, subject ID is non-empty, and subject URL is empty. Set the canonical source URL and restore the link. Do not relink intentionally unlinked rows that already retain their source URL.
5. Protect the complete flow with deterministic tests: retry, fallback identity, image normalization, database repair, and edit/import/save/reload/detail command state.

## Data Flow

`BangumiApiClient` fetches and retries details, `BangumiDtoMapper` normalizes remote URLs, `AddGameViewModel` creates either a complete or partial linked preview, `SqliteGameLibraryService` persists it and repairs legacy degraded rows, and `GameDetailViewModel` receives a non-null linked metadata snapshot with a usable source URL.

## Error Handling

Only `404` receives one additional request. Other status handling remains unchanged. A second `404` still produces a partial preview so local editing remains available. Cover download failure remains non-fatal and is shown inline.

## Verification

The test runner must demonstrate a red-green cycle for each changed behavior, then all tests and a no-restore build must pass. The repaired user database will be verified read-only after launching the rebuilt application once.

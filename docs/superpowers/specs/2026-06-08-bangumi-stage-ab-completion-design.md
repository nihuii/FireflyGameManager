# Bangumi Stage A/B Completion Design

## Goal

Complete the remaining Stage A/B behavior from `Bangumi账号与游戏信息导入实现方案.md` while preserving the existing WPF navigation and visual language.

## Scope

- Add field-level metadata import options for name, cover, summary, release date, developer, publisher, and tags.
- Show richer search results and an explicit selected-subject detail preview before applying metadata.
- Cache search results for ten minutes and allow active metadata requests to be cancelled.
- Refresh linked metadata through an inline difference preview; apply only fields confirmed by the user.
- Add a collapsible long-summary presentation on the game detail page.
- Apply a 15-second Bangumi request timeout, one retry for a single 5xx response, and clear messages for 401/403/404/429/5xx/timeout.
- Mark a saved Bangumi account as requiring reconnection when an authenticated request is rejected.
- Decode downloaded JPEG/PNG bytes before accepting them as a cover.
- Distinguish collection creation (`POST`) from modification (`PATCH`).

## Boundaries

- Keep metadata search inside the existing add/edit page and account settings inside the existing settings page.
- Do not add a new top-level navigation item.
- Do not implement OAuth, additional metadata providers, batch matching, or WebDAV Stage C.
- The current official Bangumi API documents no endpoint for deleting a subject collection. `None` remains the local representation of a missing collection, but Firefly will not send an undocumented delete request.

## Architecture

`MetadataImportOptions` owns field selection and metadata merging. `BangumiGameMetadataProvider` owns the ten-minute public search cache. `BangumiApiClient` owns request timeout, retry, status translation, and POST/PATCH selection. Existing page view models coordinate cancellation tokens and inline previews without adding modal dialogs.

`BangumiAccount.RequiresReconnect` is persisted in the encrypted local account record. Authenticated detail-page operations mark the account when the API reports an authentication failure; imported game data remains untouched.

## UI

The add/edit metadata area becomes a compact three-step flow: search, preview selected result, apply selected fields. Results display the cover thumbnail, localized/original names, date, and summary preview.

The game detail metadata card shows an inline refresh-difference area after downloading newer data. The user can select fields and apply or cancel. Long summaries show a compact preview with an expand/collapse command.

## Verification

Add focused console tests for each behavior, then run the complete test suite and a clean build. Launch the WPF application and visually inspect add/edit metadata search, settings account state, and game-detail metadata controls.

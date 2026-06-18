# Provider Registry and SteamGridDB Design

## Goal

Introduce a configurable provider architecture and add SteamGridDB as the first non-Bangumi provider while preserving Bangumi as the primary metadata association and using SteamGridDB as an independent artwork overlay.

## Considered Approaches

1. **Capability registry with separate metadata and artwork associations (selected).** Providers declare metadata and artwork capabilities. Bangumi can remain the primary metadata source while SteamGridDB supplies cover and hero artwork.
2. **One active provider per game.** This is smaller, but selecting SteamGridDB would replace the Bangumi subject link and remove collection functionality.
3. **Runtime-loaded plugin assemblies.** This offers maximum extensibility but adds packaging, compatibility, and trust boundaries that are unnecessary before RAWG, VNDB, and IGDB are implemented.

## Provider Contracts

- `IGameDataProvider` exposes `ProviderId`, `DisplayName`, `RequiresApiKey`, and capability flags.
- `IGameMetadataProvider` retains search and details lookup behavior for primary metadata.
- `IGameArtworkProvider` exposes game search and artwork lookup for covers and hero backgrounds.
- `IGameDataProviderRegistry` returns registered providers, enabled providers in user order, and typed metadata/artwork providers.
- Bangumi implements metadata capability. SteamGridDB implements artwork capability and may expose sparse game identity data only for candidate display.

## Settings and Security

- `MetadataProviderSettings` stores enabled state, sort order, default metadata provider, default artwork provider, and protected API credentials.
- `JsonMetadataProviderSettingsStore` writes `metadata-provider-settings.json` under the application data directory.
- API keys are protected with the existing DPAPI secret protector before serialization.
- Decrypted keys exist only in memory while constructing requests. Keys are never written to SQLite, sync logs, data exports, WebDAV payloads, status messages, or exception text.
- Bangumi is enabled by default. SteamGridDB is disabled until a valid API key is saved.

## SteamGridDB Integration

- Base URL: `https://www.steamgriddb.com/api/v2`.
- Requests use `Authorization: Bearer <api-key>`.
- Game search uses `/search/autocomplete/{query}`.
- Game identity uses `/games/id/{id}` when details are needed.
- Cover artwork uses `/grids/game/{id}` and prefers safe portrait assets by score, dimensions, and HTTPS availability.
- Background artwork uses `/heroes/game/{id}` and prefers safe landscape assets by score and HTTPS availability.
- NSFW, humor, and epilepsy-sensitive results are excluded by request filters and checked again during mapping when tags are present.
- HTTP 401/403 marks SteamGridDB configuration as needing attention. Rate limits and transient failures affect only SteamGridDB.

## Artwork Model and Persistence

- `ExternalArtworkMetadata` stores game ID, provider, provider game ID, cover URL/cache path, background URL/cache path, source update time, and local update time.
- Artwork identity is separate from `ExternalGameMetadata`; importing SteamGridDB artwork never removes a Bangumi link.
- SQLite receives a backward-compatible `game_external_artwork` table.
- `ExternalArtworkCloudSnapshot` carries provider identity and remote URLs but never credentials or local absolute paths.
- WebDAV V2 stores artwork at `v2/games/{gameId}/external-artwork.json` and cached image files under the game's cover/background directories.

## Cache Isolation

- Remote image caching accepts a provider ID and asset kind.
- Files are stored under provider-specific cover/background subdirectories.
- Existing HTTPS, size, file-signature, decode, and atomic-write protections remain mandatory.

## Application Flow

- The composition root registers Bangumi and SteamGridDB, builds the registry from saved settings, and injects the registry into view models.
- Add/edit screens expose a metadata source selector and an artwork source selector. Defaults come from enabled provider order.
- SteamGridDB search shows up to five games. Selecting a game loads several cover/hero candidates, with the highest-ranked safe assets preselected.
- The user can import cover, background, or both. No artwork is persisted before explicit confirmation.
- Batch matching exposes the selected metadata provider; SteamGridDB artwork matching remains an explicit separate action so a title match cannot silently replace artwork.
- Game detail displays the cached hero background when available and exposes refresh/unlink artwork actions independently of metadata refresh/unlink.

## Future Providers

- RAWG, VNDB, and IGDB are added later by implementing provider contracts and registering settings metadata.
- Provider-specific DTO mapping, authentication, cache, and errors stay inside each provider package.
- No future provider may assume another provider's subject IDs are compatible.

## Error Handling

- Registry lookup returns a clear disabled/unconfigured state rather than throwing for missing API keys.
- Search and artwork failures are scoped to the selected provider and preserve existing local metadata/artwork.
- Invalid or oversized images are rejected by the shared cache service and reported as non-fatal import warnings.

## Verification

- Contract tests cover registry ordering, enablement, capability filtering, and provider isolation.
- Security tests cover DPAPI round trips and absence of plaintext API keys from files, exports, WebDAV, and logs.
- HTTP contract tests cover Bearer authentication, search, grids, heroes, 401/403, rate limiting, malformed JSON, and cancellation.
- Persistence and sync tests cover artwork SQLite migration, metadata/artwork independence, provider-aware cache paths, and WebDAV round trips.
- View-model/XAML tests cover provider selectors, explicit confirmation, independent artwork actions, and unchanged Bangumi behavior.
- The full console test suite, no-restore build, and `git diff --check` must pass.

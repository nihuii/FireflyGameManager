# Provider Registry and SteamGridDB Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable provider registration and SteamGridDB cover/hero artwork without replacing Bangumi metadata links.

**Architecture:** Providers declare capabilities and are selected through a registry backed by protected settings. Primary metadata and artwork overlays use separate models, persistence, caching, and WebDAV snapshots.

**Tech Stack:** .NET 10, WPF, C#, HttpClient, System.Text.Json, DPAPI, Microsoft.Data.Sqlite, WebDAV V2

---

### Task 1: Add provider contracts, registry, and protected settings

**Files:**
- Create: `GameManager.App/Models/MetadataProviderSettings.cs`
- Create: `GameManager.App/Services/IGameDataProvider.cs`
- Create: `GameManager.App/Services/IGameArtworkProvider.cs`
- Create: `GameManager.App/Services/IGameDataProviderRegistry.cs`
- Create: `GameManager.App/Services/GameDataProviderRegistry.cs`
- Create: `GameManager.App/Services/IMetadataProviderSettingsStore.cs`
- Create: `GameManager.App/Services/JsonMetadataProviderSettingsStore.cs`
- Modify: `GameManager.App/Services/IGameMetadataProvider.cs`
- Modify: `GameManager.App/Services/BangumiGameMetadataProvider.cs`
- Modify: `GameManager.App/Services/AppPaths.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing registry/security tests**

Test provider ordering, enabled filtering, metadata/artwork capability filtering, default Bangumi settings, protected-key round trip, and absence of plaintext API keys in the JSON file.

- [ ] **Step 2: Run tests and verify RED**

Expected: compile failure because contracts and settings do not exist.

- [ ] **Step 3: Implement contracts and registry**

Define:

```csharp
[Flags]
public enum GameDataProviderCapabilities { None = 0, Metadata = 1, Artwork = 2 }

public interface IGameDataProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool RequiresApiKey { get; }
    GameDataProviderCapabilities Capabilities { get; }
}
```

Add typed provider interfaces and a registry that returns enabled providers in saved order. Extend Bangumi with `DisplayName => "Bangumi"`, `RequiresApiKey => false`, and metadata capability.

- [ ] **Step 4: Implement protected settings**

Store enabled/order/default fields plus DPAPI-protected credentials in `metadata-provider-settings.json`. Never expose protected text through status messages or `ToString()`.

- [ ] **Step 5: Run tests and verify GREEN**

Run: `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj`

Expected: registry and secret-store tests pass.

### Task 2: Implement the SteamGridDB HTTP client and artwork provider

**Files:**
- Create: `GameManager.App/Models/GameArtworkSearchResult.cs`
- Create: `GameManager.App/Models/GameArtworkCandidate.cs`
- Create: `GameManager.App/Services/ISteamGridDbApiClient.cs`
- Create: `GameManager.App/Services/SteamGridDbApiClient.cs`
- Create: `GameManager.App/Services/SteamGridDbArtworkProvider.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing HTTP contract tests**

Using the existing recording HTTP handler pattern, assert Bearer auth and encoded paths for `/search/autocomplete/{query}`, `/games/id/{id}`, `/grids/game/{id}`, and `/heroes/game/{id}`. Add 401/403, 429, malformed JSON, HTTPS filtering, unsafe-tag filtering, score ordering, and cancellation tests.

- [ ] **Step 2: Run tests and verify RED**

Expected: compile failure because the client/provider do not exist.

- [ ] **Step 3: Implement DTO mapping and client**

Parse `{ success, data, errors }`, throw a provider-specific exception with sanitized messages, add `Authorization: Bearer`, and map safe HTTPS assets. Rank portrait grids and landscape heroes by dimensions, score, and votes.

- [ ] **Step 4: Implement provider behavior**

Return up to five game identities from search and grouped cover/background candidates from artwork lookup. Missing credentials return an unconfigured status without sending a request.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command; expect SteamGridDB contract tests to pass.

### Task 3: Add independent artwork persistence, cache paths, and WebDAV V2

**Files:**
- Create: `GameManager.App/Models/ExternalArtworkMetadata.cs`
- Create: `GameManager.App/Models/ExternalArtworkCloudSnapshot.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Modify: `GameManager.App/Services/DesignGameLibraryService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/IRemoteImageCacheService.cs`
- Modify: `GameManager.App/Services/RemoteImageCacheService.cs`
- Modify: `GameManager.App/Services/IWebDavGameSyncService.cs`
- Modify: `GameManager.App/Services/WebDavGameSyncService.cs`
- Modify: `GameManager.App/Services/WebDavFullSyncService.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing persistence/sync tests**

Assert SQLite migration and reopen, independent Bangumi metadata plus SteamGridDB artwork, provider/kind-specific cache paths, cloud snapshots without local paths/keys, WebDAV round trips, and legacy databases with no artwork row.

- [ ] **Step 2: Run tests and verify RED**

Expected: compile failure because artwork models and APIs do not exist.

- [ ] **Step 3: Implement models and SQLite table**

Create `game_external_artwork` keyed by game ID with provider, provider game ID, remote cover/background URLs, local cache paths, and timestamps. Add get/update/unlink methods without changing `game_external_metadata`.

- [ ] **Step 4: Isolate caches and sync snapshots**

Extend cache calls with provider and asset kind; sanitize both path segments. Add `external-artwork.json` download/upload and merge it independently in full sync.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command; expect artwork persistence/cache/sync tests to pass.

### Task 4: Compose the registry and expose provider settings

**Files:**
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `GameManager.App/ViewModels/AppearanceSettingsViewModel.cs`
- Modify: `GameManager.App/Views/AppearanceSettingsView.xaml`
- Modify: `GameManager.App/Views/AppearanceSettingsView.xaml.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing composition/settings tests**

Assert Bangumi remains enabled by default, SteamGridDB stays disabled without a key, saving a key enables connection testing, ordering/default changes persist, and reset clears protected provider credentials.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because production composition injects one metadata provider and settings expose only Bangumi.

- [ ] **Step 3: Build and inject the registry**

Create the settings store and both providers in the composition root, register them, and inject the registry into main/settings/add/manage/detail view models while retaining compatibility constructors for tests/design data.

- [ ] **Step 4: Add text-based provider settings UI**

In “账号与数据源”, add enabled/default/order controls and a SteamGridDB password box, save/test/clear commands, and sanitized status text. Do not display the saved key.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command; expect settings, reset, and XAML tests to pass.

### Task 5: Add artwork selection, import, refresh, and unlink flows

**Files:**
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`
- Modify: `GameManager.App/ViewModels/ManageGameLibraryViewModel.cs`
- Modify: `GameManager.App/Views/ManageGameLibraryView.xaml`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing interaction tests**

Assert metadata and artwork provider selectors are independent; SteamGridDB search retains up to five games and multiple assets; no artwork persists before confirmation; cover/background imports cache independently; batch metadata matching does not invoke artwork; detail refresh/unlink artwork does not alter Bangumi metadata.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because registry and artwork state are not exposed by view models.

- [ ] **Step 3: Implement add/edit artwork workflow**

Expose enabled provider options, selected artwork provider, game results, cover candidates, background candidates, and explicit import flags. Persist only after import confirmation.

- [ ] **Step 4: Implement management/detail integration**

Add a separate artwork-match action in management. On detail, show cached background, source/status, refresh, and unlink commands independently from metadata commands.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command; expect provider-selection, confirmation, isolation, and XAML tests to pass.

### Task 6: Stage 6 verification and documentation

**Files:**
- Modify: `未实现功能实现方案.md`
- Modify: `新对话上下文_FireflyGameManager.md`

- [ ] **Step 1: Run full tests and build**

Run:

```powershell
dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj
dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore
git diff --check
```

Expected: tests exit 0, build has 0 errors, and diff check has no whitespace errors.

- [ ] **Step 2: Verify security boundaries**

Save a synthetic SteamGridDB key through the test store and scan generated provider settings, exported data, WebDAV requests, and sync logs. The plaintext key must occur in none of them.

- [ ] **Step 3: Update project status**

Document Provider registry and SteamGridDB as complete, and list RAWG, VNDB, and IGDB as separate future provider implementations.


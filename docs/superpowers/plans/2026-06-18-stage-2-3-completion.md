# Stage 2/3 Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every synchronization, batch matching, collection, and search gap listed in `阶段2、3未实现功能.md`.

**Architecture:** Centralize external-metadata upload policy, title confidence scoring, and batch import preparation in focused services. Preserve the existing ViewModel and SQLite architecture while adding backward-compatible fields and per-row batch state.

**Tech Stack:** .NET 10, WPF, C#, Microsoft.Data.Sqlite, HttpClient, existing console test harness

---

### Task 1: Block conflicted external-metadata uploads and complete logging

**Files:**
- Create: `GameManager.App/Services/ExternalMetadataSyncPolicy.cs`
- Modify: `GameManager.App/Services/WebDavGameSyncService.cs`
- Modify: `GameManager.App/Services/WebDavFullSyncService.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing tests**

Register tests that create a pending conflict, run full sync, and assert no `external-metadata.json` PUT occurs; resolve with local/cloud/unlink and assert the next sync uploads only the remaining linked snapshot. Add a recording `ISyncLogService` assertion requiring `download/success`, `download/conflict`, `upload/success`, and `upload/failure` records whose messages exclude a seeded token, password, comment, private note, and long summary.

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj`

Expected: FAIL because `UploadGameAsync` writes external metadata before conflict gating and full sync does not log all outcomes.

- [ ] **Step 3: Add the upload policy**

Implement:

```csharp
public static class ExternalMetadataSyncPolicy
{
    public static bool CanUpload(IGameLibraryService library, string gameId) =>
        library.GetExternalMetadataConflict(gameId) is null;

    public static string Describe(ExternalGameMetadataCloudSnapshot snapshot, string outcome) =>
        $"{outcome}：provider={snapshot.Provider}，subject={snapshot.SubjectId}，game={snapshot.GameId}";
}
```

Remove the external-metadata PUT from `WebDavGameSyncService.UploadGameAsync`. In `WebDavFullSyncService`, inject optional `ISyncLogService`, log compact download outcomes, skip upload while a conflict exists, and use only `UploadExternalMetadataAsync` for allowed snapshots.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command; expect all Stage 2 tests to pass.

### Task 2: Add exact confidence scoring, aliases, developer, and token ranking

**Files:**
- Create: `GameManager.App/Services/MetadataMatchScorer.cs`
- Modify: `GameManager.App/Models/GameMetadataSearchResult.cs`
- Modify: `GameManager.App/Services/BangumiDtoMapper.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing ranking tests**

Add tests asserting aliases participate in exact matching, developer appears in `AuxiliaryInfo`, `"千恋 万花"` matches `"千恋＊万花"`, and reversed tokens such as `"万花 千恋"` receive the same token score. Assert unrelated first results are not high confidence.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because aliases/developer do not exist and current ranking concatenates the whole query.

- [ ] **Step 3: Implement result fields and scorer**

Add init-only properties to preserve positional constructor compatibility:

```csharp
public IReadOnlyList<string> Aliases { get; init; } = [];
public string Developer { get; init; } = string.Empty;
```

Implement `MetadataMatchScorer.Score(query, result)` and `IsExactMatch(query, result)` using FormKC, Unicode letters/digits, token sets, and candidate names from display/localized/original/aliases. Use the scorer in `BangumiApiClient` ordering and map aliases/developer when present.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command; expect the new search tests and existing Bangumi ranking tests to pass.

### Task 3: Complete batch candidates, manual selection, and row retry

**Files:**
- Modify: `GameManager.App/ViewModels/ManageGameItemViewModel.cs`
- Modify: `GameManager.App/ViewModels/ManageGameLibraryViewModel.cs`
- Modify: `GameManager.App/Views/ManageGameLibraryView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing ViewModel tests**

Add tests that return five candidates with the first unrelated and a later exact alias match. Assert all five are retained, only the exact candidate is selected automatically, low-confidence rows remain unselected with `需手动选择`, changing `SelectedMetadataMatchResult` enables apply, and `RetryMetadataMatchCommand` searches only the supplied row.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because each row stores only one result and has no retry command.

- [ ] **Step 3: Add candidate state and commands**

Add:

```csharp
public ObservableCollection<GameMetadataSearchResult> MetadataMatchCandidates { get; } = [];
public GameMetadataSearchResult? SelectedMetadataMatchResult { get; set; }
public bool IsHighConfidenceMatch { get; set; }
```

Populate at most five results. Auto-select only `MetadataMatchScorer.IsExactMatch`. Add an async command accepting `ManageGameItemViewModel` and reuse one `MatchItemAsync` method for full batch and row retry.

- [ ] **Step 4: Add compact text UI**

Bind a `ComboBox` to candidates and selected result, show `AuxiliaryInfo` plus `CompactSummaryPreview`, and add a small retry button per card. Keep apply gated by row check plus explicit candidate selection.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command; expect batch and XAML binding tests to pass.

### Task 4: Reuse lookup, import options, and cover cache in batch apply

**Files:**
- Create: `GameManager.App/Services/MetadataImportCoordinator.cs`
- Modify: `GameManager.App/ViewModels/ManageGameLibraryViewModel.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing batch import tests**

Assert apply calls `LookupDetailsAsync(subjectId, game.Name)`, merges with `MetadataImportOptions.ForExistingGame`, preserves the local name/cover by default, imports descriptive fields, and optionally downloads/replaces the cover when `ImportCover` is enabled. Assert one cover failure does not stop other rows.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because batch apply calls `GetDetailsAsync` and writes the complete snapshot directly.

- [ ] **Step 3: Implement the coordinator**

Create a coordinator that accepts provider, image cache, and import options, performs lookup, merges metadata, optionally caches the cover, and returns a result containing merged metadata, optional new display name, optional cached cover, and warning text. Inject it into management flow and persist through `IGameLibraryService`.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command; expect lookup fallback, merge, and non-fatal cover tests to pass.

### Task 5: Complete Bangumi collection fields and reconnect behavior

**Files:**
- Modify: `GameManager.App/Models/BangumiCollectionState.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add failing model/API/UI tests**

Add tests for `IsPrivate`, local `PrivateNote`, clamped `ProgressPercent`, SQLite reopen persistence, JSON payload containing `"rate":0`, `"comment":""`, and `"private":true`, and a reconnecting account that keeps the collection section visible while disabling remote commands.

- [ ] **Step 2: Run tests and verify RED**

Expected: FAIL because fields, columns, payload values, and reconnect presentation do not exist.

- [ ] **Step 3: Add backward-compatible fields and migration**

Add init properties to `BangumiCollectionState` and SQLite columns `is_private`, `private_note`, and `progress_percent` with safe defaults. Clamp progress on ViewModel assignment and persistence.

- [ ] **Step 4: Correct API mapping and payload**

Always include `rate`, `comment`, and `private` when saving. Read the remote `private` field. Keep private note/progress local and out of HTTP payloads.

- [ ] **Step 5: Update detail presentation**

Keep the section visible for linked Bangumi metadata; show reconnect text when required; bind privacy, note, and progress controls; disable refresh/save until a valid account exists.

- [ ] **Step 6: Run tests and verify GREEN**

Run the full test command; expect collection persistence, API, reconnect, and XAML tests to pass.

### Task 6: Stage 2/3 verification

**Files:**
- Modify if status changed: `未实现功能实现方案.md`
- Modify if status changed: `阶段2、3未实现功能.md`

- [ ] **Step 1: Run full tests**

Run: `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj`

Expected: every registered test prints `PASS` and process exits 0.

- [ ] **Step 2: Run build**

Run: `dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore`

Expected: exit 0 with 0 errors.

- [ ] **Step 3: Check formatting and requirement coverage**

Run: `git diff --check`

Expected: no whitespace errors. Re-read every item in `阶段2、3未实现功能.md` and mark it implemented only when backed by a passing test.


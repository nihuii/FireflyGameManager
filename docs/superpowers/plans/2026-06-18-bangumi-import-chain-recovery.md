# Bangumi Import Chain Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Bangumi search, detail preview, selected-field import, cover caching, SQLite persistence, and detail-page actions recover correctly from transient missing-detail responses and previously degraded rows.

**Architecture:** Keep the existing provider/view-model/library boundaries. Add one bounded retry at the API boundary, preserve identity in fallback metadata, normalize Bangumi image URLs in the DTO mapper, and perform an idempotent narrow repair in SQLite initialization.

**Tech Stack:** .NET 10, WPF, C#, Microsoft.Data.Sqlite, existing console test harness

---

### Task 1: Preserve a usable Bangumi identity during fallback

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`

- [ ] **Step 1: Change the fallback regression test to require a linked canonical identity**

Update `AddGameMetadataPreviewFallsBackToSearchResultDetails` to assert:

```csharp
AssertEqual(true, viewModel.ExternalMetadata?.IsLinked);
AssertEqual("https://bgm.tv/subject/fallback-subject", viewModel.ExternalMetadata?.SubjectUrl);
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj`

Expected: FAIL because fallback metadata is currently unlinked and has an empty source URL.

- [ ] **Step 3: Implement the minimal fallback identity fix**

In `CreateMetadataPreviewFromSearchResult`, set:

```csharp
IsLinked = true,
SubjectUrl = $"https://bgm.tv/subject/{Uri.EscapeDataString(result.SubjectId)}",
```

Retain the partial-preview status text so the user is not told that full details were loaded.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command and expect all registered tests to pass.

### Task 2: Retry a transient subject `404`

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`

- [ ] **Step 1: Add a failing API retry test**

Register and add `BangumiApiRetriesTransientSubjectNotFound`. Queue a `404` response followed by a valid subject JSON response, call `GetGameDetailsAsync("172612")`, and assert the metadata is returned and two requests were sent.

- [ ] **Step 2: Run tests and verify RED**

Expected: the returned metadata is null and only one request is recorded.

- [ ] **Step 3: Add one bounded retry**

Loop at most twice inside `GetGameDetailsAsync`. Return mapped metadata on success; return null only when the second response is also `404`. Do not alter retry handling for authentication, rate limits, or other errors.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command and expect the new retry test to pass.

### Task 3: Normalize legacy Bangumi cover URLs

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `GameManager.App/Services/BangumiDtoMapper.cs`

- [ ] **Step 1: Add a failing HTTP image normalization test**

Feed a search response containing `http://lain.bgm.tv/pic/cover/example.jpg` and assert `ImageUrl` equals `https://lain.bgm.tv/pic/cover/example.jpg`.

- [ ] **Step 2: Run tests and verify RED**

Expected: the mapper returns the original HTTP URL.

- [ ] **Step 3: Implement narrow HTTPS upgrading**

In `NormalizeImageUrl`, retain protocol-relative handling and upgrade HTTP only when the host is `bgm.tv` or a subdomain ending in `.bgm.tv`. Preserve unrelated HTTP URLs unchanged.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command and expect both protocol-relative and HTTP normalization tests to pass.

### Task 4: Repair previously degraded SQLite rows

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`

- [ ] **Step 1: Add a failing database repair test**

Create a game, insert or update a Bangumi metadata row to `is_linked=0` and `subject_url=''`, reopen `SqliteGameLibraryService`, and assert:

```csharp
AssertEqual(true, repaired.ExternalMetadata?.IsLinked);
AssertEqual("https://bgm.tv/subject/172612", repaired.ExternalMetadata?.SubjectUrl);
```

Also add a control row with `is_linked=0` and a non-empty source URL and assert it remains unlinked.

- [ ] **Step 2: Run tests and verify RED**

Expected: the degraded row remains unlinked with an empty source URL.

- [ ] **Step 3: Add an idempotent narrow repair**

After schema creation, execute a parameterless SQL update restricted to Bangumi rows with non-empty subject IDs and empty source URLs. Set `is_linked=1` and `subject_url='https://bgm.tv/subject/' || subject_id`. Leave timestamps and all rows with a non-empty source URL unchanged.

- [ ] **Step 4: Run tests and verify GREEN**

Run the full test command and expect repair plus intentional-unlink coverage to pass.

### Task 5: Protect the complete edit-to-detail flow

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`

- [ ] **Step 1: Add an end-to-end service/view-model regression test**

Use a temporary SQLite database and a search-result-only provider. Execute search, selection, preview, all-field import, and save through `AddGameViewModel`; reopen the library; construct `GameDetailViewModel`; assert metadata identity persisted, cached cover path persisted, and refresh/source/unlink commands are enabled.

- [ ] **Step 2: Run the test suite**

Expected: all tests pass with the corrected production behavior.

- [ ] **Step 3: Run build verification**

Run: `dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore`

Expected: exit code 0 with no compile errors.

- [ ] **Step 4: Rebuild and launch the application once**

Start the Debug application after verification so SQLite initialization repairs the matching historical row. Query the database read-only and confirm subject `172612` now has `is_linked=1` and a canonical source URL.

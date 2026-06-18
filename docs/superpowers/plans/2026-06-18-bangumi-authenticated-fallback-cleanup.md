# Bangumi Authenticated Fallback and Repository Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete solution A from `问题.md` and remove obsolete generated/test artifacts while preserving maintained tests and project history.

**Architecture:** Add account-aware and status-aware lookup at the metadata provider boundary. Persist partial-data state, reuse one recovery flow from add/edit and detail screens, and limit cleanup to proven generated/prototype files guarded by `.gitignore`.

**Tech Stack:** .NET 10, WPF, C#, Microsoft.Data.Sqlite, existing console test harness

---

### Task 1: Define status-aware metadata lookup

**Files:**
- Create: `GameManager.App/Models/GameMetadataLookupResult.cs`
- Modify: `GameManager.App/Services/IGameMetadataProvider.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add a failing provider-facing test requiring complete, partial, reconnect-required, and not-found states.
- [ ] Run the test runner and verify the new behavior fails for the expected missing API.
- [ ] Add the result enum/record and a backward-compatible default lookup implementation.
- [ ] Re-run and verify the model/interface tests pass.

### Task 2: Add authenticated details and large legacy fallback

**Files:**
- Modify: `GameManager.App/Services/IBangumiApiClient.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Modify: `GameManager.App/Services/BangumiGameMetadataProvider.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests proving detail requests carry the bearer token and legacy fallback requests `responseGroup=large`.
- [ ] Add a failing provider test proving `401/403` marks the account for reconnect and still attempts recovery.
- [ ] Run and verify the tests fail for missing authenticated/fallback behavior.
- [ ] Implement compatible client overloads and provider orchestration; inject the account store in production composition.
- [ ] Re-run and verify the focused tests pass.

### Task 3: Make add/edit and detail refresh status-aware

**Files:**
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests for partial preview wording, local-name self-healing refresh, reconnect wording, true not-found wording, and the source-login hint.
- [ ] Run and verify the tests fail on current generic messages and ID-only refresh.
- [ ] Switch both screens to the shared lookup result and retain the generic search-result fallback for non-Bangumi/test providers.
- [ ] Re-run and verify the focused behavior passes.

### Task 4: Persist partial metadata state

**Files:**
- Modify: `GameManager.App/Models/ExternalGameMetadata.cs`
- Modify: `GameManager.App/Models/ExternalGameMetadataCloudSnapshot.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add a failing SQLite round-trip test for `IsPartial` and a detail-view assertion that partial status survives reload.
- [ ] Run and verify failure because the flag is not persisted.
- [ ] Add an idempotent SQLite column migration and update read/write/compare/cloud snapshot paths.
- [ ] Re-run and verify persistence and existing Stage C tests pass.

### Task 5: Remove obsolete development artifacts

**Files:**
- Create: `.gitignore`
- Delete: `.vs/`, `_verify/`, `GameManager.App/bin/`, `GameManager.App/obj/`, `GameManager.App.Tests/bin/`, `GameManager.App.Tests/obj/`, and verified redundant top-level prototype image folders

- [ ] Compare top-level prototype icons with `GameManager.App/Assets` and scan source references.
- [ ] Add ignore rules for IDE, build, verification, and temporary outputs.
- [ ] Delete only paths proven generated or redundant; keep maintained source tests and documentation.
- [ ] Scan tracked files to ensure no ignored build/verification artifacts remain.

### Task 6: Full verification

**Files:**
- Review all changed files

- [ ] Run `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj` and require zero failed tests.
- [ ] Run `dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore` and require exit code 0.
- [ ] Run `git diff --check`.
- [ ] Review `git status --short`, deletion scope, and requirement coverage before reporting completion.

# SQLite Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist the local game library to SQLite so add/delete/update/pin operations survive application restarts.

**Architecture:** Keep the existing `IGameLibraryService` contract and add `SqliteGameLibraryService` as a drop-in replacement for `InMemoryGameLibraryService`. The service owns database creation and simple schema upgrades for the MVP; ViewModels keep using the same service interface.

**Tech Stack:** C#/.NET 10, WPF, `Microsoft.Data.Sqlite`, current console test harness.

---

### Task 1: Persistence Tests

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`

- [ ] Add tests using a temporary database path.
- [ ] Verify add persists across service instances.
- [ ] Verify update persists across service instances.
- [ ] Verify delete persists across service instances.
- [ ] Verify pin-to-top order persists across service instances.
- [ ] Run `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj` and confirm failure because `SqliteGameLibraryService` does not exist yet.

### Task 2: SQLite Service

**Files:**
- Modify: `GameManager.App/GameManager.App.csproj`
- Create: `GameManager.App/Services/SqliteGameLibraryService.cs`

- [ ] Add `Microsoft.Data.Sqlite`.
- [ ] Create `games` table on service initialization.
- [ ] Store fields: `id`, `name`, `executable_path`, `game_root_path`, `save_path`, `cover_image_path`, `total_play_seconds`, `last_launch_time`, `sort_order`, `created_at`, `updated_at`.
- [ ] Implement `GetGames`, `AddGame`, `DeleteGame`, `UpdateGame`, and `PinGameToTop`.
- [ ] Run the test console and confirm pass.

### Task 3: App Wiring

**Files:**
- Create: `GameManager.App/Services/AppPaths.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`

- [ ] Add an app path helper returning `%LOCALAPPDATA%\FireflyGameManager\app.db`.
- [ ] Use `SqliteGameLibraryService` in `MainWindow`.
- [ ] Run `dotnet build .\GameManager.App\GameManager.App.csproj` and confirm 0 warnings/errors.

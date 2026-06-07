# Launch And Playtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the detail page start a local game executable, wait for it to exit, then persist total play time and last launch time.

**Architecture:** Add `IGameLauncher` for process launching and keep database writes inside `IGameLibraryService`. `GameDetailViewModel` uses an async command so the UI does not freeze while the game is running and refreshes the current game after the launcher completes.

**Tech Stack:** C#/.NET 10, WPF, `System.Diagnostics.Process`, existing SQLite service and console test harness.

---

### Task 1: Tests

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`

- [ ] Add tests for SQLite recording launch duration and last launch time.
- [ ] Add tests for detail view launching through a fake launcher and updating display text.
- [ ] Add tests that the start command is disabled while launch is in progress.

### Task 2: Services

**Files:**
- Create: `GameManager.App/Models/LaunchResult.cs`
- Create: `GameManager.App/Services/IGameLauncher.cs`
- Create: `GameManager.App/Services/ProcessGameLauncher.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`

- [ ] Implement real process launch with `Process.Start`.
- [ ] Wait asynchronously until the process exits.
- [ ] Return start time and elapsed duration.
- [ ] Persist added duration and last launch time.

### Task 3: ViewModel Wiring

**Files:**
- Create: `GameManager.App/Commands/AsyncRelayCommand.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `GameManager.App/MainWindow.xaml.cs`

- [ ] Replace placeholder start command with async launch command.
- [ ] Prevent duplicate starts while the game is running.
- [ ] Refresh detail text and library item after completion.
- [ ] Run tests and build.

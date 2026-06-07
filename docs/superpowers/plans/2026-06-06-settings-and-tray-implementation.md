# Settings And Tray Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement every Scheme A setting plus Scheme C system tray support in the existing .NET 10 WPF game manager.

**Architecture:** Persist non-appearance preferences in a new JSON-backed `AppSettings` service. Keep OS integration, game discovery, data maintenance, and tray behavior behind focused services; let `MainWindowViewModel`, `GameDetailViewModel`, and the existing settings view model coordinate them. Store launch arguments and administrator mode per game in SQLite.

**Tech Stack:** C# 14, .NET 10 WPF, Windows Forms `NotifyIcon`, SQLite, JSON, Windows Registry, ZIP archives.

---

### Task 1: Application Settings

**Files:**
- Create: `GameManager.App/Models/AppSettings.cs`
- Create: `GameManager.App/Services/IAppSettingsStore.cs`
- Create: `GameManager.App/Services/JsonAppSettingsStore.cs`
- Modify: `GameManager.App/Services/AppPaths.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Write a failing test that saves and reloads all Scheme A preferences.
- [ ] Run the test and confirm the missing settings types cause failure.
- [ ] Implement defaults and JSON persistence.
- [ ] Run the tests and confirm settings persistence passes.

### Task 2: Per-Game Launch Configuration And Launch Workflow

**Files:**
- Modify: `GameManager.App/Models/Game.cs`
- Modify: `GameManager.App/Models/AddGameRequest.cs`
- Modify: `GameManager.App/Models/UpdateGameRequest.cs`
- Modify: `GameManager.App/Services/SqliteGameLibraryService.cs`
- Modify: `GameManager.App/Services/ProcessGameLauncher.cs`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Write failing tests for launch argument persistence, administrator launch configuration, pre-launch backup, minimize-after-launch, and restore-after-exit.
- [ ] Run the tests and confirm they fail for missing behavior.
- [ ] Add SQLite-compatible migration columns and launch workflow behavior.
- [ ] Run the tests and confirm launch behavior passes.

### Task 3: Library Display And Discovery

**Files:**
- Create: `GameManager.App/Services/IGameDiscoveryService.cs`
- Create: `GameManager.App/Services/LocalGameDiscoveryService.cs`
- Modify: `GameManager.App/ViewModels/GameLibraryViewModel.cs`
- Modify: `GameManager.App/Views/GameLibraryView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Write failing tests for sorting, card size, playtime visibility, and executable discovery.
- [ ] Run the tests and confirm missing behavior.
- [ ] Implement view-only sorting and conservative recursive EXE discovery with duplicate filtering.
- [ ] Run the tests and confirm library behavior passes.

### Task 4: Data Maintenance And Settings Center

**Files:**
- Create: `GameManager.App/Services/IDataMaintenanceService.cs`
- Create: `GameManager.App/Services/LocalDataMaintenanceService.cs`
- Create: `GameManager.App/Services/IAutoStartService.cs`
- Create: `GameManager.App/Services/RegistryAutoStartService.cs`
- Modify: `GameManager.App/Services/IFilePickerService.cs`
- Modify: `GameManager.App/Services/WpfFilePickerService.cs`
- Modify: `GameManager.App/ViewModels/AppearanceSettingsViewModel.cs`
- Modify: `GameManager.App/Views/AppearanceSettingsView.xaml`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Write failing tests for discovery import, export/import, invalid-backup cleanup, setting reset, and remembered navigation.
- [ ] Run the tests and confirm missing behavior.
- [ ] Implement the maintenance services and grouped settings commands.
- [ ] Run the tests and confirm settings-center behavior passes.

### Task 5: System Tray And Window Lifecycle

**Files:**
- Create: `GameManager.App/Services/SystemTrayService.cs`
- Modify: `GameManager.App/GameManager.App.csproj`
- Modify: `GameManager.App/MainWindow.xaml`
- Modify: `GameManager.App/MainWindow.xaml.cs`
- Modify: `GameManager.App/App.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Write failing structural tests for tray support and close/minimized lifecycle hooks.
- [ ] Run the tests and confirm missing behavior.
- [ ] Add `NotifyIcon`, open/exit actions, start-minimized behavior, and configurable close-to-tray behavior.
- [ ] Run the full test executable and build the WPF app.


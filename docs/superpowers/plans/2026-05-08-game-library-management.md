# Game Library Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add game-library management, single-game actions, and edit flow to the existing WPF MVP.

**Architecture:** Keep the current single-project MVVM structure. Wrap each `Game` in a `GameLibraryItemViewModel` so selection state, per-card commands, and display data live beside the item. Use the existing in-memory service for add/delete/update/reorder until SQLite is introduced.

**Tech Stack:** C#/.NET 10, WPF/XAML, current lightweight command and test console.

---

### Task 1: Management Behaviors

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Modify: `GameManager.App/Services/IGameLibraryService.cs`
- Modify: `GameManager.App/Services/InMemoryGameLibraryService.cs`
- Modify: `GameManager.App/ViewModels/GameLibraryViewModel.cs`
- Create: `GameManager.App/ViewModels/ManageGameLibraryViewModel.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`

- [ ] Write failing tests for opening management mode, batch delete, single delete, and pin-to-top.
- [ ] Implement in-memory delete/update/pin operations.
- [ ] Implement selection state and batch deletion in management ViewModel.
- [ ] Run `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj` and confirm pass.

### Task 2: Edit Behaviors

**Files:**
- Modify: `GameManager.App.Tests/Program.cs`
- Create: `GameManager.App/Models/UpdateGameRequest.cs`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/ViewModels/MainWindowViewModel.cs`

- [ ] Write failing tests for opening edit mode and saving modified game fields.
- [ ] Reuse `AddGameViewModel` for edit mode by accepting optional initial values and a submit label.
- [ ] Save edits into the in-memory service and refresh the existing card.
- [ ] Run the test console and confirm pass.

### Task 3: WPF UI

**Files:**
- Modify: `GameManager.App/MainWindow.xaml`
- Modify: `GameManager.App/Views/GameLibraryView.xaml`
- Create: `GameManager.App/Views/ManageGameLibraryView.xaml`
- Create: `GameManager.App/Views/ManageGameLibraryView.xaml.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`

- [ ] Add the top-right `管理` button.
- [ ] Restyle game cards so the cover fills the card, text overlays the cover, hover zoom animates, and the `...` menu appears on hover.
- [ ] Add menu items `删除`、`置顶`、`修改`.
- [ ] Add management page with checkbox overlays and bottom-right delete button.
- [ ] Run `dotnet build .\GameManager.App\GameManager.App.csproj` and confirm 0 warnings/errors.

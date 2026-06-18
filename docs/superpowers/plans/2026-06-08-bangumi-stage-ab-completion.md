# Bangumi Stage A/B Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the remaining Bangumi Stage A/B network, import, refresh, account-state, collection, image-validation, and UI details.

**Architecture:** Add small reusable models and error types, keep orchestration in the existing view models, and render all confirmation states inline in the existing WPF views. Use test-first changes for each behavior.

**Tech Stack:** C# 14, .NET 10 Windows, WPF, HttpClient, Microsoft.Data.Sqlite, DPAPI.

---

### Task 1: Metadata import selection and previews

**Files:**
- Create: `GameManager.App/Models/MetadataImportOptions.cs`
- Modify: `GameManager.App/ViewModels/AddGameViewModel.cs`
- Modify: `GameManager.App/Views/AddGameView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests for field-level merge behavior, explicit detail preview, and request cancellation.
- [ ] Implement `MetadataImportOptions` and merge helpers.
- [ ] Add selected-subject detail preview and individual field toggles.
- [ ] Add active-request cancellation and cancel requests when leaving the form.
- [ ] Run the full console test suite.

### Task 2: Bangumi request resilience and search cache

**Files:**
- Create: `GameManager.App/Services/BangumiApiException.cs`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Modify: `GameManager.App/Services/BangumiGameMetadataProvider.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests for ten-minute caching, 5xx retry, 429 messages, authentication errors, and timeout messages.
- [ ] Implement provider search caching.
- [ ] Implement request cloning, one 5xx retry, timeout, and status translation.
- [ ] Run the full console test suite.

### Task 3: Safe image decoding and account reconnection state

**Files:**
- Modify: `GameManager.App/Models/BangumiAccount.cs`
- Modify: `GameManager.App/Services/JsonBangumiAccountStore.cs`
- Modify: `GameManager.App/Services/RemoteImageCacheService.cs`
- Modify: `GameManager.App/ViewModels/AppearanceSettingsViewModel.cs`
- Modify: `GameManager.App/Views/AppearanceSettingsView.xaml`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests for invalid-but-header-matching images and persisted reconnect state.
- [ ] Decode image bytes before accepting them.
- [ ] Persist and display `RequiresReconnect`; clear it after successful verification.
- [ ] Run the full console test suite.

### Task 4: Detail refresh differences, summary folding, and collection methods

**Files:**
- Modify: `GameManager.App/ViewModels/GameDetailViewModel.cs`
- Modify: `GameManager.App/Views/GameDetailView.xaml`
- Modify: `GameManager.App/Services/BangumiApiClient.cs`
- Test: `GameManager.App.Tests/Program.cs`

- [ ] Add failing tests for refresh preview, selected-field application, summary folding, POST creation, PATCH update, and authentication rejection.
- [ ] Implement inline refresh differences and apply/cancel commands.
- [ ] Implement summary expand/collapse.
- [ ] Select POST or PATCH based on cached remote collection state.
- [ ] Mark rejected accounts as requiring reconnection.
- [ ] Run the full console test suite.

### Task 5: Final regression and documentation

**Files:**
- Modify: `Bangumi账号与游戏信息导入实现方案.md`
- Modify: `GameManager.App.Tests/Program.cs`

- [ ] Update the implementation-status section and test count.
- [ ] Run `dotnet run --project .\GameManager.App.Tests\GameManager.App.Tests.csproj`.
- [ ] Run `dotnet build .\GameManager.App.Tests\GameManager.App.Tests.csproj --no-restore`.
- [ ] Launch the WPF app and inspect the changed screens.

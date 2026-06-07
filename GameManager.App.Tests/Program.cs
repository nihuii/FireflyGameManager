using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using GameManager.App.Models;
using GameManager.App.Services;
using GameManager.App.ViewModels;
using Microsoft.Data.Sqlite;

var tests = new List<(string Name, Action Body)>
{
    ("loads library with sample games", LoadsLibraryWithSampleGames),
    ("opens selected game detail", OpensSelectedGameDetail),
    ("navigates to add game and back", NavigatesToAddGameAndBack),
    ("browses executable and infers game paths", BrowsesExecutableAndInfersGamePaths),
    ("browses save folder and cover image", BrowsesSaveFolderAndCoverImage),
    ("saves new game into memory library", SavesNewGameIntoMemoryLibrary),
    ("opens management mode", OpensManagementMode),
    ("batch deletes selected games", BatchDeletesSelectedGames),
    ("deletes single game from library", DeletesSingleGameFromLibrary),
    ("pins game to top", PinsGameToTop),
    ("opens edit mode and saves modified game", OpensEditModeAndSavesModifiedGame),
    ("edit game view keeps save actions visible", EditGameViewKeepsSaveActionsVisible),
    ("sqlite persists added game", SqlitePersistsAddedGame),
    ("sqlite persists updated game", SqlitePersistsUpdatedGame),
    ("sqlite persists deleted game", SqlitePersistsDeletedGame),
    ("sqlite persists pinned order", SqlitePersistsPinnedOrder),
    ("game card has no caption scrim", GameCardHasNoCaptionScrim),
    ("game library omits cover wall badge", GameLibraryOmitsCoverWallBadge),
    ("game cards clip covers to rounded corners", GameCardsClipCoversToRoundedCorners),
    ("game card click area has transparent button template", GameCardClickAreaHasTransparentButtonTemplate),
    ("more menu button uses text icon style", MoreMenuButtonUsesTextIconStyle),
    ("more menu follows dynamic application theme", MoreMenuFollowsDynamicApplicationTheme),
    ("sqlite records launch result", SqliteRecordsLaunchResult),
    ("detail start command updates play time", DetailStartCommandUpdatesPlayTime),
    ("detail start command disabled while launch is running", DetailStartCommandDisabledWhileLaunchIsRunning),
    ("top actions hidden on detail page", TopActionsHiddenOnDetailPage),
    ("top actions only show on library page", TopActionsOnlyShowOnLibraryPage),
    ("management page can return to library", ManagementPageCanReturnToLibrary),
    ("main window uses modern side navigation shell", MainWindowUsesModernSideNavigationShell),
    ("main window uses compact icon navigation rail", MainWindowUsesCompactIconNavigationRail),
    ("main window exposes navigation selection state", MainWindowExposesNavigationSelectionState),
    ("app uses provided desktop icon everywhere", AppUsesProvidedDesktopIconEverywhere),
    ("main window uses immersive custom title bar", MainWindowUsesImmersiveCustomTitleBar),
    ("custom title bar uses uniform vector controls", CustomTitleBarUsesUniformVectorControls),
    ("main window has rounded outer frame", MainWindowHasRoundedOuterFrame),
    ("app defines shared modern ui styles", AppDefinesSharedModernUiStyles),
    ("local save backup creates zip from save directory", LocalSaveBackupCreatesZipFromSaveDirectory),
    ("local save backup restores zip into save directory", LocalSaveBackupRestoresZipIntoSaveDirectory),
    ("detail save commands call backup service and picker", DetailSaveCommandsCallBackupServiceAndPicker),
    ("detail view has save backup and restore buttons", DetailViewHasSaveBackupAndRestoreButtons),
    ("detail view uses shared immersive visual system", DetailViewUsesSharedImmersiveVisualSystem),
    ("local save backup lists zip history newest first", LocalSaveBackupListsZipHistoryNewestFirst),
    ("local save backup deletes selected backup file", LocalSaveBackupDeletesSelectedBackupFile),
    ("detail loads backup history and restores selected backup", DetailLoadsBackupHistoryAndRestoresSelectedBackup),
    ("detail deletes selected backup and refreshes history", DetailDeletesSelectedBackupAndRefreshesHistory),
    ("detail view has backup history list actions", DetailViewHasBackupHistoryListActions),
    ("webdav settings store saves and loads config", WebDavSettingsStoreSavesAndLoadsConfig),
    ("webdav connection test sends propfind with basic auth", WebDavConnectionTestSendsPropfindWithBasicAuth),
    ("webdav connection test creates missing remote directory", WebDavConnectionTestCreatesMissingRemoteDirectory),
    ("webdav manual sync uploads user database", WebDavManualSyncUploadsUserDatabase),
    ("webdav manual sync uploads save backup zips", WebDavManualSyncUploadsSaveBackupZips),
    ("webdav manual sync downloads user database", WebDavManualSyncDownloadsUserDatabase),
    ("webdav manual sync downloads save backup zips", WebDavManualSyncDownloadsSaveBackupZips),
    ("webdav manual sync walks save backup subdirectories", WebDavManualSyncWalksSaveBackupSubdirectories),
    ("sqlite game library merge adds remote and keeps newest game info", SqliteGameLibraryMergeAddsRemoteAndKeepsNewestGameInfo),
    ("save backup merge copies missing and keeps newest file", SaveBackupMergeCopiesMissingAndKeepsNewestFile),
    ("webdav full sync downloads merges and uploads", WebDavFullSyncDownloadsMergesAndUploads),
    ("webdav settings upload commands call sync service", WebDavSettingsUploadCommandsCallSyncService),
    ("webdav settings view has manual upload buttons", WebDavSettingsViewHasManualUploadButtons),
    ("webdav settings download commands call sync service", WebDavSettingsDownloadCommandsCallSyncService),
    ("webdav settings view has manual download buttons", WebDavSettingsViewHasManualDownloadButtons),
    ("webdav settings full sync command calls sync service", WebDavSettingsFullSyncCommandCallsSyncService),
    ("webdav settings view has full sync button", WebDavSettingsViewHasFullSyncButton),
    ("webdav settings view groups sync center and advanced actions", WebDavSettingsViewGroupsSyncCenterAndAdvancedActions),
    ("webdav sync strategy is shown as tooltip", WebDavSyncStrategyIsShownAsTooltip),
    ("opens webdav settings and saves config", OpensWebDavSettingsAndSavesConfig),
    ("webdav settings test connection updates status", WebDavSettingsTestConnectionUpdatesStatus),
    ("main window separates sync and settings routes", MainWindowSeparatesSyncAndSettingsRoutes),
    ("appearance settings store saves and loads config", AppearanceSettingsStoreSavesAndLoadsConfig),
    ("appearance settings selects and applies wallpaper", AppearanceSettingsSelectsAndAppliesWallpaper),
    ("appearance settings starts on overview and opens sections", AppearanceSettingsStartsOnOverviewAndOpensSections),
    ("appearance settings view uses layered sections", AppearanceSettingsViewUsesLayeredSections),
    ("settings controls use modern shared styles", SettingsControlsUseModernSharedStyles),
    ("wpf appearance theme updates live brush resources", WpfAppearanceThemeUpdatesLiveBrushResources),
    ("wallpaper palette adapts to different color schemes", WallpaperPaletteAdaptsToDifferentColorSchemes),
    ("wallpaper palette keeps colorful accent over neutral majority", WallpaperPaletteKeepsColorfulAccentOverNeutralMajority),
    ("app settings store saves all scheme a preferences", AppSettingsStoreSavesAllSchemeAPreferences),
    ("sqlite persists per game launch options", SqlitePersistsPerGameLaunchOptions),
    ("game library applies sorting card size and playtime preferences", GameLibraryAppliesDisplayPreferences),
    ("local game discovery finds executable candidates and skips duplicates", LocalGameDiscoveryFindsExecutableCandidatesAndSkipsDuplicates),
    ("launch workflow backs up minimizes and restores", LaunchWorkflowBacksUpMinimizesAndRestores),
    ("launch workflow reports failures without throwing", LaunchWorkflowReportsFailuresWithoutThrowing),
    ("local data maintenance exports imports and clears invalid files", LocalDataMaintenanceExportsImportsAndClearsInvalidFiles),
    ("main window supports system tray lifecycle", MainWindowSupportsSystemTrayLifecycle),
    ("settings readonly paths use one way bindings", SettingsReadonlyPathsUseOneWayBindings),
};

var failures = 0;

foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex.Message);
    }
}

if (failures > 0)
{
    Environment.Exit(1);
}

static MainWindowViewModel CreateViewModel()
{
    return new MainWindowViewModel(new InMemoryGameLibraryService(), new QueuedFilePickerService());
}

static MainWindowViewModel CreateViewModelWithPicker(QueuedFilePickerService filePicker)
{
    return new MainWindowViewModel(new InMemoryGameLibraryService(), filePicker);
}

static void LoadsLibraryWithSampleGames()
{
    var viewModel = CreateViewModel();

    AssertEqual("游戏库", viewModel.PageTitle);
    AssertTrue(viewModel.CurrentViewModel is GameLibraryViewModel, "Expected library view model.");
    AssertTrue(viewModel.Library.Games.Count >= 3, "Expected sample games.");
}

static void OpensSelectedGameDetail()
{
    var viewModel = CreateViewModel();
    var game = viewModel.Library.Games[0];

    viewModel.Library.OpenGameDetailCommand.Execute(game);

    AssertTrue(viewModel.CurrentViewModel is GameDetailViewModel, "Expected detail view model.");
    AssertEqual(game.Name, viewModel.PageTitle);
}

static void NavigatesToAddGameAndBack()
{
    var viewModel = CreateViewModel();

    viewModel.ShowAddGameCommand.Execute(null);
    AssertTrue(viewModel.CurrentViewModel is AddGameViewModel, "Expected add game view model.");
    AssertEqual("添加游戏", viewModel.PageTitle);

    var addGame = (AddGameViewModel)viewModel.CurrentViewModel;
    addGame.CancelCommand.Execute(null);

    AssertTrue(viewModel.CurrentViewModel is GameLibraryViewModel, "Expected library view model.");
    AssertEqual("游戏库", viewModel.PageTitle);
}

static void BrowsesExecutableAndInfersGamePaths()
{
    var filePicker = new QueuedFilePickerService
    {
        ExecutablePath = @"D:\Games\ExampleGame\ExampleGame.exe"
    };
    var viewModel = CreateViewModelWithPicker(filePicker);

    viewModel.ShowAddGameCommand.Execute(null);
    var addGame = (AddGameViewModel)viewModel.CurrentViewModel;
    addGame.BrowseExecutableCommand.Execute(null);

    AssertEqual(@"D:\Games\ExampleGame\ExampleGame.exe", addGame.ExecutablePath);
    AssertEqual(@"D:\Games\ExampleGame", addGame.GameRootPath);
    AssertEqual("ExampleGame", addGame.GameName);
}

static void BrowsesSaveFolderAndCoverImage()
{
    var filePicker = new QueuedFilePickerService
    {
        FolderPath = @"C:\Users\Public\Saved Games\ExampleGame",
        CoverImagePath = @"D:\Images\example-cover.jpg"
    };
    var viewModel = CreateViewModelWithPicker(filePicker);

    viewModel.ShowAddGameCommand.Execute(null);
    var addGame = (AddGameViewModel)viewModel.CurrentViewModel;
    addGame.BrowseSaveFolderCommand.Execute(null);
    addGame.BrowseCoverImageCommand.Execute(null);

    AssertEqual(@"C:\Users\Public\Saved Games\ExampleGame", addGame.SavePath);
    AssertEqual(@"D:\Images\example-cover.jpg", addGame.CoverImagePath);
}

static void SavesNewGameIntoMemoryLibrary()
{
    var viewModel = CreateViewModel();
    var initialCount = viewModel.Library.Games.Count;

    viewModel.ShowAddGameCommand.Execute(null);
    var addGame = (AddGameViewModel)viewModel.CurrentViewModel;
    addGame.GameName = "ExampleGame";
    addGame.ExecutablePath = @"D:\Games\ExampleGame\ExampleGame.exe";
    addGame.GameRootPath = @"D:\Games\ExampleGame";
    addGame.SavePath = @"C:\Users\Public\Saved Games\ExampleGame";
    addGame.CoverImagePath = @"D:\Images\example-cover.jpg";

    AssertTrue(addGame.SaveCommand.CanExecute(null), "Expected save command to be enabled.");
    addGame.SaveCommand.Execute(null);

    AssertEqual(initialCount + 1, viewModel.Library.Games.Count);
    AssertEqual("ExampleGame", viewModel.Library.Games[^1].Name);
    AssertTrue(viewModel.CurrentViewModel is GameLibraryViewModel, "Expected to return to library after saving.");
}

static void OpensManagementMode()
{
    var viewModel = CreateViewModel();

    viewModel.ShowManageLibraryCommand.Execute(null);

    AssertEqual("管理游戏库", viewModel.PageTitle);
    AssertTrue(viewModel.CurrentViewModel is ManageGameLibraryViewModel, "Expected manage library view model.");

    var manage = (ManageGameLibraryViewModel)viewModel.CurrentViewModel;
    AssertEqual(viewModel.Library.Games.Count, manage.Games.Count);
}

static void BatchDeletesSelectedGames()
{
    var viewModel = CreateViewModel();
    viewModel.ShowManageLibraryCommand.Execute(null);
    var manage = (ManageGameLibraryViewModel)viewModel.CurrentViewModel;
    var firstId = manage.Games[0].Game.Id;
    var secondId = manage.Games[1].Game.Id;

    manage.Games[0].IsSelected = true;
    manage.Games[1].IsSelected = true;
    AssertTrue(manage.DeleteSelectedCommand.CanExecute(null), "Expected batch delete to be enabled.");
    manage.DeleteSelectedCommand.Execute(null);

    AssertEqual(1, viewModel.Library.Games.Count);
    AssertEqual(1, manage.Games.Count);
    AssertTrue(!ContainsGame(viewModel.Library, firstId), "Expected first selected game to be removed.");
    AssertTrue(!ContainsGame(viewModel.Library, secondId), "Expected second selected game to be removed.");
}

static void DeletesSingleGameFromLibrary()
{
    var viewModel = CreateViewModel();
    var game = viewModel.Library.Games[0];

    viewModel.Library.DeleteGameCommand.Execute(game);

    AssertEqual(2, viewModel.Library.Games.Count);
    AssertTrue(!ContainsGame(viewModel.Library, game.Id), "Expected selected game to be removed.");
}

static void PinsGameToTop()
{
    var viewModel = CreateViewModel();
    var game = viewModel.Library.Games[2];

    viewModel.Library.PinGameCommand.Execute(game);

    AssertEqual(game.Id, viewModel.Library.Games[0].Id);
}

static void OpensEditModeAndSavesModifiedGame()
{
    var viewModel = CreateViewModel();
    var game = viewModel.Library.Games[0];

    viewModel.Library.EditGameCommand.Execute(game);

    AssertEqual("修改游戏", viewModel.PageTitle);
    AssertTrue(viewModel.CurrentViewModel is AddGameViewModel, "Expected edit form view model.");

    var editGame = (AddGameViewModel)viewModel.CurrentViewModel;
    AssertEqual(game.Name, editGame.GameName);
    editGame.GameName = "Updated Game";
    editGame.ExecutablePath = @"D:\Games\Updated\Updated.exe";
    editGame.GameRootPath = @"D:\Games\Updated";
    editGame.SavePath = @"C:\Users\Public\Saved Games\Updated";
    editGame.CoverImagePath = @"D:\Images\updated-cover.png";
    editGame.SaveCommand.Execute(null);

    var updated = FindGame(viewModel.Library, game.Id);
    AssertEqual("Updated Game", updated.Name);
    AssertEqual(@"D:\Games\Updated\Updated.exe", updated.ExecutablePath);
    AssertEqual(@"D:\Images\updated-cover.png", updated.CoverImagePath);
    AssertTrue(viewModel.CurrentViewModel is GameLibraryViewModel, "Expected to return to library after editing.");
}

static void EditGameViewKeepsSaveActionsVisible()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "AddGameView.xaml"));

    AssertTrue(xaml.Contains("x:Name=\"GameFieldsScrollViewer\"", StringComparison.Ordinal),
        "The long game information form should scroll within the available content area.");
    AssertTrue(xaml.Contains("x:Name=\"GameFormActionBar\"", StringComparison.Ordinal)
        && xaml.Contains("Content=\"{Binding SubmitButtonText}\"", StringComparison.Ordinal),
        "The save and cancel actions should remain in a fixed action bar outside the scrolling fields.");
}

static void SqlitePersistsAddedGame()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);

    var added = service.AddGame(CreateAddGameRequest("Persisted Game"));

    var reloaded = new SqliteGameLibraryService(database.Path);
    var games = reloaded.GetGames();

    AssertEqual(1, games.Count);
    AssertEqual(added.Id, games[0].Id);
    AssertEqual("Persisted Game", games[0].Name);
}

static void SqlitePersistsUpdatedGame()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);
    var added = service.AddGame(CreateAddGameRequest("Before Edit"));

    service.UpdateGame(new GameManager.App.Models.UpdateGameRequest(
        added.Id,
        "After Edit",
        @"D:\Games\After\After.exe",
        @"D:\Games\After",
        @"C:\Users\Public\Saved Games\After",
        @"D:\Images\after.png"));

    var reloaded = new SqliteGameLibraryService(database.Path);
    var game = reloaded.GetGames().Single();

    AssertEqual("After Edit", game.Name);
    AssertEqual(@"D:\Games\After\After.exe", game.ExecutablePath);
    AssertEqual(@"D:\Images\after.png", game.CoverImagePath);
}

static void SqlitePersistsDeletedGame()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);
    var added = service.AddGame(CreateAddGameRequest("Delete Me"));

    service.DeleteGame(added.Id);

    var reloaded = new SqliteGameLibraryService(database.Path);
    AssertEqual(0, reloaded.GetGames().Count);
}

static void SqlitePersistsPinnedOrder()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);
    var first = service.AddGame(CreateAddGameRequest("First"));
    var second = service.AddGame(CreateAddGameRequest("Second"));
    var third = service.AddGame(CreateAddGameRequest("Third"));

    service.PinGameToTop(third.Id);

    var reloaded = new SqliteGameLibraryService(database.Path);
    var games = reloaded.GetGames();

    AssertEqual(third.Id, games[0].Id);
    AssertEqual(first.Id, games[1].Id);
    AssertEqual(second.Id, games[2].Id);
}

static void GameCardHasNoCaptionScrim()
{
    var xaml = ReadGameLibraryViewXaml();

    AssertTrue(!xaml.Contains("Background=\"#99000000\"", StringComparison.Ordinal),
        "Game card should not use a semi-transparent black caption panel.");
    AssertTrue(!xaml.Contains("Height=\"78\"", StringComparison.Ordinal),
        "Game card should not reserve a large caption panel over the cover.");
}

static void GameLibraryOmitsCoverWallBadge()
{
    var xaml = ReadGameLibraryViewXaml();

    AssertTrue(!xaml.Contains("封面墙", StringComparison.Ordinal),
        "The redundant cover wall badge should be removed from the library header.");
}

static void GameCardsClipCoversToRoundedCorners()
{
    var libraryXaml = ReadGameLibraryViewXaml();
    var manageXaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "ManageGameLibraryView.xaml"));

    AssertTrue(libraryXaml.Contains("x:Name=\"RoundedCoverClip\"", StringComparison.Ordinal),
        "Library game cards should define a named rounded cover clip.");
    AssertTrue(manageXaml.Contains("x:Name=\"RoundedCoverClip\"", StringComparison.Ordinal),
        "Management game cards should define a named rounded cover clip.");
    AssertTrue(libraryXaml.Contains("RadiusX=\"12\"", StringComparison.Ordinal)
        && libraryXaml.Contains("RadiusY=\"12\"", StringComparison.Ordinal),
        "Library cover clip should use visible rounded corners.");
}

static void GameCardClickAreaHasTransparentButtonTemplate()
{
    var xaml = ReadGameLibraryViewXaml();

    AssertTrue(xaml.Contains("x:Name=\"CardClickButton\"", StringComparison.Ordinal),
        "Game card click area should be named for regression coverage.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource TransparentCardButtonStyle}\"", StringComparison.Ordinal),
        "Game card click area should use a transparent button template instead of the default hover chrome.");
}

static void MoreMenuButtonUsesTextIconStyle()
{
    var xaml = ReadGameLibraryViewXaml();

    AssertTrue(xaml.Contains("x:Name=\"MoreButton\"", StringComparison.Ordinal),
        "More menu button should remain present.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource TextIconButtonStyle}\"", StringComparison.Ordinal),
        "More menu button should render as text on the cover, not as a square default button.");
}

static void MoreMenuFollowsDynamicApplicationTheme()
{
    var xaml = ReadGameLibraryViewXaml();

    AssertTrue(xaml.Contains("x:Key=\"LibraryOverlayContextMenuStyle\"", StringComparison.Ordinal),
        "Context menu should define a library overlay style.");
    AssertTrue(xaml.Contains("Background=\"{DynamicResource PanelBrush}\"", StringComparison.Ordinal)
        && xaml.Contains("BorderBrush=\"{DynamicResource LineBrush}\"", StringComparison.Ordinal),
        "Context menu should follow the live panel and border theme.");
    AssertTrue(xaml.Contains("Property=\"Background\" Value=\"{DynamicResource SurfaceBrush}\"", StringComparison.Ordinal),
        "Highlighted menu items should follow the live surface theme.");
    AssertTrue(xaml.Contains("x:Key=\"LibraryOverlayMenuItemStyle\"", StringComparison.Ordinal),
        "Menu items should define a library overlay item style.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource LibraryOverlayContextMenuStyle}\"", StringComparison.Ordinal),
        "The more menu should use the library overlay context menu style.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource DangerOverlayMenuItemStyle}\"", StringComparison.Ordinal),
        "The delete menu item should use a danger style.");
}

static void SqliteRecordsLaunchResult()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);
    var game = service.AddGame(CreateAddGameRequest("Launch Persist"));
    var launchedAt = new DateTime(2026, 6, 3, 14, 30, 0);

    var updated = service.RecordLaunchResult(
        game.Id,
        new GameManager.App.Models.LaunchResult(launchedAt, TimeSpan.FromMinutes(75)));

    AssertEqual(TimeSpan.FromMinutes(75), updated.TotalPlayTime);
    AssertEqual(launchedAt, updated.LastLaunchTime);

    var reloaded = new SqliteGameLibraryService(database.Path).GetGames().Single();
    AssertEqual(TimeSpan.FromMinutes(75), reloaded.TotalPlayTime);
    AssertEqual(launchedAt, reloaded.LastLaunchTime);
}

static void DetailStartCommandUpdatesPlayTime()
{
    var service = new InMemoryGameLibraryService();
    var addedGame = service.AddGame(CreateAddGameRequest("Zero Playtime"));
    var launcher = new ImmediateGameLauncher(new GameManager.App.Models.LaunchResult(
        new DateTime(2026, 6, 3, 15, 0, 0),
        TimeSpan.FromMinutes(90)));
    var viewModel = new MainWindowViewModel(service, new QueuedFilePickerService(), launcher);
    var game = viewModel.Library.Games.Single(item => item.Id == addedGame.Id);
    viewModel.Library.OpenGameDetailCommand.Execute(game);
    var detail = (GameDetailViewModel)viewModel.CurrentViewModel;

    ((GameManager.App.Commands.AsyncRelayCommand)detail.StartGameCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual("1 小时 30 分钟", detail.TotalPlayTimeText);
    AssertEqual("2026-06-03 15:00", detail.LastLaunchTimeText);
    AssertEqual(TimeSpan.FromMinutes(90), viewModel.Library.Games.Single(item => item.Id == addedGame.Id).TotalPlayTime);
}

static void DetailStartCommandDisabledWhileLaunchIsRunning()
{
    var service = new InMemoryGameLibraryService();
    var launcher = new ControlledGameLauncher();
    var viewModel = new MainWindowViewModel(service, new QueuedFilePickerService(), launcher);
    var game = viewModel.Library.Games[0];
    viewModel.Library.OpenGameDetailCommand.Execute(game);
    var detail = (GameDetailViewModel)viewModel.CurrentViewModel;
    var command = (GameManager.App.Commands.AsyncRelayCommand)detail.StartGameCommand;

    var launchTask = command.ExecuteAsync(null);
    launcher.LaunchStarted.Task.GetAwaiter().GetResult();

    AssertTrue(!command.CanExecute(null), "Expected start command to be disabled while launch is running.");

    launcher.Complete(new GameManager.App.Models.LaunchResult(
        new DateTime(2026, 6, 3, 16, 0, 0),
        TimeSpan.FromMinutes(10)));
    launchTask.GetAwaiter().GetResult();

    AssertTrue(command.CanExecute(null), "Expected start command to be enabled after launch completes.");
}

static void TopActionsHiddenOnDetailPage()
{
    var viewModel = CreateViewModel();
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show on library page.");

    viewModel.Library.OpenGameDetailCommand.Execute(viewModel.Library.Games[0]);

    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on detail page.");
}

static void TopActionsOnlyShowOnLibraryPage()
{
    var viewModel = CreateViewModel();
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show on library page.");

    viewModel.ShowAddGameCommand.Execute(null);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on add game page.");
    ((AddGameViewModel)viewModel.CurrentViewModel).CancelCommand.Execute(null);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after returning to library.");

    viewModel.ShowManageLibraryCommand.Execute(null);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on management page.");
    ((ManageGameLibraryViewModel)viewModel.CurrentViewModel).ExitManagementCommand.Execute(null);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after exiting management.");

    viewModel.ShowSyncCommand.Execute(null);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on sync page.");
    ((WebDavSettingsViewModel)viewModel.CurrentViewModel).BackCommand.Execute(null);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after returning from sync.");

    viewModel.ShowSettingsCommand.Execute(null);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on settings page.");
    ((AppearanceSettingsViewModel)viewModel.CurrentViewModel).BackCommand.Execute(null);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after returning from settings.");

    viewModel.Library.OpenGameDetailCommand.Execute(viewModel.Library.Games[0]);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on detail page.");
    ((GameDetailViewModel)viewModel.CurrentViewModel).BackCommand.Execute(null);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after returning from detail.");

    viewModel.Library.EditGameCommand.Execute(viewModel.Library.Games[0]);
    AssertTrue(!viewModel.ShowTopActions, "Expected top actions to be hidden on edit game page.");
}

static void ManagementPageCanReturnToLibrary()
{
    var viewModel = CreateViewModel();

    viewModel.ShowManageLibraryCommand.Execute(null);
    var manage = (ManageGameLibraryViewModel)viewModel.CurrentViewModel;
    manage.ExitManagementCommand.Execute(null);

    AssertEqual("游戏库", viewModel.PageTitle);
    AssertTrue(viewModel.ShowTopActions, "Expected top actions to show after exiting management.");
    AssertTrue(viewModel.CurrentViewModel is GameLibraryViewModel, "Expected to return to library view model.");
}

static void MainWindowUsesModernSideNavigationShell()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));

    AssertTrue(xaml.Contains("x:Name=\"SideNavigationRail\"", StringComparison.Ordinal),
        "MainWindow should use a named side navigation rail.");
    AssertTrue(xaml.Contains("BorderThickness=\"0,0,1,0\"", StringComparison.Ordinal)
        && xaml.Contains("CornerRadius=\"0\"", StringComparison.Ordinal),
        "The side navigation should keep only a right-side divider and avoid a floating card outline.");
    AssertTrue(xaml.Contains("x:Name=\"MainContentArea\"", StringComparison.Ordinal)
        && xaml.Contains("Margin=\"18,8,22,22\"", StringComparison.Ordinal),
        "The side navigation should touch the window edges while the main content keeps its own spacing.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource NavigationButtonStyle}\"", StringComparison.Ordinal),
        "Navigation items should use the shared navigation style.");
    AssertTrue(xaml.Contains("x:Name=\"PageActionBar\"", StringComparison.Ordinal),
        "MainWindow should keep page-specific actions separate from global navigation.");
    AssertTrue(xaml.Contains("Visibility=\"{Binding ShowTopActions", StringComparison.Ordinal),
        "Library-only page actions should still bind visibility to ShowTopActions.");
    AssertTrue(!xaml.Contains("Text=\"{Binding ShellSubtitle}\"", StringComparison.Ordinal),
        "Page headers should not repeat the generic shell subtitle on every page.");
}

static void MainWindowUsesCompactIconNavigationRail()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));

    AssertTrue(xaml.Contains("<ColumnDefinition Width=\"112\" />", StringComparison.Ordinal),
        "The side navigation rail should be compact so the main content gains more room.");
    AssertTrue(xaml.Contains("Assets/Icons/home_icon.png", StringComparison.Ordinal)
        && xaml.Contains("Assets/Icons/synchronize_icon.png", StringComparison.Ordinal)
        && xaml.Contains("Assets/Icons/settings_icon.png", StringComparison.Ordinal),
        "Navigation items should use the provided icon assets.");
    AssertTrue(xaml.Contains("Visibility=\"{Binding IsLibraryNavigationSelected, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal)
        && xaml.Contains("Visibility=\"{Binding IsSyncNavigationSelected, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal)
        && xaml.Contains("Visibility=\"{Binding IsSettingsNavigationSelected, Converter={StaticResource BooleanToVisibilityConverter}}\"", StringComparison.Ordinal),
        "Navigation labels should only be visible for the selected navigation item.");
    AssertTrue(xaml.Contains("x:Name=\"SideNavigationAppIcon\"", StringComparison.Ordinal)
        && xaml.Contains("Width=\"72\"", StringComparison.Ordinal)
        && xaml.Contains("Height=\"72\"", StringComparison.Ordinal),
        "The app icon above the navigation should be visibly larger.");
    AssertTrue(xaml.Contains("x:Name=\"LibraryNavigationIcon\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"LibraryNavigationLabel\"", StringComparison.Ordinal)
        && xaml.Contains("Width=\"44\"", StringComparison.Ordinal)
        && xaml.Contains("Height=\"44\"", StringComparison.Ordinal),
        "Navigation icons should be larger and keep named labels below them.");
}

static void MainWindowExposesNavigationSelectionState()
{
    var viewModel = CreateViewModel();

    AssertTrue(viewModel.IsLibraryNavigationSelected, "Library navigation should be selected by default.");
    AssertTrue(!viewModel.IsSyncNavigationSelected, "Sync navigation should not be selected by default.");
    AssertTrue(!viewModel.IsSettingsNavigationSelected, "Settings navigation should not be selected by default.");

    viewModel.ShowSyncCommand.Execute(null);
    AssertTrue(!viewModel.IsLibraryNavigationSelected, "Library navigation should be cleared after opening sync.");
    AssertTrue(viewModel.IsSyncNavigationSelected, "Sync navigation should be selected after opening sync.");
    AssertTrue(!viewModel.IsSettingsNavigationSelected, "Settings navigation should remain cleared after opening sync.");

    viewModel.ShowSettingsCommand.Execute(null);
    AssertTrue(!viewModel.IsLibraryNavigationSelected, "Library navigation should be cleared after opening settings.");
    AssertTrue(!viewModel.IsSyncNavigationSelected, "Sync navigation should be cleared after opening settings.");
    AssertTrue(viewModel.IsSettingsNavigationSelected, "Settings navigation should be selected after opening settings.");

    viewModel.ShowLibraryCommand.Execute(null);
    AssertTrue(viewModel.IsLibraryNavigationSelected, "Library navigation should be selected after returning home.");
}

static void AppUsesProvidedDesktopIconEverywhere()
{
    var project = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "GameManager.App.csproj"));
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));
    var codeBehind = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml.cs"));
    var trayService = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Services", "SystemTrayService.cs"));

    AssertTrue(project.Contains("<ApplicationIcon>Assets\\desktop_icon.ico</ApplicationIcon>", StringComparison.Ordinal),
        "The executable and taskbar should use the provided desktop icon.");
    AssertTrue(project.Contains("..\\images\\2\\desktop_icon.png", StringComparison.Ordinal)
        && project.Contains("..\\images\\2\\home_icon.png", StringComparison.Ordinal)
        && project.Contains("..\\images\\2\\synchronize_icon.png", StringComparison.Ordinal)
        && project.Contains("..\\images\\2\\settings_icon.png", StringComparison.Ordinal),
        "The provided icon images should be packaged as WPF resources.");
    AssertTrue(xaml.Contains("Icon=\"/Assets/Icons/desktop_icon.png\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"AppTitleIcon\"", StringComparison.Ordinal),
        "The window and custom title bar should use the provided desktop icon.");
    AssertTrue(codeBehind.Contains("AppIconPath", StringComparison.Ordinal)
        && codeBehind.Contains("new SystemTrayService(ShowFromTray, ExitFromTray, AppIconPath)", StringComparison.Ordinal),
        "The tray service should receive the same app icon path.");
    AssertTrue(trayService.Contains("iconPath", StringComparison.Ordinal)
        && trayService.Contains("Drawing.Icon", StringComparison.Ordinal),
        "The tray service should load a custom icon instead of the default system icon.");
}

static void MainWindowUsesImmersiveCustomTitleBar()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));
    var codeBehind = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml.cs"));

    AssertTrue(xaml.Contains("WindowStyle=\"None\"", StringComparison.Ordinal),
        "MainWindow should remove the system title bar.");
    AssertTrue(xaml.Contains("<WindowChrome.WindowChrome>", StringComparison.Ordinal),
        "MainWindow should retain native resizing through WindowChrome.");
    AssertTrue(xaml.Contains("x:Name=\"CustomTitleBar\"", StringComparison.Ordinal),
        "MainWindow should draw an immersive custom title bar.");
    AssertTrue(xaml.Contains("x:Name=\"MinimizeButton\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"MaximizeButton\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"CloseButton\"", StringComparison.Ordinal),
        "Custom title bar should provide all standard window controls.");
    AssertTrue(codeBehind.Contains("CustomTitleBar_MouseLeftButtonDown", StringComparison.Ordinal),
        "Custom title bar should support dragging and double-click maximize.");
}

static void CustomTitleBarUsesUniformVectorControls()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));
    var styles = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Styles", "AppStyles.xaml"));

    AssertTrue(styles.Contains("x:Key=\"WindowControlButtonStyle\"", StringComparison.Ordinal)
        && styles.Contains("<Setter Property=\"Width\" Value=\"44\"", StringComparison.Ordinal)
        && styles.Contains("<Setter Property=\"Height\" Value=\"42\"", StringComparison.Ordinal),
        "All window controls should share one fixed button size.");
    AssertTrue(xaml.Contains("x:Name=\"MinimizeGlyph\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"MaximizeGlyph\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"CloseGlyph\"", StringComparison.Ordinal),
        "Window controls should use fixed-size vector glyphs instead of font characters.");
}

static void MainWindowHasRoundedOuterFrame()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));

    AssertTrue(xaml.Contains("CornerRadius=\"12\"", StringComparison.Ordinal),
        "The native window chrome should request rounded corners.");
    AssertTrue(xaml.Contains("x:Name=\"WindowFrame\"", StringComparison.Ordinal)
        && xaml.Contains("CornerRadius=\"12\"", StringComparison.Ordinal),
        "MainWindow content should be enclosed by a rounded outer frame.");
}

static void AppDefinesSharedModernUiStyles()
{
    var appXaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "App.xaml"));
    var stylesXaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Styles", "AppStyles.xaml"));

    AssertTrue(appXaml.Contains("Styles/AppStyles.xaml", StringComparison.Ordinal),
        "Application resources should merge the shared modern style dictionary.");
    AssertTrue(stylesXaml.Contains("x:Key=\"PrimaryButtonStyle\"", StringComparison.Ordinal),
        "Shared styles should define a primary button style.");
    AssertTrue(stylesXaml.Contains("x:Key=\"SecondaryButtonStyle\"", StringComparison.Ordinal),
        "Shared styles should define a secondary button style.");
    AssertTrue(stylesXaml.Contains("x:Key=\"NavigationButtonStyle\"", StringComparison.Ordinal),
        "Shared styles should define a side navigation button style.");
    AssertTrue(stylesXaml.Contains("x:Key=\"SectionCardStyle\"", StringComparison.Ordinal),
        "Shared styles should define section card containers.");
}

static void LocalSaveBackupCreatesZipFromSaveDirectory()
{
    using var workspace = TempDirectory.Create();
    var saveDirectory = System.IO.Path.Combine(workspace.Path, "Saves");
    Directory.CreateDirectory(System.IO.Path.Combine(saveDirectory, "Profile"));
    File.WriteAllText(System.IO.Path.Combine(saveDirectory, "slot1.sav"), "slot-one");
    File.WriteAllText(System.IO.Path.Combine(saveDirectory, "Profile", "settings.json"), "{\"volume\":70}");
    var service = new LocalSaveBackupService(System.IO.Path.Combine(workspace.Path, "Backups"));
    var game = CreateGame("backup-game", "Backup Game", saveDirectory);

    var backupPath = service.BackupAsync(game).GetAwaiter().GetResult();

    AssertTrue(File.Exists(backupPath), "Expected backup zip file to be created.");
    using var zip = ZipFile.OpenRead(backupPath);
    AssertTrue(zip.Entries.Any(entry => entry.FullName == "slot1.sav"), "Expected root save file in backup zip.");
    AssertTrue(zip.Entries.Any(entry => entry.FullName == "Profile/settings.json"), "Expected nested save file in backup zip.");
}

static void LocalSaveBackupRestoresZipIntoSaveDirectory()
{
    using var workspace = TempDirectory.Create();
    var saveDirectory = System.IO.Path.Combine(workspace.Path, "Saves");
    Directory.CreateDirectory(saveDirectory);
    File.WriteAllText(System.IO.Path.Combine(saveDirectory, "slot1.sav"), "before-backup");
    var service = new LocalSaveBackupService(System.IO.Path.Combine(workspace.Path, "Backups"));
    var game = CreateGame("restore-game", "Restore Game", saveDirectory);
    var backupPath = service.BackupAsync(game).GetAwaiter().GetResult();
    File.WriteAllText(System.IO.Path.Combine(saveDirectory, "slot1.sav"), "changed");

    service.RestoreAsync(game, backupPath).GetAwaiter().GetResult();

    AssertEqual("before-backup", File.ReadAllText(System.IO.Path.Combine(saveDirectory, "slot1.sav")));
}

static void DetailSaveCommandsCallBackupServiceAndPicker()
{
    var game = CreateGame("detail-save-game", "Detail Save Game", @"D:\Games\DetailSave\Saves");
    var backupService = new RecordingSaveBackupService(@"D:\Backups\Detail Save Game\backup.zip");
    var picker = new QueuedFilePickerService
    {
        SaveBackupPath = @"D:\Backups\Detail Save Game\backup.zip"
    };
    var detail = new GameDetailViewModel(
        game,
        new ImmediateGameLauncher(new GameManager.App.Models.LaunchResult(DateTime.Now, TimeSpan.Zero)),
        (current, _) => current,
        _ => { },
        () => { },
        backupService,
        picker);

    ((GameManager.App.Commands.AsyncRelayCommand)detail.BackupSaveCommand).ExecuteAsync(null).GetAwaiter().GetResult();
    ((GameManager.App.Commands.AsyncRelayCommand)detail.RestoreSaveCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual(game.Id, backupService.BackupGameId);
    AssertEqual(game.Id, backupService.RestoreGameId);
    AssertEqual(@"D:\Backups\Detail Save Game", picker.LastBackupInitialDirectory);
    AssertEqual(@"D:\Backups\Detail Save Game\backup.zip", backupService.RestoreBackupPath);
    AssertTrue(detail.SaveBackupStatusText.Contains("backup.zip", StringComparison.Ordinal),
        "Expected detail status text to mention the selected backup file.");
}

static void DetailViewHasSaveBackupAndRestoreButtons()
{
    var xaml = ReadGameDetailViewXaml();

    AssertTrue(xaml.Contains("Command=\"{Binding BackupSaveCommand}\"", StringComparison.Ordinal),
        "Detail view should bind a button to BackupSaveCommand.");
    AssertTrue(xaml.Contains("Command=\"{Binding RestoreSaveCommand}\"", StringComparison.Ordinal),
        "Detail view should bind a button to RestoreSaveCommand.");
    AssertTrue(xaml.Contains("SaveBackupStatusText", StringComparison.Ordinal),
        "Detail view should show save backup status text.");
}

static void DetailViewUsesSharedImmersiveVisualSystem()
{
    var xaml = ReadGameDetailViewXaml();

    AssertTrue(xaml.Contains("Source=\"{Binding Game.CoverImagePath}\"", StringComparison.Ordinal),
        "Detail view should display the selected game's real cover image.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", StringComparison.Ordinal)
        && xaml.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", StringComparison.Ordinal)
        && xaml.Contains("Style=\"{StaticResource SectionCardStyle}\"", StringComparison.Ordinal),
        "Detail view should use the application's shared button and card styles.");
    AssertTrue(xaml.Contains("DynamicResource TextBrush", StringComparison.Ordinal)
        && xaml.Contains("DynamicResource MutedTextBrush", StringComparison.Ordinal),
        "Detail view text should follow the live wallpaper theme.");
    AssertTrue(xaml.Contains("BasedOn=\"{StaticResource ModernScrollBarStyle}\"", StringComparison.Ordinal),
        "Detail view should use the shared modern scrollbar.");
    AssertTrue(!xaml.Contains("x:Key=\"DetailSecondaryButtonStyle\"", StringComparison.Ordinal)
        && !xaml.Contains("Text=\"{Binding Game.Name}\"", StringComparison.Ordinal),
        "Detail view should not retain old local styles or repeat the shell page title.");
}

static void LocalSaveBackupListsZipHistoryNewestFirst()
{
    using var workspace = TempDirectory.Create();
    var service = new LocalSaveBackupService(System.IO.Path.Combine(workspace.Path, "Backups"));
    var game = CreateGame("history-game", "History Game", System.IO.Path.Combine(workspace.Path, "Saves"));
    var backupDirectory = service.GetBackupDirectory(game);
    Directory.CreateDirectory(backupDirectory);
    var olderPath = System.IO.Path.Combine(backupDirectory, "older.zip");
    var newerPath = System.IO.Path.Combine(backupDirectory, "newer.zip");
    CreateZipWithFile(olderPath, "slot.sav", "older");
    CreateZipWithFile(newerPath, "slot.sav", "newer");
    File.WriteAllText(System.IO.Path.Combine(backupDirectory, "notes.txt"), "not a backup");
    File.SetLastWriteTime(olderPath, new DateTime(2026, 6, 2, 20, 0, 0));
    File.SetLastWriteTime(newerPath, new DateTime(2026, 6, 3, 21, 0, 0));

    var backups = service.GetBackups(game);

    AssertEqual(2, backups.Count);
    AssertEqual(newerPath, backups[0].Path);
    AssertEqual("newer.zip", backups[0].FileName);
    AssertEqual(new DateTime(2026, 6, 3, 21, 0, 0), backups[0].CreatedAt);
    AssertTrue(backups[0].SizeBytes > 0, "Expected listed backup to include file size.");
    AssertEqual(olderPath, backups[1].Path);
}

static void LocalSaveBackupDeletesSelectedBackupFile()
{
    using var workspace = TempDirectory.Create();
    var service = new LocalSaveBackupService(System.IO.Path.Combine(workspace.Path, "Backups"));
    var backupPath = System.IO.Path.Combine(workspace.Path, "backup.zip");
    CreateZipWithFile(backupPath, "slot.sav", "save");

    var deleted = service.DeleteBackup(backupPath);

    AssertTrue(deleted, "Expected delete to return true for an existing backup.");
    AssertTrue(!File.Exists(backupPath), "Expected selected backup zip to be deleted.");
}

static void DetailLoadsBackupHistoryAndRestoresSelectedBackup()
{
    var first = new SaveBackupEntry(@"D:\Backups\first.zip", "first.zip", new DateTime(2026, 6, 3, 21, 0, 0), 2048);
    var second = new SaveBackupEntry(@"D:\Backups\second.zip", "second.zip", new DateTime(2026, 6, 2, 21, 0, 0), 1024);
    var game = CreateGame("detail-history-game", "Detail History Game", @"D:\Games\DetailHistory\Saves");
    var backupService = new RecordingSaveBackupService(@"D:\Backups\Detail History Game\new.zip", first, second);
    var detail = new GameDetailViewModel(
        game,
        new ImmediateGameLauncher(new GameManager.App.Models.LaunchResult(DateTime.Now, TimeSpan.Zero)),
        (current, _) => current,
        _ => { },
        () => { },
        backupService,
        new QueuedFilePickerService());

    AssertEqual(2, detail.SaveBackups.Count);
    AssertEqual("first.zip", detail.SaveBackups[0].FileName);

    ((GameManager.App.Commands.AsyncRelayCommand)detail.RestoreBackupCommand).ExecuteAsync(detail.SaveBackups[0]).GetAwaiter().GetResult();

    AssertEqual(first.Path, backupService.RestoreBackupPath);
    AssertTrue(detail.SaveBackupStatusText.Contains("first.zip", StringComparison.Ordinal),
        "Expected status text to mention the restored history item.");
}

static void DetailDeletesSelectedBackupAndRefreshesHistory()
{
    var first = new SaveBackupEntry(@"D:\Backups\first.zip", "first.zip", new DateTime(2026, 6, 3, 21, 0, 0), 2048);
    var second = new SaveBackupEntry(@"D:\Backups\second.zip", "second.zip", new DateTime(2026, 6, 2, 21, 0, 0), 1024);
    var game = CreateGame("detail-delete-backup-game", "Detail Delete Backup Game", @"D:\Games\DetailDeleteBackup\Saves");
    var backupService = new RecordingSaveBackupService(@"D:\Backups\Detail Delete Backup Game\new.zip", first, second);
    var detail = new GameDetailViewModel(
        game,
        new ImmediateGameLauncher(new GameManager.App.Models.LaunchResult(DateTime.Now, TimeSpan.Zero)),
        (current, _) => current,
        _ => { },
        () => { },
        backupService,
        new QueuedFilePickerService());

    ((GameManager.App.Commands.AsyncRelayCommand)detail.DeleteBackupCommand).ExecuteAsync(detail.SaveBackups[0]).GetAwaiter().GetResult();

    AssertEqual(first.Path, backupService.DeletedBackupPath);
    AssertEqual(1, detail.SaveBackups.Count);
    AssertEqual("second.zip", detail.SaveBackups[0].FileName);
}

static void DetailViewHasBackupHistoryListActions()
{
    var xaml = ReadGameDetailViewXaml();

    AssertTrue(xaml.Contains("ItemsSource=\"{Binding SaveBackups}\"", StringComparison.Ordinal),
        "Detail view should bind a backup history list.");
    AssertTrue(xaml.Contains("RestoreBackupCommand", StringComparison.Ordinal),
        "Backup history should expose restore actions.");
    AssertTrue(xaml.Contains("DeleteBackupCommand", StringComparison.Ordinal),
        "Backup history should expose delete actions.");
    AssertTrue(xaml.Contains("CreatedAtText", StringComparison.Ordinal),
        "Backup history should show backup time.");
    AssertTrue(xaml.Contains("SizeText", StringComparison.Ordinal),
        "Backup history should show backup size.");
}

static void WebDavSettingsStoreSavesAndLoadsConfig()
{
    using var workspace = TempDirectory.Create();
    var store = new JsonWebDavSettingsStore(System.IO.Path.Combine(workspace.Path, "webdav-settings.json"));
    var settings = new WebDavSettings(
        "https://dav.jianguoyun.com/dav/",
        "player@example.com",
        "app-password",
        "FireflyGameManager");

    store.Save(settings);
    var loaded = store.Load();

    AssertEqual("https://dav.jianguoyun.com/dav/", loaded.ServerUrl);
    AssertEqual("player@example.com", loaded.Username);
    AssertEqual("app-password", loaded.ApplicationPassword);
    AssertEqual("FireflyGameManager", loaded.RemoteDirectory);
}

static void WebDavConnectionTestSendsPropfindWithBasicAuth()
{
    var handler = new RecordingHttpMessageHandler(new HttpResponseMessage((HttpStatusCode)207));
    var service = new WebDavConnectionTestService(() => new HttpClient(handler));
    var settings = new WebDavSettings(
        "https://dav.jianguoyun.com/dav/",
        "player@example.com",
        "app-password",
        "FireflyGameManager");

    var result = service.TestConnectionAsync(settings).GetAwaiter().GetResult();

    AssertTrue(result.Success, "Expected 207 Multi-Status to be treated as a successful WebDAV connection.");
    AssertEqual("PROPFIND", handler.LastRequest!.Method.Method);
    AssertEqual("https://dav.jianguoyun.com/dav/FireflyGameManager/", handler.LastRequest.RequestUri!.ToString());
    AssertEqual("0", handler.LastRequest.Headers.GetValues("Depth").Single());
    AssertEqual("Basic", handler.LastRequest.Headers.Authorization!.Scheme);
    AssertEqual("player@example.com:app-password", DecodeBasicAuth(handler.LastRequest.Headers.Authorization));
}

static void WebDavConnectionTestCreatesMissingRemoteDirectory()
{
    var handler = new SequentialHttpMessageHandler(
        new HttpResponseMessage(HttpStatusCode.NotFound),
        new HttpResponseMessage(HttpStatusCode.Created));
    var service = new WebDavConnectionTestService(() => new HttpClient(handler));
    var settings = new WebDavSettings(
        "https://dav.jianguoyun.com/dav/",
        "player@example.com",
        "app-password",
        "FireflyGameManager");

    var result = service.TestConnectionAsync(settings).GetAwaiter().GetResult();

    AssertTrue(result.Success, "Expected missing remote directory to be created during connection test.");
    AssertTrue(result.Message.Contains("已创建远程目录", StringComparison.Ordinal),
        "Expected status text to explain that the remote directory was created.");
    AssertEqual(2, handler.Requests.Count);
    AssertEqual("PROPFIND", handler.Requests[0].Method.Method);
    AssertEqual("MKCOL", handler.Requests[1].Method.Method);
    AssertEqual("https://dav.jianguoyun.com/dav/FireflyGameManager/", handler.Requests[1].RequestUri!.ToString());
    AssertEqual("player@example.com:app-password", DecodeBasicAuth(handler.Requests[1].Headers.Authorization!));
}

static void WebDavManualSyncUploadsUserDatabase()
{
    using var workspace = TempDirectory.Create();
    var databasePath = System.IO.Path.Combine(workspace.Path, "app.db");
    File.WriteAllText(databasePath, "local-library-db");
    var handler = new RecordingUploadHttpMessageHandler();
    var service = new WebDavManualSyncService(() => new HttpClient(handler));
    var settings = CreateWebDavSettings();

    var result = service.UploadUserDataAsync(settings, databasePath).GetAwaiter().GetResult();

    AssertEqual(1, result.UploadedCount);
    AssertEqual(0, result.FailedCount);
    AssertTrue(result.Success, "Expected user database upload to succeed.");
    AssertTrue(handler.Requests.Any(request =>
            request.Method.Method == "MKCOL" &&
            request.RequestUri!.ToString() == "https://dav.jianguoyun.com/dav/FireflyGameManager/metadata/"),
        "Expected metadata directory to be created.");
    AssertTrue(handler.Requests.Any(request =>
            request.Method.Method == "PUT" &&
            request.RequestUri!.AbsoluteUri == "https://dav.jianguoyun.com/dav/FireflyGameManager/metadata/app.db"),
        "Expected app.db to be uploaded with PUT.");
    AssertEqual("local-library-db", handler.UploadedText["https://dav.jianguoyun.com/dav/FireflyGameManager/metadata/app.db"]);
}

static void WebDavManualSyncUploadsSaveBackupZips()
{
    using var workspace = TempDirectory.Create();
    var backupsRoot = System.IO.Path.Combine(workspace.Path, "SaveBackups");
    var gameBackupDirectory = System.IO.Path.Combine(backupsRoot, "Game One-game-id");
    Directory.CreateDirectory(gameBackupDirectory);
    var olderBackupPath = System.IO.Path.Combine(gameBackupDirectory, "older.zip");
    var newerBackupPath = System.IO.Path.Combine(gameBackupDirectory, "newer.zip");
    File.WriteAllText(olderBackupPath, "old-zip-bytes");
    File.WriteAllText(newerBackupPath, "new-zip-bytes");
    File.SetLastWriteTime(olderBackupPath, new DateTime(2026, 6, 3, 20, 0, 0));
    File.SetLastWriteTime(newerBackupPath, new DateTime(2026, 6, 4, 20, 0, 0));
    File.WriteAllText(System.IO.Path.Combine(gameBackupDirectory, "notes.txt"), "not uploaded");
    var handler = new RecordingUploadHttpMessageHandler();
    var service = new WebDavManualSyncService(() => new HttpClient(handler));
    var settings = CreateWebDavSettings();

    var result = service.UploadSaveBackupsAsync(settings, backupsRoot).GetAwaiter().GetResult();

    AssertEqual(1, result.UploadedCount);
    AssertEqual(0, result.FailedCount);
    AssertTrue(result.Success, "Expected save backup upload to succeed.");
    AssertTrue(handler.Requests.Any(request =>
            request.Method.Method == "MKCOL" &&
            request.RequestUri!.AbsoluteUri == "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/"),
        "Expected per-game backup directory to be created.");
    AssertTrue(handler.Requests.Any(request =>
            request.Method.Method == "PUT" &&
            request.RequestUri!.AbsoluteUri == "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/newer.zip"),
        "Expected latest backup zip to be uploaded with PUT.");
    AssertEqual("new-zip-bytes", handler.UploadedText["https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/newer.zip"]);
    AssertTrue(!handler.Requests.Any(request =>
            request.Method.Method == "PUT" &&
            request.RequestUri!.AbsoluteUri == "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/older.zip"),
        "Expected older backup zip in the same game directory to be skipped.");
    AssertTrue(!handler.Requests.Any(request => request.RequestUri!.ToString().EndsWith("notes.txt", StringComparison.Ordinal)),
        "Expected non-zip files to be ignored.");
}

static void WebDavManualSyncDownloadsUserDatabase()
{
    using var workspace = TempDirectory.Create();
    var databasePath = System.IO.Path.Combine(workspace.Path, "app.db");
    File.WriteAllText(databasePath, "local-library-db");
    var handler = new RecordingDownloadHttpMessageHandler();
    handler.RespondWithText(
        "GET",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/metadata/app.db",
        "remote-library-db");
    var service = new WebDavManualSyncService(() => new HttpClient(handler));
    var settings = CreateWebDavSettings();

    var result = service.DownloadUserDataAsync(settings, databasePath).GetAwaiter().GetResult();

    AssertEqual(1, result.DownloadedCount);
    AssertEqual(0, result.FailedCount);
    AssertTrue(result.Success, "Expected user database download to succeed.");
    AssertEqual("remote-library-db", File.ReadAllText(databasePath));
    AssertEqual("local-library-db", File.ReadAllText(databasePath + ".bak"));
    AssertEqual("GET", handler.Requests.Single().Method.Method);
    AssertEqual("https://dav.jianguoyun.com/dav/FireflyGameManager/metadata/app.db", handler.Requests.Single().RequestUri!.AbsoluteUri);
    AssertEqual("player@example.com:app-password", DecodeBasicAuth(handler.Requests.Single().Headers.Authorization!));
}

static void WebDavManualSyncDownloadsSaveBackupZips()
{
    using var workspace = TempDirectory.Create();
    var backupsRoot = System.IO.Path.Combine(workspace.Path, "SaveBackups");
    var handler = new RecordingDownloadHttpMessageHandler();
    handler.RespondWithText(
        "PROPFIND",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/",
        """
        <?xml version="1.0" encoding="utf-8"?>
        <d:multistatus xmlns:d="DAV:">
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/</d:href>
            </d:response>
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20One-game-id/newer.zip</d:href>
            </d:response>
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20One-game-id/notes.txt</d:href>
            </d:response>
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20Two-game-id/latest.zip</d:href>
            </d:response>
        </d:multistatus>
        """);
    handler.RespondWithText(
        "GET",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/newer.zip",
        "zip-one");
    handler.RespondWithText(
        "GET",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20Two-game-id/latest.zip",
        "zip-two");
    var service = new WebDavManualSyncService(() => new HttpClient(handler));
    var settings = CreateWebDavSettings();

    var result = service.DownloadSaveBackupsAsync(settings, backupsRoot).GetAwaiter().GetResult();

    AssertEqual(2, result.DownloadedCount);
    AssertEqual(0, result.FailedCount);
    AssertTrue(result.Success, "Expected save backup download to succeed.");
    AssertEqual("zip-one", File.ReadAllText(System.IO.Path.Combine(backupsRoot, "Game One-game-id", "newer.zip")));
    AssertEqual("zip-two", File.ReadAllText(System.IO.Path.Combine(backupsRoot, "Game Two-game-id", "latest.zip")));
    AssertTrue(!File.Exists(System.IO.Path.Combine(backupsRoot, "Game One-game-id", "notes.txt")),
        "Expected non-zip WebDAV entries to be ignored.");
    var propfindRequest = handler.Requests.Single(request => request.Method.Method == "PROPFIND");
    AssertEqual("https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/", propfindRequest.RequestUri!.AbsoluteUri);
    AssertTrue(propfindRequest.Headers.TryGetValues("Depth", out var depthValues) &&
            depthValues.Single() == "1",
        "Expected bounded PROPFIND for save backup downloads.");
}

static void WebDavManualSyncWalksSaveBackupSubdirectories()
{
    using var workspace = TempDirectory.Create();
    var backupsRoot = System.IO.Path.Combine(workspace.Path, "SaveBackups");
    var handler = new RecordingDownloadHttpMessageHandler();
    handler.RespondWithText(
        "PROPFIND",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/",
        """
        <?xml version="1.0" encoding="utf-8"?>
        <d:multistatus xmlns:d="DAV:">
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/</d:href>
            </d:response>
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20One-game-id/</d:href>
            </d:response>
        </d:multistatus>
        """);
    handler.RespondWithText(
        "PROPFIND",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/",
        """
        <?xml version="1.0" encoding="utf-8"?>
        <d:multistatus xmlns:d="DAV:">
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20One-game-id/</d:href>
            </d:response>
            <d:response>
                <d:href>/dav/FireflyGameManager/save-backups/Game%20One-game-id/latest.zip</d:href>
            </d:response>
        </d:multistatus>
        """);
    handler.RespondWithText(
        "GET",
        "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/latest.zip",
        "zip-from-subdirectory");
    var service = new WebDavManualSyncService(() => new HttpClient(handler));
    var settings = CreateWebDavSettings();

    var result = service.DownloadSaveBackupsAsync(settings, backupsRoot).GetAwaiter().GetResult();

    AssertEqual(1, result.DownloadedCount);
    AssertEqual(0, result.FailedCount);
    AssertTrue(result.Success, "Expected backup zip inside a game subdirectory to be downloaded.");
    AssertEqual("zip-from-subdirectory", File.ReadAllText(System.IO.Path.Combine(backupsRoot, "Game One-game-id", "latest.zip")));
    AssertTrue(handler.Requests.Any(request =>
            request.Method.Method == "PROPFIND" &&
            request.RequestUri!.AbsoluteUri == "https://dav.jianguoyun.com/dav/FireflyGameManager/save-backups/Game%20One-game-id/"),
        "Expected downloader to walk into the game backup directory.");
}

static void SqliteGameLibraryMergeAddsRemoteAndKeepsNewestGameInfo()
{
    using var workspace = TempDirectory.Create();
    var localDatabasePath = System.IO.Path.Combine(workspace.Path, "local.db");
    var remoteDatabasePath = System.IO.Path.Combine(workspace.Path, "remote.db");
    InsertGameRow(
        localDatabasePath,
        id: "local-only",
        name: "Local Only",
        totalPlaySeconds: 100,
        lastLaunchTime: "2026-06-01T08:00:00.0000000Z",
        sortOrder: 0,
        createdAt: "2026-06-01T00:00:00.0000000Z",
        updatedAt: "2026-06-01T08:00:00.0000000Z");
    InsertGameRow(
        localDatabasePath,
        id: "shared-game",
        name: "Local Name",
        totalPlaySeconds: 7200,
        lastLaunchTime: "2026-06-03T08:00:00.0000000Z",
        sortOrder: 1,
        createdAt: "2026-06-01T00:00:00.0000000Z",
        updatedAt: "2026-06-03T08:00:00.0000000Z");
    InsertGameRow(
        remoteDatabasePath,
        id: "shared-game",
        name: "Remote Name",
        executablePath: @"D:\Remote\Shared\Shared.exe",
        totalPlaySeconds: 3600,
        lastLaunchTime: "2026-06-04T08:00:00.0000000Z",
        sortOrder: 0,
        createdAt: "2026-06-01T00:00:00.0000000Z",
        updatedAt: "2026-06-04T08:00:00.0000000Z");
    InsertGameRow(
        remoteDatabasePath,
        id: "remote-only",
        name: "Remote Only",
        totalPlaySeconds: 300,
        lastLaunchTime: "2026-06-04T09:00:00.0000000Z",
        sortOrder: 1,
        createdAt: "2026-06-04T00:00:00.0000000Z",
        updatedAt: "2026-06-04T09:00:00.0000000Z");
    var mergeService = new SqliteGameLibraryMergeService();

    var result = mergeService.MergeRemoteIntoLocal(localDatabasePath, remoteDatabasePath);

    AssertEqual(1, result.AddedCount);
    AssertEqual(1, result.UpdatedCount);
    var games = new SqliteGameLibraryService(localDatabasePath).GetGames();
    AssertTrue(games.Any(game => game.Id == "local-only"), "Expected local-only game to stay.");
    AssertTrue(games.Any(game => game.Id == "remote-only"), "Expected remote-only game to be added locally.");
    var shared = games.Single(game => game.Id == "shared-game");
    AssertEqual("Remote Name", shared.Name);
    AssertEqual(@"D:\Remote\Shared\Shared.exe", shared.ExecutablePath);
    AssertEqual(TimeSpan.FromSeconds(7200), shared.TotalPlayTime);
    AssertEqual(new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc), shared.LastLaunchTime!.Value.ToUniversalTime());
}

static void SaveBackupMergeCopiesMissingAndKeepsNewestFile()
{
    using var workspace = TempDirectory.Create();
    var localBackups = System.IO.Path.Combine(workspace.Path, "local");
    var remoteBackups = System.IO.Path.Combine(workspace.Path, "remote");
    var localGameDirectory = System.IO.Path.Combine(localBackups, "Game One-game-id");
    var remoteGameDirectory = System.IO.Path.Combine(remoteBackups, "Game One-game-id");
    Directory.CreateDirectory(localGameDirectory);
    Directory.CreateDirectory(remoteGameDirectory);
    var sharedLocal = System.IO.Path.Combine(localGameDirectory, "shared.zip");
    var sharedRemote = System.IO.Path.Combine(remoteGameDirectory, "shared.zip");
    var localOnly = System.IO.Path.Combine(localGameDirectory, "local-only.zip");
    var remoteOnly = System.IO.Path.Combine(remoteGameDirectory, "remote-only.zip");
    File.WriteAllText(sharedLocal, "older-local");
    File.WriteAllText(sharedRemote, "newer-remote");
    File.WriteAllText(localOnly, "keep-local");
    File.WriteAllText(remoteOnly, "copy-remote");
    File.SetLastWriteTimeUtc(sharedLocal, new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc));
    File.SetLastWriteTimeUtc(sharedRemote, new DateTime(2026, 6, 4, 8, 0, 0, DateTimeKind.Utc));
    var mergeService = new SaveBackupMergeService();

    var result = mergeService.MergeRemoteIntoLocal(localBackups, remoteBackups);

    AssertEqual(1, result.AddedCount);
    AssertEqual(1, result.UpdatedCount);
    AssertEqual("newer-remote", File.ReadAllText(sharedLocal));
    AssertEqual("copy-remote", File.ReadAllText(System.IO.Path.Combine(localGameDirectory, "remote-only.zip")));
    AssertEqual("keep-local", File.ReadAllText(localOnly));
}

static void WebDavFullSyncDownloadsMergesAndUploads()
{
    using var workspace = TempDirectory.Create();
    var localDatabasePath = System.IO.Path.Combine(workspace.Path, "app.db");
    var localBackups = System.IO.Path.Combine(workspace.Path, "SaveBackups");
    InsertGameRow(
        localDatabasePath,
        id: "local-only",
        name: "Local Only",
        totalPlaySeconds: 100,
        lastLaunchTime: string.Empty,
        sortOrder: 0,
        createdAt: "2026-06-01T00:00:00.0000000Z",
        updatedAt: "2026-06-01T08:00:00.0000000Z");
    Directory.CreateDirectory(System.IO.Path.Combine(localBackups, "Local Game-local-only"));
    File.WriteAllText(System.IO.Path.Combine(localBackups, "Local Game-local-only", "local.zip"), "local-backup");
    var remoteDatabasePath = System.IO.Path.Combine(workspace.Path, "remote.db");
    InsertGameRow(
        remoteDatabasePath,
        id: "remote-only",
        name: "Remote Only",
        totalPlaySeconds: 200,
        lastLaunchTime: string.Empty,
        sortOrder: 0,
        createdAt: "2026-06-02T00:00:00.0000000Z",
        updatedAt: "2026-06-02T08:00:00.0000000Z");
    var remoteBackups = System.IO.Path.Combine(workspace.Path, "RemoteBackups");
    Directory.CreateDirectory(System.IO.Path.Combine(remoteBackups, "Remote Game-remote-only"));
    File.WriteAllText(System.IO.Path.Combine(remoteBackups, "Remote Game-remote-only", "remote.zip"), "remote-backup");
    var manualSync = new RecordingFullSyncManualSyncService(remoteDatabasePath, remoteBackups);
    var fullSync = new WebDavFullSyncService(
        manualSync,
        new SqliteGameLibraryMergeService(),
        new SaveBackupMergeService(),
        () => System.IO.Path.Combine(workspace.Path, "sync-temp"));
    var settings = CreateWebDavSettings();

    var result = fullSync.SynchronizeAsync(settings, localDatabasePath, localBackups).GetAwaiter().GetResult();

    AssertTrue(result.Success, "Expected full sync to succeed.");
    AssertTrue(new SqliteGameLibraryService(localDatabasePath).GetGames().Any(game => game.Id == "remote-only"),
        "Expected remote game to be merged into local database.");
    AssertEqual("remote-backup", File.ReadAllText(System.IO.Path.Combine(localBackups, "Remote Game-remote-only", "remote.zip")));
    AssertEqual(localDatabasePath, manualSync.UploadedUserDataPath);
    AssertEqual(localBackups, manualSync.UploadedSaveBackupsDirectory);
}

static void WebDavSettingsUploadCommandsCallSyncService()
{
    var store = new RecordingWebDavSettingsStore();
    var syncService = new RecordingWebDavManualSyncService();
    var settings = new WebDavSettingsViewModel(
        store,
        new RecordingWebDavConnectionTester(true, "连接成功"),
        syncService,
        () => { },
        @"D:\Local\app.db",
        @"D:\Local\SaveBackups")
    {
        ServerUrl = "https://dav.jianguoyun.com/dav/",
        Username = "player@example.com",
        ApplicationPassword = "app-password",
        RemoteDirectory = "FireflyGameManager"
    };

    ((GameManager.App.Commands.AsyncRelayCommand)settings.UploadUserDataCommand).ExecuteAsync(null).GetAwaiter().GetResult();
    ((GameManager.App.Commands.AsyncRelayCommand)settings.UploadSaveBackupsCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual(@"D:\Local\app.db", syncService.UserDataPath);
    AssertEqual(@"D:\Local\SaveBackups", syncService.SaveBackupsDirectory);
    AssertEqual("FireflyGameManager", syncService.LastSettings!.RemoteDirectory);
    AssertTrue(settings.UploadStatusText.Contains("存档备份", StringComparison.Ordinal),
        "Expected upload status text to mention the last upload operation.");
}

static void WebDavSettingsViewHasManualUploadButtons()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "WebDavSettingsView.xaml"));

    AssertTrue(xaml.Contains("Command=\"{Binding UploadUserDataCommand}\"", StringComparison.Ordinal),
        "Settings view should expose an upload user data button.");
    AssertTrue(xaml.Contains("Command=\"{Binding UploadSaveBackupsCommand}\"", StringComparison.Ordinal),
        "Settings view should expose an upload save backups button.");
    AssertTrue(xaml.Contains("UploadStatusText", StringComparison.Ordinal),
        "Settings view should show manual upload status.");
}

static void WebDavSettingsDownloadCommandsCallSyncService()
{
    var store = new RecordingWebDavSettingsStore();
    var syncService = new RecordingWebDavManualSyncService();
    var settings = new WebDavSettingsViewModel(
        store,
        new RecordingWebDavConnectionTester(true, "连接成功"),
        syncService,
        () => { },
        @"D:\Local\app.db",
        @"D:\Local\SaveBackups")
    {
        ServerUrl = "https://dav.jianguoyun.com/dav/",
        Username = "player@example.com",
        ApplicationPassword = "app-password",
        RemoteDirectory = "FireflyGameManager"
    };

    ((GameManager.App.Commands.AsyncRelayCommand)settings.DownloadUserDataCommand).ExecuteAsync(null).GetAwaiter().GetResult();
    ((GameManager.App.Commands.AsyncRelayCommand)settings.DownloadSaveBackupsCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual(@"D:\Local\app.db", syncService.DownloadUserDataPath);
    AssertEqual(@"D:\Local\SaveBackups", syncService.DownloadSaveBackupsDirectory);
    AssertEqual("FireflyGameManager", syncService.LastSettings!.RemoteDirectory);
    AssertTrue(settings.DownloadStatusText.Contains("save backups", StringComparison.Ordinal),
        "Expected download status text to mention the last download operation.");
}

static void WebDavSettingsViewHasManualDownloadButtons()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "WebDavSettingsView.xaml"));

    AssertTrue(xaml.Contains("Command=\"{Binding DownloadUserDataCommand}\"", StringComparison.Ordinal),
        "Settings view should expose a download user data button.");
    AssertTrue(xaml.Contains("Command=\"{Binding DownloadSaveBackupsCommand}\"", StringComparison.Ordinal),
        "Settings view should expose a download save backups button.");
    AssertTrue(xaml.Contains("DownloadStatusText", StringComparison.Ordinal),
        "Settings view should show manual download status.");
}

static void WebDavSettingsFullSyncCommandCallsSyncService()
{
    var store = new RecordingWebDavSettingsStore();
    var syncService = new RecordingWebDavManualSyncService();
    var fullSyncService = new RecordingWebDavFullSyncService();
    var settings = new WebDavSettingsViewModel(
        store,
        new RecordingWebDavConnectionTester(true, "连接成功"),
        syncService,
        fullSyncService,
        () => { },
        @"D:\Local\app.db",
        @"D:\Local\SaveBackups")
    {
        ServerUrl = "https://dav.jianguoyun.com/dav/",
        Username = "player@example.com",
        ApplicationPassword = "app-password",
        RemoteDirectory = "FireflyGameManager"
    };

    ((GameManager.App.Commands.AsyncRelayCommand)settings.FullSyncCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual(@"D:\Local\app.db", fullSyncService.DatabasePath);
    AssertEqual(@"D:\Local\SaveBackups", fullSyncService.SaveBackupsDirectory);
    AssertEqual("FireflyGameManager", fullSyncService.LastSettings!.RemoteDirectory);
    AssertTrue(settings.SyncStatusText.Contains("同步完成", StringComparison.Ordinal),
        "Expected sync status text to show the full sync result.");
}

static void WebDavSettingsViewHasFullSyncButton()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "WebDavSettingsView.xaml"));

    AssertTrue(xaml.Contains("Command=\"{Binding FullSyncCommand}\"", StringComparison.Ordinal),
        "Settings view should expose a full sync button.");
    AssertTrue(xaml.Contains("SyncStatusText", StringComparison.Ordinal),
        "Settings view should show full sync status.");
}

static void WebDavSettingsViewGroupsSyncCenterAndAdvancedActions()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "WebDavSettingsView.xaml"));

    AssertTrue(xaml.Contains("x:Name=\"SyncCenterCard\"", StringComparison.Ordinal),
        "Settings view should present full sync as a dedicated sync center card.");
    AssertTrue(xaml.Contains("x:Name=\"AdvancedSyncActions\"", StringComparison.Ordinal),
        "Settings view should move manual upload/download commands into an advanced area.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource SectionCardStyle}\"", StringComparison.Ordinal),
        "Settings view should use shared section cards instead of loose stacked controls.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", StringComparison.Ordinal),
        "Full sync should use the shared primary button style.");
}

static void WebDavSyncStrategyIsShownAsTooltip()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "WebDavSettingsView.xaml"));

    AssertTrue(xaml.Contains("x:Name=\"SyncPolicyInfoButton\"", StringComparison.Ordinal),
        "Sync center should expose the sync policy from an info icon.");
    AssertTrue(xaml.Contains("<Button.ToolTip>", StringComparison.Ordinal),
        "Sync policy info icon should reveal its text on hover.");
    AssertTrue(!xaml.Contains("x:Name=\"SyncPolicyCard\"", StringComparison.Ordinal),
        "Sync policy should no longer occupy a separate card.");
}

static void OpensWebDavSettingsAndSavesConfig()
{
    var settingsStore = new RecordingWebDavSettingsStore();
    var viewModel = new MainWindowViewModel(
        new InMemoryGameLibraryService(),
        new QueuedFilePickerService(),
        new ImmediateGameLauncher(new GameManager.App.Models.LaunchResult(DateTime.Now, TimeSpan.Zero)),
        new RecordingSaveBackupService(@"D:\Backups\new.zip"),
        settingsStore,
        new RecordingWebDavConnectionTester(true, "连接成功"));

    viewModel.ShowSyncCommand.Execute(null);
    var settings = (WebDavSettingsViewModel)viewModel.CurrentViewModel;
    settings.ServerUrl = "https://dav.jianguoyun.com/dav/";
    settings.Username = "player@example.com";
    settings.ApplicationPassword = "app-password";
    settings.RemoteDirectory = "FireflyGameManager";
    settings.SaveSettingsCommand.Execute(null);

    AssertEqual("同步", viewModel.PageTitle);
    AssertTrue(viewModel.CurrentViewModel is WebDavSettingsViewModel, "Expected WebDAV settings view model.");
    AssertEqual("https://dav.jianguoyun.com/dav/", settingsStore.SavedSettings!.ServerUrl);
    AssertEqual("player@example.com", settingsStore.SavedSettings.Username);
    AssertEqual("app-password", settingsStore.SavedSettings.ApplicationPassword);
    AssertEqual("FireflyGameManager", settingsStore.SavedSettings.RemoteDirectory);
}

static void WebDavSettingsTestConnectionUpdatesStatus()
{
    var store = new RecordingWebDavSettingsStore();
    var tester = new RecordingWebDavConnectionTester(true, "连接成功");
    var settings = new WebDavSettingsViewModel(store, tester, () => { })
    {
        ServerUrl = "https://dav.jianguoyun.com/dav/",
        Username = "player@example.com",
        ApplicationPassword = "app-password",
        RemoteDirectory = "FireflyGameManager"
    };

    ((GameManager.App.Commands.AsyncRelayCommand)settings.TestConnectionCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertTrue(tester.LastSettings is not null, "Expected test connection to receive settings.");
    AssertEqual("https://dav.jianguoyun.com/dav/", tester.LastSettings!.ServerUrl);
    AssertEqual("连接成功", settings.ConnectionStatusText);
}

static void MainWindowSeparatesSyncAndSettingsRoutes()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));

    AssertTrue(xaml.Contains("DataType=\"{x:Type vm:WebDavSettingsViewModel}\"", StringComparison.Ordinal),
        "MainWindow should define a sync data template.");
    AssertTrue(xaml.Contains("DataType=\"{x:Type vm:AppearanceSettingsViewModel}\"", StringComparison.Ordinal),
        "MainWindow should define a separate appearance settings data template.");
    AssertTrue(xaml.Contains("Command=\"{Binding ShowSyncCommand}\"", StringComparison.Ordinal),
        "MainWindow should expose a dedicated sync button.");
    AssertTrue(xaml.Contains("Command=\"{Binding ShowSettingsCommand}\"", StringComparison.Ordinal),
        "MainWindow should expose a settings button.");
    AssertTrue(!xaml.Contains("SQLite + WebDAV", StringComparison.Ordinal),
        "The obsolete local library status box should be removed from navigation.");
}

static void AppearanceSettingsStoreSavesAndLoadsConfig()
{
    using var workspace = TempDirectory.Create();
    var path = System.IO.Path.Combine(workspace.Path, "appearance-settings.json");
    var store = new JsonAppearanceSettingsStore(path);
    var expected = new AppearanceSettings(@"D:\Wallpapers\forest.jpg", true);

    store.Save(expected);
    var actual = store.Load();

    AssertEqual(expected.WallpaperPath, actual.WallpaperPath);
    AssertEqual(expected.IsTransparentUi, actual.IsTransparentUi);
}

static void AppearanceSettingsSelectsAndAppliesWallpaper()
{
    var picker = new QueuedFilePickerService
    {
        WallpaperPath = @"D:\Wallpapers\forest.jpg"
    };
    var store = new RecordingAppearanceSettingsStore();
    AppearanceSettings? applied = null;
    var settings = new AppearanceSettingsViewModel(picker, store, value => applied = value, () => { });

    settings.SelectWallpaperCommand.Execute(null);
    settings.IsTransparentUi = true;

    AssertEqual(@"D:\Wallpapers\forest.jpg", settings.WallpaperPath);
    AssertEqual(1, picker.WallpaperPickerCalls);
    AssertEqual(@"D:\Wallpapers\forest.jpg", store.SavedSettings!.WallpaperPath);
    AssertTrue(store.SavedSettings.IsTransparentUi, "Expected transparent UI preference to be saved.");
    AssertEqual(store.SavedSettings.WallpaperPath, applied!.WallpaperPath);
    AssertTrue(applied.IsTransparentUi, "Expected appearance changes to be applied immediately.");
}

static void AppearanceSettingsStartsOnOverviewAndOpensSections()
{
    var settings = new AppearanceSettingsViewModel(
        new QueuedFilePickerService(),
        new RecordingAppearanceSettingsStore(),
        _ => { },
        () => { });

    AssertTrue(settings.IsOverviewSelected, "Settings should open on the category overview.");
    AssertTrue(!settings.IsGeneralSectionSelected, "General settings should be hidden until selected.");

    settings.ShowSectionCommand.Execute("General");

    AssertTrue(!settings.IsOverviewSelected, "Overview should hide after selecting a category.");
    AssertTrue(settings.IsGeneralSectionSelected, "General settings should show after selecting the category.");
    AssertTrue(!settings.IsAppearanceSectionSelected, "Appearance settings should remain hidden.");

    settings.ShowOverviewCommand.Execute(null);

    AssertTrue(settings.IsOverviewSelected, "Overview should show again after going back.");
    AssertTrue(!settings.IsGeneralSectionSelected, "General settings should hide after going back.");
}

static void AppearanceSettingsViewUsesLayeredSections()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "AppearanceSettingsView.xaml"));

    AssertTrue(xaml.Contains("x:Name=\"SettingsOverview\"", StringComparison.Ordinal),
        "Settings should first show a category overview.");
    AssertTrue(xaml.Contains("Command=\"{Binding ShowSectionCommand}\"", StringComparison.Ordinal)
        && xaml.Contains("CommandParameter=\"General\"", StringComparison.Ordinal)
        && xaml.Contains("CommandParameter=\"Appearance\"", StringComparison.Ordinal)
        && xaml.Contains("CommandParameter=\"Data\"", StringComparison.Ordinal),
        "Category rows should open their matching setting sections.");
    AssertTrue(xaml.Contains("x:Name=\"GeneralSettingsSection\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"LaunchSettingsSection\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"LibrarySettingsSection\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"AppearanceSettingsSection\"", StringComparison.Ordinal)
        && xaml.Contains("x:Name=\"DataSettingsSection\"", StringComparison.Ordinal),
        "Settings details should be split into individual sections.");
    AssertTrue(xaml.Contains("Command=\"{Binding ShowOverviewCommand}\"", StringComparison.Ordinal),
        "Each settings detail view should be able to return to the category overview.");
    AssertTrue(!xaml.Contains("MaxWidth=\"820\"", StringComparison.Ordinal),
        "Settings overview and details should use the available content width.");
    AssertTrue(xaml.Contains("HorizontalContentAlignment=\"Stretch\"", StringComparison.Ordinal),
        "Settings scroll viewers should stretch their content across the main area.");
}

static void SettingsControlsUseModernSharedStyles()
{
    var styles = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Styles", "AppStyles.xaml"));
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "AppearanceSettingsView.xaml"));

    AssertTrue(styles.Contains("x:Key=\"SettingsSectionButtonStyle\"", StringComparison.Ordinal),
        "Settings category rows should have a shared style.");
    AssertTrue(styles.Contains("x:Key=\"SettingsOptionRowStyle\"", StringComparison.Ordinal),
        "Settings option rows should have a shared style.");
    AssertTrue(styles.Contains("x:Key=\"ModernComboBoxStyle\"", StringComparison.Ordinal),
        "Settings dropdowns should use a modern shared combo box style.");
    AssertTrue(styles.Contains("x:Key=\"ModernScrollBarStyle\"", StringComparison.Ordinal),
        "Settings should use a modern shared scrollbar style.");
    AssertTrue(styles.Contains("SelectedItem.Label", StringComparison.Ordinal),
        "The custom combo box template should display the selected option label instead of the option object.");
    AssertTrue(styles.Contains("Content.Label", StringComparison.Ordinal),
        "The custom combo box item template should display each option label instead of the option object.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource ModernComboBoxStyle}\"", StringComparison.Ordinal),
        "Settings dropdowns such as close behavior and language should use the modern combo box style.");
    AssertTrue(xaml.Contains("Style=\"{StaticResource SettingsOptionRowStyle}\"", StringComparison.Ordinal)
        && xaml.Contains("Style=\"{StaticResource ToggleSwitchStyle}\"", StringComparison.Ordinal),
        "Settings rows and right-side switches should share the modern styling system.");
    AssertTrue(xaml.Contains("BasedOn=\"{StaticResource ModernScrollBarStyle}\"", StringComparison.Ordinal),
        "Settings scroll viewers should replace the default system scrollbar chrome.");
}

static void AppSettingsStoreSavesAllSchemeAPreferences()
{
    using var workspace = TempDirectory.Create();
    var store = new JsonAppSettingsStore(System.IO.Path.Combine(workspace.Path, "app-settings.json"));
    var expected = new AppSettings
    {
        StartWithWindows = true,
        StartMinimized = true,
        CloseBehavior = AppCloseBehavior.MinimizeToTray,
        RememberLastPage = true,
        LastPage = AppPage.Sync,
        Language = AppLanguage.English,
        MinimizeAfterGameLaunch = true,
        RestoreAfterGameExit = true,
        BackupBeforeGameLaunch = true,
        DefaultSort = GameSortMode.PlayTime,
        CardSize = GameCardSize.Large,
        ShowPlayTimeOnCards = true,
        ScanDirectory = @"D:\Games"
    };

    store.Save(expected);
    var actual = store.Load();

    AssertEqual(expected.StartWithWindows, actual.StartWithWindows);
    AssertEqual(expected.CloseBehavior, actual.CloseBehavior);
    AssertEqual(expected.LastPage, actual.LastPage);
    AssertEqual(expected.Language, actual.Language);
    AssertEqual(expected.BackupBeforeGameLaunch, actual.BackupBeforeGameLaunch);
    AssertEqual(expected.DefaultSort, actual.DefaultSort);
    AssertEqual(expected.CardSize, actual.CardSize);
    AssertEqual(expected.ScanDirectory, actual.ScanDirectory);
}

static void SqlitePersistsPerGameLaunchOptions()
{
    using var database = TempDatabase.Create();
    var service = new SqliteGameLibraryService(database.Path);
    var request = new AddGameRequest(
        "Configured Game",
        @"D:\Games\Configured\Configured.exe",
        @"D:\Games\Configured",
        @"C:\Saves\Configured",
        null,
        "-windowed -skipintro",
        true);

    var game = service.AddGame(request);
    var reloaded = new SqliteGameLibraryService(database.Path).GetGames().Single();

    AssertEqual("-windowed -skipintro", game.LaunchArguments);
    AssertTrue(game.RunAsAdministrator, "Expected administrator launch setting.");
    AssertEqual(game.LaunchArguments, reloaded.LaunchArguments);
    AssertEqual(game.RunAsAdministrator, reloaded.RunAsAdministrator);
}

static void GameLibraryAppliesDisplayPreferences()
{
    var games = new[]
    {
        new Game("1", "Bravo", "b.exe", "b", "b-save", null, TimeSpan.FromMinutes(10), new DateTime(2026, 1, 1)),
        new Game("2", "Alpha", "a.exe", "a", "a-save", null, TimeSpan.FromMinutes(80), new DateTime(2026, 2, 1))
    };
    var library = new GameLibraryViewModel(games, _ => { }, _ => { }, _ => { }, _ => { });

    library.ApplySettings(new AppSettings
    {
        DefaultSort = GameSortMode.PlayTime,
        CardSize = GameCardSize.Large,
        ShowPlayTimeOnCards = true
    });

    AssertEqual("Alpha", library.Games[0].Name);
    AssertEqual(220d, library.CardWidth);
    AssertEqual(302d, library.CardHeight);
    AssertTrue(library.ShowPlayTimeOnCards, "Expected play time labels to be visible.");
}

static void LocalGameDiscoveryFindsExecutableCandidatesAndSkipsDuplicates()
{
    using var workspace = TempDirectory.Create();
    var firstDirectory = System.IO.Path.Combine(workspace.Path, "First");
    var secondDirectory = System.IO.Path.Combine(workspace.Path, "Second");
    Directory.CreateDirectory(firstDirectory);
    Directory.CreateDirectory(secondDirectory);
    var firstExecutable = System.IO.Path.Combine(firstDirectory, "FirstGame.exe");
    File.WriteAllText(firstExecutable, string.Empty);
    File.WriteAllText(System.IO.Path.Combine(firstDirectory, "unins000.exe"), string.Empty);
    File.WriteAllText(System.IO.Path.Combine(secondDirectory, "SecondGame.exe"), string.Empty);

    var discovered = new LocalGameDiscoveryService().Discover(workspace.Path, [firstExecutable]);

    AssertEqual(1, discovered.Count);
    AssertEqual("SecondGame", discovered[0].Name);
}

static void LaunchWorkflowBacksUpMinimizesAndRestores()
{
    var game = CreateGame("launch-workflow", "Launch Workflow", @"C:\Saves\LaunchWorkflow");
    var launcher = new ImmediateGameLauncher(new LaunchResult(DateTime.Now, TimeSpan.FromMinutes(5)));
    var backup = new RecordingSaveBackupService(@"C:\Backups\launch.zip");
    var presentation = new RecordingGameSessionPresentationService();
    var settingsStore = new RecordingAppSettingsStore(new AppSettings
    {
        BackupBeforeGameLaunch = true,
        MinimizeAfterGameLaunch = true,
        RestoreAfterGameExit = true
    });
    var detail = new GameDetailViewModel(
        game,
        launcher,
        (_, _) => game,
        _ => { },
        () => { },
        backup,
        new QueuedFilePickerService(),
        settingsStore,
        presentation);

    ((GameManager.App.Commands.AsyncRelayCommand)detail.StartGameCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertEqual(game.Id, backup.BackupGameId);
    AssertEqual(1, presentation.MinimizeCalls);
    AssertEqual(1, presentation.RestoreCalls);
}

static void LaunchWorkflowReportsFailuresWithoutThrowing()
{
    var game = CreateGame("launch-failure", "Launch Failure", string.Empty);
    var detail = new GameDetailViewModel(
        game,
        new FailingGameLauncher("UAC cancelled"),
        (_, _) => game,
        _ => { },
        () => { },
        new RecordingSaveBackupService(@"C:\Backups\launch.zip"),
        new QueuedFilePickerService());

    ((GameManager.App.Commands.AsyncRelayCommand)detail.StartGameCommand).ExecuteAsync(null).GetAwaiter().GetResult();

    AssertTrue(detail.LaunchStatusText.Contains("UAC cancelled", StringComparison.Ordinal),
        "Expected launch failure to be shown in the detail view.");
}

static void LocalDataMaintenanceExportsImportsAndClearsInvalidFiles()
{
    using var workspace = TempDirectory.Create();
    var dataDirectory = System.IO.Path.Combine(workspace.Path, "data");
    var backups = System.IO.Path.Combine(dataDirectory, "SaveBackups");
    var validDirectory = System.IO.Path.Combine(backups, "Valid-valid-id");
    var invalidDirectory = System.IO.Path.Combine(backups, "Missing-missing-id");
    var coverCache = System.IO.Path.Combine(dataDirectory, "CoverCache");
    Directory.CreateDirectory(validDirectory);
    Directory.CreateDirectory(invalidDirectory);
    Directory.CreateDirectory(coverCache);
    File.WriteAllText(System.IO.Path.Combine(dataDirectory, "app.db"), "database");
    CreateZipWithFile(System.IO.Path.Combine(validDirectory, "valid.zip"), "save.txt", "save");
    CreateZipWithFile(System.IO.Path.Combine(invalidDirectory, "invalid.zip"), "save.txt", "save");
    File.WriteAllText(System.IO.Path.Combine(coverCache, "cover.jpg"), "cover");
    var exportPath = System.IO.Path.Combine(workspace.Path, "export.zip");
    var service = new LocalDataMaintenanceService(dataDirectory);

    service.Export(exportPath);
    File.Delete(System.IO.Path.Combine(dataDirectory, "app.db"));
    service.Import(exportPath);
    var removed = service.ClearInvalidBackups(["valid-id"]);
    service.ClearCoverCache();

    AssertTrue(File.Exists(System.IO.Path.Combine(dataDirectory, "app.db")), "Expected import to restore database.");
    AssertEqual(1, removed);
    AssertTrue(Directory.Exists(validDirectory), "Expected valid backup directory to remain.");
    AssertTrue(!Directory.Exists(invalidDirectory), "Expected invalid backup directory to be removed.");
    AssertEqual(0, Directory.EnumerateFiles(coverCache).Count());
}

static void MainWindowSupportsSystemTrayLifecycle()
{
    var project = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "GameManager.App.csproj"));
    var window = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml"));
    var codeBehind = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "MainWindow.xaml.cs"));
    var trayService = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Services", "SystemTrayService.cs"));

    AssertTrue(project.Contains("<UseWindowsForms>true</UseWindowsForms>", StringComparison.Ordinal),
        "Expected Windows Forms support for NotifyIcon.");
    AssertTrue(window.Contains("Closing=\"MainWindow_Closing\"", StringComparison.Ordinal),
        "Expected close behavior to be intercepted.");
    AssertTrue(codeBehind.Contains("StartMinimized", StringComparison.Ordinal),
        "Expected startup minimized behavior.");
    AssertTrue(trayService.Contains("NotifyIcon", StringComparison.Ordinal),
        "Expected NotifyIcon-backed tray service.");
}

static void SettingsReadonlyPathsUseOneWayBindings()
{
    var xaml = File.ReadAllText(System.IO.Path.Combine("GameManager.App", "Views", "AppearanceSettingsView.xaml"));

    AssertTrue(xaml.Contains("Text=\"{Binding ScanDirectory, Mode=OneWay}\"", StringComparison.Ordinal),
        "Read-only scan directory text box must not create a TwoWay binding to a private setter.");
}

static void WpfAppearanceThemeUpdatesLiveBrushResources()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        using var workspace = TempDirectory.Create();
        try
        {
            var warmPath = System.IO.Path.Combine(workspace.Path, "warm.png");
            var coolPath = System.IO.Path.Combine(workspace.Path, "cool.png");
            WriteSolidPng(warmPath, System.Windows.Media.Color.FromRgb(225, 65, 40));
            WriteSolidPng(coolPath, System.Windows.Media.Color.FromRgb(35, 130, 220));

            var application = new System.Windows.Application();
            application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri("/GameManager.App;component/Styles/AppStyles.xaml", UriKind.RelativeOrAbsolute)
            });
            var service = new WpfAppearanceThemeService();

            service.Apply(new AppearanceSettings(warmPath, true));

            var background = (System.Windows.Media.SolidColorBrush)application.Resources["AppBackgroundBrush"];
            var surface = (System.Windows.Media.SolidColorBrush)application.Resources["SurfaceBrush"];
            var panel = (System.Windows.Media.SolidColorBrush)application.Resources["PanelBrush"];
            var warmAccent = ((System.Windows.Media.SolidColorBrush)application.Resources["AccentBrush"]).Color;
            var warmSurface = surface.Color;
            AssertTrue(background.Color.A <= 32, "Transparent appearance should keep the wallpaper clearly visible.");
            AssertTrue(surface.Color.A <= 170, "Navigation and content surfaces should behave like translucent glass.");
            AssertTrue(panel.Color.A <= 200, "Cards should remain translucent enough to reveal the wallpaper.");

            service.Apply(new AppearanceSettings(coolPath, true));
            var coolAccent = ((System.Windows.Media.SolidColorBrush)application.Resources["AccentBrush"]).Color;
            var coolSurface = ((System.Windows.Media.SolidColorBrush)application.Resources["SurfaceBrush"]).Color;
            AssertTrue(warmAccent != coolAccent, "Changing wallpaper should refresh the live accent brush.");
            AssertTrue(warmSurface != coolSurface, "Changing wallpaper should refresh the live surface brush.");
            application.Shutdown();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw failure;
    }
}

static void WallpaperPaletteAdaptsToDifferentColorSchemes()
{
    var warm = WallpaperThemePaletteFactory.CreateFromColors([
        System.Windows.Media.Color.FromRgb(220, 62, 38),
        System.Windows.Media.Color.FromRgb(242, 170, 64),
        System.Windows.Media.Color.FromRgb(252, 218, 170)
    ], true);
    var cool = WallpaperThemePaletteFactory.CreateFromColors([
        System.Windows.Media.Color.FromRgb(30, 78, 170),
        System.Windows.Media.Color.FromRgb(62, 180, 205),
        System.Windows.Media.Color.FromRgb(170, 226, 238)
    ], true);

    AssertTrue(warm.Accent != cool.Accent, "Different wallpapers should produce different accent colors.");
    AssertTrue(warm.Surface != cool.Surface, "Different wallpapers should tint glass surfaces differently.");
    AssertTrue(warm.Panel != cool.Panel, "Different wallpapers should tint cards differently.");
    AssertTrue(warm.Text != warm.PrimaryButtonText || cool.Text != cool.PrimaryButtonText,
        "Palette should calculate readable text contrast independently.");
}

static void WallpaperPaletteKeepsColorfulAccentOverNeutralMajority()
{
    var colors = Enumerable.Repeat(System.Windows.Media.Color.FromRgb(122, 126, 128), 100)
        .Concat(Enumerable.Repeat(System.Windows.Media.Color.FromRgb(38, 188, 212), 4));

    var palette = WallpaperThemePaletteFactory.CreateFromColors(colors, true);

    AssertTrue(palette.Accent.B > palette.Accent.R + 35,
        "A meaningful colorful swatch should become the accent even when neutral pixels are more common.");
    AssertTrue(palette.Surface.B > palette.Surface.R,
        "Glass surfaces should inherit part of the selected wallpaper accent.");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static bool ContainsGame(GameLibraryViewModel library, string id)
{
    return library.Games.Any(game => game.Id == id);
}

static GameManager.App.Models.Game FindGame(GameLibraryViewModel library, string id)
{
    return library.Games.Single(game => game.Id == id);
}

static GameManager.App.Models.AddGameRequest CreateAddGameRequest(string name)
{
    return new GameManager.App.Models.AddGameRequest(
        name,
        $@"D:\Games\{name}\{name}.exe",
        $@"D:\Games\{name}",
        $@"C:\Users\Public\Saved Games\{name}",
        $@"D:\Images\{name}.jpg");
}

static GameManager.App.Models.Game CreateGame(string id, string name, string savePath)
{
    return new GameManager.App.Models.Game(
        id,
        name,
        $@"D:\Games\{name}\{name}.exe",
        $@"D:\Games\{name}",
        savePath,
        null,
        TimeSpan.Zero,
        null);
}

static WebDavSettings CreateWebDavSettings()
{
    return new WebDavSettings(
        "https://dav.jianguoyun.com/dav/",
        "player@example.com",
        "app-password",
        "FireflyGameManager");
}

static void InsertGameRow(
    string databasePath,
    string id,
    string name,
    long totalPlaySeconds,
    string lastLaunchTime,
    long sortOrder,
    string createdAt,
    string updatedAt,
    string? executablePath = null,
    string? gameRootPath = null,
    string? savePath = null,
    string? coverImagePath = null)
{
    _ = new SqliteGameLibraryService(databasePath);
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Pooling = false
    }.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText =
        """
        INSERT INTO games (
            id,
            name,
            executable_path,
            game_root_path,
            save_path,
            cover_image_path,
            total_play_seconds,
            last_launch_time,
            sort_order,
            created_at,
            updated_at
        )
        VALUES (
            $id,
            $name,
            $executablePath,
            $gameRootPath,
            $savePath,
            $coverImagePath,
            $totalPlaySeconds,
            $lastLaunchTime,
            $sortOrder,
            $createdAt,
            $updatedAt
        );
        """;
    command.Parameters.AddWithValue("$id", id);
    command.Parameters.AddWithValue("$name", name);
    command.Parameters.AddWithValue("$executablePath", executablePath ?? $@"D:\Games\{name}\{name}.exe");
    command.Parameters.AddWithValue("$gameRootPath", gameRootPath ?? $@"D:\Games\{name}");
    command.Parameters.AddWithValue("$savePath", savePath ?? $@"C:\Users\Public\Saved Games\{name}");
    command.Parameters.AddWithValue("$coverImagePath", string.IsNullOrWhiteSpace(coverImagePath) ? DBNull.Value : coverImagePath);
    command.Parameters.AddWithValue("$totalPlaySeconds", totalPlaySeconds);
    command.Parameters.AddWithValue("$lastLaunchTime", lastLaunchTime);
    command.Parameters.AddWithValue("$sortOrder", sortOrder);
    command.Parameters.AddWithValue("$createdAt", createdAt);
    command.Parameters.AddWithValue("$updatedAt", updatedAt);
    command.ExecuteNonQuery();
}

static string ReadGameLibraryViewXaml()
{
    return File.ReadAllText(
        System.IO.Path.Combine("GameManager.App", "Views", "GameLibraryView.xaml"));
}

static string ReadGameDetailViewXaml()
{
    return File.ReadAllText(
        System.IO.Path.Combine("GameManager.App", "Views", "GameDetailView.xaml"));
}

static void CreateZipWithFile(string zipPath, string entryName, string content)
{
    using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    var entry = zip.CreateEntry(entryName);
    using var writer = new StreamWriter(entry.Open());
    writer.Write(content);
}

static void WriteSolidPng(string path, System.Windows.Media.Color color)
{
    var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(
        8,
        8,
        96,
        96,
        System.Windows.Media.PixelFormats.Bgra32,
        null);
    var pixels = Enumerable.Range(0, 64)
        .SelectMany(_ => new byte[] { color.B, color.G, color.R, color.A })
        .ToArray();
    bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, 8, 8), pixels, 8 * 4, 0);

    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using var stream = File.Create(path);
    encoder.Save(stream);
}

static string DecodeBasicAuth(AuthenticationHeaderValue authorization)
{
    var bytes = Convert.FromBase64String(authorization.Parameter ?? string.Empty);
    return System.Text.Encoding.UTF8.GetString(bytes);
}

sealed class QueuedFilePickerService : IFilePickerService
{
    public string? ExecutablePath { get; set; }

    public string? FolderPath { get; set; }

    public string? CoverImagePath { get; set; }

    public string? WallpaperPath { get; set; }

    public int WallpaperPickerCalls { get; private set; }

    public string? SaveBackupPath { get; set; }

    public string? ExportArchivePath { get; set; }

    public string? ImportArchivePath { get; set; }

    public string? LastBackupInitialDirectory { get; private set; }

    public string? PickExecutableFile()
    {
        return ExecutablePath;
    }

    public string? PickFolder(string title)
    {
        return FolderPath;
    }

    public string? PickCoverImage()
    {
        return CoverImagePath;
    }

    public string? PickWallpaperImage()
    {
        WallpaperPickerCalls++;
        return WallpaperPath;
    }

    public string? PickSaveBackupFile(string initialDirectory)
    {
        LastBackupInitialDirectory = initialDirectory;
        return SaveBackupPath;
    }

    public string? PickExportArchivePath()
    {
        return ExportArchivePath;
    }

    public string? PickImportArchiveFile()
    {
        return ImportArchivePath;
    }
}

sealed class RecordingAppearanceSettingsStore : IAppearanceSettingsStore
{
    public AppearanceSettings SettingsToLoad { get; set; } = AppearanceSettings.Default;

    public AppearanceSettings? SavedSettings { get; private set; }

    public AppearanceSettings Load()
    {
        return SettingsToLoad;
    }

    public void Save(AppearanceSettings settings)
    {
        SavedSettings = settings;
        SettingsToLoad = settings;
    }
}

sealed class RecordingAppSettingsStore : IAppSettingsStore
{
    private AppSettings settings;

    public RecordingAppSettingsStore(AppSettings settings)
    {
        this.settings = settings;
    }

    public AppSettings Load()
    {
        return settings;
    }

    public void Save(AppSettings settings)
    {
        this.settings = settings;
    }
}

sealed class RecordingGameSessionPresentationService : IGameSessionPresentationService
{
    public int MinimizeCalls { get; private set; }

    public int RestoreCalls { get; private set; }

    public void Minimize()
    {
        MinimizeCalls++;
    }

    public void Restore()
    {
        RestoreCalls++;
    }
}

sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FireflyGameManagerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}

sealed class TempDatabase : IDisposable
{
    private TempDatabase(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempDatabase Create()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FireflyGameManagerTests");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, $"{Guid.NewGuid():N}.db");
        return new TempDatabase(path);
    }

    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}

sealed class ImmediateGameLauncher : IGameLauncher
{
    private readonly GameManager.App.Models.LaunchResult result;

    public ImmediateGameLauncher(GameManager.App.Models.LaunchResult result)
    {
        this.result = result;
    }

    public Task<GameManager.App.Models.LaunchResult> LaunchAsync(GameManager.App.Models.Game game)
    {
        return Task.FromResult(result);
    }
}

sealed class ControlledGameLauncher : IGameLauncher
{
    private readonly TaskCompletionSource<GameManager.App.Models.LaunchResult> completion = new();

    public TaskCompletionSource<bool> LaunchStarted { get; } = new();

    public Task<GameManager.App.Models.LaunchResult> LaunchAsync(GameManager.App.Models.Game game)
    {
        LaunchStarted.SetResult(true);
        return completion.Task;
    }

    public void Complete(GameManager.App.Models.LaunchResult result)
    {
        completion.SetResult(result);
    }
}

sealed class FailingGameLauncher : IGameLauncher
{
    private readonly string message;

    public FailingGameLauncher(string message)
    {
        this.message = message;
    }

    public Task<LaunchResult> LaunchAsync(Game game)
    {
        return Task.FromException<LaunchResult>(new InvalidOperationException(message));
    }
}

sealed class RecordingSaveBackupService : ISaveBackupService
{
    private readonly string backupPath;
    private readonly List<SaveBackupEntry> backups;

    public RecordingSaveBackupService(string backupPath, params SaveBackupEntry[] backups)
    {
        this.backupPath = backupPath;
        this.backups = backups.ToList();
    }

    public string? BackupGameId { get; private set; }

    public string? RestoreGameId { get; private set; }

    public string? RestoreBackupPath { get; private set; }

    public string? DeletedBackupPath { get; private set; }

    public Task<string> BackupAsync(GameManager.App.Models.Game game)
    {
        BackupGameId = game.Id;
        backups.Insert(0, new SaveBackupEntry(backupPath, System.IO.Path.GetFileName(backupPath), DateTime.Now, 100));
        return Task.FromResult(backupPath);
    }

    public Task RestoreAsync(GameManager.App.Models.Game game, string backupPath)
    {
        RestoreGameId = game.Id;
        RestoreBackupPath = backupPath;
        return Task.CompletedTask;
    }

    public string GetBackupDirectory(GameManager.App.Models.Game game)
    {
        return System.IO.Path.GetDirectoryName(backupPath) ?? string.Empty;
    }

    public IReadOnlyList<SaveBackupEntry> GetBackups(GameManager.App.Models.Game game)
    {
        return backups.ToList();
    }

    public bool DeleteBackup(string backupPath)
    {
        DeletedBackupPath = backupPath;
        var index = backups.FindIndex(backup => backup.Path == backupPath);
        if (index < 0)
        {
            return false;
        }

        backups.RemoveAt(index);
        return true;
    }
}

sealed class RecordingWebDavSettingsStore : IWebDavSettingsStore
{
    private WebDavSettings settings = WebDavSettings.Default;

    public WebDavSettings? SavedSettings { get; private set; }

    public WebDavSettings Load()
    {
        return SavedSettings ?? settings;
    }

    public void Save(WebDavSettings settings)
    {
        SavedSettings = settings;
        this.settings = settings;
    }
}

sealed class RecordingWebDavConnectionTester : IWebDavConnectionTester
{
    private readonly bool success;
    private readonly string message;

    public RecordingWebDavConnectionTester(bool success, string message)
    {
        this.success = success;
        this.message = message;
    }

    public WebDavSettings? LastSettings { get; private set; }

    public Task<WebDavConnectionTestResult> TestConnectionAsync(WebDavSettings settings)
    {
        LastSettings = settings;
        return Task.FromResult(new WebDavConnectionTestResult(success, message));
    }
}

sealed class RecordingWebDavManualSyncService : IWebDavManualSyncService
{
    public WebDavSettings? LastSettings { get; private set; }

    public string? UserDataPath { get; private set; }

    public string? SaveBackupsDirectory { get; private set; }

    public string? DownloadUserDataPath { get; private set; }

    public string? DownloadSaveBackupsDirectory { get; private set; }

    public Task<WebDavUploadResult> UploadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        LastSettings = settings;
        UserDataPath = databasePath;
        return Task.FromResult(new WebDavUploadResult(true, "用户信息上传完成", 1, 0));
    }

    public Task<WebDavUploadResult> UploadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        LastSettings = settings;
        SaveBackupsDirectory = saveBackupsDirectory;
        return Task.FromResult(new WebDavUploadResult(true, "存档备份上传完成", 2, 0));
    }
    public Task<WebDavDownloadResult> DownloadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        LastSettings = settings;
        DownloadUserDataPath = databasePath;
        return Task.FromResult(new WebDavDownloadResult(true, "download user data complete", 1, 0));
    }

    public Task<WebDavDownloadResult> DownloadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        LastSettings = settings;
        DownloadSaveBackupsDirectory = saveBackupsDirectory;
        return Task.FromResult(new WebDavDownloadResult(true, "download save backups complete", 2, 0));
    }
}

sealed class RecordingWebDavFullSyncService : IWebDavFullSyncService
{
    public WebDavSettings? LastSettings { get; private set; }

    public string? DatabasePath { get; private set; }

    public string? SaveBackupsDirectory { get; private set; }

    public Task<WebDavFullSyncResult> SynchronizeAsync(WebDavSettings settings, string databasePath, string saveBackupsDirectory)
    {
        LastSettings = settings;
        DatabasePath = databasePath;
        SaveBackupsDirectory = saveBackupsDirectory;
        return Task.FromResult(new WebDavFullSyncResult(true, "同步完成", 1, 1, 1, 1));
    }
}

sealed class RecordingFullSyncManualSyncService : IWebDavManualSyncService
{
    private readonly string remoteDatabasePath;
    private readonly string remoteSaveBackupsDirectory;

    public RecordingFullSyncManualSyncService(string remoteDatabasePath, string remoteSaveBackupsDirectory)
    {
        this.remoteDatabasePath = remoteDatabasePath;
        this.remoteSaveBackupsDirectory = remoteSaveBackupsDirectory;
    }

    public string? UploadedUserDataPath { get; private set; }

    public string? UploadedSaveBackupsDirectory { get; private set; }

    public Task<WebDavUploadResult> UploadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        UploadedUserDataPath = databasePath;
        return Task.FromResult(new WebDavUploadResult(true, "user data uploaded", 1, 0));
    }

    public Task<WebDavUploadResult> UploadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        UploadedSaveBackupsDirectory = saveBackupsDirectory;
        return Task.FromResult(new WebDavUploadResult(true, "save backups uploaded", 1, 0));
    }

    public Task<WebDavDownloadResult> DownloadUserDataAsync(WebDavSettings settings, string databasePath)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(databasePath)!);
        File.Copy(remoteDatabasePath, databasePath, true);
        return Task.FromResult(new WebDavDownloadResult(true, "user data downloaded", 1, 0));
    }

    public Task<WebDavDownloadResult> DownloadSaveBackupsAsync(WebDavSettings settings, string saveBackupsDirectory)
    {
        CopyDirectory(remoteSaveBackupsDirectory, saveBackupsDirectory);
        return Task.FromResult(new WebDavDownloadResult(true, "save backups downloaded", 1, 0));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = System.IO.Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = System.IO.Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }
}

sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage response;

    public RecordingHttpMessageHandler(HttpResponseMessage response)
    {
        this.response = response;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(response);
    }
}

sealed class RecordingUploadHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public Dictionary<string, string> UploadedText { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            UploadedText[request.RequestUri!.AbsoluteUri] = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(request.Method.Method == "PUT" ? HttpStatusCode.Created : HttpStatusCode.Created);
    }
}

sealed class RecordingDownloadHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode StatusCode, string Content)> responses = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    public void RespondWithText(string method, string uri, string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        responses[$"{method.ToUpperInvariant()} {uri}"] = (statusCode, content);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var key = $"{request.Method.Method.ToUpperInvariant()} {request.RequestUri!.AbsoluteUri}";
        if (!responses.TryGetValue(key, out var response))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(response.StatusCode)
        {
            Content = new StringContent(response.Content)
        });
    }
}

sealed class SequentialHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses;

    public SequentialHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        this.responses = new Queue<HttpResponseMessage>(responses);
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        return Task.FromResult(responses.Dequeue());
    }
}

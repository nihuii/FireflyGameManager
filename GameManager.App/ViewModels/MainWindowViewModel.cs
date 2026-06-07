using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IGameLibraryService gameLibraryService;
    private readonly IFilePickerService filePickerService;
    private readonly IGameLauncher gameLauncher;
    private readonly ISaveBackupService saveBackupService;
    private readonly IWebDavSettingsStore webDavSettingsStore;
    private readonly IWebDavConnectionTester webDavConnectionTester;
    private readonly IAppearanceSettingsStore appearanceSettingsStore;
    private readonly IAppearanceThemeService appearanceThemeService;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly IGameDiscoveryService gameDiscoveryService;
    private readonly IDataMaintenanceService dataMaintenanceService;
    private readonly IAutoStartService autoStartService;
    private readonly IGameSessionPresentationService presentationService;
    private readonly ISaveSyncCoordinator saveSyncCoordinator;
    private readonly ICoverCacheService coverCacheService;
    private readonly ISyncLogService syncLogService;
    private object currentViewModel;
    private string pageTitle = "游戏库";
    private bool showTopActions = true;
    private string wallpaperPath = string.Empty;
    private AppLanguage language = AppLanguage.SimplifiedChinese;
    private AppPage selectedNavigation = AppPage.Library;

    public MainWindowViewModel(IGameLibraryService gameLibraryService)
        : this(
            gameLibraryService,
            new NoopFilePickerService(),
            new ProcessGameLauncher(),
            new LocalSaveBackupService(AppPaths.SaveBackupsDirectory),
            new JsonWebDavSettingsStore(AppPaths.WebDavSettingsPath),
            new WebDavConnectionTestService())
    {
    }

    public MainWindowViewModel(IGameLibraryService gameLibraryService, IFilePickerService filePickerService)
        : this(
            gameLibraryService,
            filePickerService,
            new ProcessGameLauncher(),
            new LocalSaveBackupService(AppPaths.SaveBackupsDirectory),
            new JsonWebDavSettingsStore(AppPaths.WebDavSettingsPath),
            new WebDavConnectionTestService())
    {
    }

    public MainWindowViewModel(IGameLibraryService gameLibraryService, IFilePickerService filePickerService, IGameLauncher gameLauncher)
        : this(
            gameLibraryService,
            filePickerService,
            gameLauncher,
            new LocalSaveBackupService(AppPaths.SaveBackupsDirectory),
            new JsonWebDavSettingsStore(AppPaths.WebDavSettingsPath),
            new WebDavConnectionTestService())
    {
    }

    public MainWindowViewModel(
        IGameLibraryService gameLibraryService,
        IFilePickerService filePickerService,
        IGameLauncher gameLauncher,
        ISaveBackupService saveBackupService)
        : this(
            gameLibraryService,
            filePickerService,
            gameLauncher,
            saveBackupService,
            new JsonWebDavSettingsStore(AppPaths.WebDavSettingsPath),
            new WebDavConnectionTestService())
    {
    }

    public MainWindowViewModel(
        IGameLibraryService gameLibraryService,
        IFilePickerService filePickerService,
        IGameLauncher gameLauncher,
        ISaveBackupService saveBackupService,
        IWebDavSettingsStore webDavSettingsStore,
        IWebDavConnectionTester webDavConnectionTester)
        : this(
            gameLibraryService,
            filePickerService,
            gameLauncher,
            saveBackupService,
            webDavSettingsStore,
            webDavConnectionTester,
            new JsonAppearanceSettingsStore(AppPaths.AppearanceSettingsPath),
            new NoopAppearanceThemeService())
    {
    }

    public MainWindowViewModel(
        IGameLibraryService gameLibraryService,
        IFilePickerService filePickerService,
        IGameLauncher gameLauncher,
        ISaveBackupService saveBackupService,
        IWebDavSettingsStore webDavSettingsStore,
        IWebDavConnectionTester webDavConnectionTester,
        IAppearanceSettingsStore appearanceSettingsStore,
        IAppearanceThemeService appearanceThemeService)
        : this(
            gameLibraryService,
            filePickerService,
            gameLauncher,
            saveBackupService,
            webDavSettingsStore,
            webDavConnectionTester,
            appearanceSettingsStore,
            appearanceThemeService,
            new InMemoryAppSettingsStore(),
            new LocalGameDiscoveryService(),
            new LocalDataMaintenanceService(AppPaths.DataDirectory),
            new NoopAutoStartService(),
            new NoopGameSessionPresentationService())
    {
    }

    public MainWindowViewModel(
        IGameLibraryService gameLibraryService,
        IFilePickerService filePickerService,
        IGameLauncher gameLauncher,
        ISaveBackupService saveBackupService,
        IWebDavSettingsStore webDavSettingsStore,
        IWebDavConnectionTester webDavConnectionTester,
        IAppearanceSettingsStore appearanceSettingsStore,
        IAppearanceThemeService appearanceThemeService,
        IAppSettingsStore appSettingsStore,
        IGameDiscoveryService gameDiscoveryService,
        IDataMaintenanceService dataMaintenanceService,
        IAutoStartService autoStartService,
        IGameSessionPresentationService presentationService,
        ISaveSyncCoordinator? saveSyncCoordinator = null,
        ICoverCacheService? coverCacheService = null,
        ISyncLogService? syncLogService = null)
    {
        this.gameLibraryService = gameLibraryService;
        this.filePickerService = filePickerService;
        this.gameLauncher = gameLauncher;
        this.saveBackupService = saveBackupService;
        this.webDavSettingsStore = webDavSettingsStore;
        this.webDavConnectionTester = webDavConnectionTester;
        this.appearanceSettingsStore = appearanceSettingsStore;
        this.appearanceThemeService = appearanceThemeService;
        this.appSettingsStore = appSettingsStore;
        this.gameDiscoveryService = gameDiscoveryService;
        this.dataMaintenanceService = dataMaintenanceService;
        this.autoStartService = autoStartService;
        this.presentationService = presentationService;
        this.saveSyncCoordinator = saveSyncCoordinator ?? new NoopSaveSyncCoordinator();
        this.coverCacheService = coverCacheService ?? new NoopCoverCacheService();
        this.syncLogService = syncLogService ?? new InMemorySyncLogService();
        Library = new GameLibraryViewModel(gameLibraryService.GetGames(), ShowGameDetail, DeleteGame, PinGame, ShowEditGame);
        currentViewModel = Library;
        ShowAddGameCommand = new RelayCommand(_ => ShowAddGame());
        ShowLibraryCommand = new RelayCommand(_ => ShowLibrary());
        ShowManageLibraryCommand = new RelayCommand(_ => ShowManageLibrary());
        ShowSyncCommand = new RelayCommand(_ => ShowSync());
        ShowSettingsCommand = new RelayCommand(_ => ShowSettings());
        ApplyAppearance(appearanceSettingsStore.Load());
        var appSettings = appSettingsStore.Load();
        ApplyAppSettings(appSettings);
        RestoreRememberedPage(appSettings);
    }

    public GameLibraryViewModel Library { get; }

    public object CurrentViewModel
    {
        get => currentViewModel;
        private set => SetProperty(ref currentViewModel, value);
    }

    public string PageTitle
    {
        get => pageTitle;
        private set => SetProperty(ref pageTitle, value);
    }

    public bool ShowTopActions
    {
        get => showTopActions;
        private set => SetProperty(ref showTopActions, value);
    }

    public string WallpaperPath
    {
        get => wallpaperPath;
        private set => SetProperty(ref wallpaperPath, value);
    }

    public string LibraryNavigationText => language == AppLanguage.English ? "Library" : "游戏库";

    public string SyncNavigationText => language == AppLanguage.English ? "Sync" : "同步";

    public string SettingsNavigationText => language == AppLanguage.English ? "Settings" : "设置";

    public string ShellSubtitle => language == AppLanguage.English
        ? "Manage local games, save backups, and WebDAV sync"
        : "管理本地游戏、存档备份与 WebDAV 同步";

    public string ManageActionText => language == AppLanguage.English ? "Manage" : "管理";

    public string AddGameActionText => language == AppLanguage.English ? "Add game" : "添加游戏";

    public bool IsLibraryNavigationSelected => selectedNavigation == AppPage.Library;

    public bool IsSyncNavigationSelected => selectedNavigation == AppPage.Sync;

    public bool IsSettingsNavigationSelected => selectedNavigation == AppPage.Settings;

    public ICommand ShowAddGameCommand { get; }

    public ICommand ShowLibraryCommand { get; }

    public ICommand ShowManageLibraryCommand { get; }

    public ICommand ShowSyncCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public void RefreshLibrary()
    {
        ReloadLibrary();
    }

    private void ShowLibrary()
    {
        SelectNavigation(AppPage.Library);
        ShowTopActions = true;
        PageTitle = LibraryNavigationText;
        CurrentViewModel = Library;
        SaveLastPage(AppPage.Library);
    }

    private void ShowAddGame()
    {
        SelectNavigation(AppPage.Library);
        ShowTopActions = false;
        PageTitle = language == AppLanguage.English ? "Add game" : "添加游戏";
        CurrentViewModel = new AddGameViewModel(filePickerService, SaveGame, ShowLibrary);
    }

    private void ShowManageLibrary()
    {
        SelectNavigation(AppPage.Library);
        ShowTopActions = false;
        PageTitle = language == AppLanguage.English ? "Manage library" : "管理游戏库";
        CurrentViewModel = new ManageGameLibraryViewModel(Library.Games, DeleteGames, ShowLibrary);
    }

    private void ShowSync()
    {
        SelectNavigation(AppPage.Sync);
        ShowTopActions = false;
        PageTitle = SyncNavigationText;
        CurrentViewModel = new WebDavSettingsViewModel(
            webDavSettingsStore,
            webDavConnectionTester,
            ShowLibrary,
            ReloadLibrary,
            syncLogService);
        SaveLastPage(AppPage.Sync);
    }

    private void ShowSettings()
    {
        SelectNavigation(AppPage.Settings);
        ShowTopActions = false;
        PageTitle = SettingsNavigationText;
        CurrentViewModel = new AppearanceSettingsViewModel(
            filePickerService,
            appearanceSettingsStore,
            ApplyAppearance,
            ShowLibrary,
            appSettingsStore,
            ApplyAppSettings,
            autoStartService,
            gameDiscoveryService,
            () => Library.Games.Select(game => game.ExecutablePath).ToList(),
            AddDiscoveredGames,
            dataMaintenanceService,
            () => Library.Games.Select(game => game.Id).ToList(),
            ReloadLibrary,
            webDavSettingsStore);
        SaveLastPage(AppPage.Settings);
    }

    private void ShowGameDetail(Game game)
    {
        SelectNavigation(AppPage.Library);
        PageTitle = game.Name;
        ShowTopActions = false;
        CurrentViewModel = new GameDetailViewModel(
            game,
            gameLauncher,
            RecordLaunchResult,
            Library.ReplaceGame,
            ShowLibrary,
            saveBackupService,
            filePickerService,
            appSettingsStore,
            presentationService,
            saveSyncCoordinator,
            ShowEditGame,
            OpenDirectory);
    }

    private void ShowEditGame(Game game)
    {
        SelectNavigation(AppPage.Library);
        ShowTopActions = false;
        PageTitle = language == AppLanguage.English ? "Edit game" : "修改游戏";
        var initialValues = new AddGameRequest(
            game.Name,
            game.ExecutablePath,
            game.GameRootPath,
            game.SavePath,
            game.CoverImagePath,
            game.LaunchArguments,
            game.RunAsAdministrator,
            game.WorkingDirectory,
            game.MonitorProcessName,
            game.SyncEnabled);
        CurrentViewModel = new AddGameViewModel(
            filePickerService,
            request => UpdateGame(game.Id, request),
            ShowLibrary,
            initialValues,
            "保存修改");
    }

    private void SaveGame(AddGameRequest request)
    {
        var game = CacheCover(gameLibraryService.AddGame(request));
        Library.AddGame(game);
        ShowLibrary();
    }

    private void UpdateGame(string id, AddGameRequest request)
    {
        var game = gameLibraryService.UpdateGame(new UpdateGameRequest(
            id,
            request.Name,
            request.ExecutablePath,
            request.GameRootPath,
            request.SavePath,
            request.CoverImagePath,
            request.LaunchArguments,
            request.RunAsAdministrator,
            request.WorkingDirectory,
            request.MonitorProcessName,
            request.SyncEnabled));
        game = CacheCover(game);
        Library.ReplaceGame(game);
        ShowLibrary();
    }

    private void DeleteGame(Game game)
    {
        if (gameLibraryService.DeleteGame(game.Id))
        {
            Library.RemoveGame(game.Id);
        }
    }

    private void DeleteGames(IReadOnlyList<Game> games)
    {
        foreach (var game in games)
        {
            DeleteGame(game);
        }
    }

    private void PinGame(Game game)
    {
        if (gameLibraryService.PinGameToTop(game.Id))
        {
            Library.MoveGameToTop(game.Id);
        }
    }

    private Game RecordLaunchResult(Game game, LaunchResult result)
    {
        return gameLibraryService.RecordLaunchResult(game.Id, result);
    }

    private void ApplyAppearance(AppearanceSettings settings)
    {
        WallpaperPath = settings.WallpaperPath;
        appearanceThemeService.Apply(settings);
    }

    private void ApplyAppSettings(AppSettings settings)
    {
        language = settings.Language;
        Library.ApplySettings(settings);
        OnPropertyChanged(nameof(LibraryNavigationText));
        OnPropertyChanged(nameof(SyncNavigationText));
        OnPropertyChanged(nameof(SettingsNavigationText));
        OnPropertyChanged(nameof(ShellSubtitle));
        OnPropertyChanged(nameof(ManageActionText));
        OnPropertyChanged(nameof(AddGameActionText));
        PageTitle = CurrentViewModel switch
        {
            GameLibraryViewModel => LibraryNavigationText,
            WebDavSettingsViewModel => SyncNavigationText,
            AppearanceSettingsViewModel => SettingsNavigationText,
            _ => PageTitle
        };
    }

    private void SelectNavigation(AppPage page)
    {
        if (selectedNavigation == page)
        {
            return;
        }

        selectedNavigation = page;
        OnPropertyChanged(nameof(IsLibraryNavigationSelected));
        OnPropertyChanged(nameof(IsSyncNavigationSelected));
        OnPropertyChanged(nameof(IsSettingsNavigationSelected));
    }

    private void AddDiscoveredGames(IReadOnlyList<AddGameRequest> requests)
    {
        foreach (var request in requests)
        {
            Library.AddGame(gameLibraryService.AddGame(request));
        }
    }

    private void ReloadLibrary()
    {
        Library.ReloadGames(gameLibraryService.GetGames());
        Library.ApplySettings(appSettingsStore.Load());
    }

    private void SaveLastPage(AppPage page)
    {
        var settings = appSettingsStore.Load();
        if (!settings.RememberLastPage || settings.LastPage == page)
        {
            return;
        }

        settings.LastPage = page;
        appSettingsStore.Save(settings);
    }

    private void RestoreRememberedPage(AppSettings settings)
    {
        if (!settings.RememberLastPage)
        {
            return;
        }

        switch (settings.LastPage)
        {
            case AppPage.Sync:
                ShowSync();
                break;
            case AppPage.Settings:
                ShowSettings();
                break;
        }
    }

    private static void OpenDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private Game CacheCover(Game game)
    {
        var cachedPath = coverCacheService.Cache(game);
        if (string.Equals(cachedPath, game.CoverImagePath, StringComparison.OrdinalIgnoreCase))
        {
            return game;
        }

        return gameLibraryService.UpdateGame(new UpdateGameRequest(
            game.Id,
            game.Name,
            game.ExecutablePath,
            game.GameRootPath,
            game.SavePath,
            cachedPath,
            game.LaunchArguments,
            game.RunAsAdministrator,
            game.WorkingDirectory,
            game.MonitorProcessName,
            game.SyncEnabled));
    }
}

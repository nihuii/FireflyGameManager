using System.Globalization;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class AppearanceSettingsViewModel : ViewModelBase
{
    private readonly IFilePickerService filePickerService;
    private readonly IAppearanceSettingsStore appearanceSettingsStore;
    private readonly Action<AppearanceSettings> applyAppearance;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly Action<AppSettings> applyAppSettings;
    private readonly IAutoStartService autoStartService;
    private readonly IGameDiscoveryService gameDiscoveryService;
    private readonly Func<IReadOnlyList<string>> existingExecutablePaths;
    private readonly Action<IReadOnlyList<AddGameRequest>> addDiscoveredGames;
    private readonly IDataMaintenanceService dataMaintenanceService;
    private readonly Func<IReadOnlyList<string>> validGameIds;
    private readonly Action reloadLibrary;
    private readonly IWebDavSettingsStore? webDavSettingsStore;
    private AppSettings appSettings;
    private string wallpaperPath;
    private bool isTransparentUi;
    private string statusText = "设置会自动保存";
    private string selectedSection = SettingsOverviewSection;

    private const string SettingsOverviewSection = "Overview";
    private const string GeneralSection = "General";
    private const string LaunchSection = "Launch";
    private const string LibrarySection = "Library";
    private const string AppearanceSection = "Appearance";
    private const string DataSection = "Data";

    public AppearanceSettingsViewModel(
        IFilePickerService filePickerService,
        IAppearanceSettingsStore settingsStore,
        Action<AppearanceSettings> applyAppearance,
        Action goBack)
        : this(
            filePickerService,
            settingsStore,
            applyAppearance,
            goBack,
            new InMemoryAppSettingsStore(),
            _ => { },
            new NoopAutoStartService(),
            new LocalGameDiscoveryService(),
            () => [],
            _ => { },
            new LocalDataMaintenanceService(AppPaths.DataDirectory),
            () => [],
            () => { })
    {
    }

    public AppearanceSettingsViewModel(
        IFilePickerService filePickerService,
        IAppearanceSettingsStore appearanceSettingsStore,
        Action<AppearanceSettings> applyAppearance,
        Action goBack,
        IAppSettingsStore appSettingsStore,
        Action<AppSettings> applyAppSettings,
        IAutoStartService autoStartService,
        IGameDiscoveryService gameDiscoveryService,
        Func<IReadOnlyList<string>> existingExecutablePaths,
        Action<IReadOnlyList<AddGameRequest>> addDiscoveredGames,
        IDataMaintenanceService dataMaintenanceService,
        Func<IReadOnlyList<string>> validGameIds,
        Action reloadLibrary,
        IWebDavSettingsStore? webDavSettingsStore = null)
    {
        this.filePickerService = filePickerService;
        this.appearanceSettingsStore = appearanceSettingsStore;
        this.applyAppearance = applyAppearance;
        this.appSettingsStore = appSettingsStore;
        this.applyAppSettings = applyAppSettings;
        this.autoStartService = autoStartService;
        this.gameDiscoveryService = gameDiscoveryService;
        this.existingExecutablePaths = existingExecutablePaths;
        this.addDiscoveredGames = addDiscoveredGames;
        this.dataMaintenanceService = dataMaintenanceService;
        this.validGameIds = validGameIds;
        this.reloadLibrary = reloadLibrary;
        this.webDavSettingsStore = webDavSettingsStore;

        var appearanceSettings = appearanceSettingsStore.Load();
        wallpaperPath = appearanceSettings.WallpaperPath;
        isTransparentUi = appearanceSettings.IsTransparentUi;
        appSettings = appSettingsStore.Load();

        SelectWallpaperCommand = new RelayCommand(_ => SelectWallpaper());
        ClearWallpaperCommand = new RelayCommand(_ => ClearWallpaper(), _ => !string.IsNullOrWhiteSpace(WallpaperPath));
        BrowseScanDirectoryCommand = new RelayCommand(_ => BrowseScanDirectory());
        ScanGamesCommand = new RelayCommand(_ => ScanGames(), _ => !string.IsNullOrWhiteSpace(ScanDirectory));
        OpenDataDirectoryCommand = new RelayCommand(_ => RunMaintenance(dataMaintenanceService.OpenDataDirectory, "已打开本地数据目录"));
        ExportDataCommand = new RelayCommand(_ => ExportData());
        ImportDataCommand = new RelayCommand(_ => ImportData());
        ClearInvalidBackupsCommand = new RelayCommand(_ => ClearInvalidBackups());
        ClearCoverCacheCommand = new RelayCommand(_ => RunMaintenance(dataMaintenanceService.ClearCoverCache, "封面缓存已清理"));
        ResetAllSettingsCommand = new RelayCommand(_ => ResetAllSettings());
        ShowSectionCommand = new RelayCommand(parameter => ShowSection(parameter as string));
        ShowOverviewCommand = new RelayCommand(_ => ShowSection(SettingsOverviewSection));
        BackCommand = new RelayCommand(_ => goBack());
    }

    public IReadOnlyList<SettingOption<AppCloseBehavior>> CloseBehaviorOptions { get; } =
    [
        new(AppCloseBehavior.Exit, "退出应用"),
        new(AppCloseBehavior.MinimizeToTray, "最小化到系统托盘")
    ];

    public IReadOnlyList<SettingOption<AppLanguage>> LanguageOptions { get; } =
    [
        new(AppLanguage.SimplifiedChinese, "简体中文"),
        new(AppLanguage.English, "English")
    ];

    public IReadOnlyList<SettingOption<GameSortMode>> SortOptions { get; } =
    [
        new(GameSortMode.Manual, "手动顺序"),
        new(GameSortMode.RecentLaunch, "最近启动"),
        new(GameSortMode.Name, "游戏名称"),
        new(GameSortMode.PlayTime, "游玩时长")
    ];

    public IReadOnlyList<SettingOption<GameCardSize>> CardSizeOptions { get; } =
    [
        new(GameCardSize.Compact, "紧凑"),
        new(GameCardSize.Standard, "标准"),
        new(GameCardSize.Large, "大")
    ];

    public IReadOnlyList<SettingOption<int>> BackupRetentionOptions { get; } =
    [
        new(5, "保留 5 份"),
        new(10, "保留 10 份"),
        new(20, "保留 20 份"),
        new(50, "保留 50 份")
    ];

    public string WallpaperPath
    {
        get => wallpaperPath;
        private set
        {
            if (SetProperty(ref wallpaperPath, value))
            {
                OnPropertyChanged(nameof(WallpaperPathDisplay));
                ((RelayCommand)ClearWallpaperCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string WallpaperPathDisplay => string.IsNullOrWhiteSpace(WallpaperPath) ? "尚未选择壁纸" : WallpaperPath;

    public bool IsTransparentUi
    {
        get => isTransparentUi;
        set
        {
            if (SetProperty(ref isTransparentUi, value))
            {
                SaveAndApplyAppearance("透明模式已更新");
            }
        }
    }

    public bool StartWithWindows
    {
        get => appSettings.StartWithWindows;
        set
        {
            if (appSettings.StartWithWindows == value)
            {
                return;
            }

            appSettings.StartWithWindows = value;
            autoStartService.SetEnabled(value);
            SaveAppSettings("开机启动设置已更新");
            OnPropertyChanged();
        }
    }

    public bool StartMinimized
    {
        get => appSettings.StartMinimized;
        set => UpdateSetting(() => appSettings.StartMinimized = value, appSettings.StartMinimized, value);
    }

    public AppCloseBehavior CloseBehavior
    {
        get => appSettings.CloseBehavior;
        set => UpdateSetting(() => appSettings.CloseBehavior = value, appSettings.CloseBehavior, value);
    }

    public bool RememberLastPage
    {
        get => appSettings.RememberLastPage;
        set => UpdateSetting(() => appSettings.RememberLastPage = value, appSettings.RememberLastPage, value);
    }

    public AppLanguage Language
    {
        get => appSettings.Language;
        set
        {
            if (appSettings.Language == value)
            {
                return;
            }

            appSettings.Language = value;
            ApplyLanguage(value);
            SaveAppSettings("语言设置已更新，部分界面会在重启后刷新");
            OnPropertyChanged();
        }
    }

    public bool MinimizeAfterGameLaunch
    {
        get => appSettings.MinimizeAfterGameLaunch;
        set => UpdateSetting(() => appSettings.MinimizeAfterGameLaunch = value, appSettings.MinimizeAfterGameLaunch, value);
    }

    public bool RestoreAfterGameExit
    {
        get => appSettings.RestoreAfterGameExit;
        set => UpdateSetting(() => appSettings.RestoreAfterGameExit = value, appSettings.RestoreAfterGameExit, value);
    }

    public bool BackupBeforeGameLaunch
    {
        get => appSettings.BackupBeforeGameLaunch;
        set => UpdateSetting(() => appSettings.BackupBeforeGameLaunch = value, appSettings.BackupBeforeGameLaunch, value);
    }

    public bool AutoSyncBeforeGameLaunch
    {
        get => appSettings.AutoSyncBeforeGameLaunch;
        set => UpdateSetting(() => appSettings.AutoSyncBeforeGameLaunch = value, appSettings.AutoSyncBeforeGameLaunch, value);
    }

    public bool AutoSyncAfterGameExit
    {
        get => appSettings.AutoSyncAfterGameExit;
        set => UpdateSetting(() => appSettings.AutoSyncAfterGameExit = value, appSettings.AutoSyncAfterGameExit, value);
    }

    public int BackupRetentionCount
    {
        get => appSettings.BackupRetentionCount;
        set => UpdateSetting(() => appSettings.BackupRetentionCount = value, appSettings.BackupRetentionCount, value);
    }

    public GameSortMode DefaultSort
    {
        get => appSettings.DefaultSort;
        set => UpdateSetting(() => appSettings.DefaultSort = value, appSettings.DefaultSort, value);
    }

    public GameCardSize CardSize
    {
        get => appSettings.CardSize;
        set => UpdateSetting(() => appSettings.CardSize = value, appSettings.CardSize, value);
    }

    public bool ShowPlayTimeOnCards
    {
        get => appSettings.ShowPlayTimeOnCards;
        set => UpdateSetting(() => appSettings.ShowPlayTimeOnCards = value, appSettings.ShowPlayTimeOnCards, value);
    }

    public string ScanDirectory
    {
        get => appSettings.ScanDirectory;
        private set
        {
            if (appSettings.ScanDirectory == value)
            {
                return;
            }

            appSettings.ScanDirectory = value;
            SaveAppSettings("扫描目录已更新");
            OnPropertyChanged();
            ((RelayCommand)ScanGamesCommand).RaiseCanExecuteChanged();
        }
    }

    public string DataDirectory => dataMaintenanceService.DataDirectory;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool IsOverviewSelected => selectedSection == SettingsOverviewSection;

    public bool IsGeneralSectionSelected => selectedSection == GeneralSection;

    public bool IsLaunchSectionSelected => selectedSection == LaunchSection;

    public bool IsLibrarySectionSelected => selectedSection == LibrarySection;

    public bool IsAppearanceSectionSelected => selectedSection == AppearanceSection;

    public bool IsDataSectionSelected => selectedSection == DataSection;

    public ICommand SelectWallpaperCommand { get; }
    public ICommand ClearWallpaperCommand { get; }
    public ICommand BrowseScanDirectoryCommand { get; }
    public ICommand ScanGamesCommand { get; }
    public ICommand OpenDataDirectoryCommand { get; }
    public ICommand ExportDataCommand { get; }
    public ICommand ImportDataCommand { get; }
    public ICommand ClearInvalidBackupsCommand { get; }
    public ICommand ClearCoverCacheCommand { get; }
    public ICommand ResetAllSettingsCommand { get; }
    public ICommand ShowSectionCommand { get; }
    public ICommand ShowOverviewCommand { get; }
    public ICommand BackCommand { get; }

    private void ShowSection(string? section)
    {
        var normalized = section switch
        {
            GeneralSection => GeneralSection,
            LaunchSection => LaunchSection,
            LibrarySection => LibrarySection,
            AppearanceSection => AppearanceSection,
            DataSection => DataSection,
            _ => SettingsOverviewSection
        };

        if (selectedSection == normalized)
        {
            return;
        }

        selectedSection = normalized;
        OnPropertyChanged(nameof(IsOverviewSelected));
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsLaunchSectionSelected));
        OnPropertyChanged(nameof(IsLibrarySectionSelected));
        OnPropertyChanged(nameof(IsAppearanceSectionSelected));
        OnPropertyChanged(nameof(IsDataSectionSelected));
    }

    private void SelectWallpaper()
    {
        var selectedPath = filePickerService.PickWallpaperImage();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        WallpaperPath = selectedPath;
        SaveAndApplyAppearance("壁纸与自适应色彩已更新");
    }

    private void ClearWallpaper()
    {
        WallpaperPath = string.Empty;
        SaveAndApplyAppearance("已恢复默认背景");
    }

    private void BrowseScanDirectory()
    {
        var path = filePickerService.PickFolder("选择要扫描的游戏目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ScanDirectory = path;
        }
    }

    private void ScanGames()
    {
        RunMaintenance(() =>
        {
            var discovered = gameDiscoveryService.Discover(ScanDirectory, existingExecutablePaths());
            addDiscoveredGames(discovered);
            StatusText = discovered.Count == 0 ? "没有发现新的游戏 EXE" : $"已添加 {discovered.Count} 个游戏";
        }, null);
    }

    private void ExportData()
    {
        var path = filePickerService.PickExportArchivePath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            RunMaintenance(() => dataMaintenanceService.Export(path), "本地数据已导出");
        }
    }

    private void ImportData()
    {
        var path = filePickerService.PickImportArchiveFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            RunMaintenance(() =>
            {
                dataMaintenanceService.Import(path);
                reloadLibrary();
            }, "本地数据已导入");
        }
    }

    private void ClearInvalidBackups()
    {
        RunMaintenance(() =>
        {
            var removed = dataMaintenanceService.ClearInvalidBackups(validGameIds());
            StatusText = $"已清理 {removed} 个无效备份项目";
        }, null);
    }

    private void ResetAllSettings()
    {
        appSettings = AppSettings.Default;
        appSettingsStore.Save(appSettings);
        autoStartService.SetEnabled(false);
        webDavSettingsStore?.Save(WebDavSettings.Default);
        var appearance = AppearanceSettings.Default;
        appearanceSettingsStore.Save(appearance);
        WallpaperPath = appearance.WallpaperPath;
        isTransparentUi = appearance.IsTransparentUi;
        OnPropertyChanged(nameof(IsTransparentUi));
        applyAppearance(appearance);
        applyAppSettings(appSettings);
        RaiseAllAppSettingProperties();
        StatusText = "所有应用设置已恢复默认值";
    }

    private void SaveAndApplyAppearance(string status)
    {
        var settings = new AppearanceSettings(WallpaperPath, IsTransparentUi);
        appearanceSettingsStore.Save(settings);
        applyAppearance(settings);
        StatusText = status;
    }

    private void SaveAppSettings(string status)
    {
        appSettingsStore.Save(appSettings);
        applyAppSettings(appSettings);
        StatusText = status;
    }

    private void UpdateSetting<T>(Action update, T current, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        update();
        SaveAppSettings("设置已更新");
        OnPropertyChanged(propertyName);
    }

    private void RunMaintenance(Action action, string? successMessage)
    {
        try
        {
            action();
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                StatusText = successMessage;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"操作失败：{ex.Message}";
        }
    }

    private static void ApplyLanguage(AppLanguage language)
    {
        var culture = CultureInfo.GetCultureInfo(language == AppLanguage.English ? "en-US" : "zh-CN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private void RaiseAllAppSettingProperties()
    {
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(CloseBehavior));
        OnPropertyChanged(nameof(RememberLastPage));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(MinimizeAfterGameLaunch));
        OnPropertyChanged(nameof(RestoreAfterGameExit));
        OnPropertyChanged(nameof(BackupBeforeGameLaunch));
        OnPropertyChanged(nameof(AutoSyncBeforeGameLaunch));
        OnPropertyChanged(nameof(AutoSyncAfterGameExit));
        OnPropertyChanged(nameof(BackupRetentionCount));
        OnPropertyChanged(nameof(DefaultSort));
        OnPropertyChanged(nameof(CardSize));
        OnPropertyChanged(nameof(ShowPlayTimeOnCards));
        OnPropertyChanged(nameof(ScanDirectory));
    }
}

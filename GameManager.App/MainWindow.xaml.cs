using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using GameManager.App.Services;
using GameManager.App.ViewModels;

namespace GameManager.App;

public partial class MainWindow : Window
{
    private static readonly string AppIconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "desktop_icon.ico");
    private readonly IAppSettingsStore appSettingsStore;
    private readonly IWebDavSettingsStore webDavSettingsStore;
    private readonly IWebDavCloudMetadataPullService cloudMetadataPullService;
    private readonly MainWindowViewModel viewModel;
    private readonly SystemTrayService systemTrayService;
    private bool isExiting;
    private bool startupMetadataPullCompleted;

    public MainWindow()
    {
        InitializeComponent();
        appSettingsStore = new JsonAppSettingsStore(AppPaths.AppSettingsPath);
        var machineId = new MachineIdentityService(AppPaths.MachineIdPath).GetOrCreate();
        var gameLibraryService = new SqliteGameLibraryService(AppPaths.DatabasePath, machineId);
        var syncLogService = new SqliteSyncLogService(AppPaths.DatabasePath);
        var gameSyncService = new WebDavGameSyncService();
        var saveBackupService = new LocalSaveBackupService(
            AppPaths.SaveBackupsDirectory,
            () => appSettingsStore.Load().BackupRetentionCount);
        webDavSettingsStore = new JsonWebDavSettingsStore(AppPaths.WebDavSettingsPath);
        cloudMetadataPullService = new WebDavCloudMetadataPullService(
            gameLibraryService,
            gameSyncService,
            syncLogService,
            machineId,
            AppPaths.CoverCacheDirectory);
        var saveSyncCoordinator = new SaveSyncCoordinator(
            webDavSettingsStore,
            appSettingsStore,
            gameSyncService,
            new SaveManifestService(),
            saveBackupService,
            new SqliteSaveSyncStateStore(AppPaths.DatabasePath),
            syncLogService,
            machineId,
            gameLibraryService.GetPlaySessions);
        var bangumiApiClient = new BangumiApiClient();
        var bangumiAccountStore = new JsonBangumiAccountStore(AppPaths.BangumiAccountPath);
        var metadataProvider = new BangumiGameMetadataProvider(bangumiApiClient, bangumiAccountStore);
        viewModel = new MainWindowViewModel(
            gameLibraryService,
            new WpfFilePickerService(),
            new ProcessGameLauncher(),
            saveBackupService,
            webDavSettingsStore,
            new WebDavConnectionTestService(),
            new JsonAppearanceSettingsStore(AppPaths.AppearanceSettingsPath),
            new WpfAppearanceThemeService(),
            appSettingsStore,
            new LocalGameDiscoveryService(),
            new LocalDataMaintenanceService(AppPaths.DataDirectory),
            new RegistryAutoStartService("FireflyGameManager", Environment.ProcessPath ?? string.Empty),
            new WpfGameSessionPresentationService(),
            saveSyncCoordinator,
            new LocalCoverCacheService(AppPaths.CoverCacheDirectory),
            syncLogService,
            bangumiApiClient,
            bangumiAccountStore,
            metadataProvider,
            new RemoteImageCacheService(AppPaths.MetadataCacheDirectory));
        DataContext = viewModel;
        systemTrayService = new SystemTrayService(ShowFromTray, ExitFromTray, AppIconPath);
    }

    private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizedState();
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            var pointer = e.GetPosition(this);
            var widthRatio = pointer.X / Math.Max(ActualWidth, 1);
            WindowState = WindowState.Normal;
            Left = pointer.X - (RestoreBounds.Width * widthRatio);
            Top = 0;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizedState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = appSettingsStore.Load();
        var launchedMinimized = Environment.GetCommandLineArgs()
            .Any(argument => argument.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (settings.StartMinimized || launchedMinimized)
        {
            Hide();
        }

        var webDavSettings = webDavSettingsStore.Load();
        if (!startupMetadataPullCompleted && IsWebDavConfigured(webDavSettings))
        {
            startupMetadataPullCompleted = true;
            var result = await cloudMetadataPullService.PullAsync(webDavSettings);
            if (result.Success)
            {
                viewModel.RefreshLibrary();
            }
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (isExiting || appSettingsStore.Load().CloseBehavior != Models.AppCloseBehavior.MinimizeToTray)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        systemTrayService.ShowNotification("Firefly Game Manager", "Firefly 仍在系统托盘中运行");
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        systemTrayService.Dispose();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is not null)
        {
            MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
            MaximizeGlyph.Data = Geometry.Parse(WindowState == WindowState.Maximized
                ? "M 3.5,1.5 L 10.5,1.5 L 10.5,8.5 L 8.5,8.5 M 1.5,3.5 L 8.5,3.5 L 8.5,10.5 L 1.5,10.5 Z"
                : "M 1.5,1.5 L 10.5,1.5 L 10.5,10.5 L 1.5,10.5 Z");
        }
    }

    private void ToggleMaximizedState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void ExitFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            isExiting = true;
            systemTrayService.Dispose();
            Application.Current.Shutdown();
        });
    }

    private static bool IsWebDavConfigured(Models.WebDavSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.ServerUrl) &&
            !string.IsNullOrWhiteSpace(settings.Username) &&
            !string.IsNullOrWhiteSpace(settings.ApplicationPassword);
    }
}

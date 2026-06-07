using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class WebDavSettingsViewModel : ViewModelBase
{
    private readonly IWebDavSettingsStore settingsStore;
    private readonly IWebDavConnectionTester connectionTester;
    private readonly IWebDavManualSyncService manualSyncService;
    private readonly IWebDavFullSyncService fullSyncService;
    private readonly string databasePath;
    private readonly string saveBackupsDirectory;
    private readonly ISyncLogService syncLogService;
    private readonly Action syncCompleted;
    private string serverUrl;
    private string username;
    private string applicationPassword;
    private string remoteDirectory;
    private string connectionStatusText = "尚未测试连接";
    private string uploadStatusText = "尚未上传";
    private string downloadStatusText = "尚未下载";
    private string syncStatusText = "尚未同步";

    public WebDavSettingsViewModel(
        IWebDavSettingsStore settingsStore,
        IWebDavConnectionTester connectionTester,
        Action goBack)
        : this(settingsStore, connectionTester, goBack, () => { })
    {
    }

    public WebDavSettingsViewModel(
        IWebDavSettingsStore settingsStore,
        IWebDavConnectionTester connectionTester,
        Action goBack,
        Action syncCompleted,
        ISyncLogService? syncLogService = null)
        : this(
            settingsStore,
            connectionTester,
            new WebDavManualSyncService(),
            goBack,
            AppPaths.DatabasePath,
            AppPaths.SaveBackupsDirectory,
            syncCompleted,
            syncLogService)
    {
    }

    public WebDavSettingsViewModel(
        IWebDavSettingsStore settingsStore,
        IWebDavConnectionTester connectionTester,
        IWebDavManualSyncService manualSyncService,
        Action goBack,
        string databasePath,
        string saveBackupsDirectory,
        Action? syncCompleted = null,
        ISyncLogService? syncLogService = null)
        : this(
            settingsStore,
            connectionTester,
            manualSyncService,
            CreateFullSyncService(manualSyncService),
            goBack,
            databasePath,
            saveBackupsDirectory,
            syncLogService,
            syncCompleted)
    {
    }

    public WebDavSettingsViewModel(
        IWebDavSettingsStore settingsStore,
        IWebDavConnectionTester connectionTester,
        IWebDavManualSyncService manualSyncService,
        IWebDavFullSyncService fullSyncService,
        Action goBack,
        string databasePath,
        string saveBackupsDirectory,
        ISyncLogService? syncLogService = null,
        Action? syncCompleted = null)
    {
        this.settingsStore = settingsStore;
        this.connectionTester = connectionTester;
        this.manualSyncService = manualSyncService;
        this.fullSyncService = fullSyncService;
        this.databasePath = databasePath;
        this.saveBackupsDirectory = saveBackupsDirectory;
        this.syncLogService = syncLogService ?? new InMemorySyncLogService();
        this.syncCompleted = syncCompleted ?? (() => { });
        var settings = settingsStore.Load();
        serverUrl = settings.ServerUrl;
        username = settings.Username;
        applicationPassword = settings.ApplicationPassword;
        remoteDirectory = settings.RemoteDirectory;
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        UploadUserDataCommand = new AsyncRelayCommand(_ => UploadUserDataAsync());
        UploadSaveBackupsCommand = new AsyncRelayCommand(_ => UploadSaveBackupsAsync());
        DownloadUserDataCommand = new AsyncRelayCommand(_ => DownloadUserDataAsync());
        DownloadSaveBackupsCommand = new AsyncRelayCommand(_ => DownloadSaveBackupsAsync());
        FullSyncCommand = new AsyncRelayCommand(_ => FullSyncAsync());
        BackCommand = new RelayCommand(_ => goBack());
        RefreshSyncRecords();
    }

    public string ServerUrl
    {
        get => serverUrl;
        set => SetProperty(ref serverUrl, value);
    }

    public string Username
    {
        get => username;
        set => SetProperty(ref username, value);
    }

    public string ApplicationPassword
    {
        get => applicationPassword;
        set => SetProperty(ref applicationPassword, value);
    }

    public string RemoteDirectory
    {
        get => remoteDirectory;
        set => SetProperty(ref remoteDirectory, value);
    }

    public string ConnectionStatusText
    {
        get => connectionStatusText;
        private set => SetProperty(ref connectionStatusText, value);
    }

    public string UploadStatusText
    {
        get => uploadStatusText;
        private set => SetProperty(ref uploadStatusText, value);
    }

    public string DownloadStatusText
    {
        get => downloadStatusText;
        private set => SetProperty(ref downloadStatusText, value);
    }

    public string SyncStatusText
    {
        get => syncStatusText;
        private set => SetProperty(ref syncStatusText, value);
    }

    public ICommand SaveSettingsCommand { get; }

    public ICommand TestConnectionCommand { get; }

    public ICommand UploadUserDataCommand { get; }

    public ICommand UploadSaveBackupsCommand { get; }

    public ICommand DownloadUserDataCommand { get; }

    public ICommand DownloadSaveBackupsCommand { get; }

    public ICommand FullSyncCommand { get; }

    public ICommand BackCommand { get; }

    public ObservableCollection<SyncRecord> RecentSyncRecords { get; } = [];

    public bool HasSyncRecords => RecentSyncRecords.Count > 0;

    private void SaveSettings()
    {
        settingsStore.Save(CreateSettings());
        ConnectionStatusText = "配置已保存";
    }

    private async Task TestConnectionAsync()
    {
        var settings = SaveCurrentSettings();
        ConnectionStatusText = "正在测试连接...";
        var result = await connectionTester.TestConnectionAsync(settings);
        ConnectionStatusText = result.Message;
    }

    private async Task UploadUserDataAsync()
    {
        var settings = SaveCurrentSettings();
        UploadStatusText = "正在上传用户信息...";
        try
        {
            var result = await manualSyncService.UploadUserDataAsync(settings, databasePath);
            UploadStatusText = result.Message;
            RecordSync("metadata", "upload", result.Success, result.Message);
        }
        catch (Exception ex)
        {
            UploadStatusText = $"上传用户信息失败：{ex.Message}";
            RecordSync("metadata", "upload", false, UploadStatusText);
        }
    }

    private async Task UploadSaveBackupsAsync()
    {
        var settings = SaveCurrentSettings();
        UploadStatusText = "正在上传存档备份...";
        try
        {
            var result = await manualSyncService.UploadSaveBackupsAsync(settings, saveBackupsDirectory);
            UploadStatusText = result.Message;
            RecordSync("save", "upload", result.Success, result.Message);
        }
        catch (Exception ex)
        {
            UploadStatusText = $"上传存档备份失败：{ex.Message}";
            RecordSync("save", "upload", false, UploadStatusText);
        }
    }

    private async Task DownloadUserDataAsync()
    {
        var settings = SaveCurrentSettings();
        DownloadStatusText = "正在下载用户信息...";
        try
        {
            var result = await manualSyncService.DownloadUserDataAsync(settings, databasePath);
            DownloadStatusText = result.Message;
            RecordSync("metadata", "download", result.Success, result.Message);
            if (result.Success)
            {
                syncCompleted();
            }
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"下载用户信息失败：{ex.Message}";
            RecordSync("metadata", "download", false, DownloadStatusText);
        }
    }

    private async Task DownloadSaveBackupsAsync()
    {
        var settings = SaveCurrentSettings();
        DownloadStatusText = "正在下载存档备份...";
        try
        {
            var result = await manualSyncService.DownloadSaveBackupsAsync(settings, saveBackupsDirectory);
            DownloadStatusText = result.Message;
            RecordSync("save", "download", result.Success, result.Message);
        }
        catch (Exception ex)
        {
            DownloadStatusText = $"下载存档备份失败：{ex.Message}";
            RecordSync("save", "download", false, DownloadStatusText);
        }
    }

    private async Task FullSyncAsync()
    {
        var settings = SaveCurrentSettings();
        SyncStatusText = "正在同步...";
        try
        {
            var result = await fullSyncService.SynchronizeAsync(settings, databasePath, saveBackupsDirectory);
            SyncStatusText = result.Message;
            RecordSync("full", "both", result.Success, result.Message);
            if (result.Success)
            {
                syncCompleted();
            }
        }
        catch (Exception ex)
        {
            SyncStatusText = $"同步失败：{ex.Message}";
            RecordSync("full", "both", false, SyncStatusText);
        }
    }

    private WebDavSettings SaveCurrentSettings()
    {
        var settings = CreateSettings();
        settingsStore.Save(settings);
        return settings;
    }

    private WebDavSettings CreateSettings()
    {
        return new WebDavSettings(
            ServerUrl.Trim(),
            Username.Trim(),
            ApplicationPassword,
            RemoteDirectory.Trim());
    }

    private static IWebDavFullSyncService CreateFullSyncService(IWebDavManualSyncService manualSyncService)
    {
        return new WebDavFullSyncService(
            manualSyncService,
            new SqliteGameLibraryMergeService(),
            new SaveBackupMergeService(),
            () => Path.Combine(Path.GetTempPath(), "FireflyGameManagerSync"),
            new WebDavGameSyncService(),
            new SaveManifestService(),
            new MachineIdentityService(AppPaths.MachineIdPath).GetOrCreate());
    }

    private void RecordSync(string type, string direction, bool success, string message)
    {
        syncLogService.Add(null, type, direction, success ? "success" : "failed", message);
        RefreshSyncRecords();
    }

    private void RefreshSyncRecords()
    {
        RecentSyncRecords.Clear();
        foreach (var record in syncLogService.GetRecent(20))
        {
            RecentSyncRecords.Add(record);
        }

        OnPropertyChanged(nameof(HasSyncRecords));
    }
}

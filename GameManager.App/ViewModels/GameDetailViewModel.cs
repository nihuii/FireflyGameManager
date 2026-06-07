using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class GameDetailViewModel : ViewModelBase
{
    private readonly IGameLauncher gameLauncher;
    private readonly ISaveBackupService saveBackupService;
    private readonly IFilePickerService filePickerService;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly IGameSessionPresentationService presentationService;
    private readonly ISaveSyncCoordinator saveSyncCoordinator;
    private readonly Action<Game> editGame;
    private readonly Action<string> openDirectory;
    private readonly Func<Game, LaunchResult, Game> recordLaunchResult;
    private readonly Action<Game> gameUpdated;
    private Game game;
    private string launchStatusText = "准备就绪";
    private string saveBackupStatusText = "尚未备份存档";
    private string syncStatusText = "尚未同步";
    private bool hasSyncConflict;
    private bool requiresCloudDownloadConfirmation;

    public GameDetailViewModel(
        Game game,
        IGameLauncher gameLauncher,
        Func<Game, LaunchResult, Game> recordLaunchResult,
        Action<Game> gameUpdated,
        Action goBack,
        ISaveBackupService saveBackupService,
        IFilePickerService filePickerService)
        : this(
            game,
            gameLauncher,
            recordLaunchResult,
            gameUpdated,
            goBack,
            saveBackupService,
            filePickerService,
            new InMemoryAppSettingsStore(),
            new NoopGameSessionPresentationService())
    {
    }

    public GameDetailViewModel(
        Game game,
        IGameLauncher gameLauncher,
        Func<Game, LaunchResult, Game> recordLaunchResult,
        Action<Game> gameUpdated,
        Action goBack,
        ISaveBackupService saveBackupService,
        IFilePickerService filePickerService,
        IAppSettingsStore appSettingsStore,
        IGameSessionPresentationService presentationService,
        ISaveSyncCoordinator? saveSyncCoordinator = null,
        Action<Game>? editGame = null,
        Action<string>? openDirectory = null)
    {
        this.game = game;
        this.gameLauncher = gameLauncher;
        this.recordLaunchResult = recordLaunchResult;
        this.gameUpdated = gameUpdated;
        this.saveBackupService = saveBackupService;
        this.filePickerService = filePickerService;
        this.appSettingsStore = appSettingsStore;
        this.presentationService = presentationService;
        this.saveSyncCoordinator = saveSyncCoordinator ?? new NoopSaveSyncCoordinator();
        this.editGame = editGame ?? (_ => { });
        this.openDirectory = openDirectory ?? (_ => { });
        BackCommand = new RelayCommand(_ => goBack());
        StartGameCommand = new AsyncRelayCommand(_ => StartGameAsync());
        BackupSaveCommand = new AsyncRelayCommand(_ => BackupSaveAsync(), _ => HasSavePath);
        RestoreSaveCommand = new AsyncRelayCommand(_ => RestoreSaveAsync(), _ => HasSavePath);
        SyncSaveCommand = new AsyncRelayCommand(_ => SyncSaveAsync(), _ => HasSavePath);
        RestoreBackupCommand = new AsyncRelayCommand(parameter => RestoreBackupAsync(parameter as SaveBackupEntry), parameter => parameter is SaveBackupEntry);
        DeleteBackupCommand = new AsyncRelayCommand(parameter => DeleteBackupAsync(parameter as SaveBackupEntry), parameter => parameter is SaveBackupEntry);
        ResolveUseLocalCommand = new AsyncRelayCommand(_ => ResolveConflictAsync(SaveConflictResolution.UseLocal), _ => HasSyncDecision);
        ResolveUseCloudCommand = new AsyncRelayCommand(_ => ResolveConflictAsync(SaveConflictResolution.UseCloud), _ => HasSyncDecision);
        ResolveKeepBothCommand = new AsyncRelayCommand(_ => ResolveConflictAsync(SaveConflictResolution.KeepBoth), _ => HasSyncDecision);
        CancelConflictCommand = new AsyncRelayCommand(_ => ResolveConflictAsync(SaveConflictResolution.Cancel), _ => HasSyncDecision);
        EditGameCommand = new RelayCommand(_ => this.editGame(Game));
        OpenGameDirectoryCommand = new RelayCommand(_ => this.openDirectory(Game.GameRootPath), _ => !string.IsNullOrWhiteSpace(Game.GameRootPath));
        OpenSaveDirectoryCommand = new RelayCommand(_ => this.openDirectory(Game.SavePath), _ => !string.IsNullOrWhiteSpace(Game.SavePath));
        RefreshSyncState();
        RefreshSaveBackups();
    }

    public Game Game
    {
        get => game;
        private set
        {
            if (SetProperty(ref game, value))
            {
                OnPropertyChanged(nameof(TotalPlayTimeText));
                OnPropertyChanged(nameof(LastLaunchTimeText));
            }
        }
    }

    public string TotalPlayTimeText => $"{(int)Game.TotalPlayTime.TotalHours} 小时 {Game.TotalPlayTime.Minutes} 分钟";

    public string LastLaunchTimeText => Game.LastLaunchTime?.ToString("yyyy-MM-dd HH:mm") ?? "从未启动";

    public string SaveBackupStatusText
    {
        get => saveBackupStatusText;
        private set => SetProperty(ref saveBackupStatusText, value);
    }

    public string LaunchStatusText
    {
        get => launchStatusText;
        private set => SetProperty(ref launchStatusText, value);
    }

    public string SyncStatusText
    {
        get => syncStatusText;
        private set => SetProperty(ref syncStatusText, value);
    }

    public bool HasSyncConflict
    {
        get => hasSyncConflict;
        private set
        {
            if (SetProperty(ref hasSyncConflict, value))
            {
                OnPropertyChanged(nameof(HasSyncDecision));
                OnPropertyChanged(nameof(SyncDecisionTitle));
                OnPropertyChanged(nameof(SyncDecisionDescription));
                OnPropertyChanged(nameof(UseLocalActionText));
                OnPropertyChanged(nameof(UseCloudActionText));
                RaiseConflictCommandState();
            }
        }
    }

    public bool RequiresCloudDownloadConfirmation
    {
        get => requiresCloudDownloadConfirmation;
        private set
        {
            if (SetProperty(ref requiresCloudDownloadConfirmation, value))
            {
                OnPropertyChanged(nameof(HasSyncDecision));
                OnPropertyChanged(nameof(SyncDecisionTitle));
                OnPropertyChanged(nameof(SyncDecisionDescription));
                OnPropertyChanged(nameof(UseLocalActionText));
                OnPropertyChanged(nameof(UseCloudActionText));
                RaiseConflictCommandState();
            }
        }
    }

    public bool HasSyncDecision => HasSyncConflict || RequiresCloudDownloadConfirmation;

    public string SyncDecisionTitle => RequiresCloudDownloadConfirmation ? "发现较新的云端存档" : "发现存档冲突";

    public string SyncDecisionDescription => RequiresCloudDownloadConfirmation
        ? "云端存档在上次同步后发生了变化。确认处理方式前，Firefly 不会覆盖本地内容。"
        : "本地与云端存档都发生了变化。选择处理方式前，两端内容都不会被覆盖。";

    public string UseLocalActionText => RequiresCloudDownloadConfirmation ? "保留本地并上传" : "使用本地并上传";

    public string UseCloudActionText => RequiresCloudDownloadConfirmation ? "下载云端存档" : "使用云端";

    public ObservableCollection<SaveBackupEntry> SaveBackups { get; } = [];

    public bool HasSaveBackups => SaveBackups.Count > 0;

    public bool HasNoSaveBackups => !HasSaveBackups;

    public ICommand BackCommand { get; }

    public ICommand StartGameCommand { get; }

    public ICommand BackupSaveCommand { get; }

    public ICommand RestoreSaveCommand { get; }

    public ICommand SyncSaveCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand DeleteBackupCommand { get; }

    public ICommand ResolveUseLocalCommand { get; }
    public ICommand ResolveUseCloudCommand { get; }
    public ICommand ResolveKeepBothCommand { get; }
    public ICommand CancelConflictCommand { get; }
    public ICommand EditGameCommand { get; }
    public ICommand OpenGameDirectoryCommand { get; }
    public ICommand OpenSaveDirectoryCommand { get; }

    private bool HasSavePath => !string.IsNullOrWhiteSpace(Game.SavePath);

    private async Task StartGameAsync()
    {
        var settings = appSettingsStore.Load();
        LaunchStatusText = "正在准备启动游戏...";
        var beforeSync = await saveSyncCoordinator.CheckBeforeLaunchAsync(Game);
        ApplySyncResult(beforeSync);
        if (beforeSync.RequiresUserAction)
        {
            LaunchStatusText = "存档同步需要确认，请先选择处理方式";
            return;
        }

        try
        {
            if (settings.BackupBeforeGameLaunch && HasSavePath && Directory.Exists(Game.SavePath))
            {
                SaveBackupStatusText = "正在创建启动前存档备份...";
                await saveBackupService.BackupAsync(Game);
                RefreshSaveBackups();
            }
        }
        catch (Exception ex)
        {
            LaunchStatusText = $"启动前备份失败：{ex.Message}";
            return;
        }

        if (settings.MinimizeAfterGameLaunch)
        {
            presentationService.Minimize();
        }

        try
        {
            LaunchStatusText = "游戏运行中";
            var result = await gameLauncher.LaunchAsync(Game);
            var updatedGame = recordLaunchResult(Game, result);
            Game = updatedGame;
            gameUpdated(updatedGame);
            var afterSync = await saveSyncCoordinator.SyncAfterExitAsync(updatedGame);
            ApplySyncResult(afterSync);
            LaunchStatusText = "游戏已退出，游玩记录已更新";
        }
        catch (Exception ex)
        {
            LaunchStatusText = $"启动失败：{ex.Message}";
        }
        finally
        {
            if (settings.RestoreAfterGameExit)
            {
                presentationService.Restore();
            }
        }
    }

    private async Task BackupSaveAsync()
    {
        try
        {
            SaveBackupStatusText = "正在备份存档...";
            var backupPath = await saveBackupService.BackupAsync(Game);
            RefreshSaveBackups();
            SaveBackupStatusText = $"备份完成：{Path.GetFileName(backupPath)}";
        }
        catch (Exception ex)
        {
            SaveBackupStatusText = $"备份失败：{ex.Message}";
        }
    }

    private async Task RestoreSaveAsync()
    {
        var backupPath = filePickerService.PickSaveBackupFile(saveBackupService.GetBackupDirectory(Game));
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            SaveBackupStatusText = "已取消恢复存档";
            return;
        }

        try
        {
            SaveBackupStatusText = "正在恢复存档...";
            await saveBackupService.RestoreAsync(Game, backupPath);
            RefreshSaveBackups();
            SaveBackupStatusText = $"恢复完成：{Path.GetFileName(backupPath)}";
        }
        catch (Exception ex)
        {
            SaveBackupStatusText = $"恢复失败：{ex.Message}";
        }
    }

    private async Task SyncSaveAsync()
    {
        SyncStatusText = "正在同步当前游戏存档...";
        var result = await saveSyncCoordinator.SynchronizeNowAsync(Game);
        ApplySyncResult(result);
        RefreshSaveBackups();
    }

    private async Task RestoreBackupAsync(SaveBackupEntry? backup)
    {
        if (backup is null)
        {
            return;
        }

        try
        {
            SaveBackupStatusText = "正在恢复存档...";
            await saveBackupService.RestoreAsync(Game, backup.Path);
            RefreshSaveBackups();
            SaveBackupStatusText = $"恢复完成：{backup.FileName}";
        }
        catch (Exception ex)
        {
            SaveBackupStatusText = $"恢复失败：{ex.Message}";
        }
    }

    private async Task DeleteBackupAsync(SaveBackupEntry? backup)
    {
        await Task.CompletedTask;
        if (backup is null)
        {
            return;
        }

        if (saveBackupService.DeleteBackup(backup.Path))
        {
            SaveBackupStatusText = $"已删除备份：{backup.FileName}";
            RefreshSaveBackups();
        }
        else
        {
            SaveBackupStatusText = $"删除失败：{backup.FileName}";
        }
    }

    private void RefreshSaveBackups()
    {
        SaveBackups.Clear();
        foreach (var backup in saveBackupService.GetBackups(Game))
        {
            SaveBackups.Add(backup);
        }

        OnPropertyChanged(nameof(HasSaveBackups));
        OnPropertyChanged(nameof(HasNoSaveBackups));
    }

    private async Task ResolveConflictAsync(SaveConflictResolution resolution)
    {
        var result = await saveSyncCoordinator.ResolveConflictAsync(Game, resolution);
        ApplySyncResult(result);
        RefreshSaveBackups();
    }

    private void RefreshSyncState()
    {
        var state = saveSyncCoordinator.GetState(Game.Id);
        if (state is null)
        {
            return;
        }

        SyncStatusText = state.Status switch
        {
            "synced" => "存档已同步",
            "conflict" => "发现存档冲突",
            "local-newer" => "本地存档较新",
            "local-only" => "云端暂无存档",
            "cloud-newer" => "云端存档较新，等待确认",
            "retry-pending" => "上次同步失败，将在下次操作时重试",
            "conflict-preserved" => "冲突版本已分别保留",
            _ => state.Status
        };
        HasSyncConflict = state.Status == "conflict";
        RequiresCloudDownloadConfirmation = state.Status == "cloud-newer";
    }

    private void ApplySyncResult(SaveSyncOperationResult result)
    {
        SyncStatusText = result.Message;
        HasSyncConflict = result.HasConflict;
        RequiresCloudDownloadConfirmation = result.RequiresCloudDownloadConfirmation;
    }

    private void RaiseConflictCommandState()
    {
        ((AsyncRelayCommand)ResolveUseLocalCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ResolveUseCloudCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ResolveKeepBothCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)CancelConflictCommand).RaiseCanExecuteChanged();
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly IGameLibraryService? gameLibraryService;
    private readonly IGameMetadataProvider? metadataProvider;
    private readonly IBangumiAccountStore? bangumiAccountStore;
    private readonly IBangumiApiClient? bangumiApiClient;
    private readonly IRemoteImageCacheService? remoteImageCacheService;
    private Game game;
    private string launchStatusText = "准备就绪";
    private string saveBackupStatusText = "尚未备份存档";
    private string syncStatusText = "尚未同步";
    private bool hasSyncConflict;
    private bool requiresCloudDownloadConfirmation;
    private string externalMetadataStatusText = string.Empty;
    private string bangumiCollectionStatusText = "尚未同步 Bangumi 收藏状态";
    private BangumiCollectionType selectedBangumiCollectionType;
    private ExternalGameMetadata? metadataRefreshPreview;
    private bool refreshImportName;
    private bool refreshImportCover;
    private bool refreshImportSummary = true;
    private bool refreshImportReleaseDate = true;
    private bool refreshImportDeveloper = true;
    private bool refreshImportPublisher = true;
    private bool refreshImportTags = true;
    private bool isExternalSummaryExpanded;
    private bool bangumiCollectionExistsRemotely;
    private ExternalMetadataConflict? externalMetadataConflict;
    private int bangumiRating;
    private string bangumiComment = string.Empty;

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
        Action<string>? openDirectory = null,
        IGameLibraryService? gameLibraryService = null,
        IGameMetadataProvider? metadataProvider = null,
        IBangumiAccountStore? bangumiAccountStore = null,
        IBangumiApiClient? bangumiApiClient = null,
        IRemoteImageCacheService? remoteImageCacheService = null)
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
        this.gameLibraryService = gameLibraryService;
        this.metadataProvider = metadataProvider;
        this.bangumiAccountStore = bangumiAccountStore;
        this.bangumiApiClient = bangumiApiClient;
        this.remoteImageCacheService = remoteImageCacheService;
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
        RefreshExternalMetadataCommand = new AsyncRelayCommand(_ => RefreshExternalMetadataAsync(), _ => HasLinkedExternalMetadata);
        ApplyExternalMetadataRefreshCommand = new AsyncRelayCommand(
            _ => ApplyExternalMetadataRefreshAsync(),
            _ => HasMetadataRefreshPreview);
        CancelExternalMetadataRefreshCommand = new RelayCommand(_ => CancelExternalMetadataRefresh(), _ => HasMetadataRefreshPreview);
        ToggleExternalSummaryCommand = new RelayCommand(_ => ToggleExternalSummary(), _ => CanToggleExternalSummary);
        UnlinkExternalMetadataCommand = new RelayCommand(_ => UnlinkExternalMetadata(), _ => HasLinkedExternalMetadata);
        OpenExternalSourceCommand = new RelayCommand(_ => OpenExternalSource(), _ => !string.IsNullOrWhiteSpace(Game.ExternalMetadata?.SubjectUrl));
        UseLocalExternalMetadataCommand = new RelayCommand(
            _ => ResolveExternalMetadataConflict(ExternalMetadataConflictResolution.UseLocal),
            _ => HasExternalMetadataConflict);
        UseCloudExternalMetadataCommand = new RelayCommand(
            _ => ResolveExternalMetadataConflict(ExternalMetadataConflictResolution.UseCloud),
            _ => HasExternalMetadataConflict);
        UnlinkConflictingExternalMetadataCommand = new RelayCommand(
            _ => ResolveExternalMetadataConflict(ExternalMetadataConflictResolution.UnlinkLocal),
            _ => HasExternalMetadataConflict);
        RefreshBangumiCollectionCommand = new AsyncRelayCommand(_ => RefreshBangumiCollectionAsync(), _ => ShowBangumiCollection);
        SaveBangumiCollectionCommand = new AsyncRelayCommand(
            _ => SaveBangumiCollectionAsync(),
            _ => ShowBangumiCollection && SelectedBangumiCollectionType != BangumiCollectionType.None);
        var cachedCollection = gameLibraryService?.GetBangumiCollectionState(game.Id);
        selectedBangumiCollectionType = cachedCollection?.Type ?? BangumiCollectionType.None;
        bangumiRating = cachedCollection?.Rating ?? 0;
        bangumiComment = cachedCollection?.Comment ?? string.Empty;
        bangumiCollectionExistsRemotely = cachedCollection is not null;
        RefreshExternalMetadataConflict();
        RefreshSyncState();
        RefreshSaveBackups();
        if (ShowBangumiCollection)
        {
            _ = RefreshBangumiCollectionAsync();
        }
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
                RaiseMetadataProperties();
                RefreshExternalMetadataConflict();
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
    public ICommand RefreshExternalMetadataCommand { get; }
    public ICommand ApplyExternalMetadataRefreshCommand { get; }
    public ICommand CancelExternalMetadataRefreshCommand { get; }
    public ICommand ToggleExternalSummaryCommand { get; }
    public ICommand UnlinkExternalMetadataCommand { get; }
    public ICommand OpenExternalSourceCommand { get; }
    public ICommand UseLocalExternalMetadataCommand { get; }
    public ICommand UseCloudExternalMetadataCommand { get; }
    public ICommand UnlinkConflictingExternalMetadataCommand { get; }
    public ICommand RefreshBangumiCollectionCommand { get; }
    public ICommand SaveBangumiCollectionCommand { get; }

    public bool HasExternalMetadata => Game.ExternalMetadata is not null;

    public bool HasLinkedExternalMetadata => Game.ExternalMetadata?.IsLinked == true;

    public bool ShowBangumiCollection =>
        HasLinkedExternalMetadata &&
        string.Equals(Game.ExternalMetadata?.Provider, "bangumi", StringComparison.OrdinalIgnoreCase) &&
        bangumiAccountStore?.Load() is { RequiresReconnect: false };

    public string ExternalMetadataTitle => Game.ExternalMetadata is null
        ? "尚未导入在线游戏资料"
        : string.IsNullOrWhiteSpace(Game.ExternalMetadata.LocalizedName)
            ? Game.ExternalMetadata.OriginalName
            : Game.ExternalMetadata.LocalizedName;

    public string ExternalMetadataSummary => Game.ExternalMetadata?.Summary ?? string.Empty;

    public string ExternalMetadataSummaryDisplay
    {
        get
        {
            var summary = ExternalMetadataSummary;
            return !IsExternalSummaryExpanded && CanToggleExternalSummary
                ? $"{summary[..320].TrimEnd()}..."
                : summary;
        }
    }

    public bool CanToggleExternalSummary => ExternalMetadataSummary.Length > 320;

    public bool IsExternalSummaryExpanded
    {
        get => isExternalSummaryExpanded;
        private set
        {
            if (SetProperty(ref isExternalSummaryExpanded, value))
            {
                OnPropertyChanged(nameof(ExternalMetadataSummaryDisplay));
                OnPropertyChanged(nameof(ExternalSummaryToggleText));
            }
        }
    }

    public string ExternalSummaryToggleText => IsExternalSummaryExpanded ? "收起简介" : "展开简介";

    public string ExternalMetadataInfo => Game.ExternalMetadata is null
        ? string.Empty
        : string.Join(" · ", new[]
        {
            Game.ExternalMetadata.ReleaseDate,
            Game.ExternalMetadata.Developer,
            Game.ExternalMetadata.Publisher
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string ExternalMetadataTags => string.Join("  ", Game.ExternalMetadata?.Tags ?? []);

    public string ExternalMetadataStatusText
    {
        get => externalMetadataStatusText;
        private set => SetProperty(ref externalMetadataStatusText, value);
    }

    public ExternalMetadataConflict? ExternalMetadataConflict
    {
        get => externalMetadataConflict;
        private set
        {
            if (SetProperty(ref externalMetadataConflict, value))
            {
                OnPropertyChanged(nameof(HasExternalMetadataConflict));
                OnPropertyChanged(nameof(ExternalMetadataConflictText));
                RaiseExternalMetadataConflictCommandState();
            }
        }
    }

    public bool HasExternalMetadataConflict => ExternalMetadataConflict is not null;

    public string ExternalMetadataConflictText
    {
        get
        {
            var conflict = ExternalMetadataConflict;
            return conflict is null
                ? string.Empty
                : $"本机 {conflict.LocalSnapshot.Provider}/{conflict.LocalSnapshot.SubjectId} 与云端 {conflict.CloudSnapshot.Provider}/{conflict.CloudSnapshot.SubjectId} 不一致";
        }
    }

    public ExternalGameMetadata? MetadataRefreshPreview
    {
        get => metadataRefreshPreview;
        private set
        {
            if (SetProperty(ref metadataRefreshPreview, value))
            {
                OnPropertyChanged(nameof(HasMetadataRefreshPreview));
                OnPropertyChanged(nameof(MetadataRefreshDifferences));
                ((AsyncRelayCommand)ApplyExternalMetadataRefreshCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelExternalMetadataRefreshCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasMetadataRefreshPreview => MetadataRefreshPreview is not null;

    public string MetadataRefreshDifferences
    {
        get
        {
            var current = Game.ExternalMetadata;
            var preview = MetadataRefreshPreview;
            if (current is null || preview is null)
            {
                return string.Empty;
            }

            var changed = new List<string>();
            AddDifference(changed, "名称", current.OriginalName != preview.OriginalName || current.LocalizedName != preview.LocalizedName);
            AddDifference(changed, "封面", current.ImageUrl != preview.ImageUrl);
            AddDifference(changed, "简介", current.Summary != preview.Summary);
            AddDifference(changed, "发行日期", current.ReleaseDate != preview.ReleaseDate);
            AddDifference(changed, "开发商", current.Developer != preview.Developer);
            AddDifference(changed, "发行商", current.Publisher != preview.Publisher);
            AddDifference(changed, "标签", !current.Tags.SequenceEqual(preview.Tags, StringComparer.Ordinal));
            return changed.Count == 0 ? "在线资料没有可见变化" : $"发现变化：{string.Join("、", changed)}";
        }
    }

    public bool RefreshImportName
    {
        get => refreshImportName;
        set => SetProperty(ref refreshImportName, value);
    }

    public bool RefreshImportCover
    {
        get => refreshImportCover;
        set => SetProperty(ref refreshImportCover, value);
    }

    public bool RefreshImportSummary
    {
        get => refreshImportSummary;
        set => SetProperty(ref refreshImportSummary, value);
    }

    public bool RefreshImportReleaseDate
    {
        get => refreshImportReleaseDate;
        set => SetProperty(ref refreshImportReleaseDate, value);
    }

    public bool RefreshImportDeveloper
    {
        get => refreshImportDeveloper;
        set => SetProperty(ref refreshImportDeveloper, value);
    }

    public bool RefreshImportPublisher
    {
        get => refreshImportPublisher;
        set => SetProperty(ref refreshImportPublisher, value);
    }

    public bool RefreshImportTags
    {
        get => refreshImportTags;
        set => SetProperty(ref refreshImportTags, value);
    }

    public IReadOnlyList<SettingOption<BangumiCollectionType>> BangumiCollectionOptions { get; } =
    [
        new(BangumiCollectionType.Wish, "想玩"),
        new(BangumiCollectionType.Collect, "玩过"),
        new(BangumiCollectionType.Doing, "在玩"),
        new(BangumiCollectionType.OnHold, "搁置"),
        new(BangumiCollectionType.Dropped, "抛弃")
    ];

    public IReadOnlyList<SettingOption<int>> BangumiRatingOptions { get; } =
    [
        new(0, "不评分"),
        new(1, "1"),
        new(2, "2"),
        new(3, "3"),
        new(4, "4"),
        new(5, "5"),
        new(6, "6"),
        new(7, "7"),
        new(8, "8"),
        new(9, "9"),
        new(10, "10")
    ];

    public BangumiCollectionType SelectedBangumiCollectionType
    {
        get => selectedBangumiCollectionType;
        set
        {
            if (SetProperty(ref selectedBangumiCollectionType, value))
            {
                ((AsyncRelayCommand)SaveBangumiCollectionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string BangumiCollectionStatusText
    {
        get => bangumiCollectionStatusText;
        private set => SetProperty(ref bangumiCollectionStatusText, value);
    }

    public int BangumiRating
    {
        get => bangumiRating;
        set => SetProperty(ref bangumiRating, Math.Clamp(value, 0, 10));
    }

    public string BangumiComment
    {
        get => bangumiComment;
        set => SetProperty(ref bangumiComment, value ?? string.Empty);
    }

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

    private void RaiseExternalMetadataConflictCommandState()
    {
        ((RelayCommand)UseLocalExternalMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UseCloudExternalMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UnlinkConflictingExternalMetadataCommand).RaiseCanExecuteChanged();
    }

    private void RefreshExternalMetadataConflict()
    {
        ExternalMetadataConflict = gameLibraryService?.GetExternalMetadataConflict(Game.Id);
    }

    private void ResolveExternalMetadataConflict(ExternalMetadataConflictResolution resolution)
    {
        if (gameLibraryService is null || !HasExternalMetadataConflict)
        {
            return;
        }

        try
        {
            Game = gameLibraryService.ResolveExternalMetadataConflict(Game.Id, resolution);
            gameUpdated(Game);
            MetadataRefreshPreview = null;
            RefreshExternalMetadataConflict();
            ExternalMetadataStatusText = resolution switch
            {
                ExternalMetadataConflictResolution.UseCloud => "已采用云端在线资料",
                ExternalMetadataConflictResolution.UnlinkLocal => "已解除本地在线关联",
                _ => "已保留本机在线资料"
            };
        }
        catch (Exception ex)
        {
            ExternalMetadataStatusText = $"处理在线资料冲突失败：{ex.Message}";
        }
    }

    private async Task RefreshExternalMetadataAsync()
    {
        var current = Game.ExternalMetadata;
        if (current is null || metadataProvider is null || gameLibraryService is null)
        {
            return;
        }

        try
        {
            MetadataRefreshPreview = null;
            ExternalMetadataStatusText = "正在刷新在线资料...";
            var refreshed = await metadataProvider.GetDetailsAsync(current.SubjectId);
            if (refreshed is null)
            {
                ExternalMetadataStatusText = "在线条目不存在或暂时不可用";
                return;
            }

            MetadataRefreshPreview = refreshed;
            ExternalMetadataStatusText = "在线资料已读取，请确认需要更新的字段";
        }
        catch (Exception ex)
        {
            ExternalMetadataStatusText = $"刷新失败：{ex.Message}";
        }
    }

    private async Task ApplyExternalMetadataRefreshAsync()
    {
        var current = Game.ExternalMetadata;
        if (current is null || MetadataRefreshPreview is null || gameLibraryService is null)
        {
            return;
        }

        try
        {
            var options = new MetadataImportOptions(
                RefreshImportName,
                RefreshImportCover,
                RefreshImportSummary,
                RefreshImportReleaseDate,
                RefreshImportDeveloper,
                RefreshImportPublisher,
                RefreshImportTags);
            var preview = MetadataRefreshPreview;
            var mergedMetadata = options.Merge(current, preview);
            var updatedName = Game.Name;
            if (RefreshImportName)
            {
                var importedName = string.IsNullOrWhiteSpace(preview.LocalizedName)
                    ? preview.OriginalName
                    : preview.LocalizedName;
                if (!string.IsNullOrWhiteSpace(importedName))
                {
                    updatedName = importedName;
                }
            }

            var updatedCover = Game.CoverImagePath;
            if (RefreshImportCover &&
                remoteImageCacheService is not null &&
                !string.IsNullOrWhiteSpace(preview.ImageUrl))
            {
                var downloaded = await remoteImageCacheService.DownloadAsync(
                    preview.Provider,
                    preview.SubjectId,
                    preview.ImageUrl);
                if (!string.IsNullOrWhiteSpace(downloaded))
                {
                    updatedCover = downloaded;
                }
            }

            Game = gameLibraryService.UpdateGame(new UpdateGameRequest(
                Game.Id,
                updatedName,
                Game.ExecutablePath,
                Game.GameRootPath,
                Game.SavePath,
                updatedCover,
                Game.LaunchArguments,
                Game.RunAsAdministrator,
                Game.WorkingDirectory,
                Game.MonitorProcessName,
                Game.SyncEnabled,
                mergedMetadata));
            gameUpdated(Game);
            MetadataRefreshPreview = null;
            ExternalMetadataStatusText = "已应用选中的在线资料字段";
        }
        catch (Exception ex)
        {
            ExternalMetadataStatusText = $"应用失败：{ex.Message}";
        }
    }

    private void CancelExternalMetadataRefresh()
    {
        MetadataRefreshPreview = null;
        ExternalMetadataStatusText = "已取消资料刷新";
    }

    private void ToggleExternalSummary()
    {
        IsExternalSummaryExpanded = !IsExternalSummaryExpanded;
    }

    private void UnlinkExternalMetadata()
    {
        if (Game.ExternalMetadata is null || gameLibraryService is null)
        {
            return;
        }

        Game = gameLibraryService.UpdateExternalMetadata(
            Game.Id,
            Game.ExternalMetadata with { IsLinked = false });
        gameUpdated(Game);
        MetadataRefreshPreview = null;
        ExternalMetadataStatusText = "已解除在线关联，资料快照仍保留在本机";
    }

    private void OpenExternalSource()
    {
        var url = Game.ExternalMetadata?.SubjectUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private async Task RefreshBangumiCollectionAsync()
    {
        var account = bangumiAccountStore?.Load();
        var metadata = Game.ExternalMetadata;
        if (account is null || metadata is null || bangumiApiClient is null)
        {
            return;
        }

        try
        {
            BangumiCollectionStatusText = "正在读取 Bangumi 收藏状态...";
            var state = await bangumiApiClient.GetCollectionAsync(account, Game.Id, metadata.SubjectId);
            SelectedBangumiCollectionType = state?.Type ?? BangumiCollectionType.None;
            BangumiRating = state?.Rating ?? 0;
            BangumiComment = state?.Comment ?? string.Empty;
            bangumiCollectionExistsRemotely = state is not null;
            if (state is not null)
            {
                gameLibraryService?.SaveBangumiCollectionState(state);
            }

            BangumiCollectionStatusText = state is null ? "Bangumi 中尚未收藏" : "Bangumi 收藏状态已同步";
        }
        catch (Exception ex)
        {
            HandleBangumiFailure(account, ex, "读取失败");
        }
    }

    private async Task SaveBangumiCollectionAsync()
    {
        var account = bangumiAccountStore?.Load();
        var metadata = Game.ExternalMetadata;
        if (account is null || metadata is null || bangumiApiClient is null)
        {
            return;
        }

        var previousType = gameLibraryService?.GetBangumiCollectionState(Game.Id)?.Type ?? BangumiCollectionType.None;
        try
        {
            BangumiCollectionStatusText = "正在更新 Bangumi 收藏状态...";
            var cached = gameLibraryService?.GetBangumiCollectionState(Game.Id);
            var state = new BangumiCollectionState(
                Game.Id,
                metadata.SubjectId,
                account.Username,
                SelectedBangumiCollectionType,
                BangumiRating,
                BangumiComment.Trim(),
                cached?.RemoteUpdatedAtUtc,
                DateTime.UtcNow);
            var saved = await bangumiApiClient.SaveCollectionAsync(account, state, bangumiCollectionExistsRemotely);
            gameLibraryService?.SaveBangumiCollectionState(saved);
            bangumiCollectionExistsRemotely = true;
            BangumiCollectionStatusText = "Bangumi 收藏状态已更新";
        }
        catch (Exception ex)
        {
            SelectedBangumiCollectionType = previousType;
            HandleBangumiFailure(account, ex, "更新失败");
        }
    }

    private void HandleBangumiFailure(BangumiAccount account, Exception exception, string prefix)
    {
        if (exception is BangumiApiException { IsAuthenticationFailure: true } && bangumiAccountStore is not null)
        {
            bangumiAccountStore.Save(account with { RequiresReconnect = true });
            BangumiCollectionStatusText = "Bangumi 授权已失效，请在设置中重新连接";
            RaiseMetadataProperties();
            return;
        }

        BangumiCollectionStatusText = $"{prefix}：{exception.Message}";
    }

    private void RaiseMetadataProperties()
    {
        OnPropertyChanged(nameof(HasExternalMetadata));
        OnPropertyChanged(nameof(HasLinkedExternalMetadata));
        OnPropertyChanged(nameof(ShowBangumiCollection));
        OnPropertyChanged(nameof(ExternalMetadataTitle));
        OnPropertyChanged(nameof(ExternalMetadataSummary));
        OnPropertyChanged(nameof(ExternalMetadataSummaryDisplay));
        OnPropertyChanged(nameof(CanToggleExternalSummary));
        OnPropertyChanged(nameof(ExternalSummaryToggleText));
        OnPropertyChanged(nameof(ExternalMetadataInfo));
        OnPropertyChanged(nameof(ExternalMetadataTags));
        OnPropertyChanged(nameof(HasExternalMetadataConflict));
        OnPropertyChanged(nameof(ExternalMetadataConflictText));
        ((AsyncRelayCommand)RefreshExternalMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UnlinkExternalMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenExternalSourceCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)RefreshBangumiCollectionCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SaveBangumiCollectionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ToggleExternalSummaryCommand).RaiseCanExecuteChanged();
        RaiseExternalMetadataConflictCommandState();
    }

    private static void AddDifference(List<string> values, string label, bool changed)
    {
        if (changed)
        {
            values.Add(label);
        }
    }
}

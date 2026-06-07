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
    private readonly Func<Game, LaunchResult, Game> recordLaunchResult;
    private readonly Action<Game> gameUpdated;
    private Game game;
    private string launchStatusText = "准备就绪";
    private string saveBackupStatusText = "尚未备份存档";

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
        IGameSessionPresentationService presentationService)
    {
        this.game = game;
        this.gameLauncher = gameLauncher;
        this.recordLaunchResult = recordLaunchResult;
        this.gameUpdated = gameUpdated;
        this.saveBackupService = saveBackupService;
        this.filePickerService = filePickerService;
        this.appSettingsStore = appSettingsStore;
        this.presentationService = presentationService;
        BackCommand = new RelayCommand(_ => goBack());
        StartGameCommand = new AsyncRelayCommand(_ => StartGameAsync());
        BackupSaveCommand = new AsyncRelayCommand(_ => BackupSaveAsync(), _ => HasSavePath);
        RestoreSaveCommand = new AsyncRelayCommand(_ => RestoreSaveAsync(), _ => HasSavePath);
        RestoreBackupCommand = new AsyncRelayCommand(parameter => RestoreBackupAsync(parameter as SaveBackupEntry), parameter => parameter is SaveBackupEntry);
        DeleteBackupCommand = new AsyncRelayCommand(parameter => DeleteBackupAsync(parameter as SaveBackupEntry), parameter => parameter is SaveBackupEntry);
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

    public ObservableCollection<SaveBackupEntry> SaveBackups { get; } = [];

    public bool HasSaveBackups => SaveBackups.Count > 0;

    public bool HasNoSaveBackups => !HasSaveBackups;

    public ICommand BackCommand { get; }

    public ICommand StartGameCommand { get; }

    public ICommand BackupSaveCommand { get; }

    public ICommand RestoreSaveCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand DeleteBackupCommand { get; }

    private bool HasSavePath => !string.IsNullOrWhiteSpace(Game.SavePath);

    private async Task StartGameAsync()
    {
        var settings = appSettingsStore.Load();
        LaunchStatusText = "正在准备启动游戏...";
        try
        {
            if (settings.BackupBeforeGameLaunch && HasSavePath)
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
}

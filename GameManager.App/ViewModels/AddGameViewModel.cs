using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class AddGameViewModel : ViewModelBase, IDisposable
{
    private readonly IFilePickerService filePickerService;
    private readonly Action<AddGameRequest> save;
    private readonly RelayCommand saveCommand;
    private readonly IGameMetadataProvider? metadataProvider;
    private readonly IRemoteImageCacheService? remoteImageCacheService;
    private readonly RelayCommand cancelMetadataRequestCommand;
    private CancellationTokenSource? metadataRequestCancellation;
    private string gameName = string.Empty;
    private string executablePath = string.Empty;
    private string gameRootPath = string.Empty;
    private string savePath = string.Empty;
    private string coverImagePath = string.Empty;
    private string launchArguments = string.Empty;
    private bool runAsAdministrator;
    private string workingDirectory = string.Empty;
    private string monitorProcessName = string.Empty;
    private bool syncEnabled = true;
    private string metadataSearchQuery = string.Empty;
    private GameMetadataSearchResult? selectedMetadataResult;
    private ExternalGameMetadata? externalMetadata;
    private ExternalGameMetadata? metadataPreview;
    private string metadataStatusText = "可按游戏名称从 Bangumi 搜索资料";
    private bool isMetadataBusy;
    private bool importMetadataName;
    private bool importMetadataCover;
    private bool importMetadataSummary;
    private bool importMetadataReleaseDate;
    private bool importMetadataDeveloper;
    private bool importMetadataPublisher;
    private bool importMetadataTags;
    private bool usedSearchResultMetadataPreview;

    public AddGameViewModel(IFilePickerService filePickerService, Action<AddGameRequest> save, Action cancel)
        : this(filePickerService, save, cancel, null, "保存")
    {
    }

    public AddGameViewModel(
        IFilePickerService filePickerService,
        Action<AddGameRequest> save,
        Action cancel,
        AddGameRequest? initialValues,
        string submitButtonText,
        IGameMetadataProvider? metadataProvider = null,
        IRemoteImageCacheService? remoteImageCacheService = null)
    {
        this.filePickerService = filePickerService;
        this.save = save;
        this.metadataProvider = metadataProvider ?? new BangumiGameMetadataProvider(new BangumiApiClient());
        this.remoteImageCacheService = remoteImageCacheService ?? new RemoteImageCacheService(AppPaths.MetadataCacheDirectory);
        SubmitButtonText = submitButtonText;
        var importOptions = initialValues is null || initialValues.ExternalMetadata is null
            ? MetadataImportOptions.ForNewGame
            : MetadataImportOptions.ForExistingGame;
        var hasImportedName = initialValues?.ExternalMetadata is { } initialMetadata &&
            (!string.IsNullOrWhiteSpace(initialMetadata.OriginalName) ||
             !string.IsNullOrWhiteSpace(initialMetadata.LocalizedName));
        var hasLocalCover = !string.IsNullOrWhiteSpace(initialValues?.CoverImagePath);
        importMetadataName = importOptions.ImportName || !hasImportedName;
        importMetadataCover = importOptions.ImportCover || !hasLocalCover;
        importMetadataSummary = importOptions.ImportSummary;
        importMetadataReleaseDate = importOptions.ImportReleaseDate;
        importMetadataDeveloper = importOptions.ImportDeveloper;
        importMetadataPublisher = importOptions.ImportPublisher;
        importMetadataTags = importOptions.ImportTags;

        if (initialValues is not null)
        {
            gameName = initialValues.Name;
            executablePath = initialValues.ExecutablePath;
            gameRootPath = initialValues.GameRootPath;
            savePath = initialValues.SavePath;
            coverImagePath = initialValues.CoverImagePath ?? string.Empty;
            launchArguments = initialValues.LaunchArguments;
            runAsAdministrator = initialValues.RunAsAdministrator;
            workingDirectory = initialValues.WorkingDirectory;
            monitorProcessName = initialValues.MonitorProcessName;
            syncEnabled = initialValues.SyncEnabled;
            externalMetadata = initialValues.ExternalMetadata;
        }

        metadataSearchQuery = gameName;
        BrowseExecutableCommand = new RelayCommand(_ => BrowseExecutable());
        BrowseGameRootFolderCommand = new RelayCommand(_ => BrowseGameRootFolder());
        BrowseSaveFolderCommand = new RelayCommand(_ => BrowseSaveFolder());
        BrowseCoverImageCommand = new RelayCommand(_ => BrowseCoverImage());
        BrowseWorkingDirectoryCommand = new RelayCommand(_ => BrowseWorkingDirectory());
        CancelCommand = new RelayCommand(_ =>
        {
            CancelMetadataRequest();
            cancel();
        });
        saveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        SaveCommand = saveCommand;
        SearchMetadataCommand = new AsyncRelayCommand(_ => SearchMetadataAsync(), _ => !IsMetadataBusy);
        PreviewMetadataCommand = new AsyncRelayCommand(
            _ => PreviewMetadataAsync(),
            _ => !IsMetadataBusy && SelectedMetadataResult is not null);
        ApplyMetadataCommand = new AsyncRelayCommand(
            _ => ApplyMetadataAsync(),
            _ => !IsMetadataBusy && MetadataPreview is not null);
        cancelMetadataRequestCommand = new RelayCommand(_ => CancelMetadataRequest(), _ => IsMetadataBusy);
        CancelMetadataRequestCommand = cancelMetadataRequestCommand;
        UnlinkMetadataCommand = new RelayCommand(_ => UnlinkMetadata(), _ => ExternalMetadata is not null);
    }

    public string SubmitButtonText { get; }

    public string GameName
    {
        get => gameName;
        set
        {
            if (SetProperty(ref gameName, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ExecutablePath
    {
        get => executablePath;
        set
        {
            if (SetProperty(ref executablePath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string GameRootPath
    {
        get => gameRootPath;
        set
        {
            if (SetProperty(ref gameRootPath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SavePath
    {
        get => savePath;
        set
        {
            if (SetProperty(ref savePath, value))
            {
                saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CoverImagePath
    {
        get => coverImagePath;
        set => SetProperty(ref coverImagePath, value);
    }

    public string LaunchArguments
    {
        get => launchArguments;
        set => SetProperty(ref launchArguments, value);
    }

    public bool RunAsAdministrator
    {
        get => runAsAdministrator;
        set => SetProperty(ref runAsAdministrator, value);
    }

    public string WorkingDirectory
    {
        get => workingDirectory;
        set => SetProperty(ref workingDirectory, value);
    }

    public string MonitorProcessName
    {
        get => monitorProcessName;
        set => SetProperty(ref monitorProcessName, value);
    }

    public bool SyncEnabled
    {
        get => syncEnabled;
        set => SetProperty(ref syncEnabled, value);
    }

    public string MetadataSearchQuery
    {
        get => metadataSearchQuery;
        set => SetProperty(ref metadataSearchQuery, value);
    }

    public GameMetadataSearchResult? SelectedMetadataResult
    {
        get => selectedMetadataResult;
        set
        {
            if (SetProperty(ref selectedMetadataResult, value))
            {
                MetadataPreview = null;
                ((AsyncRelayCommand)PreviewMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ExternalGameMetadata? ExternalMetadata
    {
        get => externalMetadata;
        private set
        {
            if (SetProperty(ref externalMetadata, value))
            {
                OnPropertyChanged(nameof(HasExternalMetadata));
                OnPropertyChanged(nameof(ExternalMetadataDisplay));
                ((RelayCommand)UnlinkMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasExternalMetadata => ExternalMetadata is not null;

    public string ExternalMetadataDisplay => ExternalMetadata is null
        ? "尚未导入在线资料"
        : ExternalMetadata.IsLinked
            ? $"已关联 Bangumi #{ExternalMetadata.SubjectId} - {ExternalMetadata.LocalizedName}"
            : $"已保留 Bangumi #{ExternalMetadata.SubjectId} 的本地资料快照";

    public ExternalGameMetadata? MetadataPreview
    {
        get => metadataPreview;
        private set
        {
            if (SetProperty(ref metadataPreview, value))
            {
                OnPropertyChanged(nameof(HasMetadataPreview));
                OnPropertyChanged(nameof(MetadataPreviewTitle));
                OnPropertyChanged(nameof(MetadataPreviewInfo));
                OnPropertyChanged(nameof(MetadataPreviewSummary));
                OnPropertyChanged(nameof(MetadataPreviewTags));
                ((AsyncRelayCommand)ApplyMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasMetadataPreview => MetadataPreview is not null;

    public string MetadataPreviewTitle => MetadataPreview is null
        ? string.Empty
        : string.IsNullOrWhiteSpace(MetadataPreview.LocalizedName)
            ? MetadataPreview.OriginalName
            : MetadataPreview.LocalizedName;

    public string MetadataPreviewInfo => MetadataPreview is null
        ? string.Empty
        : string.Join(" 路 ", new[]
        {
            MetadataPreview.OriginalName,
            MetadataPreview.ReleaseDate,
            MetadataPreview.Developer,
            MetadataPreview.Publisher
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string MetadataPreviewSummary => MetadataPreview?.Summary ?? string.Empty;

    public string MetadataPreviewTags => string.Join("  ", MetadataPreview?.Tags ?? []);

    public string MetadataStatusText
    {
        get => metadataStatusText;
        private set => SetProperty(ref metadataStatusText, value);
    }

    public bool IsMetadataBusy
    {
        get => isMetadataBusy;
        private set
        {
            if (SetProperty(ref isMetadataBusy, value))
            {
                cancelMetadataRequestCommand?.RaiseCanExecuteChanged();
                ((AsyncRelayCommand)SearchMetadataCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)PreviewMetadataCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ApplyMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool ImportMetadataName
    {
        get => importMetadataName;
        set => SetProperty(ref importMetadataName, value);
    }

    public bool ImportMetadataCover
    {
        get => importMetadataCover;
        set => SetProperty(ref importMetadataCover, value);
    }

    public bool ImportMetadataSummary
    {
        get => importMetadataSummary;
        set => SetProperty(ref importMetadataSummary, value);
    }

    public bool ImportMetadataReleaseDate
    {
        get => importMetadataReleaseDate;
        set => SetProperty(ref importMetadataReleaseDate, value);
    }

    public bool ImportMetadataDeveloper
    {
        get => importMetadataDeveloper;
        set => SetProperty(ref importMetadataDeveloper, value);
    }

    public bool ImportMetadataPublisher
    {
        get => importMetadataPublisher;
        set => SetProperty(ref importMetadataPublisher, value);
    }

    public bool ImportMetadataTags
    {
        get => importMetadataTags;
        set => SetProperty(ref importMetadataTags, value);
    }

    public ObservableCollection<GameMetadataSearchResult> MetadataSearchResults { get; } = [];

    public ICommand BrowseExecutableCommand { get; }

    public ICommand BrowseGameRootFolderCommand { get; }

    public ICommand BrowseSaveFolderCommand { get; }

    public ICommand BrowseCoverImageCommand { get; }

    public ICommand BrowseWorkingDirectoryCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SearchMetadataCommand { get; }

    public ICommand PreviewMetadataCommand { get; }

    public ICommand ApplyMetadataCommand { get; }

    public ICommand CancelMetadataRequestCommand { get; }

    public ICommand UnlinkMetadataCommand { get; }

    private void BrowseExecutable()
    {
        var path = filePickerService.PickExecutableFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ExecutablePath = path;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && string.IsNullOrWhiteSpace(GameRootPath))
        {
            GameRootPath = directory;
        }

        if (string.IsNullOrWhiteSpace(GameName))
        {
            GameName = Path.GetFileNameWithoutExtension(path);
        }
    }

    private void BrowseGameRootFolder()
    {
        var path = filePickerService.PickFolder("选择游戏目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            GameRootPath = path;
        }
    }

    private void BrowseSaveFolder()
    {
        var path = filePickerService.PickFolder("选择存档目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            SavePath = path;
        }
    }

    private void BrowseCoverImage()
    {
        var path = filePickerService.PickCoverImage();
        if (!string.IsNullOrWhiteSpace(path))
        {
            CoverImagePath = path;
        }
    }

    private void BrowseWorkingDirectory()
    {
        var path = filePickerService.PickFolder("选择工作目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            WorkingDirectory = path;
        }
    }

    private void Save()
    {
        if (!CanSave())
        {
            return;
        }

        CancelMetadataRequest();
        save(new AddGameRequest(
            GameName.Trim(),
            ExecutablePath.Trim(),
            GameRootPath.Trim(),
            SavePath.Trim(),
            string.IsNullOrWhiteSpace(CoverImagePath) ? null : CoverImagePath.Trim(),
            LaunchArguments.Trim(),
            RunAsAdministrator,
            WorkingDirectory.Trim(),
            MonitorProcessName.Trim(),
            SyncEnabled,
            ExternalMetadata));
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(GameName)
            && !string.IsNullOrWhiteSpace(ExecutablePath)
            && !string.IsNullOrWhiteSpace(GameRootPath);
    }

    private async Task SearchMetadataAsync()
    {
        if (metadataProvider is null)
        {
            MetadataStatusText = "当前未配置在线资料服务";
            return;
        }

        var query = string.IsNullOrWhiteSpace(MetadataSearchQuery) ? GameName : MetadataSearchQuery;
        if (string.IsNullOrWhiteSpace(query))
        {
            MetadataStatusText = "请先输入游戏名称";
            return;
        }

        var cancellation = BeginMetadataRequest("正在搜索 Bangumi...");
        try
        {
            MetadataSearchResults.Clear();
            foreach (var result in await metadataProvider.SearchAsync(query, cancellation.Token))
            {
                MetadataSearchResults.Add(result);
            }

            SelectedMetadataResult = null;
            MetadataStatusText = MetadataSearchResults.Count == 0
                ? "没有找到匹配的游戏资料"
                : $"找到 {MetadataSearchResults.Count} 条结果，请选择条目并查看详情";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            MetadataStatusText = "已取消在线资料请求";
        }
        catch (Exception ex)
        {
            MetadataStatusText = $"搜索失败：{ex.Message}";
        }
        finally
        {
            EndMetadataRequest(cancellation);
        }
    }

    private async Task PreviewMetadataAsync()
    {
        if (metadataProvider is null || SelectedMetadataResult is null)
        {
            return;
        }

        var cancellation = BeginMetadataRequest("正在读取条目详情...");
        try
        {
            var metadata = await metadataProvider.GetDetailsAsync(SelectedMetadataResult.SubjectId, cancellation.Token);
            if (metadata is null)
            {
                metadata = CreateMetadataPreviewFromSearchResult(SelectedMetadataResult);
                usedSearchResultMetadataPreview = true;
            }
            if (metadata is null)
            {
                MetadataStatusText = "未能读取该条目的完整资料";
                return;
            }

            MetadataPreview = metadata;
            MetadataStatusText = "已读取完整资料，请确认要导入的字段";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            MetadataStatusText = "已取消在线资料请求";
        }
        catch (Exception ex)
        {
            MetadataStatusText = $"读取详情失败：{ex.Message}";
        }
        finally
        {
            EndMetadataRequest(cancellation);
        }
    }

    private async Task ApplyMetadataAsync()
    {
        if (MetadataPreview is null)
        {
            return;
        }

        var cancellation = BeginMetadataRequest("正在导入游戏资料...");
        try
        {
            var options = CurrentImportOptions();
            var mergedMetadata = options.Merge(ExternalMetadata, MetadataPreview);
            var updatedName = GameName;
            var updatedCover = CoverImagePath;
            string? coverImportWarning = null;
            var importedName = string.IsNullOrWhiteSpace(MetadataPreview.LocalizedName)
                ? MetadataPreview.OriginalName
                : MetadataPreview.LocalizedName;
            if (ImportMetadataName && !string.IsNullOrWhiteSpace(importedName))
            {
                updatedName = importedName;
            }

            if (ImportMetadataCover &&
                remoteImageCacheService is not null &&
                !string.IsNullOrWhiteSpace(MetadataPreview.ImageUrl))
            {
                try
                {
                    var cachedCover = await remoteImageCacheService.DownloadAsync(
                        MetadataPreview.Provider,
                        MetadataPreview.SubjectId,
                        MetadataPreview.ImageUrl,
                        cancellation.Token);
                    if (!string.IsNullOrWhiteSpace(cachedCover))
                    {
                        updatedCover = cachedCover;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    coverImportWarning = $"封面下载失败：{ex.Message}";
                }
            }

            cancellation.Token.ThrowIfCancellationRequested();
            ExternalMetadata = mergedMetadata;
            GameName = updatedName;
            CoverImagePath = updatedCover;
            MetadataStatusText = coverImportWarning is null
                ? "Bangumi 资料已导入，请保存修改。"
                : $"Bangumi 资料已导入，请保存修改。{coverImportWarning}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            MetadataStatusText = "已取消在线资料请求";
        }
        catch (Exception ex)
        {
            MetadataStatusText = $"导入失败：{ex.Message}";
        }
        finally
        {
            EndMetadataRequest(cancellation);
        }
    }

    private void UnlinkMetadata()
    {
        if (ExternalMetadata is null)
        {
            return;
        }

        ExternalMetadata = ExternalMetadata with { IsLinked = false };
        MetadataStatusText = "已解除在线关联，导入的资料快照仍会保留";
    }

    private MetadataImportOptions CurrentImportOptions()
    {
        return new MetadataImportOptions(
            ImportMetadataName,
            ImportMetadataCover,
            ImportMetadataSummary,
            ImportMetadataReleaseDate,
            ImportMetadataDeveloper,
            ImportMetadataPublisher,
            ImportMetadataTags);
    }

    private static ExternalGameMetadata CreateMetadataPreviewFromSearchResult(GameMetadataSearchResult result)
    {
        return new ExternalGameMetadata
        {
            Provider = result.Provider,
            SubjectId = result.SubjectId,
            IsLinked = true,
            OriginalName = result.Name,
            LocalizedName = result.LocalizedName,
            Summary = result.SummaryPreview,
            ReleaseDate = result.ReleaseDate,
            ImageUrl = result.ImageUrl,
            SubjectUrl = $"https://bgm.tv/subject/{Uri.EscapeDataString(result.SubjectId)}",
            SourceUpdatedAtUtc = DateTime.UtcNow
        };
    }

    private CancellationTokenSource BeginMetadataRequest(string status)
    {
        CancelMetadataRequest();
        usedSearchResultMetadataPreview = false;
        var cancellation = new CancellationTokenSource();
        metadataRequestCancellation = cancellation;
        IsMetadataBusy = true;
        MetadataStatusText = status;
        return cancellation;
    }

    private void EndMetadataRequest(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(metadataRequestCancellation, cancellation))
        {
            cancellation.Dispose();
            return;
        }

        metadataRequestCancellation = null;
        cancellation.Dispose();
        IsMetadataBusy = false;
        if (usedSearchResultMetadataPreview && MetadataPreview is not null)
        {
            MetadataStatusText = "未能读取完整资料，已使用搜索结果生成可导入预览";
        }
    }

    private void CancelMetadataRequest()
    {
        metadataRequestCancellation?.Cancel();
    }

    public void Dispose()
    {
        CancelMetadataRequest();
    }
}

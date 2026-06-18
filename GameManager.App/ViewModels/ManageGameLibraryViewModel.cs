using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;
using GameManager.App.Services;

namespace GameManager.App.ViewModels;

public sealed class ManageGameLibraryViewModel : ViewModelBase
{
    private readonly RelayCommand deleteSelectedCommand;
    private readonly AsyncRelayCommand applyMatchedMetadataCommand;
    private readonly IGameLibraryService? gameLibraryService;
    private readonly IGameMetadataProvider? metadataProvider;
    private readonly Action<Game> gameUpdated;
    private string batchMetadataStatusText = string.Empty;

    public ManageGameLibraryViewModel(
        IEnumerable<Game> games,
        Action<IReadOnlyList<Game>> deleteGames,
        Action exitManagement,
        IGameLibraryService? gameLibraryService = null,
        IGameMetadataProvider? metadataProvider = null,
        Action<Game>? gameUpdated = null)
    {
        this.gameLibraryService = gameLibraryService;
        this.metadataProvider = metadataProvider;
        this.gameUpdated = gameUpdated ?? (_ => { });
        Games = new ObservableCollection<ManageGameItemViewModel>(
            games.Select(game => new ManageGameItemViewModel(game)));
        foreach (var game in Games)
        {
            game.PropertyChanged += OnGamePropertyChanged;
        }

        deleteSelectedCommand = new RelayCommand(
            _ => DeleteSelected(deleteGames),
            _ => Games.Any(game => game.IsSelected));
        DeleteSelectedCommand = deleteSelectedCommand;
        MatchUnlinkedMetadataCommand = new AsyncRelayCommand(
            _ => MatchUnlinkedMetadataAsync(),
            _ => this.metadataProvider is not null);
        applyMatchedMetadataCommand = new AsyncRelayCommand(
            _ => ApplyMatchedMetadataAsync(),
            _ => CanApplyMatchedMetadata);
        ApplyMatchedMetadataCommand = applyMatchedMetadataCommand;
        ExitManagementCommand = new RelayCommand(_ => exitManagement());
    }

    public ObservableCollection<ManageGameItemViewModel> Games { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand MatchUnlinkedMetadataCommand { get; }

    public ICommand ApplyMatchedMetadataCommand { get; }

    public ICommand ExitManagementCommand { get; }

    public string BatchMetadataStatusText
    {
        get => batchMetadataStatusText;
        private set => SetProperty(ref batchMetadataStatusText, value);
    }

    public bool HasBatchMetadataSupport => metadataProvider is not null && gameLibraryService is not null;

    private bool CanApplyMatchedMetadata =>
        gameLibraryService is not null &&
        metadataProvider is not null &&
        Games.Any(game => game.IsSelected && game.MetadataMatchResult is not null);

    private async Task MatchUnlinkedMetadataAsync()
    {
        if (metadataProvider is null)
        {
            return;
        }

        var candidates = Games
            .Where(game => game.Game.ExternalMetadata?.IsLinked != true)
            .ToList();
        if (candidates.Count == 0)
        {
            BatchMetadataStatusText = "没有需要匹配的未关联游戏";
            return;
        }

        var matched = 0;
        BatchMetadataStatusText = $"正在匹配 {candidates.Count} 个未关联游戏...";
        foreach (var item in candidates)
        {
            try
            {
                var results = await metadataProvider.SearchAsync(item.Game.Name);
                var result = results.FirstOrDefault();
                item.MetadataMatchResult = result;
                item.MetadataMatchStatusText = result is null
                    ? "未找到候选"
                    : $"待确认：{result.DisplayName}";
                if (result is not null)
                {
                    matched++;
                }
            }
            catch (Exception ex)
            {
                item.MetadataMatchResult = null;
                item.MetadataMatchStatusText = $"匹配失败：{ex.Message}";
            }
        }

        BatchMetadataStatusText = matched == 0
            ? "没有找到可确认的在线资料候选"
            : $"已找到 {matched} 个候选，勾选后应用";
        applyMatchedMetadataCommand.RaiseCanExecuteChanged();
    }

    private async Task ApplyMatchedMetadataAsync()
    {
        if (gameLibraryService is null || metadataProvider is null)
        {
            return;
        }

        var selected = Games
            .Where(game => game.IsSelected && game.MetadataMatchResult is not null)
            .ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var applied = 0;
        BatchMetadataStatusText = $"正在应用 {selected.Count} 个在线资料候选...";
        foreach (var item in selected)
        {
            var result = item.MetadataMatchResult!;
            try
            {
                var metadata = await metadataProvider.GetDetailsAsync(result.SubjectId);
                if (metadata is null)
                {
                    item.MetadataMatchStatusText = "候选详情不可用";
                    continue;
                }

                var updated = gameLibraryService.UpdateExternalMetadata(
                    item.Game.Id,
                    metadata with
                    {
                        Provider = result.Provider,
                        SubjectId = result.SubjectId,
                        IsLinked = true
                    });
                item.Game = updated;
                item.MetadataMatchResult = null;
                item.MetadataMatchStatusText = $"已应用：{result.DisplayName}";
                gameUpdated(updated);
                applied++;
            }
            catch (Exception ex)
            {
                item.MetadataMatchStatusText = $"应用失败：{ex.Message}";
            }
        }

        BatchMetadataStatusText = applied == 0
            ? "没有应用任何在线资料候选"
            : $"已应用 {applied} 个在线资料候选";
        applyMatchedMetadataCommand.RaiseCanExecuteChanged();
    }

    private void DeleteSelected(Action<IReadOnlyList<Game>> deleteGames)
    {
        var selected = Games.Where(game => game.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        deleteGames(selected.Select(game => game.Game).ToList());

        foreach (var game in selected)
        {
            game.PropertyChanged -= OnGamePropertyChanged;
            Games.Remove(game);
        }

        deleteSelectedCommand.RaiseCanExecuteChanged();
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManageGameItemViewModel.IsSelected))
        {
            deleteSelectedCommand.RaiseCanExecuteChanged();
            applyMatchedMetadataCommand.RaiseCanExecuteChanged();
        }

        if (e.PropertyName == nameof(ManageGameItemViewModel.MetadataMatchResult))
        {
            applyMatchedMetadataCommand.RaiseCanExecuteChanged();
        }
    }
}

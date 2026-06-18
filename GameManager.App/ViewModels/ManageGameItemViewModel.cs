using System.Collections.ObjectModel;
using GameManager.App.Models;

namespace GameManager.App.ViewModels;

public sealed class ManageGameItemViewModel : ViewModelBase
{
    private Game game;
    private bool isSelected;
    private GameMetadataSearchResult? selectedMetadataMatchResult;
    private string metadataMatchStatusText = string.Empty;
    private bool isHighConfidenceMatch;

    public ManageGameItemViewModel(Game game)
    {
        this.game = game;
    }

    public Game Game
    {
        get => game;
        set => SetProperty(ref game, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public ObservableCollection<GameMetadataSearchResult> MetadataMatchCandidates { get; } = [];

    public GameMetadataSearchResult? SelectedMetadataMatchResult
    {
        get => selectedMetadataMatchResult;
        set
        {
            if (SetProperty(ref selectedMetadataMatchResult, value))
            {
                OnPropertyChanged(nameof(HasMetadataMatchResult));
                OnPropertyChanged(nameof(MetadataMatchResult));
            }
        }
    }

    public GameMetadataSearchResult? MetadataMatchResult
    {
        get => SelectedMetadataMatchResult;
        set => SelectedMetadataMatchResult = value;
    }

    public bool HasMetadataMatchResult => SelectedMetadataMatchResult is not null;

    public bool IsHighConfidenceMatch
    {
        get => isHighConfidenceMatch;
        set => SetProperty(ref isHighConfidenceMatch, value);
    }

    public string MetadataMatchStatusText
    {
        get => metadataMatchStatusText;
        set => SetProperty(ref metadataMatchStatusText, value);
    }
}

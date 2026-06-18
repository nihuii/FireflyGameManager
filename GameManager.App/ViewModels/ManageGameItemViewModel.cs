using GameManager.App.Models;

namespace GameManager.App.ViewModels;

public sealed class ManageGameItemViewModel : ViewModelBase
{
    private Game game;
    private bool isSelected;
    private GameMetadataSearchResult? metadataMatchResult;
    private string metadataMatchStatusText = string.Empty;

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

    public GameMetadataSearchResult? MetadataMatchResult
    {
        get => metadataMatchResult;
        set
        {
            if (SetProperty(ref metadataMatchResult, value))
            {
                OnPropertyChanged(nameof(HasMetadataMatchResult));
            }
        }
    }

    public bool HasMetadataMatchResult => MetadataMatchResult is not null;

    public string MetadataMatchStatusText
    {
        get => metadataMatchStatusText;
        set => SetProperty(ref metadataMatchStatusText, value);
    }
}

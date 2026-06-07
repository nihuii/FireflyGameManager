using GameManager.App.Models;

namespace GameManager.App.ViewModels;

public sealed class ManageGameItemViewModel : ViewModelBase
{
    private bool isSelected;

    public ManageGameItemViewModel(Game game)
    {
        Game = game;
    }

    public Game Game { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

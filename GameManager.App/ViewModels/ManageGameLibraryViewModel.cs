using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;

namespace GameManager.App.ViewModels;

public sealed class ManageGameLibraryViewModel : ViewModelBase
{
    private readonly RelayCommand deleteSelectedCommand;

    public ManageGameLibraryViewModel(IEnumerable<Game> games, Action<IReadOnlyList<Game>> deleteGames, Action exitManagement)
    {
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
        ExitManagementCommand = new RelayCommand(_ => exitManagement());
    }

    public ObservableCollection<ManageGameItemViewModel> Games { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand ExitManagementCommand { get; }

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
        }
    }
}

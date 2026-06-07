using System.Collections.ObjectModel;
using System.Windows.Input;
using GameManager.App.Commands;
using GameManager.App.Models;

namespace GameManager.App.ViewModels;

public sealed class GameLibraryViewModel : ViewModelBase
{
    private readonly List<Game> manualGames;
    private GameSortMode sortMode = GameSortMode.Manual;
    private double cardWidth = 178;
    private double cardHeight = 244;
    private bool showPlayTimeOnCards;
    private string searchText = string.Empty;

    public GameLibraryViewModel(
        IEnumerable<Game> games,
        Action<Game> openGameDetail,
        Action<Game> deleteGame,
        Action<Game> pinGame,
        Action<Game> editGame)
    {
        manualGames = games.ToList();
        Games = new ObservableCollection<Game>(manualGames);
        OpenGameDetailCommand = new RelayCommand(parameter =>
        {
            if (parameter is Game game)
            {
                openGameDetail(game);
            }
        });
        DeleteGameCommand = new RelayCommand(parameter =>
        {
            if (parameter is Game game)
            {
                deleteGame(game);
            }
        });
        PinGameCommand = new RelayCommand(parameter =>
        {
            if (parameter is Game game)
            {
                pinGame(game);
            }
        });
        EditGameCommand = new RelayCommand(parameter =>
        {
            if (parameter is Game game)
            {
                editGame(game);
            }
        });
    }

    public ObservableCollection<Game> Games { get; }

    public double CardWidth
    {
        get => cardWidth;
        private set => SetProperty(ref cardWidth, value);
    }

    public double CardHeight
    {
        get => cardHeight;
        private set => SetProperty(ref cardHeight, value);
    }

    public bool ShowPlayTimeOnCards
    {
        get => showPlayTimeOnCards;
        private set => SetProperty(ref showPlayTimeOnCards, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                RefreshGames();
            }
        }
    }

    public ICommand OpenGameDetailCommand { get; }

    public ICommand DeleteGameCommand { get; }

    public ICommand PinGameCommand { get; }

    public ICommand EditGameCommand { get; }

    public void AddGame(Game game)
    {
        manualGames.Add(game);
        RefreshGames();
    }

    public void RemoveGame(string id)
    {
        var game = Games.FirstOrDefault(item => item.Id == id);
        if (game is not null)
        {
            manualGames.RemoveAll(item => item.Id == id);
            RefreshGames();
        }
    }

    public void ReplaceGame(Game updatedGame)
    {
        var index = IndexOf(updatedGame.Id);
        if (index >= 0)
        {
            var manualIndex = manualGames.FindIndex(game => game.Id == updatedGame.Id);
            if (manualIndex >= 0)
            {
                manualGames[manualIndex] = updatedGame;
            }

            RefreshGames();
        }
    }

    public void MoveGameToTop(string id)
    {
        var index = manualGames.FindIndex(game => game.Id == id);
        if (index <= 0)
        {
            return;
        }

        var game = manualGames[index];
        manualGames.RemoveAt(index);
        manualGames.Insert(0, game);
        RefreshGames();
    }

    public void ApplySettings(AppSettings settings)
    {
        sortMode = settings.DefaultSort;
        ShowPlayTimeOnCards = settings.ShowPlayTimeOnCards;
        (CardWidth, CardHeight) = settings.CardSize switch
        {
            GameCardSize.Compact => (146d, 200d),
            GameCardSize.Large => (220d, 302d),
            _ => (178d, 244d)
        };
        RefreshGames();
    }

    public void ReloadGames(IEnumerable<Game> games)
    {
        manualGames.Clear();
        manualGames.AddRange(games);
        RefreshGames();
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < Games.Count; i++)
        {
            if (Games[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshGames()
    {
        IEnumerable<Game> ordered = sortMode switch
        {
            GameSortMode.RecentLaunch => manualGames
                .OrderByDescending(game => game.LastLaunchTime ?? DateTime.MinValue),
            GameSortMode.Name => manualGames.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase),
            GameSortMode.PlayTime => manualGames.OrderByDescending(game => game.TotalPlayTime),
            _ => manualGames
        };
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            ordered = ordered.Where(game => game.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        Games.Clear();
        foreach (var game in ordered)
        {
            Games.Add(game);
        }
    }
}

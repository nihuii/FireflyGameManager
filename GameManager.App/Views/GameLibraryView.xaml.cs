using System.Windows.Controls;

namespace GameManager.App.Views;

public partial class GameLibraryView : UserControl
{
    public GameLibraryView()
    {
        InitializeComponent();
    }

    private void MoreButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }
}

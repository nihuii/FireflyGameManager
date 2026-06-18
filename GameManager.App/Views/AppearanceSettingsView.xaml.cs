using System.Windows.Controls;
using GameManager.App.ViewModels;

namespace GameManager.App.Views;

public partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView()
    {
        InitializeComponent();
    }

    private void BangumiAccessTokenBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AppearanceSettingsViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.BangumiAccessToken = passwordBox.Password;
        }
    }
}

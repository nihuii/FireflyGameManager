using System.Windows.Controls;
using GameManager.App.ViewModels;

namespace GameManager.App.Views;

public partial class WebDavSettingsView : UserControl
{
    private bool isUpdatingPassword;

    public WebDavSettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdatePasswordBox();
        Loaded += (_, _) => UpdatePasswordBox();
    }

    private void UpdatePasswordBox()
    {
        if (DataContext is not WebDavSettingsViewModel viewModel ||
            ApplicationPasswordBox.Password == viewModel.ApplicationPassword)
        {
            return;
        }

        isUpdatingPassword = true;
        ApplicationPasswordBox.Password = viewModel.ApplicationPassword;
        isUpdatingPassword = false;
    }

    private void ApplicationPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!isUpdatingPassword && DataContext is WebDavSettingsViewModel viewModel)
        {
            viewModel.ApplicationPassword = ApplicationPasswordBox.Password;
        }
    }
}

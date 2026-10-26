using System.Windows;
using CafeRevenue.Data;

namespace CafeRevenue.Views;
public partial class LoginWindow : Window
{
    private readonly CafeRepository repository = new();
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoginBox.Focus();
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        ErrorText.Text = "Проверка учётной записи…";
        try
        {
            var user = await repository.LoginAsync(LoginBox.Text, PasswordBox.Password);
            if (user is null)
            {
                ErrorText.Text = "Неверный логин или пароль либо учётная запись отключена.";
                return;
            }

            var workspace = new MainWindow(user, repository);
            Application.Current.MainWindow = workspace;
            workspace.Show();
            Close();
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorText.Text = App.ReportError(ex);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}

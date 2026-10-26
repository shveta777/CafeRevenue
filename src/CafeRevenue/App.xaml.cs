using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace CafeRevenue;
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ru-RU")));
        new Views.LoginWindow().Show();
    }

    public static string ReportError(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CafeRevenue");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "errors.log"), $"{DateTime.Now:O} {exception}\n");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return "Не удалось выполнить операцию. Проверьте подключение к SQL Server. Подробности сохранены в журнале приложения.";
    }
}

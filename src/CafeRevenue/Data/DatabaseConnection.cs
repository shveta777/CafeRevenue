using Microsoft.Extensions.Configuration;

namespace CafeRevenue.Data;
public static class DatabaseConnection
{
    public static IConfigurationRoot LoadConfiguration() => new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: false).AddJsonFile("appsettings.Local.json", optional: true).Build();
}

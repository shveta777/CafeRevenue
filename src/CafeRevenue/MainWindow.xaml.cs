using System.Windows;
using CafeRevenue.Data;
using CafeRevenue.Models;
namespace CafeRevenue;
public partial class MainWindow : Window
{
    public MainWindow(UserSession user, CafeRepository repository)
    {
        InitializeComponent();
        Title = $"CafeRevenue — {user.Name}";
    }
}

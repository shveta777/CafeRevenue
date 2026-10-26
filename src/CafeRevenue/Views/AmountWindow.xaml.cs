using System.Windows;
using CafeRevenue.Models;

namespace CafeRevenue.Views;
public partial class AmountWindow : Window
{
    public decimal Amount { get; private set; }

    public AmountWindow(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        Loaded += (_, _) =>
        {
            AmountBox.Focus();
            AmountBox.SelectAll();
        };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Amount = MoneyRules.Parse(AmountBox.Text);
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }
}

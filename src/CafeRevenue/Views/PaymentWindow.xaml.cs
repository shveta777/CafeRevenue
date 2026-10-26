using System.ComponentModel;
using System.Windows;
using CafeRevenue.Data;
using CafeRevenue.Models;

namespace CafeRevenue.Views;
public partial class PaymentWindow : Window
{
    private readonly int userId;
    private readonly OrderDetails order;
    private readonly CafeRepository repository;
    private bool isBusy;
    public PaymentResult? Result { get; private set; }

    public PaymentWindow(int userId, OrderDetails order, CafeRepository repository)
    {
        this.userId = userId;
        this.order = order;
        this.repository = repository;
        InitializeComponent();
        TitleText.Text = $"Заказ №{order.Id} · {order.Total:N2} ₽";
        MethodBox.SelectedIndex = 0;
    }

    private void InputChanged(object sender, RoutedEventArgs e)
    {
        if (PayButton is null || isBusy)
            return;
        bool cash = MethodBox.SelectedIndex == 0;
        CashPanel.Visibility = cash ? Visibility.Visible : Visibility.Collapsed;
        CardCheck.Visibility = cash ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = "";
        if (!cash)
        {
            PayButton.IsEnabled = CardCheck.IsChecked == true;
            return;
        }

        try
        {
            decimal received = MoneyRules.Parse(ReceivedBox.Text);
            ChangeText.Text = received >= order.Total ? $"Сдача: {received - order.Total:N2} ₽" : $"Не хватает: {order.Total - received:N2} ₽";
            PayButton.IsEnabled = received >= order.Total;
        }
        catch (ArgumentException)
        {
            ChangeText.Text = "Введите полученную сумму.";
            PayButton.IsEnabled = false;
        }
    }

    private async void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy)
            return;
        isBusy = true;
        PayButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        MethodBox.IsEnabled = false;
        try
        {
            bool cash = MethodBox.SelectedIndex == 0;
            Result = await repository.PayOrderAsync(userId, order.Id, order.Version, cash ? "Наличные" : "Банковская карта", cash ? MoneyRules.Parse(ReceivedBox.Text) : 0, CardCheck.IsChecked == true);
            isBusy = false;
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            ErrorText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorText.Text = App.ReportError(ex);
        }
        finally
        {
            isBusy = false;
            CancelButton.IsEnabled = true;
            MethodBox.IsEnabled = true;
            PayButton.IsEnabled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (isBusy)
            e.Cancel = true;
    }
}

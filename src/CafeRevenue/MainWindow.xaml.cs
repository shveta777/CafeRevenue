using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CafeRevenue.Data;
using CafeRevenue.Models;

namespace CafeRevenue;
public partial class MainWindow : Window
{
    private readonly UserSession user;
    private readonly CafeRepository repository;
    private readonly ObservableCollection<OrderLine> cart = new();
    private List<MenuDish> menu = new();
    private Shift? shift;
    private OrderDetails? order;
    private bool hasChanges, isBusy, isLoading, isSwitchingUser;
    public MainWindow(UserSession user, CafeRepository repository)
    {
        this.user = user;
        this.repository = repository;
        InitializeComponent();
        CartGrid.ItemsSource = cart;
        UserText.Text = $"{user.Name} | {user.Role}";
        Workspace.Visibility = user.CanTakeOrders ? Visibility.Visible : Visibility.Collapsed;
        AccessText.Visibility = user.CanTakeOrders ? Visibility.Collapsed : Visibility.Visible;
        Loaded += async (_, _) =>
        {
            if (user.CanTakeOrders)
                await RunAsync(async () =>
                {
                    await ReloadAsync();
                    NewDraft();
                });
        };
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (isBusy)
            return;
        isBusy = true;
        Workspace.IsEnabled = false;
        LogoutButton.IsEnabled = false;
        try
        {
            await action();
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = App.ReportError(ex);
        }
        finally
        {
            isBusy = false;
            Workspace.IsEnabled = true;
            LogoutButton.IsEnabled = true;
            UpdateButtons();
        }
    }

    private async Task ReloadAsync()
    {
        isLoading = true;
        try
        {
            shift = await repository.GetOpenShiftAsync(user.Id);
            ShiftText.Text = shift is null ? "Смена закрыта. Откройте смену для оформления заказов." : $"Смена №{shift.Id} · {shift.OpenedAt:dd.MM HH:mm} · размен {shift.OpeningCash:N2} ₽";
            menu = await repository.GetMenuAsync(user.Id);
            string? category = CategoryPicker.SelectedItem as string;
            var categories = new List<string> { "Все категории" };
            foreach (var dish in menu)
            {
                if (!categories.Contains(dish.Category))
                    categories.Add(dish.Category);
            }
            CategoryPicker.ItemsSource = categories;
            CategoryPicker.SelectedItem = category ?? "Все категории";
            var tables = await repository.GetTablesAsync(user.Id);
            int? tableId = (TablePicker.SelectedItem as CafeTable)?.Id;
            TablePicker.ItemsSource = tables;
            TablePicker.SelectedItem = tables.FirstOrDefault(x => x.Id == tableId) ?? tables[0];
            var orders = await repository.GetOrdersAsync(user.Id);
            OrderPicker.ItemsSource = orders;
            OrderPicker.SelectedItem = orders.FirstOrDefault(x => x.Id == order?.Id);
            FilterMenu();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void NewDraft()
    {
        isLoading = true;
        order = null;
        cart.Clear();
        OrderPicker.SelectedIndex = -1;
        TablePicker.SelectedIndex = 0;
        isLoading = false;
        hasChanges = false;
        OrderTitle.Text = "Новый заказ";
        UpdateTotal();
        UpdateButtons();
        StatusText.Text = shift is null ? "Для нового заказа сначала откройте смену." : "Выберите блюдо, количество и нажмите «Добавить».";
    }

    private async Task LoadOrderAsync(int id)
    {
        var loaded = await repository.GetOrderAsync(user.Id, id);
        isLoading = true;
        try
        {
            order = loaded;
            cart.Clear();
            foreach (var line in loaded.Lines)
                cart.Add(line);
            TablePicker.SelectedItem = TablePicker.Items.Cast<CafeTable>().First(x => x.Id == loaded.TableId);
            OrderTitle.Text = $"Заказ №{loaded.Id} · {loaded.Status}";
            hasChanges = false;
            UpdateTotal();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void UpdateButtons()
    {
        bool editable = shift is not null && (order is null || order.Status == "открыт");
        foreach (var button in new[]
        {
            AddButton,
            MinusButton,
            PlusButton,
            RemoveButton,
            SaveOrderButton
        }

        )
            button.IsEnabled = editable;
        TablePicker.IsEnabled = editable;
        PayButton.IsEnabled = editable && order is not null && !hasChanges;
        OpenShiftButton.IsEnabled = shift is null;
        CloseShiftButton.IsEnabled = shift is not null;
        NewOrderButton.IsEnabled = shift is not null;
    }

    private void UpdateTotal()
    {
        TotalText.Text = $"К оплате: {cart.Sum(x => x.Total) - (order?.Discount ?? 0):N2} ₽";
        DiscountText.Text = $"Скидка: {order?.Discount ?? 0:N2} ₽";
    }

    private void MarkChanged()
    {
        hasChanges = true;
        UpdateTotal();
        UpdateButtons();
    }

    private void FilterMenu()
    {
        if (MenuGrid is null || SearchBox is null)
            return;
        string category = CategoryPicker.SelectedItem as string ?? "Все категории";
        string search = SearchBox.Text.Trim();
        var filteredMenu = new List<MenuDish>();
        foreach (var dish in menu)
        {
            bool matchesCategory = category == "Все категории" || dish.Category == category;
            bool matchesSearch = dish.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
            if (matchesCategory && matchesSearch)
                filteredMenu.Add(dish);
        }
        MenuGrid.ItemsSource = filteredMenu;
    }

    private void MenuFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!isLoading)
            FilterMenu();
    }

    private void TablePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!isLoading && IsLoaded)
            MarkChanged();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (MenuGrid.SelectedItem is not MenuDish dish)
        {
            StatusText.Text = "Выберите блюдо в меню.";
            return;
        }

        if (!dish.IsAvailable)
        {
            StatusText.Text = "Блюдо находится в стоп-листе.";
            return;
        }

        if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity < 1 || quantity > 999)
        {
            StatusText.Text = "Количество должно быть целым числом от 1 до 999.";
            return;
        }

        var existing = cart.FirstOrDefault(x => x.MenuItemId == dish.Id);
        if ((existing?.Quantity ?? 0) + quantity > 999)
        {
            StatusText.Text = "Максимальное количество — 999.";
            return;
        }

        if (existing is null)
            cart.Add(new(dish.Id, dish.Name, quantity, dish.Price));
        else
            cart[cart.IndexOf(existing)] = new OrderLine(existing.MenuItemId,
                existing.Name, existing.Quantity + quantity, existing.Price);
        MarkChanged();
        StatusText.Text = "Изменения не сохранены. Нажмите «Сохранить заказ».";
    }

    private void Plus_Click(object sender, RoutedEventArgs e) => ChangeQuantity(1);
    private void Minus_Click(object sender, RoutedEventArgs e) => ChangeQuantity(-1);
    private void ChangeQuantity(int delta)
    {
        if (CartGrid.SelectedItem is not OrderLine line)
            return;
        if (line.Quantity + delta < 1 || line.Quantity + delta > 999)
        {
            StatusText.Text = "Количество — от 1 до 999. Для удаления нажмите «Удалить позицию».";
            return;
        }

        if (delta > 0 && menu.Any(x => x.Id == line.MenuItemId && !x.IsAvailable))
        {
            StatusText.Text = "Нельзя увеличить количество блюда из стоп-листа.";
            return;
        }

        int index = cart.IndexOf(line);
        cart[index] = new OrderLine(line.MenuItemId, line.Name, line.Quantity + delta, line.Price);
        CartGrid.SelectedIndex = index;
        MarkChanged();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (CartGrid.SelectedItem is OrderLine line)
        {
            cart.Remove(line);
            MarkChanged();
        }
    }

    private bool ConfirmDiscard() => !hasChanges || MessageBox.Show(this, "В заказе есть несохранённые изменения. Отменить их?", "CafeRevenue", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void NewOrder_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmDiscard())
            NewDraft();
    }

    private async void OrderPicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || isBusy || OrderPicker.SelectedItem is not OrderHeader header)
            return;
        if (!ConfirmDiscard())
        {
            isLoading = true;
            OrderPicker.SelectedItem = OrderPicker.Items.Cast<OrderHeader>().FirstOrDefault(x => x.Id == order?.Id);
            isLoading = false;
            return;
        }

        await RunAsync(async () =>
        {
            await LoadOrderAsync(header.Id);
            StatusText.Text = "Заказ загружен из базы.";
        });
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
            return;
        await RunAsync(async () =>
        {
            await ReloadAsync();
            if (order is not null)
                await LoadOrderAsync(order.Id);
            else
                NewDraft();
            StatusText.Text = "Данные обновлены.";
        });
    }

    private async void SaveOrder_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            int id = await repository.SaveOrderAsync(user.Id, order?.Id, order?.Version, (TablePicker.SelectedItem as CafeTable)?.Id, cart.Select(x => new OrderLineInput(x.MenuItemId, x.Quantity)).ToList());
            // Отдельно фиксируем успех записи: сбой чтения не должен привести к созданию дубликата.
            hasChanges = false;
            try
            {
                await LoadOrderAsync(id);
                await ReloadAsync();
                StatusText.Text = $"Заказ №{id} сохранён. Можно перейти к оплате.";
            }
            catch (Exception ex)
            {
                NewDraft();
                StatusText.Text = $"Заказ №{id} сохранён. Нажмите «Обновить» и выберите его перед продолжением. " + App.ReportError(ex);
            }
        });
    }

    private async void OpenShift_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.AmountWindow("Открытие смены", "Разменный фонд, ₽")
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;
        await RunAsync(async () =>
        {
            await repository.OpenShiftAsync(user.Id, dialog.Amount);
            await ReloadAsync();
            StatusText.Text = "Смена открыта. Можно создавать заказы.";
        });
    }

    private async void CloseShift_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
            return;
        await RunAsync(async () =>
        {
            var summary = await repository.GetShiftSummaryAsync(user.Id);
            var dialog = new Views.AmountWindow("Закрытие смены", $"Выручка: {summary.Revenue:N2} ₽. Ожидается наличных: {summary.ExpectedCash:N2} ₽.\nФактический остаток, ₽")
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true)
                return;
            decimal difference = await repository.CloseShiftAsync(user.Id, dialog.Amount);
            await ReloadAsync();
            NewDraft();
            StatusText.Text = $"Смена закрыта. Расхождение наличных: {difference:N2} ₽.";
        });
    }

    private async void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (order is null || hasChanges)
        {
            StatusText.Text = "Перед оплатой сохраните заказ.";
            return;
        }

        var dialog = new Views.PaymentWindow(user.Id, order, repository)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;
        await RunAsync(async () =>
        {
            await LoadOrderAsync(order.Id);
            await ReloadAsync();
            StatusText.Text = $"Заказ оплачен. Сдача: {dialog.Result!.Change:N2} ₽.";
        });
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy || !ConfirmDiscard())
            return;
        isSwitchingUser = true;
        var login = new Views.LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (isBusy || (!isSwitchingUser && !ConfirmDiscard()))
            e.Cancel = true;
    }
}

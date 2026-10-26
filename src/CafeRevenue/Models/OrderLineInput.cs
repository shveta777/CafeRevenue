namespace CafeRevenue.Models;

public class OrderLineInput
{
    public int MenuItemId { get; }
    public int Quantity { get; }

    public OrderLineInput(int menuItemId, int quantity)
    {
        MenuItemId = menuItemId;
        Quantity = quantity;
    }
}

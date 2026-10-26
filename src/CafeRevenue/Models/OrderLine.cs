using CafeRevenue.Services;

namespace CafeRevenue.Models;

public class OrderLine
{
    public int MenuItemId { get; }
    public string Name { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public decimal Total => Quantity * Price;

    public OrderLine(int menuItemId, string name, int quantity, decimal price)
    {
        if (quantity < 1 || quantity > 999)
            throw new ArgumentException("Количество должно быть от 1 до 999.");
        MoneyRules.Validate(price, true);
        MenuItemId = menuItemId;
        Name = name;
        Quantity = quantity;
        Price = price;
    }

    public OrderLine WithQuantity(int quantity)
    {
        return new OrderLine(MenuItemId, Name, quantity, Price);
    }
}

namespace CafeRevenue.Models;

public class MenuDish
{
    public int Id { get; }
    public string Name { get; }
    public string Category { get; }
    public decimal Price { get; }
    public bool IsAvailable { get; }

    public string Availability => IsAvailable ? "В продаже" : "Стоп-лист";

    public MenuDish(int id, string name, string category, decimal price, bool isAvailable)
    {
        Id = id;
        Name = name;
        Category = category;
        Price = price;
        IsAvailable = isAvailable;
    }
}

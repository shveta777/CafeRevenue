using System.Globalization;

namespace CafeRevenue.Models;
public sealed record UserSession(int Id, string Login, string Name, string Role)
{
    public bool CanTakeOrders => Role == "Кассир" || Role == "Администратор";
}

public sealed record MenuDish(int Id, string Name, string Category, decimal Price, bool IsAvailable)
{
    public string Availability => IsAvailable ? "В продаже" : "Стоп-лист";
}

public sealed record CafeTable(int? Id, string Name);
public sealed record Shift(int Id, DateTime OpenedAt, decimal OpeningCash);
public sealed record OrderHeader(int Id, DateTime CreatedAt, string Status, decimal Total)
{
    public string Display => $"№{Id} · {CreatedAt:dd.MM HH:mm} · {Status} · {Total:N2} ₽";
}

public sealed record OrderLine(int MenuItemId, string Name, int Quantity, decimal Price)
{
    public decimal Total => Quantity * Price;
}

public sealed record OrderDetails(int Id, int? TableId, string Status, byte[] Version, decimal Discount, List<OrderLine> Lines)
{
    public decimal Total => Lines.Sum(x => x.Total) - Discount;
}

public sealed record OrderLineInput(int MenuItemId, int Quantity);
public sealed record PaymentResult(decimal Total, decimal Received, decimal Change);
public sealed record ShiftSummary(decimal Revenue, decimal ExpectedCash);
public static class MoneyRules
{
    public const decimal Maximum = 99_999_999.99m;
    public static decimal Parse(string text)
    {
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal value))
            throw new ArgumentException("Введите сумму числом, например 1200,00.");
        Validate(value, true);
        return value;
    }

    public static void Validate(decimal value, bool allowZero = false)
    {
        if (value < 0 || (!allowZero && value == 0) || value > Maximum || decimal.Round(value, 2) != value)
            throw new ArgumentException("Укажите допустимую сумму с точностью до двух знаков после запятой.");
    }
}

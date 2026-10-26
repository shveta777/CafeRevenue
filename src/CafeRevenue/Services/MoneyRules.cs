using System.Globalization;

namespace CafeRevenue.Services;

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

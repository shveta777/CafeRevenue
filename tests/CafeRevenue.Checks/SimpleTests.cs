using CafeRevenue.Models;
using CafeRevenue.Services;
using Xunit;

namespace CafeRevenue.Checks;

public class SimpleTests
{
    [Fact(DisplayName = "B01. Стоимость позиции")]
    public void LineTotal()
    {
        var line = new OrderLine(1, "Капучино", 2, 250.25m);
        Assert.Equal(500.50m, line.Total);
    }

    [Fact(DisplayName = "B02. Изменение количества")]
    public void ChangeQuantity()
    {
        var line = new OrderLine(1, "Капучино", 2, 250m);
        var changed = line.WithQuantity(3);
        Assert.Equal(3, changed.Quantity);
        Assert.Equal(750m, changed.Total);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(250m, changed.Price);
    }

    [Fact(DisplayName = "B03. Нулевое количество")]
    public void ZeroQuantity()
    {
        Assert.Throws<ArgumentException>(() =>
            new OrderLine(1, "Капучино", 0, 250m));
    }

    [Fact(DisplayName = "B04. Слишком большое количество")]
    public void LargeQuantity()
    {
        Assert.Throws<ArgumentException>(() =>
            new OrderLine(1, "Капучино", 1000, 250m));
    }

    [Fact(DisplayName = "B05. Отрицательная цена")]
    public void NegativePrice()
    {
        Assert.Throws<ArgumentException>(() =>
            new OrderLine(1, "Капучино", 1, -1m));
    }

    [Fact(DisplayName = "B06. Сумма заказа")]
    public void OrderTotal()
    {
        var lines = new List<OrderLine>();
        lines.Add(new OrderLine(1, "Капучино", 2, 250m));
        lines.Add(new OrderLine(2, "Паста", 1, 340m));
        var order = new OrderDetails(1, null, "открыт", new byte[8], 0m, lines);
        Assert.Equal(840m, order.Total);
    }

    [Fact(DisplayName = "B07. Сумма заказа со скидкой")]
    public void OrderDiscount()
    {
        var lines = new List<OrderLine>();
        lines.Add(new OrderLine(1, "Капучино", 2, 250m));
        lines.Add(new OrderLine(2, "Паста", 1, 340m));
        var order = new OrderDetails(1, null, "открыт", new byte[8], 100m, lines);
        Assert.Equal(740m, order.Total);
    }

    [Fact(DisplayName = "B08. Сумма с запятой")]
    public void MoneyWithComma()
    {
        Assert.Equal(1200.50m, MoneyRules.Parse("1200,50"));
    }

    [Fact(DisplayName = "B09. Сумма с точкой")]
    public void MoneyWithDot()
    {
        Assert.Equal(1200.50m, MoneyRules.Parse("1200.50"));
    }

    [Fact(DisplayName = "B10. Отрицательная сумма")]
    public void NegativeMoney()
    {
        Assert.Throws<ArgumentException>(() => MoneyRules.Parse("-1"));
    }

    [Fact(DisplayName = "B11. Текст вместо суммы")]
    public void TextInsteadOfMoney()
    {
        Assert.Throws<ArgumentException>(() => MoneyRules.Parse("abc"));
    }

    [Fact(DisplayName = "B12. Лишние знаки после запятой")]
    public void TooManyDecimals()
    {
        Assert.Throws<ArgumentException>(() => MoneyRules.Parse("12,345"));
    }

    [Fact(DisplayName = "B13. Максимальная сумма")]
    public void MaximumMoney()
    {
        Assert.Equal(99999999.99m, MoneyRules.Parse("99999999,99"));
    }

    [Fact(DisplayName = "B14. Превышение максимальной суммы")]
    public void TooMuchMoney()
    {
        Assert.Throws<ArgumentException>(() => MoneyRules.Parse("100000000"));
    }
}

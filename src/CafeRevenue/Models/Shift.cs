namespace CafeRevenue.Models;

public class Shift
{
    public int Id { get; }
    public DateTime OpenedAt { get; }
    public decimal OpeningCash { get; }

    public Shift(int id, DateTime openedAt, decimal openingCash)
    {
        Id = id;
        OpenedAt = openedAt;
        OpeningCash = openingCash;
    }
}

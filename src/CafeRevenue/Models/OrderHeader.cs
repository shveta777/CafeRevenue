namespace CafeRevenue.Models;

public class OrderHeader
{
    public int Id { get; }
    public DateTime CreatedAt { get; }
    public string Status { get; }
    public decimal Total { get; }

    public string Display => $"№{Id} · {CreatedAt:dd.MM HH:mm} · {Status} · {Total:N2} ₽";

    public OrderHeader(int id, DateTime createdAt, string status, decimal total)
    {
        Id = id;
        CreatedAt = createdAt;
        Status = status;
        Total = total;
    }
}

namespace CafeRevenue.Models;

public class OrderDetails
{
    private readonly byte[] version;

    public int Id { get; }
    public int? TableId { get; }
    public string Status { get; }
    public byte[] Version => (byte[])version.Clone();
    public decimal Discount { get; }
    public IReadOnlyList<OrderLine> Lines { get; }
    public decimal Total => Lines.Sum(line => line.Total) - Discount;

    public OrderDetails(int id, int? tableId, string status, byte[] version,
        decimal discount, IEnumerable<OrderLine> lines)
    {
        Id = id;
        TableId = tableId;
        Status = status;
        this.version = (byte[])version.Clone();
        Discount = discount;
        Lines = Array.AsReadOnly(lines.ToArray());
    }
}

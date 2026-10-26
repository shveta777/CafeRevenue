namespace CafeRevenue.Models;

public class PaymentResult
{
    public decimal Total { get; }
    public decimal Received { get; }
    public decimal Change { get; }

    public PaymentResult(decimal total, decimal received, decimal change)
    {
        Total = total;
        Received = received;
        Change = change;
    }
}

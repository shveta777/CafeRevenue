namespace CafeRevenue.Models;

public class ShiftSummary
{
    public decimal Revenue { get; }
    public decimal ExpectedCash { get; }

    public ShiftSummary(decimal revenue, decimal expectedCash)
    {
        Revenue = revenue;
        ExpectedCash = expectedCash;
    }
}

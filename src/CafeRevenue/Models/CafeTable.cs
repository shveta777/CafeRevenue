namespace CafeRevenue.Models;

public class CafeTable
{
    public int? Id { get; }
    public string Name { get; }

    public CafeTable(int? id, string name)
    {
        Id = id;
        Name = name;
    }
}

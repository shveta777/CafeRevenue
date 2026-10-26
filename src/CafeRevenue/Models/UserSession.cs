namespace CafeRevenue.Models;

public class UserSession
{
    public int Id { get; }
    public string Login { get; }
    public string Name { get; }
    public string Role { get; }

    public bool CanTakeOrders => Role == "Кассир" || Role == "Администратор";

    public UserSession(int id, string login, string name, string role)
    {
        Id = id;
        Login = login;
        Name = name;
        Role = role;
    }
}

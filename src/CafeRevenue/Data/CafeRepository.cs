using CafeRevenue.Services;
using System.Data;
using System.Security.Cryptography;
using CafeRevenue.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CafeRevenue.Data;
public class CafeRepository
{
    public const int PasswordIterations = 100_000;
    private readonly string connectionString;
    public CafeRepository(string? connectionString = null)
    {
        if (connectionString != null)
        {
            this.connectionString = connectionString;
        }
        else
        {
            var settings = DatabaseConnection.LoadConfiguration();
            this.connectionString = settings.GetConnectionString("CafeRevenueDb")
                ?? throw new InvalidOperationException("Не задана строка CafeRevenueDb.");
        }
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync();
            using var settings = new SqlCommand("SET ARITHABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;", connection);
            await settings.ExecuteNonQueryAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<UserSession?> LoginAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Введите логин и пароль.");
        await using var connection = await OpenAsync();
        using var command = new SqlCommand("""
            SELECT u.Id,u.Login,COALESCE(e.LastName + N' ' + e.FirstName,u.Login),
                   r.Name,u.PasswordHash,u.PasswordSalt
            FROM dbo.Users u JOIN dbo.Roles r ON r.Id=u.RoleId
            LEFT JOIN dbo.Employees e ON e.Id=u.EmployeeId
            WHERE u.Login=@login AND u.IsActive=1
            """, connection);
        command.Parameters.Add("@login", SqlDbType.NVarChar, 50).Value = login.Trim();
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, (byte[])reader[5], PasswordIterations, HashAlgorithmName.SHA256, 32);
        if (!CryptographicOperations.FixedTimeEquals(actual, (byte[])reader[4]))
            return null;
        return new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            SqlDbType type = SqlDbType.Int;
            if (value is decimal)
                type = SqlDbType.Decimal;
            else if (value is byte[])
                type = SqlDbType.Binary;
            else if (value is string)
                type = SqlDbType.NVarChar;
            var parameter = command.Parameters.Add(name, type);
            if (value is decimal)
            {
                parameter.Precision = 10;
                parameter.Scale = 2;
            }

            if (value is byte[])
                parameter.Size = 8;
            if (value is string)
                parameter.Size = 50;
            parameter.Value = value ?? DBNull.Value;
        }

        return command;
    }

    private static async Task RequireOrderAccessAsync(SqlConnection connection, SqlTransaction? transaction, int userId)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Roles r ON r.Id=u.RoleId
            WHERE u.Id=@user AND u.IsActive=1 AND r.Name IN (N'Кассир',N'Администратор')
            """, ("@user", userId));
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new UnauthorizedAccessException("Работа с заказами доступна кассиру и администратору.");
    }

    public async Task<Shift?> GetOpenShiftAsync(int userId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        return await ReadShiftAsync(connection, null, userId);
    }

    private static async Task<Shift?> ReadShiftAsync(SqlConnection connection, SqlTransaction? transaction, int userId)
    {
        using var command = CreateCommand(connection, transaction, "SELECT Id,OpenedAt,OpeningCash FROM dbo.Shifts WITH (UPDLOCK,HOLDLOCK) WHERE UserId=@user AND Status=N'открыта'", ("@user", userId));
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new(reader.GetInt32(0), reader.GetDateTime(1), reader.GetDecimal(2)) : null;
    }

    public async Task<int> OpenShiftAsync(int userId, decimal openingCash)
    {
        MoneyRules.Validate(openingCash, true);
        await using var connection = await OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await RequireOrderAccessAsync(connection, transaction, userId);
            if (await ReadShiftAsync(connection, transaction, userId) is not null)
                throw new ArgumentException("У вас уже есть открытая смена.");
            using var command = CreateCommand(connection, transaction, "INSERT dbo.Shifts(UserId,OpenedAt,OpeningCash,Status) OUTPUT INSERTED.Id VALUES(@user,SYSDATETIME(),@cash,N'открыта')", ("@user", userId), ("@cash", openingCash));
            int shiftId = Convert.ToInt32(await command.ExecuteScalarAsync());
            await transaction.CommitAsync();
            return shiftId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ShiftSummary> GetShiftSummaryAsync(int userId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        using var command = CreateCommand(connection, null, "SELECT Revenue,ExpectedCash FROM dbo.vShiftSummary WHERE UserId=@user AND Status=N'открыта'", ("@user", userId));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new ArgumentException("Нет открытой смены.");
        return new(reader.GetDecimal(0), reader.GetDecimal(1));
    }

    public async Task<decimal> CloseShiftAsync(int userId, decimal actualCash)
    {
        MoneyRules.Validate(actualCash, true);
        await using var connection = await OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await RequireOrderAccessAsync(connection, transaction, userId);
            var shift = await ReadShiftAsync(connection, transaction, userId) ?? throw new ArgumentException("Нет открытой смены.");
            using var check = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE ShiftId=@shift AND Status=N'открыт'", ("@shift", shift.Id));
            if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0)
                throw new ArgumentException("Сначала оплатите открытые заказы этой смены.");
            using var total = CreateCommand(connection, transaction, "SELECT ExpectedCash FROM dbo.vShiftSummary WHERE ShiftId=@shift", ("@shift", shift.Id));
            decimal expected = Convert.ToDecimal(await total.ExecuteScalarAsync());
            using var close = CreateCommand(connection, transaction, "UPDATE dbo.Shifts SET ClosedAt=SYSDATETIME(),ClosingCash=@cash,Status=N'закрыта' WHERE Id=@shift", ("@cash", actualCash), ("@shift", shift.Id));
            await close.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return actualCash - expected;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<MenuDish>> GetMenuAsync(int userId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        using var command = CreateCommand(connection, null, """
            SELECT m.Id,m.Name,c.Name,
              CASE WHEN m.IsPromoted=1 AND m.PromoUntil>=SYSDATETIME() AND m.PromoPrice IS NOT NULL
                   THEN m.PromoPrice ELSE m.Price END,
              CONVERT(bit,CASE WHEN m.IsAvailable=1 AND NOT EXISTS
                (SELECT 1 FROM dbo.StopList s WHERE s.MenuItemId=m.Id AND s.ResumedAt IS NULL) THEN 1 ELSE 0 END)
            FROM dbo.MenuItems m JOIN dbo.Categories c ON c.Id=m.CategoryId
            ORDER BY c.SortOrder,m.Name
            """);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<MenuDish>();
        while (await reader.ReadAsync())
            rows.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4)));
        return rows;
    }

    public async Task<List<CafeTable>> GetTablesAsync(int userId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        using var command = CreateCommand(connection, null, "SELECT Id,Number,Seats FROM dbo.Tables ORDER BY Number");
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<CafeTable>
        {
            new(null, "Без столика")
        };
        while (await reader.ReadAsync())
            rows.Add(new(reader.GetInt32(0), $"Столик {reader.GetInt32(1)} · {reader.GetInt32(2)} мест"));
        return rows;
    }

    public async Task<List<OrderHeader>> GetOrdersAsync(int userId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        using var command = CreateCommand(connection, null, """
            SELECT TOP (100) o.Id,o.CreatedAt,o.Status,v.TotalAmount FROM dbo.Orders o
            JOIN dbo.vOrderTotals v ON v.OrderId=o.Id WHERE o.UserId=@user ORDER BY o.Id DESC
            """, ("@user", userId));
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<OrderHeader>();
        while (await reader.ReadAsync())
            rows.Add(new(reader.GetInt32(0), reader.GetDateTime(1), reader.GetString(2), reader.GetDecimal(3)));
        return rows;
    }

    public async Task<OrderDetails> GetOrderAsync(int userId, int orderId)
    {
        await using var connection = await OpenAsync();
        await RequireOrderAccessAsync(connection, null, userId);
        // Единый снимок заголовка и состава защищает от чтения половины сохранения.
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await ReadOrderAsync(connection, transaction, userId, orderId);
        await transaction.CommitAsync();
        return result;
    }

    private static async Task<OrderDetails> ReadOrderAsync(SqlConnection connection, SqlTransaction transaction, int userId, int id)
    {
        using var command = CreateCommand(connection, transaction, "SELECT TableId,Status,Version,COALESCE(DiscountValueAtMoment,0) FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND UserId=@user", ("@id", id), ("@user", userId));
        int? table;
        string status;
        byte[] version;
        decimal discount;
        await using (var orderReader = await command.ExecuteReaderAsync())
        {
            if (!await orderReader.ReadAsync())
                throw new ArgumentException("Заказ не найден или принадлежит другому пользователю.");
            table = orderReader.IsDBNull(0) ? null : orderReader.GetInt32(0);
            status = orderReader.GetString(1);
            version = (byte[])orderReader[2];
            discount = orderReader.GetDecimal(3);
        }

        using var items = CreateCommand(connection, transaction, "SELECT i.MenuItemId,m.Name,i.Quantity,i.PriceAtMoment FROM dbo.OrderItems i JOIN dbo.MenuItems m ON m.Id=i.MenuItemId WHERE i.OrderId=@id ORDER BY i.Id", ("@id", id));
        await using var reader = await items.ExecuteReaderAsync();
        var lines = new List<OrderLine>();
        while (await reader.ReadAsync())
            lines.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3)));
        return new(id, table, status, version, discount, lines);
    }

    public async Task<int> SaveOrderAsync(int userId, int? orderId, byte[]? version, int? tableId, IReadOnlyList<OrderLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0 || lines.Count > 100 || lines.Any(x => x.Quantity < 1 || x.Quantity > 999) || lines.Select(x => x.MenuItemId).Distinct().Count() != lines.Count)
            throw new ArgumentException("Добавьте от 1 до 100 разных блюд. Количество — от 1 до 999.");
        await using var connection = await OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await RequireOrderAccessAsync(connection, transaction, userId);
            var shift = await ReadShiftAsync(connection, transaction, userId) ?? throw new ArgumentException("Для сохранения заказа откройте смену.");
            OrderDetails? previous = null;
            int id;
            if (orderId is int existing)
            {
                previous = await ReadOrderAsync(connection, transaction, userId, existing);
                if (previous.Status != "открыт")
                    throw new ArgumentException("Оплаченный заказ нельзя изменять.");
                if (version is null || !version.SequenceEqual(previous.Version))
                    throw new ArgumentException("Заказ изменён в другом окне. Откройте его заново.");
                using var update = CreateCommand(connection, transaction, "UPDATE dbo.Orders SET TableId=@table WHERE Id=@id AND ShiftId=@shift AND (ReservationId IS NULL OR TableId=@table)", ("@table", tableId), ("@id", existing), ("@shift", shift.Id));
                if (await update.ExecuteNonQueryAsync() != 1)
                    throw new ArgumentException("Нельзя изменить смену или столик заказа, связанного с бронью.");
                id = existing;
            }
            else
            {
                using var create = CreateCommand(connection, transaction, "INSERT dbo.Orders(ShiftId,TableId,UserId,DiscountValueAtMoment,CreatedAt,Status) OUTPUT INSERTED.Id VALUES(@shift,@table,@user,0,SYSDATETIME(),N'открыт')", ("@shift", shift.Id), ("@table", tableId), ("@user", userId));
                id = Convert.ToInt32(await create.ExecuteScalarAsync());
            }

            var stored = new List<OrderLine>();
            foreach (var line in lines)
            {
                using var menu = CreateCommand(connection, transaction, """
                    SELECT CASE WHEN IsPromoted=1 AND PromoUntil>=SYSDATETIME() AND PromoPrice IS NOT NULL
                        THEN PromoPrice ELSE Price END,
                      CONVERT(bit,CASE WHEN IsAvailable=1 AND NOT EXISTS(SELECT 1 FROM dbo.StopList s
                        WHERE s.MenuItemId=m.Id AND s.ResumedAt IS NULL) THEN 1 ELSE 0 END), m.Name
                    FROM dbo.MenuItems m WITH (HOLDLOCK) WHERE Id=@menu
                    """, ("@menu", line.MenuItemId));
                await using var reader = await menu.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new ArgumentException("Выбранное блюдо не найдено в меню.");
                var old = previous?.Lines.SingleOrDefault(x => x.MenuItemId == line.MenuItemId);
                if (!reader.GetBoolean(1) && (old is null || line.Quantity > old.Quantity))
                    throw new ArgumentException("Блюдо находится в стоп-листе: добавление и увеличение количества запрещены.");
                decimal price = old?.Price ?? reader.GetDecimal(0);
                stored.Add(new OrderLine(line.MenuItemId, reader.GetString(2), line.Quantity, price));
            }

            decimal total = stored.Sum(x => x.Quantity * x.Price) - (previous?.Discount ?? 0);
            MoneyRules.Validate(total);
            using var delete = CreateCommand(connection, transaction, "DELETE dbo.OrderItems WHERE OrderId=@id", ("@id", id));
            await delete.ExecuteNonQueryAsync();
            foreach (var line in stored)
            {
                // LineTotal заполняется SQL Server, цена ранее добавленных блюд сохраняется.
                using var insert = CreateCommand(connection, transaction, "INSERT dbo.OrderItems(OrderId,MenuItemId,Quantity,PriceAtMoment) VALUES(@id,@menu,@qty,@price)", ("@id", id), ("@menu", line.MenuItemId), ("@qty", line.Quantity), ("@price", line.Price));
                await insert.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PaymentResult> PayOrderAsync(int userId, int orderId, byte[] version, string method, decimal received, bool cardConfirmed)
    {
        if (method != "Наличные" && method != "Банковская карта")
            throw new ArgumentException("Выберите наличные или банковскую карту.");
        if (method == "Наличные")
            MoneyRules.Validate(received);
        if (method == "Банковская карта" && !cardConfirmed)
            throw new ArgumentException("Подтвердите успешную оплату на внешнем терминале.");
        await using var connection = await OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await RequireOrderAccessAsync(connection, transaction, userId);
            var shift = await ReadShiftAsync(connection, transaction, userId) ?? throw new ArgumentException("Нет открытой смены.");
            var order = await ReadOrderAsync(connection, transaction, userId, orderId);
            if (order.Status != "открыт")
                throw new ArgumentException("Заказ уже оплачен или закрыт.");
            if (!version.SequenceEqual(order.Version))
                throw new ArgumentException("Состав заказа изменён. Откройте его заново перед оплатой.");
            using var belongs = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM dbo.Orders WHERE Id=@id AND ShiftId=@shift", ("@id", orderId), ("@shift", shift.Id));
            if (Convert.ToInt32(await belongs.ExecuteScalarAsync()) != 1)
                throw new ArgumentException("Заказ относится к другой смене.");
            using var totalCmd = CreateCommand(connection, transaction, "SELECT TotalAmount FROM dbo.vOrderTotals WHERE OrderId=@id", ("@id", orderId));
            decimal total = Convert.ToDecimal(await totalCmd.ExecuteScalarAsync());
            MoneyRules.Validate(total);
            if (method == "Наличные" && received < total)
                throw new ArgumentException($"Недостаточно наличных. Не хватает {total - received:N2} ₽.");
            using var duplicate = CreateCommand(connection, transaction, "SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=@id", ("@id", orderId));
            if (Convert.ToInt32(await duplicate.ExecuteScalarAsync()) != 0)
                throw new ArgumentException("Оплата этого заказа уже зарегистрирована.");
            using var pay = CreateCommand(connection, transaction, """
                INSERT dbo.Payments(OrderId,PaymentMethodId,Amount,PaidAt)
                SELECT @id,Id,@amount,SYSDATETIME() FROM dbo.PaymentMethods WHERE Name=@method
                """, ("@id", orderId), ("@amount", total), ("@method", method));
            if (await pay.ExecuteNonQueryAsync() != 1)
                throw new ArgumentException("Способ оплаты не найден.");
            using var close = CreateCommand(connection, transaction, "UPDATE dbo.Orders SET Status=N'оплачен',ClosedAt=SYSDATETIME() WHERE Id=@id", ("@id", orderId));
            await close.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return new PaymentResult(total, method == "Наличные" ? received : total, method == "Наличные" ? received - total : 0);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

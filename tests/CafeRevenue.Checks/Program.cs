using CafeRevenue.Services;
using System.Data;
using System.Security.Cryptography;
using CafeRevenue.Data;
using CafeRevenue.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

int passed=0;
void Check(bool condition,string name)
{
    if(!condition)throw new Exception("FAIL: "+name);
    Console.WriteLine("PASS: "+name);passed++;
}
async Task Reject(Func<Task> action,Type type,string name)
{
    try{await action();}
    catch(Exception ex)when(type.IsInstanceOfType(ex)){Check(true,name);return;}
    throw new Exception("FAIL: "+name);
}
var modelLine = new OrderLine(1, "Проверка", 2, 100.25m);
var changedLine = modelLine.WithQuantity(3);
Check(modelLine.Quantity == 2 && changedLine.Quantity == 3 && changedLine.Total == 300.75m,
    "Quantity change preserves the original position and recalculates total");
await Reject(() => { modelLine.WithQuantity(0); return Task.CompletedTask; },
    typeof(ArgumentException), "Model rejects invalid quantity");
await Reject(() => { new OrderLine(1, "Проверка", 1, -1); return Task.CompletedTask; },
    typeof(ArgumentException), "Model rejects negative price");
var modelVersion = new byte[] { 1, 2, 3 };
var modelLines = new List<OrderLine> { modelLine };
var modelOrder = new OrderDetails(1, null, "открыт", modelVersion, 0.50m, modelLines);
modelLines.Clear();
modelVersion[0] = 9;
var exposedVersion = modelOrder.Version;
exposedVersion[0] = 8;
Check(modelOrder.Lines.Count == 1 && modelOrder.Total == 200m,
    "Order protects its positions from changes to the source collection");
Check(modelOrder.Version[0] == 1,
    "Order protects its concurrency version from external changes");
await Reject(() => { ((IList<OrderLine>)modelOrder.Lines).Clear(); return Task.CompletedTask; },
    typeof(NotSupportedException), "Order positions cannot be changed through the exposed collection");
string cs=DatabaseConnection.LoadConfiguration().GetConnectionString("CafeRevenueDb")!;
var repository=new CafeRepository();
string marker="check_"+Guid.NewGuid().ToString("N");
int userId=0,dishA=0,dishB=0;
async Task<object?> Sql(string text,params (string,object)[] parameters)
{
    using var c=new SqlConnection(cs);await c.OpenAsync();
    using(var settings=new SqlCommand("SET ARITHABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;",c))await settings.ExecuteNonQueryAsync();
    using var cmd=new SqlCommand(text,c);
    foreach(var (name,value)in parameters)cmd.Parameters.AddWithValue(name,value);
    return await cmd.ExecuteScalarAsync();
}
try
{
    byte[] salt=RandomNumberGenerator.GetBytes(16);
    byte[] hash=Rfc2898DeriveBytes.Pbkdf2("Cafe2026!",salt,CafeRepository.PasswordIterations,HashAlgorithmName.SHA256,32);
    userId=Convert.ToInt32(await Sql("INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive) OUTPUT INSERTED.Id SELECT @login,@hash,@salt,Id,1 FROM dbo.Roles WHERE Name=N'Кассир'",("@login",marker),("@hash",hash),("@salt",salt)));
    dishA=Convert.ToInt32(await Sql("INSERT dbo.MenuItems(Name,CategoryId,Price,Cost,IsAvailable,IsPromoted) OUTPUT INSERTED.Id SELECT @name,MIN(Id),250.25,50,1,0 FROM dbo.Categories",("@name",marker+"_A")));
    dishB=Convert.ToInt32(await Sql("INSERT dbo.MenuItems(Name,CategoryId,Price,Cost,IsAvailable,IsPromoted) OUTPUT INSERTED.Id SELECT @name,MIN(Id),100.10,30,1,0 FROM dbo.Categories",("@name",marker+"_B")));
    Check((await repository.LoginAsync(marker,"Cafe2026!"))?.CanTakeOrders==true,"Cashier login");
    Check(await repository.LoginAsync(marker,"wrong") is null,"Wrong password denied");
    Check(await repository.LoginAsync("' OR 1=1--","wrong") is null,"Parameterized login");
    Check(MoneyRules.Parse("1200,50")==1200.50m && MoneyRules.Parse("1200.50")==1200.50m,"Decimal separators");
    var manager=await repository.LoginAsync("manager","Cafe2026!") ?? throw new Exception("Missing manager");
    await Reject(()=>repository.GetMenuAsync(manager.Id),typeof(UnauthorizedAccessException),"Manager cannot take orders");
    var lines=new[]{new OrderLineInput(dishA,2)};
    await Reject(()=>repository.SaveOrderAsync(userId,null,null,null,lines),typeof(ArgumentException),"Order requires open shift");
    await Reject(()=>repository.OpenShiftAsync(userId,-1),typeof(ArgumentException),"Negative opening cash denied");
    await repository.OpenShiftAsync(userId,2000m);
    Check(await repository.GetOpenShiftAsync(userId) is not null,"Shift opened");
    await Reject(()=>repository.OpenShiftAsync(userId,0),typeof(ArgumentException),"Duplicate open shift denied");
    await Reject(()=>repository.SaveOrderAsync(userId,null,null,null,Array.Empty<OrderLineInput>()),typeof(ArgumentException),"Empty order denied");
    await Reject(()=>repository.SaveOrderAsync(userId,null,null,null,new[]{new OrderLineInput(dishA,0)}),typeof(ArgumentException),"Zero quantity denied");
    int countBefore=Convert.ToInt32(await Sql("SELECT COUNT(*) FROM dbo.Orders WHERE UserId=@id",("@id",userId)));
    await Sql("UPDATE dbo.MenuItems SET IsAvailable=0 WHERE Id=@id",("@id",dishB));
    await Reject(()=>repository.SaveOrderAsync(userId,null,null,null,new[]{new OrderLineInput(dishA,1),new OrderLineInput(dishB,1)}),typeof(ArgumentException),"Stop-list blocks new items");
    Check(Convert.ToInt32(await Sql("SELECT COUNT(*) FROM dbo.Orders WHERE UserId=@id",("@id",userId)))==countBefore,"Failed save rolls back order header and items");
    await Sql("UPDATE dbo.MenuItems SET IsAvailable=1 WHERE Id=@id",("@id",dishB));
    int id=await repository.SaveOrderAsync(userId,null,null,null,lines);
    var original=await repository.GetOrderAsync(userId,id);
    Check(original.Total==500.50m,"Order persisted with correct total");
    Check(Convert.ToDecimal(await Sql("SELECT LineTotal FROM dbo.OrderItems WHERE OrderId=@id",("@id",id)))==500.50m,"SQL computed LineTotal preserves kopecks");
    await Sql("UPDATE dbo.MenuItems SET Price=300 WHERE Id=@id",("@id",dishA));
    await repository.SaveOrderAsync(userId,id,original.Version,null,new[]{new OrderLineInput(dishA,3),new OrderLineInput(dishB,1)});
    var changed=await repository.GetOrderAsync(userId,id);
    Check(changed.Lines.Single(x=>x.MenuItemId==dishA).Price==250.25m,"Historical price retained after menu price change");
    Check(changed.Total==850.85m,"Quantity and new dish update total");
    await Reject(()=>repository.SaveOrderAsync(userId,id,original.Version,null,lines),typeof(ArgumentException),"Stale edit rejected");
    await repository.SaveOrderAsync(userId,id,changed.Version,null,lines);
    var saved=await new CafeRepository().GetOrderAsync(userId,id);
    Check(saved.Lines.Count==1 && saved.Total==500.50m,"Removing a dish persists");
    await Reject(()=>repository.CloseShiftAsync(userId,2000),typeof(ArgumentException),"Open order blocks shift close");
    await Reject(()=>repository.PayOrderAsync(userId,id,saved.Version,"Наличные",100,false),typeof(ArgumentException),"Insufficient cash rejected");
    await Reject(()=>repository.PayOrderAsync(userId,id,saved.Version,"Банковская карта",0,false),typeof(ArgumentException),"Card requires terminal confirmation");
    var cashier=await repository.LoginAsync("cashier","Cafe2026!") ?? throw new Exception("Missing cashier");
    await Reject(()=>repository.GetOrderAsync(cashier.Id,id),typeof(ArgumentException),"Another user's order is inaccessible");
    var paid=await repository.PayOrderAsync(userId,id,saved.Version,"Наличные",1000,false);
    Check(paid.Change==499.50m,"Cash change computed correctly");
    Check((await repository.GetOrderAsync(userId,id)).Status=="оплачен","Payment closes order");
    await Reject(()=>repository.PayOrderAsync(userId,id,saved.Version,"Наличные",1000,false),typeof(ArgumentException),"Repeated payment rejected");
    await Reject(()=>repository.SaveOrderAsync(userId,id,saved.Version,null,lines),typeof(ArgumentException),"Paid order cannot be edited");
    var summary=await repository.GetShiftSummaryAsync(userId);
    Check(summary.ExpectedCash==2500.50m,"Cash revenue increases expected cash");
    int cardId=await repository.SaveOrderAsync(userId,null,null,null,new[]{new OrderLineInput(dishB,3)});
    var cardOrder=await repository.GetOrderAsync(userId,cardId);
    async Task<bool> TryPay()
    {
        try{await new CafeRepository().PayOrderAsync(userId,cardId,cardOrder.Version,"Банковская карта",0,true);return true;}
        catch(ArgumentException){return false;}
    }
    var results=await Task.WhenAll(TryPay(),TryPay());
    Check(results.Count(x=>x)==1,"Concurrent payment registers once");
    Check(Convert.ToInt32(await Sql("SELECT COUNT(*) FROM dbo.Payments WHERE OrderId=@id",("@id",cardId)))==1,"One payment row after concurrent requests");
    summary=await repository.GetShiftSummaryAsync(userId);
    Check(summary.Revenue==800.80m && summary.ExpectedCash==2500.50m,"Card revenue does not increase cash");
    Check(await repository.CloseShiftAsync(userId,2500.50m)==0,"Shift closes without discrepancy");
    Check(await repository.GetOpenShiftAsync(userId) is null,"Closed shift no longer active");
    await Reject(()=>repository.SaveOrderAsync(userId,null,null,null,lines),typeof(ArgumentException),"Closed shift blocks new order");
    Console.WriteLine($"Completed: {passed} checks passed.");
}
finally
{
    if(userId!=0)
        await Sql("DELETE p FROM dbo.Payments p JOIN dbo.Orders o ON o.Id=p.OrderId WHERE o.UserId=@user; DELETE i FROM dbo.OrderItems i JOIN dbo.Orders o ON o.Id=i.OrderId WHERE o.UserId=@user; DELETE dbo.Orders WHERE UserId=@user; DELETE dbo.Shifts WHERE UserId=@user; DELETE dbo.Users WHERE Id=@user AND Login=@login;",("@user",userId),("@login",marker));
    if(dishA!=0)await Sql("DELETE dbo.MenuItems WHERE Id=@id AND Name=@name",("@id",dishA),("@name",marker+"_A"));
    if(dishB!=0)await Sql("DELETE dbo.MenuItems WHERE Id=@id AND Name=@name",("@id",dishB),("@name",marker+"_B"));
}

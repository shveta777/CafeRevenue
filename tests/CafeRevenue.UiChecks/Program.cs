using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CafeRevenue;
using CafeRevenue.Models;
using CafeRevenue.Views;
using CafeRevenue.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal static class Program
{
    private static string output = "";
    [STAThread]
    private static void Main(string[] args)
    {
        output = args[0]; Directory.CreateDirectory(output);
        var app = new App(); app.InitializeComponent();
        app.Startup += async (_, _) =>
        {
            try { await RunAsync(app); File.WriteAllText(Path.Combine(output,"result.txt"),"UI scenarios passed"); app.Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output,"result.txt"),ex.ToString()); Environment.Exit(1); }
        };
        app.Run();
    }
    private static T Control<T>(Window window, string name) where T : class => (T)window.FindName(name);
    private static async Task WaitAsync(Func<bool> done)
    {
        for (int i=0;i<200;i++) { if (done()) return; await Task.Delay(50); }
        throw new TimeoutException("UI did not reach expected state");
    }
    private static void Click(Window window,string name) => Control<Button>(window,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Capture(Window window,string name)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)(content.ActualWidth*dpi.DpiScaleX),
            (int)(content.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle((Brush)new BrushConverter().ConvertFromString("#F3F6F7")!,null,new Rect(0,0,content.ActualWidth,content.ActualHeight));
            dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));
        }
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output,name+".png")); encoder.Save(stream);
    }
    private static async Task RunAsync(App app)
    {
        var repository=new CafeRepository();
        string uiLogin="ui_"+Guid.NewGuid().ToString("N");
        using(var connection=new SqlConnection(DatabaseConnection.LoadConfiguration().GetConnectionString("CafeRevenueDb")))
        {
            await connection.OpenAsync();
            using var create=new SqlCommand("INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive) SELECT @login,PasswordHash,PasswordSalt,RoleId,1 FROM dbo.Users WHERE Login=N'cashier'",connection);
            create.Parameters.AddWithValue("@login",uiLogin);
            if(await create.ExecuteNonQueryAsync()!=1)throw new Exception("Missing cashier fixture");
        }
        var cashier=await repository.LoginAsync(uiLogin,"Cafe2026!") ?? throw new Exception("Test login failed");
        int shiftId=0;
        try
        {
            await Task.Delay(300);
            var login=app.Windows.OfType<LoginWindow>().Single();
            Control<TextBox>(login,"LoginBox").Text=uiLogin;
            Control<PasswordBox>(login,"PasswordBox").Password="Cafe2026!";
            Click(login,"LoginButton");
            await WaitAsync(()=>app.MainWindow is MainWindow);
            var window=(MainWindow)app.MainWindow;
            await Ready(window);
            Capture(window,"01-closed-shift");
            var opening=HandleAmountAsync(app,"2000", "02-open-shift");
            Click(window,"OpenShiftButton");await opening;await Ready(window);
            shiftId=(await repository.GetOpenShiftAsync(cashier.Id))!.Id;
            SelectDish(window,"Капучино");
            Control<TextBox>(window,"QuantityBox").Text="0";
            Click(window,"AddButton");
            if(!Control<TextBlock>(window,"StatusText").Text.StartsWith("Количество"))throw new Exception("Quantity validation missing");
            Control<TextBox>(window,"QuantityBox").Text="2";Click(window,"AddButton");
            SelectDish(window,"Паста с курицей");
            Control<TextBox>(window,"QuantityBox").Text="1";Click(window,"AddButton");
            Capture(window,"03-order-draft");
            Click(window,"SaveOrderButton");
            await WaitAsync(()=>Control<TextBlock>(window,"StatusText").Text.Contains("сохранён"));await Ready(window);
            Capture(window,"04-order-saved");
            var cash=HandlePaymentAsync(app,false);
            Click(window,"PayButton");await cash;
            await Ready(window);
            if(Control<Button>(window,"SaveOrderButton").IsEnabled)throw new Exception("Paid order is editable");
            Capture(window,"07-order-paid");
            Click(window,"NewOrderButton");SelectDish(window,"Чай чёрный");
            Control<TextBox>(window,"QuantityBox").Text="2";Click(window,"AddButton");
            Click(window,"SaveOrderButton");
            await WaitAsync(()=>Control<TextBlock>(window,"StatusText").Text.Contains("сохранён"));await Ready(window);
            var card=HandlePaymentAsync(app,true);Click(window,"PayButton");await card;await Ready(window);
            var summary=await repository.GetShiftSummaryAsync(cashier.Id);
            var closing=HandleAmountAsync(app,summary.ExpectedCash.ToString(System.Globalization.CultureInfo.InvariantCulture),"09-close-shift");
            Click(window,"CloseShiftButton");await closing;await Ready(window);
            Capture(window,"10-shift-closed");
        }
        finally
        {
            if(shiftId!=0)
            {
                using var c=new SqlConnection(DatabaseConnection.LoadConfiguration().GetConnectionString("CafeRevenueDb"));
                await c.OpenAsync();
                using var cmd=new SqlCommand("SET ARITHABORT ON; SET QUOTED_IDENTIFIER ON; DELETE p FROM dbo.Payments p JOIN dbo.Orders o ON o.Id=p.OrderId WHERE o.ShiftId=@shift; DELETE i FROM dbo.OrderItems i JOIN dbo.Orders o ON o.Id=i.OrderId WHERE o.ShiftId=@shift; DELETE dbo.Orders WHERE ShiftId=@shift; DELETE dbo.Shifts WHERE Id=@shift AND UserId=@user;",c);
                cmd.Parameters.AddWithValue("@shift",shiftId);cmd.Parameters.AddWithValue("@user",cashier.Id);
                await cmd.ExecuteNonQueryAsync();
            }
            using var cleanupConnection=new SqlConnection(DatabaseConnection.LoadConfiguration().GetConnectionString("CafeRevenueDb"));
            await cleanupConnection.OpenAsync();
            using var cleanupUser=new SqlCommand("DELETE dbo.Users WHERE Id=@id AND Login=@login",cleanupConnection);
            cleanupUser.Parameters.AddWithValue("@id",cashier.Id);
            cleanupUser.Parameters.AddWithValue("@login",uiLogin);
            await cleanupUser.ExecuteNonQueryAsync();
        }
    }
    private static async Task Ready(Window window)
    {
        await WaitAsync(()=>Control<Grid>(window,"Workspace").IsEnabled);
        await Task.Delay(200);
    }
    private static void SelectDish(Window window,string name)
    {
        var grid=Control<DataGrid>(window,"MenuGrid");
        grid.SelectedItem=grid.Items.Cast<MenuDish>().Single(x=>x.Name==name);
    }
    private static async Task HandleAmountAsync(App app,string amount,string screenshot)
    {
        await WaitAsync(()=>app.Windows.OfType<AmountWindow>().Any());
        var dialog=app.Windows.OfType<AmountWindow>().Single();
        Control<TextBox>(dialog,"AmountBox").Text=amount;
        await Task.Delay(200);Capture(dialog,screenshot);Click(dialog,"ConfirmButton");
    }
    private static async Task HandlePaymentAsync(App app,bool card)
    {
        await WaitAsync(()=>app.Windows.OfType<PaymentWindow>().Any());
        var dialog=app.Windows.OfType<PaymentWindow>().Single();
        if(card)
        {
            Control<ComboBox>(dialog,"MethodBox").SelectedIndex=1;
            if(Control<Button>(dialog,"PayButton").IsEnabled)throw new Exception("Card confirmation missing");
            Control<CheckBox>(dialog,"CardCheck").IsChecked=true;
            await Task.Delay(200);Capture(dialog,"08-card-confirmation");
        }
        else
        {
            Control<TextBox>(dialog,"ReceivedBox").Text="750";
            if(Control<Button>(dialog,"PayButton").IsEnabled)throw new Exception("Insufficient cash allowed");
            await Task.Delay(200);Capture(dialog,"05-insufficient-cash");
            Control<TextBox>(dialog,"ReceivedBox").Text="1000";
            if(!Control<TextBlock>(dialog,"ChangeText").Text.Contains("160"))throw new Exception("Wrong change");
            await Task.Delay(200);Capture(dialog,"06-cash-change");
        }
        Click(dialog,"PayButton");
        await WaitAsync(()=>!app.Windows.OfType<PaymentWindow>().Any());
    }
}

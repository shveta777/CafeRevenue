-- Учебная база: по 5 строк в таблице, Roles содержит 3 согласованные роли.
-- Запускать после 001, 002 и 003. Существующие данные не удаляются.
USE [CafeRevenue];
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

INSERT dbo.Employees(LastName,FirstName,MiddleName,Position,Phone,HireDate)
SELECT v.LastName,v.FirstName,v.MiddleName,v.Position,v.Phone,'20260110'
FROM (VALUES
 (N'Иванова',N'Анна',N'Сергеевна',N'Управляющий',N'+7 000 000-00-01'),
 (N'Петров',N'Иван',N'Алексеевич',N'Администратор',N'+7 000 000-00-02'),
 (N'Смирнова',N'Мария',N'Игоревна',N'Кассир',N'+7 000 000-00-03'),
 (N'Соколова',N'Елена',N'Андреевна',N'Управляющий',N'+7 000 000-00-04'),
 (N'Кузнецов',N'Алексей',N'Дмитриевич',N'Кассир',N'+7 000 000-00-05')
) v(LastName,FirstName,MiddleName,Position,Phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.Phone=v.Phone);

-- Учебный пароль Cafe2026!. Хеш PBKDF2-SHA256, 100000 итераций.

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Login=N'manager')
INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive)
SELECT N'manager',0x2737618dc930b1f69abbdc4483f09fc4d10cf974f2ee490fe6863da60e5da4bc,0x1539fbcb7f43cf9c2736592ab8340541,Id,1 FROM dbo.Roles WHERE Name=N'Управляющий';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Login=N'admin')
INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive)
SELECT N'admin',0x06fb2a57598bb037acd38b93bab75189e331bacbc94773daa36e4d9356f321a7,0x2c01f9ab14b1295f501303f166e79746,Id,1 FROM dbo.Roles WHERE Name=N'Администратор';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Login=N'cashier')
INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive)
SELECT N'cashier',0xfbc412bb7afa0ea864d5412cfd34d3fe1dc4bc823f21077958840916379ed769,0x819d5ca04649454413ee1a3dffde80c5,Id,1 FROM dbo.Roles WHERE Name=N'Кассир';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Login=N'manager2')
INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive)
SELECT N'manager2',0x870c9f643b1ee5d28cb88ce102569df804d12c8cb2b43b17352d8c74d2547a4c,0x7e08b754b328f4ffe5123d49fdf70446,Id,1 FROM dbo.Roles WHERE Name=N'Управляющий';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Login=N'cashier2')
INSERT dbo.Users(Login,PasswordHash,PasswordSalt,RoleId,IsActive)
SELECT N'cashier2',0x77fb6e1e4e5bf4d318bbbea38860e7b2498ac25fc911b5e1b5e9e1d7cc7b5f4e,0xf282cb1b3cba677a382b6a3a16a444b5,Id,1 FROM dbo.Roles WHERE Name=N'Кассир';

UPDATE u SET EmployeeId=e.Id
FROM dbo.Users u
JOIN (VALUES (N'manager',N'+7 000 000-00-01'),(N'admin',N'+7 000 000-00-02'),
 (N'cashier',N'+7 000 000-00-03'),(N'manager2',N'+7 000 000-00-04'),
 (N'cashier2',N'+7 000 000-00-05')) v(Login,Phone) ON v.Login=u.Login
JOIN dbo.Employees e ON e.Phone=v.Phone WHERE u.EmployeeId IS NULL;

INSERT dbo.ExpenseCategories(Name)
SELECT N'Зарплата' WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpenseCategories WHERE Name=N'Зарплата');

INSERT dbo.Expenses(CategoryId,UserId,Amount,[Date],Comment)
SELECT c.Id,u.Id,v.Amount,v.[Date],v.Comment
FROM (VALUES
 (N'Аренда',9800.00,CONVERT(date,'20261001'),N'Аренда помещения'),
 (N'Продукты',7000.00,CONVERT(date,'20261003'),N'Закупка продуктов'),
 (N'Коммунальные',4600.00,CONVERT(date,'20261004'),N'Счёт за электроэнергию'),
 (N'Продукты',1200.00,CONVERT(date,'20261005'),N'Закупка продуктов для кафе'),
 (N'Зарплата',3500.00,CONVERT(date,'20261024'),N'Учебный пример выплаты за смену')
) v(Category,Amount,[Date],Comment)
JOIN dbo.ExpenseCategories c ON c.Name=v.Category
CROSS JOIN dbo.Users u WHERE u.Login=N'manager'
AND NOT EXISTS (SELECT 1 FROM dbo.Expenses e WHERE e.CategoryId=c.Id
 AND e.[Date]=v.[Date] AND e.Comment=v.Comment);

INSERT dbo.Categories(Name,SortOrder)
SELECT v.Name,v.SortOrder FROM (VALUES
 (N'Кофе',1),(N'Горячие блюда',2),(N'Десерты',3),(N'Салаты',4),(N'Чай',5)
) v(Name,SortOrder) WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.Name=v.Name);

INSERT dbo.MenuItems(Name,CategoryId,Price,Cost,IsAvailable,IsPromoted,PromoPrice,PromoUntil)
SELECT v.Name,c.Id,v.Price,v.Cost,1,0,NULL,NULL FROM (VALUES
 (N'Капучино',N'Кофе',250,90),(N'Паста с курицей',N'Горячие блюда',340,150),
 (N'Чизкейк',N'Десерты',180,70),(N'Салат Цезарь',N'Салаты',420,180),
 (N'Чай чёрный',N'Чай',150,35)
) v(Name,Category,Price,Cost) JOIN dbo.Categories c ON c.Name=v.Category
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItems m WHERE m.Name=v.Name);

-- История завершённых остановок. Сейчас все блюда доступны.
INSERT dbo.StopList(MenuItemId,UserId,Reason,StoppedAt,ResumedAt)
SELECT m.Id,u.Id,N'Учебный пример: временно закончились ингредиенты',
 '20261018 10:00:00','20261018 11:00:00'
FROM dbo.MenuItems m CROSS JOIN dbo.Users u
WHERE u.Login=N'manager' AND m.Name IN (N'Капучино',N'Паста с курицей',N'Чизкейк',N'Салат Цезарь',N'Чай чёрный')
AND NOT EXISTS (SELECT 1 FROM dbo.StopList s WHERE s.MenuItemId=m.Id AND s.StoppedAt='20261018 10:00:00');

INSERT dbo.Tables(Number,Seats)
SELECT v.Number,v.Seats FROM (VALUES (1,2),(2,4),(3,4),(4,6),(5,2)) v(Number,Seats)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Tables t WHERE t.Number=v.Number);

INSERT dbo.PaymentMethods(Name)
SELECT v.Name FROM (VALUES (N'Наличные'),(N'Банковская карта'),(N'СБП'),
 (N'Подарочный сертификат'),(N'Банковский перевод')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods p WHERE p.Name=v.Name);

INSERT dbo.DiscountTypes(Name)
SELECT v.Name FROM (VALUES (N'Разовая'),(N'По акции'),(N'На рекомендованные позиции'),
 (N'Для постоянных гостей'),(N'В день рождения')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.DiscountTypes d WHERE d.Name=v.Name);

INSERT dbo.Discounts(Name,DiscountTypeId,ValueType,Value,StartDate,EndDate,TimeFrom,TimeTo,IsActive)
SELECT v.Name,t.Id,v.ValueType,v.Value,'20260101','20261231',NULL,NULL,1
FROM (VALUES
 (N'Разовая скидка 10%',N'Разовая',N'процент',10),
 (N'Акция 5%',N'По акции',N'процент',5),
 (N'Рекомендованная позиция минус 50 рублей',N'На рекомендованные позиции',N'сумма',50),
 (N'Постоянный гость 15%',N'Для постоянных гостей',N'процент',15),
 (N'День рождения 20%',N'В день рождения',N'процент',20)
) v(Name,TypeName,ValueType,Value) JOIN dbo.DiscountTypes t ON t.Name=v.TypeName
WHERE NOT EXISTS (SELECT 1 FROM dbo.Discounts d WHERE d.Name=v.Name);

INSERT dbo.Reservations(TableId,ReservedAt,GuestsCount,ContactPhone,Deposit,SurchargePercent,Status)
SELECT t.Id,DATEADD(day,v.DayOffset,CONVERT(datetime2,'20261019 12:00:00')),
 v.Guests,v.Phone,0,0,N'исполнена'
FROM (VALUES (1,0,2,N'+7 000 000-10-01'),(2,1,3,N'+7 000 000-10-02'),
 (3,2,4,N'+7 000 000-10-03'),(4,3,5,N'+7 000 000-10-04'),(5,4,2,N'+7 000 000-10-05')
) v(Number,DayOffset,Guests,Phone) JOIN dbo.Tables t ON t.Number=v.Number
WHERE NOT EXISTS (SELECT 1 FROM dbo.Reservations r WHERE r.TableId=t.Id
 AND r.ReservedAt=DATEADD(day,v.DayOffset,CONVERT(datetime2,'20261019 12:00:00')));

INSERT dbo.Shifts(UserId,OpenedAt,ClosedAt,OpeningCash,ClosingCash,Status)
SELECT u.Id,DATEADD(day,v.DayOffset,CONVERT(datetime2,'20261019 09:00:00')),
 DATEADD(day,v.DayOffset,CONVERT(datetime2,'20261019 21:00:00')),2000,v.ClosingCash,N'закрыта'
FROM (VALUES (0,2450),(1,2000),(2,2000),(3,2000),(4,2000)) v(DayOffset,ClosingCash)
CROSS JOIN dbo.Users u WHERE u.Login=N'cashier'
AND NOT EXISTS (SELECT 1 FROM dbo.Shifts s WHERE s.UserId=u.Id
 AND s.OpenedAt=DATEADD(day,v.DayOffset,CONVERT(datetime2,'20261019 09:00:00')));

INSERT dbo.Orders(ShiftId,TableId,ReservationId,UserId,DiscountId,DiscountValueAtMoment,CreatedAt,ClosedAt,Status)
SELECT s.Id,t.Id,r.Id,u.Id,CASE WHEN t.Number=1 THEN d.Id ELSE NULL END,
 CASE WHEN t.Number=1 THEN 50 ELSE 0 END,
 DATEADD(minute,10,r.ReservedAt),DATEADD(minute,50,r.ReservedAt),N'оплачен'
FROM dbo.Reservations r JOIN dbo.Tables t ON t.Id=r.TableId
JOIN dbo.Shifts s ON CONVERT(date,s.OpenedAt)=CONVERT(date,r.ReservedAt)
JOIN dbo.Users u ON u.Id=s.UserId
CROSS JOIN dbo.Discounts d
WHERE d.Name=N'Разовая скидка 10%' AND u.Login=N'cashier'
AND r.ContactPhone LIKE N'+7 000 000-10-0[1-5]'
AND NOT EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.ReservationId=r.Id);

INSERT dbo.OrderItems(OrderId,MenuItemId,Quantity,PriceAtMoment)
SELECT o.Id,m.Id,v.Quantity,m.Price FROM dbo.Orders o
JOIN dbo.Tables t ON t.Id=o.TableId
JOIN (VALUES (1,N'Капучино',2),(2,N'Паста с курицей',1),(3,N'Чизкейк',2),
 (4,N'Салат Цезарь',1),(5,N'Чай чёрный',2)) v(Number,Name,Quantity) ON v.Number=t.Number
JOIN dbo.MenuItems m ON m.Name=v.Name
JOIN dbo.Reservations r ON r.Id=o.ReservationId AND r.ContactPhone LIKE N'+7 000 000-10-0[1-5]'
WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderItems i WHERE i.OrderId=o.Id);

INSERT dbo.Payments(OrderId,PaymentMethodId,Amount,PaidAt)
SELECT o.Id,p.Id,x.Total-COALESCE(o.DiscountValueAtMoment,0),o.ClosedAt
FROM dbo.Orders o JOIN dbo.Tables t ON t.Id=o.TableId
JOIN (VALUES (1,N'Наличные'),(2,N'Банковская карта'),(3,N'СБП'),
 (4,N'Подарочный сертификат'),(5,N'Банковский перевод')) v(Number,Name) ON v.Number=t.Number
JOIN dbo.PaymentMethods p ON p.Name=v.Name
JOIN dbo.Reservations r ON r.Id=o.ReservationId AND r.ContactPhone LIKE N'+7 000 000-10-0[1-5]'
CROSS APPLY (SELECT SUM(i.Quantity*i.PriceAtMoment) AS Total FROM dbo.OrderItems i WHERE i.OrderId=o.Id) x
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payments p WHERE p.OrderId=o.Id);

-- Если база уже содержит дополнительные записи, данные не удаляются ради числа строк.
DECLARE @counts TABLE (Name sysname, [RowCount] int);
INSERT @counts
SELECT N'Roles',COUNT(*) FROM dbo.Roles UNION ALL
SELECT N'Employees',COUNT(*) FROM dbo.Employees UNION ALL
SELECT N'Users',COUNT(*) FROM dbo.Users UNION ALL
SELECT N'Categories',COUNT(*) FROM dbo.Categories UNION ALL
SELECT N'MenuItems',COUNT(*) FROM dbo.MenuItems UNION ALL
SELECT N'StopList',COUNT(*) FROM dbo.StopList UNION ALL
SELECT N'Tables',COUNT(*) FROM dbo.Tables UNION ALL
SELECT N'Reservations',COUNT(*) FROM dbo.Reservations UNION ALL
SELECT N'PaymentMethods',COUNT(*) FROM dbo.PaymentMethods UNION ALL
SELECT N'DiscountTypes',COUNT(*) FROM dbo.DiscountTypes UNION ALL
SELECT N'Discounts',COUNT(*) FROM dbo.Discounts UNION ALL
SELECT N'Shifts',COUNT(*) FROM dbo.Shifts UNION ALL
SELECT N'Orders',COUNT(*) FROM dbo.Orders UNION ALL
SELECT N'OrderItems',COUNT(*) FROM dbo.OrderItems UNION ALL
SELECT N'Payments',COUNT(*) FROM dbo.Payments UNION ALL
SELECT N'ExpenseCategories',COUNT(*) FROM dbo.ExpenseCategories UNION ALL
SELECT N'Expenses',COUNT(*) FROM dbo.Expenses;
IF EXISTS (SELECT 1 FROM @counts WHERE [RowCount]<>CASE WHEN Name=N'Roles' THEN 3 ELSE 5 END)
 THROW 51000,N'Количество строк отличается от согласованного. Транзакция отменена без удаления данных.',1;
SELECT Name,[RowCount] FROM @counts ORDER BY Name;
COMMIT;
GO

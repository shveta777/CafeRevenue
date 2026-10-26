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

DECLARE @counts TABLE (TableName sysname, RowsCount int);
DECLARE @table sysname,@sql nvarchar(max);
DECLARE tables_cursor CURSOR LOCAL FAST_FORWARD FOR
 SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') ORDER BY name;
OPEN tables_cursor;
FETCH NEXT FROM tables_cursor INTO @table;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=N'SELECT N'''+REPLACE(@table,'''','''''')+N''',COUNT(*) FROM dbo.'+QUOTENAME(@table);
 INSERT @counts EXEC sp_executesql @sql;
 FETCH NEXT FROM tables_cursor INTO @table;
END;
CLOSE tables_cursor;
DEALLOCATE tables_cursor;
IF (SELECT COUNT(*) FROM @counts)<>17
 THROW 51001,N'Ожидается 17 таблиц.',1;
IF EXISTS (SELECT 1 FROM @counts WHERE RowsCount<>CASE WHEN TableName=N'Roles' THEN 3 ELSE 5 END)
 THROW 51002,N'Количество строк отличается от согласованного.',1;

DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
IF EXISTS (SELECT 1 FROM dbo.vOrderTotals WHERE Status=N'оплачен' AND TotalAmount<>PaidAmount)
 THROW 51003,N'Оплата не совпадает с итогом заказа.',1;
IF EXISTS (SELECT 1 FROM dbo.vShiftSummary WHERE CashDifference<>0)
 THROW 51004,N'Наличные учебной смены не совпадают с расчётом.',1;
IF EXISTS (SELECT 1 FROM dbo.Reservations r JOIN dbo.Tables t ON t.Id=r.TableId WHERE r.GuestsCount>t.Seats)
 THROW 51005,N'Число гостей превышает вместимость столика.',1;
IF EXISTS (SELECT 1 FROM dbo.MenuItems m WHERE m.IsAvailable=1
 AND EXISTS(SELECT 1 FROM dbo.StopList s WHERE s.MenuItemId=m.Id AND s.ResumedAt IS NULL))
 THROW 51006,N'Доступная позиция находится в активном стоп-листе.',1;

-- Проверка вычисляемого столбца изменением исходного количества без сохранения изменения.
BEGIN TRANSACTION;
DECLARE @item int=(SELECT MIN(Id) FROM dbo.OrderItems);
UPDATE dbo.OrderItems SET Quantity=Quantity+1 WHERE Id=@item;
IF EXISTS(SELECT 1 FROM dbo.OrderItems WHERE Id=@item AND LineTotal<>Quantity*PriceAtMoment)
BEGIN
 ROLLBACK;
 THROW 51007,N'Ошибка вычисления суммы позиции.',1;
END;
ROLLBACK;

SELECT TableName,RowsCount FROM @counts ORDER BY TableName;
SELECT OrderId,GrossAmount,DiscountAmount,TotalAmount,PaidAmount FROM dbo.vOrderTotals;
SELECT ShiftId,OpeningCash,CashRevenue,ExpectedCash,ClosingCash,CashDifference FROM dbo.vShiftSummary;
PRINT N'Проверки количества строк, связей и вычисляемых значений пройдены.';
GO

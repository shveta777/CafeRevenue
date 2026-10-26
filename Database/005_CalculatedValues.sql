USE [CafeRevenue];
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
-- Сумма строки зависит только от полей этой строки; INSERT её не заполняет.
IF COL_LENGTH('dbo.OrderItems','LineTotal') IS NULL
 ALTER TABLE dbo.OrderItems ADD LineTotal AS
  (CONVERT(decimal(20,2),Quantity * PriceAtMoment)) PERSISTED;
GO
-- Итог заказа зависит от нескольких позиций, поэтому вычисляется представлением.
-- DiscountValueAtMoment и PriceAtMoment сохраняют исторические значения продажи.
CREATE OR ALTER VIEW dbo.vOrderTotals
AS
SELECT o.Id AS OrderId,o.ShiftId,o.Status,
 COALESCE(i.GrossAmount,CONVERT(decimal(28,2),0)) AS GrossAmount,
 COALESCE(o.DiscountValueAtMoment,CONVERT(decimal(10,2),0)) AS DiscountAmount,
 COALESCE(i.GrossAmount,0)-COALESCE(o.DiscountValueAtMoment,0) AS TotalAmount,
 COALESCE(p.PaidAmount,CONVERT(decimal(28,2),0)) AS PaidAmount
FROM dbo.Orders o
OUTER APPLY (SELECT CONVERT(decimal(28,2),SUM(i.LineTotal)) AS GrossAmount FROM dbo.OrderItems i WHERE i.OrderId=o.Id) i
OUTER APPLY (SELECT CONVERT(decimal(28,2),SUM(p.Amount)) AS PaidAmount FROM dbo.Payments p WHERE p.OrderId=o.Id) p;
GO
-- Согласно ЛР4 расходы не уменьшают остаток кассовой смены автоматически.
-- В остаток включаются только наличные оплаты завершённых заказов.
CREATE OR ALTER VIEW dbo.vShiftSummary
AS
SELECT s.Id AS ShiftId,s.UserId,s.OpenedAt,s.ClosedAt,s.Status,
 s.OpeningCash,s.ClosingCash,
 COALESCE(p.Revenue,CONVERT(decimal(28,2),0)) AS Revenue,
 COALESCE(p.CashRevenue,CONVERT(decimal(28,2),0)) AS CashRevenue,
 s.OpeningCash+COALESCE(p.CashRevenue,0) AS ExpectedCash,
 s.ClosingCash-(s.OpeningCash+COALESCE(p.CashRevenue,0)) AS CashDifference
FROM dbo.Shifts s
OUTER APPLY (
 SELECT CONVERT(decimal(28,2),SUM(p.Amount)) AS Revenue,
 CONVERT(decimal(28,2),SUM(CASE WHEN m.Name=N'Наличные' THEN p.Amount ELSE CONVERT(decimal(10,2),0) END)) AS CashRevenue
 FROM dbo.Orders o JOIN dbo.Payments p ON p.OrderId=o.Id
 JOIN dbo.PaymentMethods m ON m.Id=p.PaymentMethodId
 WHERE o.ShiftId=s.Id AND o.Status=N'оплачен'
) p;
GO
-- Выручка определяется датой оплаты, расходы -- датой записи расхода.
-- Финансовый результат = выручка - расходы (управленческий показатель ЛР4).
CREATE OR ALTER VIEW dbo.vDailyFinancials
AS
WITH Revenue AS (
 SELECT CONVERT(date,p.PaidAt) AS [Date],CONVERT(decimal(28,2),SUM(p.Amount)) AS Revenue,
 COUNT(DISTINCT o.Id) AS PaidOrderCount
 FROM dbo.Payments p JOIN dbo.Orders o ON o.Id=p.OrderId
 WHERE o.Status=N'оплачен' GROUP BY CONVERT(date,p.PaidAt)
), Expenses AS (
 SELECT [Date],CONVERT(decimal(28,2),SUM(Amount)) AS Expenses FROM dbo.Expenses GROUP BY [Date]
), Dates AS (
 SELECT [Date] FROM Revenue UNION SELECT [Date] FROM Expenses
)
SELECT d.[Date],COALESCE(r.Revenue,CONVERT(decimal(28,2),0)) AS Revenue,
 COALESCE(r.PaidOrderCount,0) AS PaidOrderCount,
 CONVERT(decimal(20,2),CASE WHEN COALESCE(r.PaidOrderCount,0)=0 THEN 0
 ELSE r.Revenue / r.PaidOrderCount END) AS AverageCheck,
 COALESCE(e.Expenses,CONVERT(decimal(28,2),0)) AS Expenses,
 COALESCE(r.Revenue,0)-COALESCE(e.Expenses,0) AS FinancialResult
FROM Dates d LEFT JOIN Revenue r ON r.[Date]=d.[Date]
LEFT JOIN Expenses e ON e.[Date]=d.[Date];
GO

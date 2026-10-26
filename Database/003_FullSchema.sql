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
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.Categories') IS NULL
CREATE TABLE dbo.Categories (
 Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE,
 SortOrder int NOT NULL CHECK (SortOrder >= 0));

IF OBJECT_ID('dbo.MenuItems') IS NULL
CREATE TABLE dbo.MenuItems (
 Id int IDENTITY PRIMARY KEY, Name nvarchar(100) NOT NULL,
 CategoryId int NOT NULL REFERENCES dbo.Categories(Id),
 Price decimal(10,2) NOT NULL CHECK (Price >= 0),
 Cost decimal(10,2) NOT NULL CHECK (Cost >= 0),
 IsAvailable bit NOT NULL DEFAULT 1, IsPromoted bit NOT NULL DEFAULT 0,
 PromoPrice decimal(10,2) NULL CHECK (PromoPrice >= 0), PromoUntil datetime2 NULL,
 CONSTRAINT CK_MenuItems_Promo CHECK (PromoPrice IS NULL OR PromoPrice <= Price));

IF OBJECT_ID('dbo.StopList') IS NULL
CREATE TABLE dbo.StopList (
 Id int IDENTITY PRIMARY KEY,
 MenuItemId int NOT NULL REFERENCES dbo.MenuItems(Id),
 UserId int NOT NULL REFERENCES dbo.Users(Id),
 Reason nvarchar(200) NOT NULL,
 StoppedAt datetime2 NOT NULL, ResumedAt datetime2 NULL,
 CONSTRAINT CK_StopList_Time CHECK (ResumedAt IS NULL OR ResumedAt >= StoppedAt));

IF OBJECT_ID('dbo.Tables') IS NULL
CREATE TABLE dbo.Tables (
 Id int IDENTITY PRIMARY KEY, Number int NOT NULL UNIQUE CHECK (Number > 0),
 Seats int NOT NULL CHECK (Seats > 0));

IF OBJECT_ID('dbo.Reservations') IS NULL
CREATE TABLE dbo.Reservations (
 Id int IDENTITY PRIMARY KEY, TableId int NOT NULL REFERENCES dbo.Tables(Id),
 ReservedAt datetime2 NOT NULL, GuestsCount int NOT NULL CHECK (GuestsCount > 0),
 ContactPhone nvarchar(20) NULL,
 Deposit decimal(10,2) NULL CHECK (Deposit >= 0),
 SurchargePercent decimal(5,2) NULL CHECK (SurchargePercent BETWEEN 0 AND 100),
 Status nvarchar(20) NOT NULL CHECK (Status IN (N'ожидается',N'исполнена',N'отменена',N'неявка')));

IF OBJECT_ID('dbo.PaymentMethods') IS NULL
CREATE TABLE dbo.PaymentMethods (Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE);

IF OBJECT_ID('dbo.DiscountTypes') IS NULL
CREATE TABLE dbo.DiscountTypes (Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE);

IF OBJECT_ID('dbo.Discounts') IS NULL
CREATE TABLE dbo.Discounts (
 Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL,
 DiscountTypeId int NOT NULL REFERENCES dbo.DiscountTypes(Id),
 ValueType nvarchar(10) NOT NULL CHECK (ValueType IN (N'процент',N'сумма')),
 Value decimal(10,2) NOT NULL CHECK (Value >= 0),
 StartDate date NULL, EndDate date NULL, TimeFrom time NULL, TimeTo time NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT CK_Discounts_Percent CHECK (ValueType <> N'процент' OR Value <= 100),
 CONSTRAINT CK_Discounts_Dates CHECK (StartDate IS NULL OR EndDate IS NULL OR StartDate <= EndDate));

IF OBJECT_ID('dbo.Shifts') IS NULL
CREATE TABLE dbo.Shifts (
 Id int IDENTITY PRIMARY KEY, UserId int NOT NULL REFERENCES dbo.Users(Id),
 OpenedAt datetime2 NOT NULL, ClosedAt datetime2 NULL,
 OpeningCash decimal(10,2) NOT NULL CHECK (OpeningCash >= 0),
 ClosingCash decimal(10,2) NULL CHECK (ClosingCash >= 0),
 Status nvarchar(20) NOT NULL CHECK (Status IN (N'открыта',N'закрыта')),
 CONSTRAINT CK_Shifts_Time CHECK (ClosedAt IS NULL OR ClosedAt >= OpenedAt),
 CONSTRAINT CK_Shifts_Closed CHECK (
   (Status=N'открыта' AND ClosedAt IS NULL AND ClosingCash IS NULL) OR
   (Status=N'закрыта' AND ClosedAt IS NOT NULL AND ClosingCash IS NOT NULL)));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Shifts_OpenUser' AND object_id=OBJECT_ID('dbo.Shifts'))
CREATE UNIQUE INDEX UX_Shifts_OpenUser ON dbo.Shifts(UserId) WHERE Status=N'открыта';

IF OBJECT_ID('dbo.Orders') IS NULL
CREATE TABLE dbo.Orders (
 Id int IDENTITY PRIMARY KEY, ShiftId int NOT NULL REFERENCES dbo.Shifts(Id),
 TableId int NULL REFERENCES dbo.Tables(Id),
 ReservationId int NULL REFERENCES dbo.Reservations(Id),
 UserId int NOT NULL REFERENCES dbo.Users(Id),
 DiscountId int NULL REFERENCES dbo.Discounts(Id),
 DiscountValueAtMoment decimal(10,2) NULL CHECK (DiscountValueAtMoment >= 0),
 CreatedAt datetime2 NOT NULL, ClosedAt datetime2 NULL,
 Status nvarchar(20) NOT NULL CHECK (Status IN (N'открыт',N'оплачен',N'отменён')),
 CONSTRAINT CK_Orders_Time CHECK (ClosedAt IS NULL OR ClosedAt >= CreatedAt),
 CONSTRAINT CK_Orders_Closed CHECK (
   (Status=N'открыт' AND ClosedAt IS NULL) OR
   (Status IN (N'оплачен',N'отменён') AND ClosedAt IS NOT NULL)));

IF OBJECT_ID('dbo.OrderItems') IS NULL
CREATE TABLE dbo.OrderItems (
 Id int IDENTITY PRIMARY KEY, OrderId int NOT NULL REFERENCES dbo.Orders(Id),
 MenuItemId int NOT NULL REFERENCES dbo.MenuItems(Id),
 Quantity int NOT NULL CHECK (Quantity > 0),
 PriceAtMoment decimal(10,2) NOT NULL CHECK (PriceAtMoment >= 0));

IF OBJECT_ID('dbo.Payments') IS NULL
CREATE TABLE dbo.Payments (
 Id int IDENTITY PRIMARY KEY, OrderId int NOT NULL REFERENCES dbo.Orders(Id),
 PaymentMethodId int NOT NULL REFERENCES dbo.PaymentMethods(Id),
 Amount decimal(10,2) NOT NULL CHECK (Amount > 0), PaidAt datetime2 NOT NULL);

COMMIT;
GO

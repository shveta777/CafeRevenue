USE [CafeRevenue];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.Roles') IS NULL
CREATE TABLE dbo.Roles (Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE);
IF OBJECT_ID('dbo.Employees') IS NULL
CREATE TABLE dbo.Employees (
 Id int IDENTITY PRIMARY KEY, LastName nvarchar(50) NOT NULL,
 FirstName nvarchar(50) NOT NULL, MiddleName nvarchar(50) NULL,
 Position nvarchar(50) NOT NULL, Phone nvarchar(20) NULL, HireDate date NOT NULL);
IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users (
 Id int IDENTITY PRIMARY KEY, Login nvarchar(50) NOT NULL UNIQUE,
 PasswordHash varbinary(32) NOT NULL, PasswordSalt varbinary(16) NOT NULL,
 RoleId int NOT NULL REFERENCES dbo.Roles(Id),
 EmployeeId int NULL REFERENCES dbo.Employees(Id), IsActive bit NOT NULL DEFAULT 1);
IF OBJECT_ID('dbo.ExpenseCategories') IS NULL
CREATE TABLE dbo.ExpenseCategories (Id int IDENTITY PRIMARY KEY, Name nvarchar(50) NOT NULL UNIQUE);
IF OBJECT_ID('dbo.Expenses') IS NULL
CREATE TABLE dbo.Expenses (
 Id int IDENTITY PRIMARY KEY,
 CategoryId int NOT NULL REFERENCES dbo.ExpenseCategories(Id),
 UserId int NOT NULL REFERENCES dbo.Users(Id),
 Amount decimal(10,2) NOT NULL CHECK (Amount > 0),
 [Date] date NOT NULL, Comment nvarchar(200) NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Expenses_Date' AND object_id=OBJECT_ID('dbo.Expenses'))
CREATE INDEX IX_Expenses_Date ON dbo.Expenses([Date]) INCLUDE (CategoryId, Amount, UserId);
INSERT dbo.Roles(Name)
SELECT Name FROM (VALUES (N'Кассир'),(N'Управляющий'),(N'Администратор')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Name=v.Name);
INSERT dbo.ExpenseCategories(Name)
SELECT Name FROM (VALUES (N'Аренда'),(N'Продукты'),(N'Коммунальные'),(N'Прочее')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpenseCategories c WHERE c.Name=v.Name);
COMMIT;
GO

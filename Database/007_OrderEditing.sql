USE [CafeRevenue];
GO
SET XACT_ABORT ON;
IF COL_LENGTH('dbo.Orders','Version') IS NULL
 ALTER TABLE dbo.Orders ADD Version rowversion;
GO

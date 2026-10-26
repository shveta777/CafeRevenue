USE master;
GO
IF DB_ID(N'CafeRevenue') IS NULL
    CREATE DATABASE [CafeRevenue];
GO
USE [CafeRevenue];
GO
SELECT CAST(@@SERVERNAME AS nvarchar(128)) AS RegisteredServerName,
    CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) AS MachineName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40)) AS Version,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
    DB_NAME() AS DatabaseName;
GO

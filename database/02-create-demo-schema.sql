/*
    02-create-demo-schema.sql
    Creates the demo schema in DemoTargetDb.

        Customers
           |  \
     Orders   Addresses
        |  \
  OrderItems  Payments

    Safe to run repeatedly (drops and recreates the demo tables).
*/

USE [DemoTargetDb];
GO

IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL DROP TABLE dbo.Payments;
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID(N'dbo.Addresses', N'U') IS NOT NULL DROP TABLE dbo.Addresses;
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DROP TABLE dbo.Customers;
GO

CREATE TABLE dbo.Customers
(
    CustomerId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    FirstName    NVARCHAR(50)  NOT NULL,
    LastName     NVARCHAR(50)  NOT NULL,
    Email        VARCHAR(100)  NULL,          -- intentionally nullable for NULL-count demos
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.Addresses
(
    AddressId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Addresses PRIMARY KEY,
    CustomerId   INT NOT NULL,
    Line1        NVARCHAR(120) NOT NULL,
    City         NVARCHAR(80)  NOT NULL,
    PostalCode   VARCHAR(20)   NOT NULL,
    CONSTRAINT FK_Addresses_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId)
);
GO

CREATE TABLE dbo.Orders
(
    OrderId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId   INT NOT NULL,
    OrderDate    DATETIME2(0) NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    Status       VARCHAR(20)  NOT NULL CONSTRAINT DF_Orders_Status DEFAULT 'Pending',
    Total        DECIMAL(10,2) NOT NULL CONSTRAINT DF_Orders_Total DEFAULT 0,
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId)
);
GO

CREATE TABLE dbo.OrderItems
(
    OrderItemId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId      INT NOT NULL,
    ProductName  NVARCHAR(120) NOT NULL,
    Quantity     INT NOT NULL CONSTRAINT DF_OrderItems_Quantity DEFAULT 1,
    UnitPrice    DECIMAL(10,2) NOT NULL,
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId)
);
GO

CREATE TABLE dbo.Payments
(
    PaymentId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    OrderId      INT NOT NULL,
    Amount       DECIMAL(10,2) NOT NULL,
    Method       VARCHAR(30)  NOT NULL,
    PaidAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_PaidAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId)
);
GO

CREATE INDEX IX_Orders_CustomerId ON dbo.Orders (CustomerId);
CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems (OrderId);
CREATE INDEX IX_Payments_OrderId ON dbo.Payments (OrderId);
GO

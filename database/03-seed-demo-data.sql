/*
    03-seed-demo-data.sql
    Seeds DemoTargetDb with fictional data designed to demonstrate risk detection:

      - Some Customers.Email values are NULL          (NULL -> NOT NULL demo)
      - Some Email values exceed 50 characters        (column narrowing demo)
      - Orders / OrderItems / Payments reference parents via foreign keys (dependency demo)
      - Enough rows to demonstrate populated-table checks

    Contains only fictional data. Safe to run repeatedly (clears existing rows first).
*/

USE [DemoTargetDb];
GO

DELETE FROM dbo.Payments;
DELETE FROM dbo.OrderItems;
DELETE FROM dbo.Orders;
DELETE FROM dbo.Addresses;
DELETE FROM dbo.Customers;
GO

SET IDENTITY_INSERT dbo.Customers ON;
INSERT INTO dbo.Customers (CustomerId, FirstName, LastName, Email) VALUES
    (1,  N'Ava',      N'Nguyen',    'ava.nguyen@example.com'),
    (2,  N'Liam',     N'Patel',     NULL),
    (3,  N'Mia',      N'Johansson',  'mia.johansson.longaddress.for.narrowing.demo@example-company.com'),
    (4,  N'Noah',     N'Garcia',    'noah.garcia@example.com'),
    (5,  N'Emma',     N'Okafor',    NULL),
    (6,  N'Oliver',   N'Rossi',     'oliver.rossi@example.com'),
    (7,  N'Sophia',   N'Andersson', 'sophia.andersson.verylongemail.address.demo@example-organisation.com'),
    (8,  N'Lucas',    N'Kim',       'lucas.kim@example.com'),
    (9,  N'Isabella', N'Silva',     NULL),
    (10, N'Ethan',    N'Novak',     'ethan.novak@example.com'),
    (11, N'Amelia',   N'Costa',     'amelia.costa@example.com'),
    (12, N'James',    N'Wagner',    'james.wagner.extended.demo.address@example-enterprise-domain.com'),
    (13, N'Charlotte',N'Meyer',     'charlotte.meyer@example.com'),
    (14, N'Benjamin', N'Larsen',    NULL),
    (15, N'Harper',   N'Dubois',    'harper.dubois@example.com');
SET IDENTITY_INSERT dbo.Customers OFF;
GO

INSERT INTO dbo.Addresses (CustomerId, Line1, City, PostalCode) VALUES
    (1,  N'12 Maple Street',   N'Springfield', '10001'),
    (3,  N'8 Harbour Road',    N'Rivertown',   '20014'),
    (4,  N'44 Oak Avenue',     N'Lakeside',    '30022'),
    (6,  N'5 Birch Lane',      N'Hillcrest',   '40033'),
    (8,  N'19 Cedar Court',    N'Fairview',    '50044'),
    (11, N'77 Elm Boulevard',  N'Greenwood',   '60055'),
    (13, N'3 Willow Close',    N'Ashford',     '70066');
GO

INSERT INTO dbo.Orders (CustomerId, Status, Total) VALUES
    (1,  'Shipped',   129.90),
    (1,  'Pending',    54.00),
    (3,  'Shipped',   310.25),
    (4,  'Cancelled',  12.50),
    (6,  'Shipped',    88.75),
    (7,  'Pending',   205.00),
    (8,  'Shipped',    45.30),
    (10, 'Shipped',   150.00),
    (11, 'Pending',    99.99),
    (13, 'Shipped',   270.40);
GO

INSERT INTO dbo.OrderItems (OrderId, ProductName, Quantity, UnitPrice) VALUES
    (1, N'Wireless Mouse',      1, 29.90),
    (1, N'USB-C Cable',         2, 50.00),
    (2, N'Notebook',            3, 18.00),
    (3, N'Mechanical Keyboard', 1, 110.25),
    (3, N'Monitor Stand',       2, 100.00),
    (5, N'Desk Lamp',           1, 88.75),
    (6, N'Webcam',              1, 205.00),
    (7, N'HDMI Adapter',        1, 45.30),
    (8, N'Laptop Sleeve',       2, 75.00),
    (9, N'Water Bottle',        1, 99.99),
    (10, N'Office Chair',       1, 270.40);
GO

INSERT INTO dbo.Payments (OrderId, Amount, Method) VALUES
    (1, 129.90, 'Card'),
    (3, 310.25, 'Card'),
    (5,  88.75, 'PayPal'),
    (7,  45.30, 'Card'),
    (8, 150.00, 'BankTransfer'),
    (10,270.40, 'Card');
GO

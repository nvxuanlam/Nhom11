IF DB_ID(N'TechShopDB') IS NOT NULL
BEGIN
    ALTER DATABASE TechShopDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TechShopDB;
END
GO

CREATE DATABASE TechShopDB;
GO

USE TechShopDB;
GO

CREATE TABLE Accounts(
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(30) NOT NULL DEFAULT 'customer',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Products(
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NOT NULL,
    Price DECIMAL(18,0) NOT NULL CHECK(Price > 0),
    Stock INT NOT NULL CHECK(Stock >= 0),
    ImageUrl NVARCHAR(MAX) NOT NULL,
    SpecsJson NVARCHAR(MAX) NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Orders(
    Id INT IDENTITY PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL UNIQUE,
    CustomerName NVARCHAR(100) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    Address NVARCHAR(255) NOT NULL,
    Note NVARCHAR(MAX) NULL,
    CustomerEmail NVARCHAR(150) NULL,
    Total DECIMAL(18,0) NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT N'Mới',
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE OrderItems(
    Id INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL CHECK(Quantity > 0),
    UnitPrice DECIMAL(18,0) NOT NULL,
    FOREIGN KEY(OrderId) REFERENCES Orders(Id),
    FOREIGN KEY(ProductId) REFERENCES Products(Id)
);

CREATE TABLE StockHistory(
    Id INT IDENTITY PRIMARY KEY,
    ProductId INT NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    OldStock INT NOT NULL,
    NewStock INT NOT NULL,
    ChangedBy NVARCHAR(100) NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE OrderStatusHistory(
    Id INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    FromStatus NVARCHAR(50) NULL,
    ToStatus NVARCHAR(50) NOT NULL,
    ChangedBy NVARCHAR(100) NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY(OrderId) REFERENCES Orders(Id)
);
GO

INSERT INTO Accounts(Name,Email,PasswordHash,Role) VALUES
(N'Admin',N'admin@techshop.vn',N'Admin123',N'admin'),
(N'Khách Hàng Demo',N'user@test.vn',N'User1234',N'customer');
GO

INSERT INTO Products(Name,Description,Category,Price,Stock,ImageUrl,SpecsJson) VALUES
(N'iPhone 15 Pro Max 256GB',N'Điện thoại cao cấp Apple',N'Điện thoại',29990000,12,N'/images/iphone15.jpg',N'{"CPU":"Apple A17 Pro 6-core","RAM":"8 GB","Bộ nhớ":"256 GB","Màn hình":"6.7 Super Retina XDR"}'),

(N'Samsung Galaxy S24 Ultra',N'Flagship Samsung màn hình lớn',N'Điện thoại',31990000,5,N'/images/samsung-s24.jpg',N'{"CPU":"Snapdragon 8 Gen 3","RAM":"12 GB","Bộ nhớ":"256 GB","Màn hình":"6.8 Dynamic AMOLED 2X"}'),

(N'Xiaomi 14 Ultra',N'Điện thoại camera Leica cao cấp',N'Điện thoại',26990000,7,N'/images/xiaomi-14-ultra.jpg',N'{"CPU":"Snapdragon 8 Gen 3","RAM":"16 GB","Bộ nhớ":"512 GB","Màn hình":"6.73 AMOLED"}'),

(N'OPPO Find X7 Ultra',N'Điện thoại flagship OPPO',N'Điện thoại',28490000,6,N'/images/oppo-find-x7.jpg',N'{"CPU":"Snapdragon 8 Gen 3","RAM":"16 GB","Bộ nhớ":"512 GB","Màn hình":"6.82 AMOLED"}'),

(N'MacBook Air M3 8GB/256GB',N'Laptop mỏng nhẹ dùng chip Apple M3',N'Laptop',27490000,8,N'/images/macbook-air-m3.jpg',N'{"CPU":"Apple M3 8-core","RAM":"8 GB Unified","Ổ cứng":"256 GB SSD","Màn hình":"13.6 Liquid Retina"}'),

(N'Dell XPS 15 9530',N'Laptop cao cấp cho công việc và đồ họa',N'Laptop',45990000,3,N'/images/dell-xps15.jpg',N'{"CPU":"Intel Core i7-13700H","RAM":"16 GB DDR5","Ổ cứng":"512 GB SSD","Màn hình":"15.6 OLED"}'),

(N'ASUS ROG Strix G16',N'Laptop gaming hiệu năng cao',N'Laptop',38990000,9,N'/images/asus-rog.jpg',N'{"CPU":"Intel Core i9","RAM":"16 GB","Ổ cứng":"1 TB SSD","Màn hình":"16 240Hz"}'),

(N'Lenovo Legion 5',N'Laptop gaming Legion',N'Laptop',32990000,10,N'/images/lenovo-legion-5.jpg',N'{"CPU":"Ryzen 7","RAM":"16 GB","Ổ cứng":"512 GB SSD","Màn hình":"15.6 165Hz"}'),

(N'iPad Pro M4 11 WiFi',N'Máy tính bảng hiệu năng cao',N'Máy tính bảng',22990000,0,N'/images/ipad-pro-m4.jpg',N'{"CPU":"Apple M4","RAM":"8 GB","Bộ nhớ":"256 GB","Màn hình":"11 Liquid Retina XDR"}'),

(N'Samsung Galaxy Tab S9',N'Máy tính bảng Android cao cấp',N'Máy tính bảng',18990000,7,N'/images/samsung-tab-s9.jpg',N'{"CPU":"Snapdragon 8 Gen 2","RAM":"8 GB","Bộ nhớ":"128 GB","Màn hình":"11 Dynamic AMOLED"}'),

(N'Xiaomi Pad 6',N'Máy tính bảng Xiaomi hiệu năng cao',N'Máy tính bảng',10990000,15,N'/images/xiaomi-pad-6.jpg',N'{"CPU":"Snapdragon 870","RAM":"8 GB","Bộ nhớ":"256 GB","Màn hình":"11 144Hz"}'),

(N'AirPods Pro thế hệ 2',N'Tai nghe chống ồn chủ động',N'Phụ kiện',6490000,20,N'/images/airpods-pro.jpg',N'{"Kết nối":"Bluetooth 5.3","Chip":"Apple H2","Pin":"6h","Chống nước":"IPX4"}'),

(N'Sony WH-1000XM5',N'Tai nghe chống ồn cao cấp',N'Phụ kiện',8490000,15,N'/images/sony-wh1000xm5.jpg',N'{"Kết nối":"Bluetooth 5.2","Pin":"30h","Chống ồn":"Adaptive ANC"}'),

(N'Logitech MX Master 3S',N'Chuột văn phòng cao cấp',N'Phụ kiện',2490000,25,N'/images/logitech-mx-3s.jpg',N'{"Kết nối":"Bluetooth + Receiver","Pin":"70 ngày","DPI":"8000"}'),

(N'Keychron K8 Pro',N'Bàn phím cơ không dây',N'Phụ kiện',2890000,18,N'/images/keychron-k8.jpg',N'{"Switch":"Gateron","Kết nối":"Bluetooth/Wired","Pin":"4000mAh"}');
GO
-- Reference from supplied script. Do not run against an existing database.
-- After creating a fresh database, run database-demo.sql for hashed test accounts.
-- Also apply audit-log.sql and notification-reads.sql to the selected database.
-- For an existing database do not recreate the schema; see README.md.
CREATE DATABASE ozvs_kursach;
GO

USE ozvs_kursach;
GO

CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL
);
GO

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NULL,
    Login NVARCHAR(100) NOT NULL UNIQUE,
    Password NVARCHAR(256) NOT NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
GO

CREATE TABLE Drivers (
    DriverId INT IDENTITY(1,1) PRIMARY KEY,
    LastName NVARCHAR(100) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    MiddleName NVARCHAR(100) NULL,
    Phone NVARCHAR(20) NULL,
    LicenseNumber NVARCHAR(50) NOT NULL UNIQUE,
    LicenseCategory NVARCHAR(20) NOT NULL,
    LicenseIssueDate DATE NOT NULL,
    LicenseExpiryDate DATE NOT NULL
);
GO

CREATE TABLE Vehicles (
    VehicleId INT IDENTITY(1,1) PRIMARY KEY,
    LicensePlate NVARCHAR(20) NOT NULL UNIQUE,
    Brand NVARCHAR(100) NOT NULL,
    Model NVARCHAR(100) NOT NULL,
    YearOfManufacture INT NOT NULL,
    Mileage INT NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT N'В эксплуатации',
    VinNumber NVARCHAR(50) NULL UNIQUE
);
GO

CREATE TABLE VehicleAssignments (
    AssignmentId INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    DriverId INT NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NULL,
    CONSTRAINT FK_VehicleAssignments_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT FK_VehicleAssignments_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(DriverId)
);
GO

CREATE TABLE Documents (
    DocumentId INT IDENTITY(1,1) PRIMARY KEY,
    DocumentType NVARCHAR(50) NOT NULL,
    DocumentNumber NVARCHAR(100) NOT NULL,
    IssueDate DATE NOT NULL,
    ExpiryDate DATE NULL,
    IssuingOrganization NVARCHAR(200) NULL,
    VehicleId INT NULL,
    DriverId INT NULL,
    Cost DECIMAL(12,2) NULL,
    CONSTRAINT FK_Documents_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT FK_Documents_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(DriverId),
    CONSTRAINT CK_DocumentType CHECK (DocumentType IN (N'ОСАГО', N'КАСКО', N'Водительское удостоверение', N'Паспорт ТС'))
);
GO

CREATE TABLE MaintenanceRepairs (
    WorkId INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    WorkDate DATE NOT NULL,
    WorkType NVARCHAR(200) NOT NULL,
    Mileage INT NOT NULL,
    Cost DECIMAL(12,2) NOT NULL,
    Description NVARCHAR(1000) NULL,
    NextMaintenanceDate DATE NULL,
    NextMaintenanceMileage INT NULL,
    CONSTRAINT FK_MaintenanceRepairs_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId)
);
GO

CREATE TABLE Parts (
    PartId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    ArticleNumber NVARCHAR(100) NOT NULL UNIQUE,
    Cost DECIMAL(12,2) NOT NULL,
    Quantity INT NOT NULL DEFAULT 0,
    Category NVARCHAR(100) NULL
);
GO

CREATE TABLE WorkParts (
    CompositionId INT IDENTITY(1,1) PRIMARY KEY,
    WorkId INT NOT NULL,
    PartId INT NOT NULL,
    Quantity INT NOT NULL,
    CONSTRAINT FK_WorkParts_MaintenanceRepairs FOREIGN KEY (WorkId) REFERENCES MaintenanceRepairs(WorkId),
    CONSTRAINT FK_WorkParts_Parts FOREIGN KEY (PartId) REFERENCES Parts(PartId)
);
GO

CREATE TABLE Notifications (
    NotificationId INT IDENTITY(1,1) PRIMARY KEY,
    NotificationType NVARCHAR(50) NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    RelatedEntityType NVARCHAR(50) NOT NULL,
    RelatedEntityId INT NOT NULL,
    CriticalDate DATE NOT NULL,
    DaysBefore INT NOT NULL DEFAULT 30,
    IsRead BIT NOT NULL DEFAULT 0,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    UserId INT NULL,
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT CK_NotificationType CHECK (NotificationType IN (N'Окончание страховки', N'Плановое ТО', N'Окончание водительского удостоверения', N'Окончание паспорта ТС'))
);
GO



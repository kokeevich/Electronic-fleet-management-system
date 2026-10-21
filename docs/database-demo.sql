-- Только для учебной/тестовой БД. Существующие записи не перезаписываются.
-- Выполняйте отдельно от database-schema.sql, если база уже создана.
USE ozvs_kursach;
GO
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Roles TABLE (Name nvarchar(100));
    INSERT INTO @Roles VALUES (N'Администратор'), (N'Диспетчер'), (N'Механик'), (N'Наблюдатель');

    INSERT INTO dbo.Roles (Name, Description)
    SELECT r.Name, N'Роль учебной системы автопарка'
    FROM @Roles r WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles x WHERE x.Name = r.Name);

    IF EXISTS (SELECT r.Name FROM dbo.Roles r JOIN @Roles t ON t.Name = r.Name
               GROUP BY r.Name HAVING COUNT(*) <> 1)
        THROW 50001, N'Имена ролей не уникальны. Устраните дубли перед добавлением тестовых пользователей.', 1;

    DECLARE @Users TABLE (Login nvarchar(100), RoleName nvarchar(100), PasswordHash nvarchar(256));
    INSERT INTO @Users VALUES
    (N'fleet_admin', N'Администратор', N'PBKDF2-SHA256$1$600000$HrGX8eKrGcuAggf0Vkt/ig==$vkaUwtQuwAJrBqD7QocHYJQlej1RTDdAARWFvlCD1hA='),
    (N'fleet_dispatcher', N'Диспетчер', N'PBKDF2-SHA256$1$600000$uAT+5XczgXkn8B7URwS83A==$b8JWHj8H6+Edi/VwWmxz3DEz7saj8i8uBUxivkMh5oY='),
    (N'fleet_mechanic', N'Механик', N'PBKDF2-SHA256$1$600000$uG23NKjQztRJmZ1qFV5tEw==$wfkxv18eDr8+ictO1L0dwIVbx8IBND8YpiXWCjinfxU='),
    (N'fleet_viewer', N'Наблюдатель', N'PBKDF2-SHA256$1$600000$rwW53Vg5fEDlHSPLzL9f0w==$NS9gJ7hcNDwLJlQUY7Rudu7d9xS+INTmL86rxpncm3g=');

    INSERT INTO dbo.Users (RoleId, LastName, FirstName, MiddleName, Login, Password)
    SELECT r.RoleId, N'Тестовый', u.RoleName, NULL, u.Login, u.PasswordHash
    FROM @Users u JOIN dbo.Roles r ON r.Name = u.RoleName
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Users x WHERE x.Login = u.Login);

    DECLARE @Vehicles TABLE (Plate nvarchar(20), Brand nvarchar(100), Model nvarchar(100),
                            Year int, Mileage int, Status nvarchar(50), Vin nvarchar(50));
    INSERT INTO @Vehicles VALUES
    (N'Т101АК196', N'Hyundai', N'Solaris', 2021, 98000, N'В эксплуатации', N'Z94K241CBMR000101'),
    (N'Т102АК196', N'Kia', N'Rio', 2022, 64000, N'В ремонте', N'Z94C241BBNR000102'),
    (N'Т103АК196', N'Skoda', N'Rapid', 2020, 152000, N'Не используется', N'XW8AT2NH0LK000103');

    INSERT INTO dbo.Vehicles (LicensePlate, Brand, Model, YearOfManufacture, Mileage, Status, VinNumber)
    SELECT v.Plate, v.Brand, v.Model, v.Year, v.Mileage, v.Status, v.Vin FROM @Vehicles v
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Vehicles x WHERE x.LicensePlate = v.Plate OR x.VinNumber = v.Vin);

    COMMIT TRANSACTION;
    SELECT u.Login, r.Name AS RoleName FROM dbo.Users u
    JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE u.Login IN (N'fleet_admin', N'fleet_dispatcher', N'fleet_mechanic', N'fleet_viewer');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
GO

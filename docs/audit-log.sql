/*
   Запустить один раз в существующей базе ozvs_kursach до обновления приложения.
   Скрипт только добавляет таблицу и индексы; существующие данные не меняет.
*/
IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        AuditLogId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
        ActorUserId INT NOT NULL,
        ActorName NVARCHAR(250) NOT NULL,
        ChangedAt DATETIME2(0) NOT NULL,
        Action NVARCHAR(20) NOT NULL,
        EntityName NVARCHAR(50) NOT NULL,
        EntityId INT NOT NULL,
        BeforeData NVARCHAR(MAX) NULL,
        AfterData NVARCHAR(MAX) NULL,
        CONSTRAINT CK_AuditLog_Action CHECK (Action IN (N'Создание', N'Изменение', N'Удаление'))
    );

    CREATE INDEX IX_AuditLog_ChangedAt ON dbo.AuditLog(ChangedAt DESC);
    CREATE INDEX IX_AuditLog_ActorUserId_ChangedAt ON dbo.AuditLog(ActorUserId, ChangedAt DESC);
END;
GO

/* Пользователь приложения: чтение журнала и запись новых событий. */
-- GRANT SELECT, INSERT ON dbo.AuditLog TO [fleet_app];
-- Не выдавайте приложению UPDATE или DELETE на AuditLog.

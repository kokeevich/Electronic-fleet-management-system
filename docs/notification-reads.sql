-- Выполнить в существующей базе ozvs_kursach перед запуском версии с уведомлениями.
-- Исходная таблица Notifications и её записи сохраняются.
IF OBJECT_ID(N'dbo.NotificationReads', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationReads (
        UserId INT NOT NULL,
        EventKey NVARCHAR(200) NOT NULL,
        ReadAt DATETIME2 NOT NULL,
        CONSTRAINT PK_NotificationReads PRIMARY KEY (UserId, EventKey)
    );
END;
GO
-- При отдельном SQL-пользователе приложения выдайте SELECT, INSERT на dbo.NotificationReads.

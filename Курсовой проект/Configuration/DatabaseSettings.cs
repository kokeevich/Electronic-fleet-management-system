using System.IO;

namespace Курсовой_проект.Configuration;

public static class DatabaseSettings
{
    public static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("FLEET_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(value))
        {
            var path = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "FleetManagement", "connection.txt");
            if (File.Exists(path))
                value = File.ReadAllText(path).Trim();
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Не задано подключение к базе данных.");
        return value;
    }
}

using System.IO;

namespace Курсовой_проект.Configuration;

public static class DatabaseSettings
{
    public static string GetConnectionString()
        => Resolve(Environment.GetEnvironmentVariable("FLEET_CONNECTION_STRING"), AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string Resolve(string? environmentValue, string applicationDirectory, string localDataDirectory)
    {
        var value = environmentValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            var portablePath = Path.Combine(applicationDirectory, "connection.txt");
            if (File.Exists(portablePath)) value = File.ReadAllText(portablePath).Trim();
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            var path = Path.Combine(localDataDirectory, "FleetManagement", "connection.txt");
            if (File.Exists(path))
                value = File.ReadAllText(path).Trim();
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Не задано подключение к базе данных. Создайте connection.txt рядом с программой.");
        return value;
    }
}

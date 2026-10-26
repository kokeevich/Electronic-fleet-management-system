using System.Text.Json;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public static class AuditPresentation
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["VehicleId"] = "ID автомобиля", ["LicensePlate"] = "Госномер",
        ["Brand"] = "Марка", ["Model"] = "Модель", ["YearOfManufacture"] = "Год выпуска",
        ["Mileage"] = "Пробег, км", ["Status"] = "Статус", ["VinNumber"] = "VIN"
    };

    public static CatalogRow Present(AuditLog entry)
    {
        var before = Read(entry.BeforeData);
        var after = Read(entry.AfterData);
        var lines = new List<string>();
        if (before == null || after == null)
            lines.Add("Не удалось прочитать сохранённые подробности события.");
        else
            foreach (var key in before.Keys.Union(after.Keys))
            {
                if (key is "Password" or "PasswordConfirmation") continue;
                var oldValue = before.GetValueOrDefault(key, "");
                var newValue = after.GetValueOrDefault(key, "");
                if (entry.Action == "Изменение" && oldValue == newValue) continue;
                var label = Labels.GetValueOrDefault(key, key);
                var oldText = string.IsNullOrEmpty(oldValue) ? "Не указано" : oldValue;
                var newText = string.IsNullOrEmpty(newValue) ? "Не указано" : newValue;
                lines.Add(entry.Action switch
                {
                    "Создание" => $"{label}: {newText}",
                    "Удаление" => $"{label}: {oldText}",
                    _ => $"{label}\n    Было: {oldText}\n    Стало: {newText}"
                });
            }
        if (lines.Count == 0) lines.Add("Значения полей не изменились.");
        var utc = DateTime.SpecifyKind(entry.ChangedAt, DateTimeKind.Utc);
        return new CatalogRow($"{entry.Action}: {entry.EntityName} · #{entry.EntityId}",
            $"{entry.ActorName} (ID {entry.ActorUserId}) · {utc.ToLocalTime():dd.MM.yyyy HH:mm:ss}",
            string.Join("\n\n", lines), entry.Action, entry.AuditLogId);
    }

    private static Dictionary<string, string>? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            return document.RootElement.EnumerateObject().ToDictionary(x => x.Name,
                x => x.Value.ValueKind == JsonValueKind.Null ? "" : x.Value.ToString());
        }
        catch (JsonException) { return null; }
    }
}

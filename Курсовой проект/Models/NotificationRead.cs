namespace Курсовой_проект.Models;

// Отметки отдельны от исходных событий: предупреждения вычисляются из актуальных данных.
public sealed class NotificationRead
{
    public int UserId { get; set; }
    public string EventKey { get; set; } = "";
    public DateTime ReadAt { get; set; }
}

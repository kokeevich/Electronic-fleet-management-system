using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string RelatedEntityType { get; set; } = null!;

    public int RelatedEntityId { get; set; }

    public DateOnly CriticalDate { get; set; }

    public int DaysBefore { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? UserId { get; set; }

    public virtual User? User { get; set; }
}

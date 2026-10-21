using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class MaintenanceRepair
{
    public int WorkId { get; set; }

    public int VehicleId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string WorkType { get; set; } = null!;

    public int Mileage { get; set; }

    public decimal Cost { get; set; }

    public string? Description { get; set; }

    public DateOnly? NextMaintenanceDate { get; set; }

    public int? NextMaintenanceMileage { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<WorkPart> WorkParts { get; set; } = new List<WorkPart>();
}

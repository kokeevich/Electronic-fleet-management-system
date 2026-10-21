using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class VehicleAssignment
{
    public int AssignmentId { get; set; }

    public int VehicleId { get; set; }

    public int DriverId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public virtual Driver Driver { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}

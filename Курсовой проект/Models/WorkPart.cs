using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class WorkPart
{
    public int CompositionId { get; set; }

    public int WorkId { get; set; }

    public int PartId { get; set; }

    public int Quantity { get; set; }

    public virtual Part Part { get; set; } = null!;

    public virtual MaintenanceRepair Work { get; set; } = null!;
}

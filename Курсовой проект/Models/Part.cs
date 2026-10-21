using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class Part
{
    public int PartId { get; set; }

    public string Name { get; set; } = null!;

    public string ArticleNumber { get; set; } = null!;

    public decimal Cost { get; set; }

    public int Quantity { get; set; }

    public string? Category { get; set; }

    public virtual ICollection<WorkPart> WorkParts { get; set; } = new List<WorkPart>();
}

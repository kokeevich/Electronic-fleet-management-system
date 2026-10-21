using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class Document
{
    public int DocumentId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string DocumentNumber { get; set; } = null!;

    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? IssuingOrganization { get; set; }

    public int? VehicleId { get; set; }

    public int? DriverId { get; set; }

    public decimal? Cost { get; set; }

    public virtual Driver? Driver { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}

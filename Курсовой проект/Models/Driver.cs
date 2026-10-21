using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class Driver
{
    public int DriverId { get; set; }

    public string LastName { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? Phone { get; set; }

    public string LicenseNumber { get; set; } = null!;

    public string LicenseCategory { get; set; } = null!;

    public DateOnly LicenseIssueDate { get; set; }

    public DateOnly LicenseExpiryDate { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
}

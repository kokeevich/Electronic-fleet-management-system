using System;
using System.Collections.Generic;

namespace Курсовой_проект.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string Brand { get; set; } = null!;

    public string Model { get; set; } = null!;

    public int YearOfManufacture { get; set; }

    public int Mileage { get; set; }

    public string Status { get; set; } = null!;

    public string? VinNumber { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<MaintenanceRepair> MaintenanceRepairs { get; set; } = new List<MaintenanceRepair>();

    public virtual ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
}

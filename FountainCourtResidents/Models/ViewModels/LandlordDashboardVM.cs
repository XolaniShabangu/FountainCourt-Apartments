using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LandlordDashboardVM
    {
        public int TotalUnits { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalOccupied { get; set; }

        public List<RoomTypeSummary> Types { get; set; } = new List<RoomTypeSummary>();
        // NEW: Repairmen summary
        public List<RepairmanSummaryVM> Repairmen { get; set; } = new List<RepairmanSummaryVM>();
        public MaintenanceOverviewVM Maintenance { get; set; } = new MaintenanceOverviewVM();
    }

    public class RoomTypeSummary
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Total { get; set; }
        public int Available { get; set; }
        public int Occupied => Total - Available;
    }

    public class RepairmanSummaryVM
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public double? Rating { get; set; }
        public bool IsActive { get; set; }
    }
}
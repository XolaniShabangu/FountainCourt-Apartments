using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FountainCourtResidents.Models;

namespace FountainCourtResidents.Models.ViewModels
{
    public class MaintenanceOverviewVM
    {
        public int Open { get; set; }
        public int InProgress { get; set; }
        public int Closed { get; set; }

        // For a mini chart on the dashboard (last 6 months)
        public List<MaintenanceMonthPoint> Series { get; set; } = new List<MaintenanceMonthPoint>();
    }

    public class MaintenanceMonthPoint
    {
        public string Label { get; set; }       // e.g. "Jan 2025"
        public int Opened { get; set; }         // tickets created that month
        public int Resolved { get; set; }       // tickets closed that month
    }

    // Landlord Maintenance index page (full list)
    public class AdminMaintenanceVM
    {
        public List<AdminMaintenanceListItemVM> Items { get; set; } = new List<AdminMaintenanceListItemVM>();
        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int ClosedCount { get; set; }
    }

    public class AdminMaintenanceListItemVM
    {
        public int Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Title { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }

        public string TenantName { get; set; }
        public string TenantEmail { get; set; }
        public string TenantPhone { get; set; }

        public string Unit { get; set; }
        public string AssignedTo { get; set; }

        public int? Rating { get; set; }           // 1..5, null if not rated
    }
}
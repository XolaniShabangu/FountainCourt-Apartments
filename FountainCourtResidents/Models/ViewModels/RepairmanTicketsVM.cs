using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    // ViewModels/RepairmanTicketsVM.cs
    public class RepairmanTicketsVM
    {
        public string FullName { get; set; }
        public List<RepairmanTicketRowVM> Items { get; set; } = new List<RepairmanTicketRowVM>();

        // NEW
        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int ClosedCount { get; set; }
        public double? Rating { get; set; }   // average star rating (nullable)
    }


    public class RepairmanTicketRowVM
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string DescriptionShort { get; set; }
        public MaintenancePriority Priority { get; set; }
        public MaintenanceStatus Status { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }

        public string TenantName { get; set; }     // from application
        public string TenantEmail { get; set; }
        public string TenantPhone { get; set; }

        public string UnitLabel { get; set; }      // e.g., "Studio-1"
    }
}
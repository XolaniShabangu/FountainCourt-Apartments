using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class TenantDashboardVM
    {
        // Notices (future)
        public IList<string> Notices { get; set; } = new List<string>();

        // Lease/application
        public string FullName { get; set; }
        public string UnitType { get; set; }
        public decimal? PricePerMonth { get; set; }
        public int? SquareMeters { get; set; }

        // Rent summary
        public RentSummaryVM Rent { get; set; } = new RentSummaryVM();

        // Recent payments
        public IList<PaymentHistoryItemVM> RecentPayments { get; set; } = new List<PaymentHistoryItemVM>();

        // Maintenance (quick glance)
        public MaintenanceQuickVM Maintenance { get; set; } = new MaintenanceQuickVM();

        public string UnitNumber { get; set; }        // from Room.Number  <-- NEW
        public int? RoomId { get; set; }              // optional, if you want links
        
    }

    public class RentSummaryVM
    {
        public string NextDueLabel { get; set; }   // e.g. "September 2025"
        public int NextDueYear { get; set; }
        public int NextDueMonth { get; set; }      // 1-12
        public bool IsPaidForCurrentMonth { get; set; }
        public decimal? MonthlyAmount { get; set; }
        public int ApplicationId { get; set; }
    }

    public class PaymentHistoryItemVM
    {
        public string Period { get; set; }     // e.g. "Aug 2025"
        public decimal Amount { get; set; }
        public string Status { get; set; }     // Paid / Pending / Failed
        public DateTime CreatedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string Reference { get; set; }
    }

    public class MaintenanceQuickVM
    {
        public int OpenCount { get; set; }
        public int InProgressCount { get; set; }
        public int ClosedCount { get; set; }
    }
}
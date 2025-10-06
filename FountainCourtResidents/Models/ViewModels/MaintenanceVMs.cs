using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class MaintenanceCreateVM
    {
        [Required, StringLength(120)]
        public string Title { get; set; }

        [Required, StringLength(2000)]
        public string Description { get; set; }

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;
    }

    public class MaintenanceListItemVM
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public MaintenanceStatus Status { get; set; }
        public MaintenancePriority Priority { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public string AssignedTo { get; set; } // repairman name (optional)

        public int? Rating { get; set; }
        public string TenantComment { get; set; }
    }

    public class TenantMaintenanceVM
    {
        public int ApplicationId { get; set; }
        public List<MaintenanceListItemVM> Items { get; set; } = new List<MaintenanceListItemVM>();
        public MaintenanceCreateVM NewTicket { get; set; } = new MaintenanceCreateVM();
    }

    public class MaintenanceFeedbackVM
    {
        public int TicketId { get; set; }
        public int Rating { get; set; }  // 1–5
        public string Comment { get; set; }
    }

}
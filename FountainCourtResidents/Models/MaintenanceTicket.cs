using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public enum MaintenanceStatus { Open = 0, InProgress = 1, Closed = 2 }
    public enum MaintenancePriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }

    public class MaintenanceTicket
    {
        public int Id { get; set; }

        // Who raised it
        [Required]
        public int ApplicationId { get; set; }
        [ForeignKey(nameof(ApplicationId))]
        public virtual RentalApplication Application { get; set; }

        // Optional: link to the exact room
        public int? RoomId { get; set; }
        [ForeignKey(nameof(RoomId))]
        public virtual Room Room { get; set; }

        // Optional: assignment to a repairman (we’ll hook this up later)
        public int? AssignedRepairmanId { get; set; }
        [ForeignKey(nameof(AssignedRepairmanId))]
        public virtual Repairman AssignedRepairman { get; set; }

        [Required, StringLength(120)]
        public string Title { get; set; }

        [Required, StringLength(2000)]
        public string Description { get; set; }

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedUtc { get; set; }
        public DateTime? ClosedUtc { get; set; }

        [StringLength(1000)]
        public string TenantComment { get; set; }

        [Range(1, 5)]
        public int? Rating { get; set; }
    }
}
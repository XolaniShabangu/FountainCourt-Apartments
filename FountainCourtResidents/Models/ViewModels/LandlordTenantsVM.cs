using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LandlordTenantsVM
    {
        public string Search { get; set; }

        public List<RoomTypeGroupVM> Groups { get; set; } = new List<RoomTypeGroupVM>();

        public int TotalRooms { get; set; }
        public int TotalOccupied { get; set; }
        public int TotalVacant => TotalRooms - TotalOccupied;
    }

    public class RoomTypeGroupVM
    {
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; }
        public int TotalUnits { get; set; }
        public int OccupiedCount { get; set; }

        public List<TenantRoomVM> Rooms { get; set; } = new List<TenantRoomVM>();
    }

    public class TenantRoomVM
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; }   // e.g. "Studio-1"
        public bool IsOccupied { get; set; }

        // If occupied
        public int? ApplicationId { get; set; }
        public string TenantName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }       // Application status text

        public List<PaymentVM> Payments { get; set; } = new List<PaymentVM>();
    }

    public class PaymentVM
    {
        public DateTime CreatedUtc { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; }
        public string Reference { get; set; }
        public int BillingYear { get; set; }
        public int BillingMonth { get; set; }
    }
}
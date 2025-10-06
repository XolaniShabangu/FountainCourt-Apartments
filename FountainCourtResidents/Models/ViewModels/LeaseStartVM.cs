using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LeaseStartVM
    {
        public int ApplicationId { get; set; }
        public string Token { get; set; }

        public string ApplicantName { get; set; }
        public string NationalId { get; set; }

        public string RoomTypeName { get; set; }
        public decimal? PricePerMonth { get; set; }
        public int? SquareMeters { get; set; }

        public string PropertyAddress { get; set; }
        public DateTime LeaseStartDate { get; set; } // default: next 1st or today—your call
        public int LeaseTermMonths { get; set; }     // e.g., 12

        public bool Signed { get; set; }
    }
}
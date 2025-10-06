using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LandlordApplicationsVM
    {
        public string Sort { get; set; }                 // "newest", "oldest", "name", "status"
        public List<ApplicationCardVM> Items { get; set; } = new List<ApplicationCardVM>();
    }

    public class ApplicationCardVM
    {
        public int Id { get; set; }

        // Applicant
        public string ApplicantName { get; set; }        // "First Last"
        public string NationalId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        // Room type info (optional if missing)
        public string RoomTypeName { get; set; }
        public decimal? PricePerMonth { get; set; }
        public int? SquareMeters { get; set; }

        // Status/timestamps
        public string Status { get; set; }               // e.g., "New", "Approved", "Rejected"
        public DateTime CreatedUtc { get; set; }

        // Short-lived SAS URLs for inline preview
        public string IdSasUrl { get; set; }             // points to ID document (image/PDF)
        public string BankSasUrl { get; set; }           // points to bank statement (PDF)
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public enum ApplicationStatus { New = 0, Screening = 1, Approved = 2, Rejected = 3, Disabled = 4 }

    public class RentalApplication
    {
        public int Id { get; set; }

        [StringLength(128)]
        public string UserId { get; set; } // may be null (anonymous apply)

        [Required, StringLength(80)]
        public string FirstName { get; set; }

        [Required, StringLength(80)]
        public string LastName { get; set; }

        [Required, StringLength(13)]
        public string NationalId { get; set; }

        [StringLength(256), EmailAddress]
        public string Email { get; set; }

        [StringLength(40)]
        public string Phone { get; set; }

        [Required]                  
        public int RoomTypeId { get; set; }
        public virtual RoomType RoomType { get; set; }

        [StringLength(256)]
        public string BankStatementPath { get; set; }

        [StringLength(256)]
        public string IdDocumentPath { get; set; } // NEW: store ID path

        public ApplicationStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }


        [StringLength(512)]
        public string Notes { get; set; }


        public string LeaseToken { get; set; }                // unique token for lease link
        public DateTime? LeaseTokenExpiresUtc { get; set; }   // optional: expiry for the link
        public DateTime? ApprovedUtc { get; set; }
        public DateTime? RejectedUtc { get; set; }



        public bool Signed { get; set; }                   // completed lease review + signature
        public DateTime? LeaseAcceptedUtc { get; set; }    // when they clicked Proceed to Pay (optional)


        public int? RoomId { get; set; }          // NEW: the specific unit
        public virtual Room Room { get; set; }     // nav prop
       

    }
}
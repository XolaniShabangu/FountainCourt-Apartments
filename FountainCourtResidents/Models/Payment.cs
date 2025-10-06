using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1,
        Cancelled = 2,
        Failed = 3
    }

    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int ApplicationId { get; set; }

        [StringLength(13)]
        public string NationalId { get; set; }

        [StringLength(256)]
        public string BuyerEmail { get; set; }

        [Column(TypeName = "money")]
        public decimal Amount { get; set; }

        [Required]
        public PaymentStatus Status { get; set; }

        [StringLength(64)]
        public string ProviderRef { get; set; } // e.g., pf_payment_id (you can fill on success)

        public DateTime CreatedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }

        // nav
        [ForeignKey(nameof(ApplicationId))]
        public virtual RentalApplication Application { get; set; }


        public int BillingYear { get; set; }
        public int BillingMonth { get; set; }

        [StringLength(100)]
        public string Reference { get; set; }

    }
}
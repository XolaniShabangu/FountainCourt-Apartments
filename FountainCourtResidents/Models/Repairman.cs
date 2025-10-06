using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public class Repairman
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string FirstName { get; set; }

        [Required, StringLength(80)]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; }

        [StringLength(40)]
        public string Phone { get; set; }

        public bool IsActive { get; set; } = true;

        // 1–5 stars (nullable until you start rating)
        [Range(1, 5)]
        public double? Rating { get; set; }

        // link to Identity user for portal login
        [StringLength(128)]
        public string UserId { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public class SiteNotice
    {
        public int Id { get; set; }

        [StringLength(120)]
        public string Title { get; set; }

        [Required, StringLength(2000)]
        public string Body { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Optional—but useful to auto-hide old notices
        public DateTime? ExpiresUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
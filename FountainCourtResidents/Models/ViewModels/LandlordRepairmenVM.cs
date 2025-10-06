using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LandlordRepairmenVM
    {
        public List<Repairman> Items { get; set; } = new List<Repairman>();
        public RepairmanCreateVM New { get; set; } = new RepairmanCreateVM();
        public bool ShowForm { get; set; }
    }

    public class RepairmanCreateVM
    {
        [Required, StringLength(80)]
        public string FirstName { get; set; }

        [Required, StringLength(80)]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; }

        [StringLength(40)]
        public string Phone { get; set; }

        public bool IsActive { get; set; } = true;

        
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc; // for SelectListItem

using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class ApplicationCreateVM
    {
        [Required, StringLength(80)]
        [Display(Name = "First name")]
        public string FirstName { get; set; }

        [Required, StringLength(80)]
        [Display(Name = "Surname")]
        public string LastName { get; set; }

        [Required]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "Enter a valid 13-digit South African ID number.")]
        [Display(Name = "ID Number")]
        public string NationalId { get; set; }

        [EmailAddress, StringLength(256)]
        public string Email { get; set; }

        [StringLength(40)]
        public string Phone { get; set; }

        // REPLACED: DesiredRoomType (string) -> SelectedRoomTypeId (FK)
        [Required(ErrorMessage = "Please choose a room type")]
        [Display(Name = "Desired Room Type")]
        public int? SelectedRoomTypeId { get; set; }

        // Items for the dropdown
        public IEnumerable<SelectListItem> RoomTypes { get; set; }

        // Set after OCR upload; posted back on final submit
        public string IdDocumentPath { get; set; }
    }
}
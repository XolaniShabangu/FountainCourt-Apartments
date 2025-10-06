using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public class RoomType
    {
        public int Id { get; set; }

        [Required, StringLength(64)]
        public string Name { get; set; }           // e.g., Studio, Bachelor, 2 bedroom

        [Range(0, 9999999)]
        public decimal PricePerMonth { get; set; } // ZAR per month

        [Range(0, 100000)]
        public int SquareMeters { get; set; }      // m²

        public bool IsActive { get; set; } = true; // For toggling availability later

        public int UnitsAvailable { get; set; }

        [Range(0, 10000)]
        public int TotalUnits { get; set; }        // HOW MANY of this type exist

        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models
{
    public class Room
    {
        public int Id { get; set; }

        // e.g. "101", "B-12", "3A"
        [Required, StringLength(20)]
        public string Number { get; set; }

        // Link to the type (Studio / 2 Bedroom, etc.)
        public int RoomTypeId { get; set; }
        public virtual RoomType RoomType { get; set; }

        // Whether this specific unit is currently assigned/occupied
        public bool IsOccupied { get; set; }

        // Optional: floor, notes, etc.
        [StringLength(100)]
        public string Floor { get; set; }

        [StringLength(200)]
        public string Notes { get; set; }

        // New: pure numeric sequence per RoomType (used to auto-increment)
        public int AutoNumber { get; set; }
    }

}
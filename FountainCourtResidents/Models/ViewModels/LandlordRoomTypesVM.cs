using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FountainCourtResidents.Models.ViewModels
{
    public class LandlordRoomTypesVM
    {
        public List<RoomType> Items { get; set; } = new List<RoomType>();

        // The form model for the "Add New" panel
        public RoomType NewRoomType { get; set; } = new RoomType { IsActive = true };

        // When true, keep the collapse open (e.g., after a validation error)
        public bool ShowForm { get; set; }
    }
}
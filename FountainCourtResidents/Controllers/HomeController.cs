using FountainCourtResidents.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FountainCourtResidents.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        public ActionResult Index()
        {
            // The five static showcase cards you already have on the page:
            var showcaseNames = new[] { "Bachelor", "Studio", "2 Bedroom", "3 Bedroom", "4 Bedroom" };

            // Pull active room types once
            var active = _db.RoomTypes
                            .AsNoTracking()
                            .Where(rt => rt.IsActive)
                            .ToList();

            // Case-insensitive lookup by Name
            var lookup = active
                .GroupBy(rt => rt.Name?.Trim() ?? "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UnitsAvailable).First(),
                              StringComparer.OrdinalIgnoreCase);

            // Build a dictionary of prices for only the showcase names
            var prices = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in showcaseNames)
            {
                if (lookup.TryGetValue(name, out var rt))
                    prices[name] = rt.PricePerMonth;
                else
                    prices[name] = null; // missing in DB => “Pricing on request”
            }

            ViewBag.Prices = prices;
            ViewBag.TotalActive = active.Count;

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
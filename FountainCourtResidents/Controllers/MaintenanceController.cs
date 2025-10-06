using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;
using System.Web.Mvc;


namespace FountainCourtResidents.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // GET: /Maintenance
        public async Task<ActionResult> Index()
        {
            var q = _db.MaintenanceTickets
                       .Include(t => t.Application)
                       .Include(t => t.Room)
                       .Include(t => t.AssignedRepairman)
                       .OrderByDescending(t => t.CreatedUtc);

            var list = await q.ToListAsync();

            var vm = new AdminMaintenanceVM
            {
                Items = list.Select(t => new AdminMaintenanceListItemVM
                {
                    Id = t.Id,
                    CreatedUtc = t.CreatedUtc,
                    Title = t.Title,
                    Priority = t.Priority,
                    Status = t.Status,
                    TenantName = t.Application == null ? "" : ((t.Application.FirstName + " " + t.Application.LastName).Trim()),
                    TenantEmail = t.Application?.Email,
                    TenantPhone = t.Application?.Phone,
                    Unit = t.Room?.Number,
                    AssignedTo = t.AssignedRepairman == null ? null : ((t.AssignedRepairman.FirstName + " " + t.AssignedRepairman.LastName).Trim()),
                    Rating = t.Rating
                }).ToList()
            };

            vm.OpenCount = vm.Items.Count(i => i.Status == MaintenanceStatus.Open);
            vm.InProgressCount = vm.Items.Count(i => i.Status == MaintenanceStatus.InProgress);
            vm.ClosedCount = vm.Items.Count(i => i.Status == MaintenanceStatus.Closed);

            return View(vm);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
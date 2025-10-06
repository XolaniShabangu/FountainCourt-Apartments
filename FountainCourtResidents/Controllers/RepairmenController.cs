using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using FountainCourtResidents.Services;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;


namespace FountainCourtResidents.Controllers
{
    public class RepairmenController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // GET: /Repairmen
        public async Task<ActionResult> Index()
        {
            var vm = new LandlordRepairmenVM
            {
                Items = await _db.Repairmen.AsNoTracking()
                          .OrderByDescending(r => r.IsActive)
                          .ThenBy(r => r.LastName).ThenBy(r => r.FirstName)
                          .ToListAsync(),
                ShowForm = false
            };
            return View(vm);
        }

        // POST: /Repairmen/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(LandlordRepairmenVM vm)
        {
            // server-side validation
            if (!ModelState.IsValid)
            {
                vm.Items = await _db.Repairmen.AsNoTracking()
                           .OrderByDescending(r => r.IsActive)
                           .ThenBy(r => r.LastName).ThenBy(r => r.FirstName)
                           .ToListAsync();
                vm.ShowForm = true;
                return View("Index", vm);
            }

            var m = vm.New;

            // duplicate checks
            if (await _db.Repairmen.AnyAsync(x => x.Email == m.Email))
            {
                ModelState.AddModelError("New.Email", "A repairman with this email already exists.");
                vm.Items = await _db.Repairmen.AsNoTracking().ToListAsync();
                vm.ShowForm = true;
                return View("Index", vm);
            }

            // Ensure role exists
            using (var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(_db)))
            {
                if (!roleManager.RoleExists("Repairman"))
                    roleManager.Create(new IdentityRole("Repairman"));
            }

            // Identity user (create or reuse)
            var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var existingUser = await userManager.FindByEmailAsync(m.Email);
            string tempPassword = null;
            ApplicationUser user;

            if (existingUser == null)
            {
                tempPassword = PasswordGenerator.Generate(12);
                user = new ApplicationUser
                {
                    UserName = m.Email,
                    Email = m.Email,
                    EmailConfirmed = true
                };
                var create = await userManager.CreateAsync(user, tempPassword);
                if (!create.Succeeded)
                {
                    foreach (var e in create.Errors) ModelState.AddModelError("", e);
                    vm.Items = await _db.Repairmen.AsNoTracking().ToListAsync();
                    vm.ShowForm = true;
                    return View("Index", vm);
                }
            }
            else
            {
                user = existingUser;
            }

            if (!await userManager.IsInRoleAsync(user.Id, "Repairman"))
                await userManager.AddToRoleAsync(user.Id, "Repairman");

            // Save Repairman
            var entity = new Repairman
            {
                FirstName = m.FirstName.Trim(),
                LastName = m.LastName.Trim(),
                Email = m.Email.Trim(),
                Phone = m.Phone?.Trim(),
                IsActive = m.IsActive,
                
                UserId = user.Id
            };
            _db.Repairmen.Add(entity);
            await _db.SaveChangesAsync();

            // Email credentials (only if we created a new account)
            if (tempPassword != null)
            {
                var loginUrl = Url.Action("Login", "Account", null, Request.Url.Scheme);
                var body = $@"
<div style='font-family:Segoe UI,Arial,sans-serif'>
  <h3>Your Fountain Court Repairman Account</h3>
  <p>Hi {HttpUtility.HtmlEncode(entity.FirstName)},</p>
  <p>We’ve created your account so you can receive/track maintenance jobs.</p>
  <p><b>Email:</b> {HttpUtility.HtmlEncode(entity.Email)}<br/>
     <b>Temporary Password:</b> {HttpUtility.HtmlEncode(tempPassword)}</p>
  <p>Please sign in and change your password:</p>
  <p><a href='{loginUrl}'>{loginUrl}</a></p>
</div>";
                try { Mailer.SendHtml(entity.Email, "Your Repairman Account", body); } catch { /*best effort*/ }
            }

            TempData["ok"] = "Repairman added.";
            return RedirectToAction("Index");
        }

        // POST: /Repairmen/ToggleActive
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleActive(int id)
        {
            var r = await _db.Repairmen.FindAsync(id);
            if (r == null) return HttpNotFound();
            r.IsActive = !r.IsActive;
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        //// GET: /Repairman/Tickets  (oldest first)
        //public ActionResult Tickets()
        //{
        //    var userId = User.Identity.GetUserId();
        //    var me = _db.Repairmen.FirstOrDefault(r => r.UserId == userId);
        //    if (me == null) return new HttpStatusCodeResult(403, "No repairman profile linked to this account.");

        //    var q = _db.MaintenanceTickets
        //               .Include(t => t.Application)
        //               .Include(t => t.Room)
        //               .Where(t => t.AssignedRepairmanId == me.Id)
        //               .OrderBy(t => t.CreatedUtc);

        //    var vm = new RepairmanTicketsVM
        //    {
        //        FullName = (me.FirstName + " " + me.LastName).Trim(),
        //        Items = q.ToList().Select(t => new RepairmanTicketRowVM
        //        {
        //            Id = t.Id,
        //            Title = t.Title,
        //            DescriptionShort = (t.Description ?? "").Length <= 160
        //                ? t.Description
        //                : (t.Description.Substring(0, 157) + "..."),
        //            Priority = t.Priority,
        //            Status = t.Status,
        //            CreatedUtc = t.CreatedUtc,
        //            UpdatedUtc = t.UpdatedUtc,
        //            TenantName = (t.Application == null) ? null : ((t.Application.FirstName + " " + t.Application.LastName).Trim()),
        //            TenantEmail = t.Application?.Email,
        //            TenantPhone = t.Application?.Phone,
        //            UnitLabel = t.Room?.Number
        //        }).ToList()
        //    };

        //    return View(vm);
        //}

        // GET: /Repairman/Tickets  (Open/InProgress oldest first, Closed grouped at bottom)
        public ActionResult Tickets()
        {
            var userId = User.Identity.GetUserId();
            var me = _db.Repairmen.FirstOrDefault(r => r.UserId == userId);
            if (me == null) return new HttpStatusCodeResult(403, "No repairman profile linked to this account.");

            // Base query for assigned tickets
            var baseQ = _db.MaintenanceTickets
                           .Include(t => t.Application)
                           .Include(t => t.Room)
                           .Where(t => t.AssignedRepairmanId == me.Id);

            // Quick stats
            var openCount = baseQ.Count(t => t.Status == MaintenanceStatus.Open);
            var inProgCount = baseQ.Count(t => t.Status == MaintenanceStatus.InProgress);
            var closedCount = baseQ.Count(t => t.Status == MaintenanceStatus.Closed);

            // Order: non-Closed first (oldest first), then Closed (oldest first)
            // The boolean condition translates in EF; false==0, true==1 → Closed group goes last.
            var q = baseQ
                .OrderBy(t => t.Status == MaintenanceStatus.Closed ? 1 : 0)
                .ThenBy(t => t.Status)       // Open(0) before InProgress(1) within the top group
                .ThenBy(t => t.CreatedUtc);  // oldest first inside each status

            var vm = new RepairmanTicketsVM
            {
                FullName = (me.FirstName + " " + me.LastName).Trim(),
                OpenCount = openCount,
                InProgressCount = inProgCount,
                ClosedCount = closedCount,
                Rating = me.Rating, // already maintained by your rating logic
                Items = q.ToList().Select(t => new RepairmanTicketRowVM
                {
                    Id = t.Id,
                    Title = t.Title,
                    DescriptionShort = string.IsNullOrWhiteSpace(t.Description)
                        ? ""
                        : (t.Description.Length <= 160 ? t.Description : (t.Description.Substring(0, 157) + "...")),
                    Priority = t.Priority,
                    Status = t.Status,
                    CreatedUtc = t.CreatedUtc,
                    UpdatedUtc = t.UpdatedUtc,
                    TenantName = t.Application == null ? null : ((t.Application.FirstName + " " + t.Application.LastName).Trim()),
                    TenantEmail = t.Application?.Email,
                    TenantPhone = t.Application?.Phone,
                    UnitLabel = t.Room?.Number
                }).ToList()
            };

            return View(vm);
        }


        // POST: /Repairman/UpdateStatus
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Repairman")]
        public ActionResult UpdateStatus(int id, MaintenanceStatus status)
        {
            var userId = User.Identity.GetUserId();

            // If somehow unauthenticated (expired cookie), send 401 for AJAX:
            if (!Request.IsAuthenticated)
                return new HttpStatusCodeResult(401);

            var me = _db.Repairmen.FirstOrDefault(r => r.UserId == userId);
            if (me == null) return Json(new { ok = false, message = "No repairman profile." });

            var ticket = _db.MaintenanceTickets.FirstOrDefault(t => t.Id == id);
            if (ticket == null) return Json(new { ok = false, message = "Ticket not found." });
            if (ticket.AssignedRepairmanId != me.Id)
                return Json(new { ok = false, message = "This ticket is not assigned to you." });

            var from = ticket.Status;
            bool okTransition =
                (from == MaintenanceStatus.Open && (status == MaintenanceStatus.InProgress || status == MaintenanceStatus.Closed)) ||
                (from == MaintenanceStatus.InProgress && status == MaintenanceStatus.Closed) ||
                (from == status);

            if (!okTransition)
                return Json(new { ok = false, message = $"Invalid transition from {from} to {status}." });

            ticket.Status = status;
            ticket.UpdatedUtc = DateTime.UtcNow;
            if (status == MaintenanceStatus.Closed && !ticket.ClosedUtc.HasValue)
                ticket.ClosedUtc = DateTime.UtcNow;

            _db.SaveChanges();
            return Json(new { ok = true, status = ticket.Status.ToString() });
        }

    }
}
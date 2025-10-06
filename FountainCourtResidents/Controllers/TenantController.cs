using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FountainCourtResidents.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // GET: Tenant/Dashboard
        public ActionResult Dashboard()
        {
            var userId = User.Identity.GetUserId();

            // Find the tenant's approved application (or latest)
            var app = _db.RentalApplications
             .Include(a => a.RoomType)
             .Include(a => a.Room)                // <-- include the assigned room
             .Where(a => a.UserId == userId)
             .OrderByDescending(a => a.CreatedUtc)
             .FirstOrDefault();

            if (app == null)
            {
                // No application linked yet – show an empty dashboard shell
                return View(new TenantDashboardVM
                {
                    Notices = { "No active lease found. If you recently paid, your account will be activated shortly." }
                });
            }

            // Payments for this application
            var paymentsQ = _db.Payments
                               .Where(p => p.ApplicationId == app.Id)
                               .OrderByDescending(p => p.CreatedUtc);

            var payments = paymentsQ.Take(12).ToList(); // recent 12
            var vm = new TenantDashboardVM
            {
                FullName = (app.FirstName + " " + app.LastName).Trim(),
                UnitType = app.RoomType?.Name,
                UnitNumber = app.Room?.Number,               // <-- NEW
                RoomId = app.RoomId,                         // <-- optional
                PricePerMonth = app.RoomType?.PricePerMonth,
                SquareMeters = app.RoomType?.SquareMeters,
            };

            // Determine next due month
            var (nextYear, nextMonth) = GetNextDue(app.Id, paymentsQ);

            var culture = CultureInfo.GetCultureInfo("en-ZA");
            var nextLabel = new DateTime(nextYear, nextMonth, 1).ToString("MMMM yyyy", culture);

            vm.Rent = new RentSummaryVM
            {
                NextDueYear = nextYear,
                NextDueMonth = nextMonth,
                NextDueLabel = nextLabel,
                MonthlyAmount = app.RoomType?.PricePerMonth,
                ApplicationId = app.Id,
                IsPaidForCurrentMonth = paymentsQ.Any(p =>
                    p.Status == PaymentStatus.Paid &&
                    ((p.BillingYear == nextYear && p.BillingMonth == nextMonth) // if columns exist
                     || (p.CompletedUtc.HasValue
                         && p.CompletedUtc.Value.Year == nextYear
                         && p.CompletedUtc.Value.Month == nextMonth)))
            };

            // Top 2 active (and not expired) global notices
            vm.Notices = _db.SiteNotices
                .Where(n => n.IsActive && (n.ExpiresUtc == null || n.ExpiresUtc > DateTime.UtcNow))
                .OrderByDescending(n => n.CreatedUtc)
                .Take(2)
                .ToList()
                .Select(n => string.IsNullOrWhiteSpace(n.Title) ? n.Body : (n.Title + ": " + n.Body))
                .ToList();


            // History list
            vm.RecentPayments = payments.Select(p => new PaymentHistoryItemVM
            {
                Period = FormatPeriod(p),
                Amount = p.Amount,
                Status = p.Status.ToString(),
                CreatedUtc = p.CreatedUtc,
                CompletedUtc = p.CompletedUtc,
                Reference = p.Reference
            }).ToList();

            // Maintenance summary (placeholder counts if you don't have a model yet)
            // If you do: query your MaintenanceTickets table filtered by tenant/application
            // Maintenance summary (real counts from DB)
            var ticketQ = _db.MaintenanceTickets.Where(t => t.ApplicationId == app.Id);

            vm.Maintenance = new MaintenanceQuickVM
            {
                OpenCount = ticketQ.Count(t => t.Status == MaintenanceStatus.Open),
                InProgressCount = ticketQ.Count(t => t.Status == MaintenanceStatus.InProgress),
                ClosedCount = ticketQ.Count(t => t.Status == MaintenanceStatus.Closed),
            };


            return View(vm);
        }

        // === Helpers ===

        // Computes the next unpaid month based on the latest paid record.
        private Tuple<int, int> GetNextDue(int appId, IQueryable<Payment> paymentsQ)
        {
            // Prefer explicit BillingYear/Month if available; otherwise infer from CompletedUtc
            var latest = paymentsQ
                .Where(p => p.Status == PaymentStatus.Paid)
                .OrderByDescending(p => p.BillingYear)
                .ThenByDescending(p => p.BillingMonth)
                .FirstOrDefault();

            if (latest != null && latest.BillingYear > 0 && latest.BillingMonth >= 1 && latest.BillingMonth <= 12)
                return NextMonth(latest.BillingYear, latest.BillingMonth);

            // Fallback: infer from latest CompletedUtc
            var latestByDate = paymentsQ
                .Where(p => p.Status == PaymentStatus.Paid && p.CompletedUtc.HasValue)
                .OrderByDescending(p => p.CompletedUtc)
                .FirstOrDefault();

            if (latestByDate != null)
                return NextMonth(latestByDate.CompletedUtc.Value.Year, latestByDate.CompletedUtc.Value.Month);

            // No payments yet: current month
            var now = DateTime.UtcNow;
            return Tuple.Create(now.Year, now.Month);
        }

        private Tuple<int, int> NextMonth(int year, int month)
        {
            if (month == 12) return Tuple.Create(year + 1, 1);
            return Tuple.Create(year, month + 1);
        }

        private string FormatPeriod(Payment p)
        {
            var culture = CultureInfo.GetCultureInfo("en-ZA");
            if (p.BillingYear > 0 && p.BillingMonth >= 1 && p.BillingMonth <= 12)
                return new DateTime(p.BillingYear, p.BillingMonth, 1).ToString("MMM yyyy", culture);

            var dt = p.CompletedUtc ?? p.CreatedUtc;
            return dt.ToString("MMM yyyy", culture);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // Stubs for the separate pages (we'll flesh these out next)
        public ActionResult Rent()
        {
            return View(); // full rent details + pay flow
        }

       


        ///Maintenence//
        // GET: Tenant/Maintenance
        public ActionResult Maintenance()
        {
            var userId = User.Identity.GetUserId();

            var app = _db.RentalApplications
                         .Include(a => a.RoomType)
                         .FirstOrDefault(a => a.UserId == userId);

            if (app == null)
            {
                return View(new TenantMaintenanceVM
                {
                    Items = new System.Collections.Generic.List<MaintenanceListItemVM>(),
                    NewTicket = new MaintenanceCreateVM()
                });
            }

            var tickets = _db.MaintenanceTickets
                             .Include(t => t.AssignedRepairman)
                             .Where(t => t.ApplicationId == app.Id)
                             .OrderByDescending(t => t.CreatedUtc)
                             .ToList();

            var vm = new TenantMaintenanceVM
            {
                ApplicationId = app.Id,
                Items = tickets.Select(t => new MaintenanceListItemVM
                {
                    Id = t.Id,
                    Title = t.Title,
                    Status = t.Status,
                    Priority = t.Priority,
                    CreatedUtc = t.CreatedUtc,
                    UpdatedUtc = t.UpdatedUtc,
                    AssignedTo = t.AssignedRepairman == null
                                 ? null
                                 : (t.AssignedRepairman.FirstName + " " + t.AssignedRepairman.LastName).Trim(),

                    // NEW:
                    Rating = t.Rating,
                    TenantComment = t.TenantComment
                }).ToList(),
                NewTicket = new MaintenanceCreateVM()
            };

            return View(vm);
        }

        

        // POST: Tenant/CreateMaintenance
        [HttpPost, ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<ActionResult> CreateMaintenance(TenantMaintenanceVM vm)
        {
            var userId = User.Identity.GetUserId();
            var app = await _db.RentalApplications
                               .FirstOrDefaultAsync(a => a.UserId == userId);

            if (app == null)
            {
                ModelState.AddModelError("", "No active application found.");
            }

            if (!ModelState.IsValid || app == null)
            {
                // rehydrate list for redisplay
                var items = await _db.MaintenanceTickets
                                     .Include(t => t.AssignedRepairman)
                                     .Where(t => app != null && t.ApplicationId == app.Id)
                                     .OrderByDescending(t => t.CreatedUtc)
                                     .ToListAsync();

                return View("Maintenance", new TenantMaintenanceVM
                {
                    ApplicationId = app?.Id ?? 0,
                    Items = items.Select(t => new MaintenanceListItemVM
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Status = t.Status,
                        Priority = t.Priority,
                        CreatedUtc = t.CreatedUtc,
                        UpdatedUtc = t.UpdatedUtc,
                        AssignedTo = t.AssignedRepairman == null ? null
                : (t.AssignedRepairman.FirstName + " " + t.AssignedRepairman.LastName).Trim(),
                        Rating = t.Rating,                // NEW
                        TenantComment = t.TenantComment   // NEW
                    }).ToList(),

                    NewTicket = vm?.NewTicket ?? new MaintenanceCreateVM()
                });
            }

            var ticket = new MaintenanceTicket
            {
                ApplicationId = app.Id,
                RoomId = app.RoomId, // keep your linkage
                Title = vm.NewTicket.Title,
                Description = vm.NewTicket.Description,
                Priority = vm.NewTicket.Priority,
                Status = MaintenanceStatus.Open,
                CreatedUtc = System.DateTime.UtcNow
            };

            _db.MaintenanceTickets.Add(ticket);

            // Auto-assign (skills-free)
            await FountainCourtResidents.Services.AutoAssignService.TryAutoAssignAsync(_db, ticket);

            await _db.SaveChangesAsync();

            TempData["ok"] = "Your maintenance request was submitted.";
            return RedirectToAction("Maintenance");
        }



        //Rate
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RateMaintenance(int ticketId, int rating, string comment)
        {
            var userId = User.Identity.GetUserId();
            var app = _db.RentalApplications.FirstOrDefault(a => a.UserId == userId);
            if (app == null) return new HttpStatusCodeResult(403, "No active application found.");

            var ticket = _db.MaintenanceTickets.FirstOrDefault(t => t.Id == ticketId && t.ApplicationId == app.Id);
            if (ticket == null) return HttpNotFound("Ticket not found.");

            if (ticket.Status != MaintenanceStatus.Closed)
                return new HttpStatusCodeResult(400, "You can only rate closed tickets.");

            // Don’t allow re-rating
            if (ticket.Rating.HasValue)
            {
                TempData["ok"] = "This job has already been rated.";
                return RedirectToAction("Maintenance");
            }

            // Save this ticket’s rating
            ticket.Rating = rating;                  // 1–5
            ticket.TenantComment = comment;          // if you still keep the column (can be null/empty)
            ticket.UpdatedUtc = DateTime.UtcNow;
            _db.SaveChanges();

            // Update the assigned repairman’s average rating (if any)
            if (ticket.AssignedRepairmanId.HasValue)
                UpdateRepairmanAggregateRating(ticket.AssignedRepairmanId.Value);

            TempData["ok"] = "Thank you for your feedback!";
            return RedirectToAction("Maintenance");
        }

        // Recomputes and persists the repairman’s average rating across *all* rated tickets.
        private void UpdateRepairmanAggregateRating(int repairmanId)
        {
            // Get all ratings for this repairman
            var ratings = _db.MaintenanceTickets
                             .Where(t => t.AssignedRepairmanId == repairmanId && t.Rating.HasValue)
                             .Select(t => (double)t.Rating.Value)
                             .ToList();

            var rep = _db.Repairmen.FirstOrDefault(r => r.Id == repairmanId);
            if (rep == null) return;

            if (ratings.Count == 0)
            {
                // No ratings yet → clear it out
                rep.Rating = null;
            }
            else
            {
                // Average to 1 decimal (e.g., 4.2)
                rep.Rating = Math.Round(ratings.Average(), 1, MidpointRounding.AwayFromZero);
            }

            _db.SaveChanges();
        }


        //Rent History Email

        [Authorize(Roles = "Tenant")]
        public ActionResult EmailRentHistory()
        {
            var userId = User.Identity.GetUserId();
            var app = _db.RentalApplications
                         .Include(a => a.RoomType)
                         .FirstOrDefault(a => a.UserId == userId);

            if (app == null || string.IsNullOrWhiteSpace(app.Email))
            {
                TempData["ok"] = "Could not find your application or email address.";
                return RedirectToAction("Dashboard");
            }

            var payments = _db.Payments
                              .Where(p => p.ApplicationId == app.Id)
                              .OrderBy(p => p.CreatedUtc)
                              .ToList();

            if (!payments.Any())
            {
                TempData["ok"] = "No payments found to send.";
                return RedirectToAction("Dashboard");
            }

            // Build simple HTML table
            var rows = string.Join("", payments.Select(p =>
                $"<tr><td>{(p.CompletedUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-")}</td>" +
                $"<td>{p.Status}</td>" +
                $"<td>R {p.Amount.ToString("N2")}</td>" +
                $"<td>{HttpUtility.HtmlEncode(p.Reference)}</td></tr>"
            ));

            var body = $@"
        <div style='font-family:Segoe UI,Arial,sans-serif'>
            <h3>Your Rent Payment History</h3>
            <p>Hello {(app.FirstName + " " + app.LastName).Trim()},</p>
            <p>Here is a record of your payments:</p>
            <table border='1' cellpadding='6' cellspacing='0' style='border-collapse:collapse;font-size:14px;'>
                <thead>
                    <tr style='background:#f0f0f0;'>
                        <th>Date</th><th>Status</th><th>Amount</th><th>Reference</th>
                    </tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
        </div>";

            FountainCourtResidents.Services.Mailer.SendHtml(app.Email, "Your Rent Payment History", body);

            TempData["ok"] = "Your rent history has been emailed to you.";
            return RedirectToAction("Dashboard");
        }



    }
}
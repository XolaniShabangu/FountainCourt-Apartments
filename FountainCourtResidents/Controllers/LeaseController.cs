using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;


namespace FountainCourtResidents.Controllers
{
    [AllowAnonymous] // link arrives via email; no login required
    public class LeaseController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // GET /Lease/Start?token=abc
        public ActionResult Start(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return HttpNotFound();

            var app = _db.RentalApplications
                         .Include(a => a.RoomType)
                         .FirstOrDefault(a => a.LeaseToken == token);

            if (app == null) return HttpNotFound("Invalid or expired link.");
            if (app.LeaseTokenExpiresUtc.HasValue && app.LeaseTokenExpiresUtc.Value < DateTime.UtcNow)
                return new HttpStatusCodeResult(410, "This link has expired.");
            if (app.Status != ApplicationStatus.Approved)
                return new HttpStatusCodeResult(403, "This application is not approved.");

            var address = ConfigurationManager.AppSettings["Property:ViewingAddress"] ?? "Fountain Court, [Address]";

            // choose a lease start date (e.g., next 1st)
            var today = DateTime.Today;
            var firstNextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);

            var vm = new LeaseStartVM
            {
                ApplicationId = app.Id,
                Token = token,
                ApplicantName = $"{app.FirstName} {app.LastName}".Trim(),
                NationalId = app.NationalId,
                RoomTypeName = app.RoomType?.Name,
                PricePerMonth = app.RoomType?.PricePerMonth,
                SquareMeters = app.RoomType?.SquareMeters,
                PropertyAddress = address,
                LeaseStartDate = firstNextMonth, // tweak if you want
                LeaseTermMonths = 12,
                Signed = app.Signed
            };

            return View(vm);
        }

        // POST /Lease/Accept
        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public ActionResult Accept(int applicationId, string token, bool signed)
        {
            if (!signed) return new HttpStatusCodeResult(400, "Please sign first.");

            var app = _db.RentalApplications.Include(a => a.RoomType)
                                            .FirstOrDefault(a => a.Id == applicationId && a.LeaseToken == token);
            if (app == null) return HttpNotFound();
            if (app.LeaseTokenExpiresUtc.HasValue && app.LeaseTokenExpiresUtc.Value < DateTime.UtcNow)
                return new HttpStatusCodeResult(410, "This link has expired.");
            if (app.Status != ApplicationStatus.Approved)
                return new HttpStatusCodeResult(403, "This application is not approved.");

            // If already signed, be idempotent: resume the payment flow rather than erroring
            if (app.Signed)
            {
                var pending = _db.Payments
                                 .Where(p => p.ApplicationId == app.Id && p.Status == PaymentStatus.Pending)
                                 .OrderByDescending(p => p.CreatedUtc)
                                 .FirstOrDefault();
                if (pending != null)
                    return RedirectToAction("Process", "Payments", new { applicationId = app.Id, resume = 1 });

                var paid = _db.Payments
                              .Where(p => p.ApplicationId == app.Id && p.Status == PaymentStatus.Paid)
                              .OrderByDescending(p => p.CompletedUtc)
                              .FirstOrDefault();
                if (paid != null)
                    return RedirectToAction("Success", "Payments", new { id = paid.Id });

                // No payment yet, start one
                return RedirectToAction("Process", "Payments", new { applicationId = app.Id });
            }

            // First-time sign
            app.Signed = true;
            app.LeaseAcceptedUtc = DateTime.UtcNow;

            // Invalidate token immediately
            app.LeaseToken = null;
            app.LeaseTokenExpiresUtc = DateTime.UtcNow;

            _db.SaveChanges();

            return RedirectToAction("Process", "Payments", new { applicationId = app.Id });
        }


        // POST /Lease/Cancel
        [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
        public ActionResult Cancel(int applicationId, string token)
        {
            var app = _db.RentalApplications.FirstOrDefault(a => a.Id == applicationId && a.LeaseToken == token);
            if (app == null) return HttpNotFound();
            // No status change here (unless you want to mark them as Withdrawn later)
            TempData["msg"] = "You cancelled the lease signing.";
            return RedirectToAction("Start", new { token }); // or Home/Index
        }

        

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
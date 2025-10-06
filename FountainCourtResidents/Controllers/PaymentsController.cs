using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FountainCourtResidents.Models;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity.EntityFramework;
 

namespace FountainCourtResidents.Controllers
{
    [AllowAnonymous] // the return/cancel may be accessed without login
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // GET /Payments/Process?applicationId=123
        //public ActionResult Process(int applicationId)
        //{
        //    var app = _db.RentalApplications
        //                 .Include(a => a.RoomType)
        //                 .FirstOrDefault(a => a.Id == applicationId);

        //    if (app == null) return HttpNotFound("Application not found.");
        //    if (!app.Signed) return new HttpStatusCodeResult(403, "Please sign the lease first.");
        //    if (app.Status != ApplicationStatus.Approved)
        //        return new HttpStatusCodeResult(403, "Application is not approved.");

        //    // Determine amount (first month rent)
        //    var amount = app.RoomType?.PricePerMonth ?? 0m;
        //    if (amount <= 0m) return new HttpStatusCodeResult(400, "Invalid amount.");

        //    // Create a Payment row (Pending)
        //    var payment = new Payment
        //    {
        //        ApplicationId = app.Id,
        //        NationalId = app.NationalId,
        //        BuyerEmail = app.Email,
        //        Amount = amount,
        //        Status = PaymentStatus.Pending,
        //        CreatedUtc = DateTime.UtcNow
        //    };
        //    _db.Payments.Add(payment);
        //    _db.SaveChanges();

        //    // Build PayFast sandbox URL (minimal fields; signatures/ITN: see TODOs below)
        //    var site = ConfigurationManager.AppSettings["PayFast:Site"];
        //    var merchantId = ConfigurationManager.AppSettings["PayFast:MerchantId"];
        //    var merchantKey = ConfigurationManager.AppSettings["PayFast:MerchantKey"];

        //    // Return/Cancel URLs (absolute)
        //    var returnUrl = Url.Action("Success", "Payments", new { id = payment.Id }, Request.Url.Scheme);
        //    var cancelUrl = Url.Action("Cancel", "Payments", new { id = payment.Id }, Request.Url.Scheme);

        //    // (Optional) notify_url for ITN server-to-server validation
        //    var notifyUrl = Url.Action("Notify", "Payments", null, Request.Url.Scheme);

        //    // PayFast wants dot-decimal; culture-invariant format with 2 decimals
        //    var amountStr = payment.Amount.ToString("F2", CultureInfo.InvariantCulture);
        //    var itemName = Uri.EscapeDataString($"Fountain Court - First Month Rent (App #{app.Id})");

        //    // Minimal working redirect (sandbox accepts this)
        //    var paymentUrl =
        //        $"{site}merchant_id={Uri.EscapeDataString(merchantId)}" +
        //        $"&merchant_key={Uri.EscapeDataString(merchantKey)}" +
        //        $"&amount={Uri.EscapeDataString(amountStr)}" +
        //        $"&item_name={itemName}" +
        //        $"&return_url={Uri.EscapeDataString(returnUrl)}" +
        //        $"&cancel_url={Uri.EscapeDataString(cancelUrl)}" +
        //        $"&notify_url={Uri.EscapeDataString(notifyUrl)}" +
        //        $"&m_payment_id={payment.Id}"; // your reference

        //    return Redirect(paymentUrl);
        //}

        public ActionResult Process(int applicationId)
        {
            var app = _db.RentalApplications
                         .Include(a => a.RoomType)
                         .FirstOrDefault(a => a.Id == applicationId);

            if (app == null) return HttpNotFound("Application not found.");
            if (!app.Signed) return new HttpStatusCodeResult(403, "Please sign the lease first.");
            if (app.Status != ApplicationStatus.Approved)
                return new HttpStatusCodeResult(403, "Application is not approved.");

            var amount = app.RoomType?.PricePerMonth ?? 0m;
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m) return new HttpStatusCodeResult(400, "Invalid amount.");

            var payment = new Payment
            {
                ApplicationId = app.Id,
                NationalId = app.NationalId,
                BuyerEmail = app.Email,
                Amount = amount,
                Status = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                Reference = "First Payment"
            };
            _db.Payments.Add(payment);
            _db.SaveChanges();

            // Config
            var site = (ConfigurationManager.AppSettings["PayFast:Site"] ?? "").Trim();
            var merchantId = (ConfigurationManager.AppSettings["PayFast:MerchantId"] ?? "").Trim();
            var merchantKey = (ConfigurationManager.AppSettings["PayFast:MerchantKey"] ?? "").Trim();

            // Fallback/normalise the base URL
            if (string.IsNullOrWhiteSpace(site))
                site = "https://sandbox.payfast.co.za/eng/process";
            // Ensure there is NO trailing '?'
            site = site.TrimEnd('?');

            // Build absolute return/cancel/notify URLs
            var returnUrl = Url.Action("Success", "Payments", new { id = payment.Id }, Request.Url.Scheme);
            var cancelUrl = Url.Action("Cancel", "Payments", new { id = payment.Id }, Request.Url.Scheme);
            var notifyUrl = Url.Action("Notify", "Payments", null, Request.Url.Scheme);

            var amountStr = payment.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            // Use a proper query builder to avoid missing '?' and stray whitespace
            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["merchant_id"] = merchantId;                           // no tabs/newlines
            qs["merchant_key"] = merchantKey;
            qs["amount"] = amountStr;
            qs["item_name"] = $"Fountain Court - First Month Rent (App #{app.Id})";
            qs["return_url"] = returnUrl;
            qs["cancel_url"] = cancelUrl;
            qs["notify_url"] = notifyUrl;
            qs["m_payment_id"] = payment.Id.ToString();

            // If you add a passphrase later, you’ll also add signature fields here.

            var paymentUrl = site + "?" + qs.ToString();

            return Redirect(paymentUrl);
        }


        // GET: /Payments/Success?id=###   (PayFast will redirect here after payment)
        public ActionResult Success(int id)
        {
            var payment = _db.Payments.Include(p => p.Application.RoomType)
                                      .FirstOrDefault(p => p.Id == id);
            if (payment == null) return HttpNotFound();

            // Mark Paid here (sandbox quick path). In production, prefer ITN.
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Paid;
                payment.CompletedUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }

            var app = payment.Application;
            if (app == null) return HttpNotFound("Application missing.");

            // Idempotent provisioning: only once when paid and not yet provisioned
            if (payment.Status == PaymentStatus.Paid)
            {
                ProvisionTenantIfNeeded(app, payment);
            }

            // Update the Success copy to tell them about credentials
            ViewBag.Amount = payment.Amount;
            ViewBag.ApplicantName = (app.FirstName + " " + app.LastName).Trim();
            ViewBag.ShowCredentialsNote = true; // let the view show the “you’ll receive an email” message

            return View();
        }

        


        private void ProvisionTenantIfNeeded(RentalApplication app, Payment payment)
        {
            var changed = false;

            // 1) Create Identity user if none linked yet
            if (string.IsNullOrEmpty(app.UserId))
            {
                var email = string.IsNullOrWhiteSpace(app.Email)
                    ? $"{app.NationalId}@example.local"
                    : app.Email.Trim();

                var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();

                // Ensure "Tenant" role exists
                using (var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(_db)))
                {
                    if (!roleManager.RoleExists("Tenant"))
                        roleManager.Create(new IdentityRole("Tenant"));
                }

                // Reuse account if email already exists
                var existing = userManager.FindByEmail(email);
                string tempPassword = null;
                ApplicationUser user;

                if (existing == null)
                {
                    tempPassword = FountainCourtResidents.Services.PasswordGenerator.Generate(12);
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };
                    var create = userManager.Create(user, tempPassword);
                    if (!create.Succeeded)
                        throw new InvalidOperationException("Could not create user: " + string.Join("; ", create.Errors));

                    userManager.AddToRole(user.Id, "Tenant");

                    // Email credentials
                    var loginUrl = Url.Action("Login", "Account", null, Request.Url.Scheme);
                    var body = $@"
                        <div style='font-family:Segoe UI,Arial,sans-serif'>
                        <h3>Your Fountain Court Account</h3>
                        <p>Hi {(app.FirstName + " " + app.LastName).Trim()},</p>
                        <p>Your tenant profile has been created.</p>
                        <p><b>Email:</b> {HttpUtility.HtmlEncode(email)}<br/>
                        <b>Temporary Password:</b> {HttpUtility.HtmlEncode(tempPassword)}</p>
                        <p>Please sign in and change your password:</p>
                        <p><a href='{loginUrl}'>{loginUrl}</a></p>
                        </div>";
                    FountainCourtResidents.Services.Mailer.SendHtml(email, "Your Fountain Court Account", body);
                }
                else
                {
                    user = existing;
                    if (!userManager.IsInRole(user.Id, "Tenant"))
                        userManager.AddToRole(user.Id, "Tenant");
                }

                app.UserId = user.Id;
                changed = true;
            }

            //// 2) Assign a concrete room ONCE (idempotent via |unit| marker)
            //var unitAssigned = (payment.ProviderRef ?? "").Contains("|unit|");
            //if (!unitAssigned)
            //{
            //    if (app.RoomTypeId <= 0)
            //        throw new InvalidOperationException("No RoomType selected on application; cannot assign unit.");

            //    using (var tx = _db.Database.BeginTransaction())
            //    {
            //        // Load the RoomType fresh inside the transaction
            //        var rt = _db.RoomTypes.Single(r => r.Id == app.RoomTypeId);

            //        // Try to get a free existing room first (order by AutoNumber)
            //        var room = _db.Rooms
            //                      .Where(r => r.RoomTypeId == rt.Id && !r.IsOccupied)
            //                      .OrderBy(r => r.AutoNumber)
            //                      .FirstOrDefault();

            //        if (room == null)
            //        {
            //            // Lazily create a room if we haven’t reached TotalUnits
            //            var existingCount = _db.Rooms.Count(r => r.RoomTypeId == rt.Id);
            //            if (existingCount < rt.TotalUnits)
            //            {
            //                var nextAuto = (_db.Rooms.Where(r => r.RoomTypeId == rt.Id)
            //                                         .Select(r => (int?)r.AutoNumber)
            //                                         .Max() ?? 0) + 1;

            //                room = new Room
            //                {
            //                    RoomTypeId = rt.Id,
            //                    AutoNumber = nextAuto,
            //                    Number = $"{(string.IsNullOrWhiteSpace(rt.Name) ? "Unit" : rt.Name)}-{nextAuto}",
            //                    IsOccupied = false
            //                };
            //                _db.Rooms.Add(room);
            //                _db.SaveChanges(); // get room.Id
            //            }
            //            else
            //            {
            //                tx.Rollback();
            //                throw new InvalidOperationException("No available units for the selected Room Type.");
            //            }
            //        }

            //        // Occupy the room and link it to the application
            //        room.IsOccupied = true;
            //        app.RoomId = room.Id;

            //        // Mark idempotency for unit assignment
            //        payment.ProviderRef = (payment.ProviderRef ?? "") + "|unit|";

            //        _db.SaveChanges();
            //        tx.Commit();
            //    }

            //    changed = true;
            //}

            // 2) Assign a concrete room ONCE (idempotent via |unit| marker)
            var unitAssigned = (payment.ProviderRef ?? "").Contains("|unit|");
            if (!unitAssigned)
            {
                if (app.RoomTypeId <= 0)
                    throw new InvalidOperationException("No RoomType selected on application; cannot assign unit.");

                using (var tx = _db.Database.BeginTransaction())
                {
                    var room = _db.Rooms
                                  .Where(r => r.RoomTypeId == app.RoomTypeId && !r.IsOccupied)
                                  .OrderBy(r => r.AutoNumber)
                                  .FirstOrDefault();

                    if (room == null)
                    {
                        tx.Rollback();
                        throw new InvalidOperationException("No available units for the selected Room Type.");
                    }

                    room.IsOccupied = true;
                    app.RoomId = room.Id;

                    payment.ProviderRef = (payment.ProviderRef ?? "") + "|unit|";

                    _db.SaveChanges();
                    tx.Commit();
                }

                // Also decrement UnitsAvailable
                if (app.RoomType != null && app.RoomType.UnitsAvailable > 0)
                {
                    app.RoomType.UnitsAvailable -= 1;
                    if (app.RoomType.UnitsAvailable < 0) app.RoomType.UnitsAvailable = 0;
                    payment.ProviderRef = (payment.ProviderRef ?? "") + "|inv|";
                }

                changed = true;
            }


            // 3) Decrement UnitsAvailable ONCE (idempotent via |inv| marker)
            var invApplied = (payment.ProviderRef ?? "").Contains("|inv|");
            if (!invApplied && app.RoomType != null)
            {
                if (app.RoomType.UnitsAvailable > 0)
                {
                    app.RoomType.UnitsAvailable -= 1;
                    if (app.RoomType.UnitsAvailable < 0) app.RoomType.UnitsAvailable = 0; // guard
                }
                payment.ProviderRef = (payment.ProviderRef ?? "") + "|inv|";
                changed = true;
            }

            // 4) Advance status if desired (optional)
            if (app.Status == ApplicationStatus.Approved)
            {
                app.Status = ApplicationStatus.Approved; // or keep as Approved if you prefer
                changed = true;
            }

            if (changed) _db.SaveChanges();
        }






        // GET: /Payments/Cancel?id=###
        public ActionResult Cancel(int id)
        {
            var payment = _db.Payments.FirstOrDefault(p => p.Id == id);
            if (payment == null) return HttpNotFound();

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Cancelled;
                payment.CompletedUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }

            return View();
        }

        

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }




        [Authorize(Roles = "Tenant")]
        public ActionResult Start(int applicationId, int year, int month)
        {
            if (year < 2000 || year > 2100 || month < 1 || month > 12)
                return new HttpStatusCodeResult(400, "Invalid billing period.");

            var userId = User.Identity.GetUserId();
            var app = _db.RentalApplications
                         .Include(a => a.RoomType)
                         .FirstOrDefault(a => a.Id == applicationId);

            if (app == null) return HttpNotFound("Application not found.");
            if (app.UserId != userId) return new HttpStatusCodeResult(403, "Not your application.");
            if (app.RoomType == null) return new HttpStatusCodeResult(400, "No room type on application.");

            decimal baseAmount = app.RoomType == null ? 0m : app.RoomType.PricePerMonth;
            var amount = Math.Round(baseAmount, 2, MidpointRounding.AwayFromZero);

            if (amount <= 0m) return new HttpStatusCodeResult(400, "Invalid amount.");

            var periodLabel = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-ZA"));
            var payment = new Payment
            {
                ApplicationId = app.Id,
                NationalId = app.NationalId,
                BuyerEmail = app.Email,
                Amount = amount,
                Status = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                BillingYear = year,
                BillingMonth = month,
                Reference = $"Rent {periodLabel}"
            };
            _db.Payments.Add(payment);
            _db.SaveChanges();

            var site = (ConfigurationManager.AppSettings["PayFast:Site"] ?? "").Trim();
            var merchantId = (ConfigurationManager.AppSettings["PayFast:MerchantId"] ?? "").Trim();
            var merchantKey = (ConfigurationManager.AppSettings["PayFast:MerchantKey"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(site)) site = "https://sandbox.payfast.co.za/eng/process";
            site = site.TrimEnd('?');

            // *** point to the dedicated endpoints ***
            var returnUrl = Url.Action("SuccessRent", "Payments", new { id = payment.Id }, Request.Url.Scheme);
            var cancelUrl = Url.Action("CancelRent", "Payments", new { id = payment.Id }, Request.Url.Scheme);
            var notifyUrl = Url.Action("Notify", "Payments", null, Request.Url.Scheme);

            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["merchant_id"] = merchantId;
            qs["merchant_key"] = merchantKey;
            qs["amount"] = payment.Amount.ToString("F2", CultureInfo.InvariantCulture);
            qs["item_name"] = $"Fountain Court - {payment.Reference} (App #{app.Id})";
            qs["return_url"] = returnUrl;
            qs["cancel_url"] = cancelUrl;
            qs["notify_url"] = notifyUrl;
            qs["m_payment_id"] = payment.Id.ToString();

            var paymentUrl = site + "?" + qs.ToString();
            return Redirect(paymentUrl);
        }

        // GET: /Payments/SuccessRent?id=###
        [Authorize(Roles = "Tenant")]
        public ActionResult SuccessRent(int id)
        {
            var payment = _db.Payments.Include(p => p.Application)
                                      .FirstOrDefault(p => p.Id == id);
            if (payment == null) return HttpNotFound();

            // Mark paid (sandbox shortcut; in prod rely on ITN)
            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Paid;
                payment.CompletedUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }

            // Safety: this endpoint is only for regular rent (BillingYear/Month must be set)
            if (payment.BillingYear <= 0 || payment.BillingMonth < 1 || payment.BillingMonth > 12)
                return new HttpStatusCodeResult(400, "Invalid rent callback.");

            TempData["ok"] = $"Payment received: {payment.Reference} (R {payment.Amount:N2}).";
            return RedirectToAction("Dashboard", "Tenant");
        }

        // GET: /Payments/CancelRent?id=###
        [Authorize(Roles = "Tenant")]
        public ActionResult CancelRent(int id)
        {
            var payment = _db.Payments.Include(p => p.Application)
                                      .FirstOrDefault(p => p.Id == id);
            if (payment == null) return HttpNotFound();

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Cancelled;
                payment.CompletedUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }

            // Only for regular rent payments
            if (payment.BillingYear <= 0 || payment.BillingMonth < 1 || payment.BillingMonth > 12)
                return new HttpStatusCodeResult(400, "Invalid rent callback.");

            TempData["err"] = "Payment cancelled.";
            return RedirectToAction("Dashboard", "Tenant");
        }

    }
}
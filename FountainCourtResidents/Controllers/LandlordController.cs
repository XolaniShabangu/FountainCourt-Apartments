using Azure.Storage;
using Azure.Storage.Sas;
using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using FountainCourtResidents.Services;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;


namespace FountainCourtResidents.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class LandlordController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        public async Task<ActionResult> Dashboard()
        {
            var types = await _db.RoomTypes
                                 .AsNoTracking()
                                 .Where(rt => rt.IsActive)
                                 .OrderBy(rt => rt.PricePerMonth)
                                 .ToListAsync();

            var repairmen = await _db.Repairmen
                                     .AsNoTracking()
                                     .OrderBy(r => r.LastName)
                                     .ToListAsync();

            // Maintenance counts
            var maintQ = _db.MaintenanceTickets.AsNoTracking();
            var openCount = await maintQ.CountAsync(t => t.Status == MaintenanceStatus.Open);
            var inProgCount = await maintQ.CountAsync(t => t.Status == MaintenanceStatus.InProgress);
            var closedCount = await maintQ.CountAsync(t => t.Status == MaintenanceStatus.Closed);

            // Last 6 months series
            var sixBack = DateTime.UtcNow.AddMonths(-5);
            var startMonth = new DateTime(sixBack.Year, sixBack.Month, 1);
            var allRecent = await maintQ
                .Where(t => t.CreatedUtc >= startMonth || (t.ClosedUtc.HasValue && t.ClosedUtc.Value >= startMonth))
                .ToListAsync();

            var series = new List<MaintenanceMonthPoint>();
            for (int i = 0; i < 6; i++)
            {
                var monthStart = startMonth.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1);

                var opened = allRecent.Count(t => t.CreatedUtc >= monthStart && t.CreatedUtc < monthEnd);
                var resolved = allRecent.Count(t => t.ClosedUtc.HasValue && t.ClosedUtc.Value >= monthStart && t.ClosedUtc.Value < monthEnd);

                series.Add(new MaintenanceMonthPoint
                {
                    Label = monthStart.ToString("MMM yyyy"),
                    Opened = opened,
                    Resolved = resolved
                });
            }

            var vm = new LandlordDashboardVM
            {
                TotalUnits = types.Sum(t => t.TotalUnits),
                TotalAvailable = types.Sum(t => t.UnitsAvailable),
                Types = types.Select(t => new RoomTypeSummary
                {
                    Id = t.Id,
                    Name = t.Name,
                    Total = t.TotalUnits,
                    Available = t.UnitsAvailable
                }).ToList(),
                Repairmen = repairmen.Select(r => new RepairmanSummaryVM
                {
                    Id = r.Id,
                    FullName = (r.FirstName + " " + r.LastName).Trim(),
                    Rating = r.Rating,
                    IsActive = r.IsActive
                }).ToList(),
                Maintenance = new MaintenanceOverviewVM
                {
                    Open = openCount,
                    InProgress = inProgCount,
                    Closed = closedCount,
                    Series = series
                }
            };
            vm.TotalOccupied = vm.TotalUnits - vm.TotalAvailable;

            return View(vm);
        }



        // GET: /Landlord (Index acts as Index+Create)
        public async Task<ActionResult> Index()
        {
            var vm = new LandlordRoomTypesVM
            {
                Items = await _db.RoomTypes
                                 .Include(rt => rt.Rooms)
                                 .AsNoTracking()
                                 .OrderBy(rt => rt.PricePerMonth)
                                 .ThenBy(rt => rt.Name)
                                 .ToListAsync(),
                NewRoomType = new RoomType { IsActive = true },
                ShowForm = false
            };
            return View(vm);
        }

        

        // POST: /Landlord/Create (posts back to same Index view)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Prefix = "NewRoomType",
            Include = "Name,PricePerMonth,SquareMeters,TotalUnits,UnitsAvailable,IsActive")] RoomType model)
        {
            // server-side guards
            if (model.TotalUnits < 0) ModelState.AddModelError(nameof(model.TotalUnits), "Total units cannot be negative.");
            if (model.UnitsAvailable < 0) ModelState.AddModelError(nameof(model.UnitsAvailable), "Units available cannot be negative.");
            if (model.UnitsAvailable > model.TotalUnits)
                ModelState.AddModelError(nameof(model.UnitsAvailable), "Units available cannot exceed total units.");

            if (!ModelState.IsValid)
            {
                var vm = new FountainCourtResidents.Models.ViewModels.LandlordRoomTypesVM
                {
                    Items = await _db.RoomTypes.AsNoTracking()
                                .OrderBy(rt => rt.PricePerMonth).ThenBy(rt => rt.Name).ToListAsync(),
                    NewRoomType = model,
                    ShowForm = true // keep the panel open to show validation errors
                };
                return View("Index", vm);
            }

            // Default UnitsAvailable to TotalUnits if omitted (0) but TotalUnits > 0
            if (model.TotalUnits > 0 && model.UnitsAvailable == 0)
                model.UnitsAvailable = model.TotalUnits;

            _db.RoomTypes.Add(model);
            await _db.SaveChangesAsync();

            // --- NEW: auto-create Rooms based on TotalUnits ---
            for (int i = 1; i <= model.TotalUnits; i++)
            {
                var room = new Room
                {
                    RoomTypeId = model.Id,
                    AutoNumber = i,
                    Number = $"{(string.IsNullOrWhiteSpace(model.Name) ? "Unit" : model.Name)}-{i}",
                    IsOccupied = false
                };
                _db.Rooms.Add(room);
            }
            await _db.SaveChangesAsync();
            //end of new

            TempData["ok"] = "Room type created.";
            return RedirectToAction("Index");
        }


        // Toggle Active button from the table
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> ToggleActive(int id)
        {
            var rt = await _db.RoomTypes.FindAsync(id);
            if (rt == null) return HttpNotFound();
            rt.IsActive = !rt.IsActive;
            await _db.SaveChangesAsync();
            return RedirectToAction("Index");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }


        //retieve applications

        // SETTINGS (same keys you used in ApplicationsController)
        private readonly string _idContainer = ConfigurationManager.AppSettings["Azure:Storage:IdContainer"] ?? "ids";
        private readonly string _bankContainer = ConfigurationManager.AppSettings["Azure:Storage:BankContainer"] ?? "bank-statements";

        // GET: /Landlord/Applications?sort=newest
        // [Authorize(Roles = "Landlord")]
        public async Task<ActionResult> Applications(string sort = "newest")
        {
            var q = _db.RentalApplications
                       .AsNoTracking()
                       .Include(a => a.RoomType);

            //// Sort
            //switch ((sort ?? "").ToLowerInvariant())
            //{
            //    case "oldest":
            //        q = q.OrderBy(a => a.CreatedUtc);
            //        break;
            //    case "name":
            //    case "a-z":
            //    case "alphabetical":
            //        q = q.OrderBy(a => a.LastName).ThenBy(a => a.FirstName);
            //        break;
            //    case "status":
            //        q = q.OrderBy(a => a.Status).ThenByDescending(a => a.CreatedUtc);
            //        break;
            //    default: // newest
            //        q = q.OrderByDescending(a => a.CreatedUtc);
            //        break;
            //}

            // Sort / Filter
            switch ((sort ?? "").ToLowerInvariant())
            {
                case "oldest":
                    q = q.OrderBy(a => a.CreatedUtc);
                    break;
                case "name":
                case "a-z":
                case "alphabetical":
                    q = q.OrderBy(a => a.LastName).ThenBy(a => a.FirstName);
                    break;
                case "status":
                    q = q.OrderBy(a => a.Status).ThenByDescending(a => a.CreatedUtc);
                    break;
                case "accepted":
                    q = q.Where(a => a.Status == ApplicationStatus.Approved)
                         .OrderByDescending(a => a.CreatedUtc);
                    break;
                case "disabled":
                    q = q.Where(a => a.Status == ApplicationStatus.Disabled)
                         .OrderByDescending(a => a.CreatedUtc);
                    break;
                case "rejected":
                    q = q.Where(a => a.Status == ApplicationStatus.Rejected)
                         .OrderByDescending(a => a.CreatedUtc);
                    break;
                default: // newest
                    q = q.OrderByDescending(a => a.CreatedUtc);
                    break;
            }



            var list = await q.ToListAsync();

            // Build VM with short-lived SAS for each doc (5 min)
            var vm = new LandlordApplicationsVM { Sort = sort };
            foreach (var a in list)
            {
                var card = new ApplicationCardVM
                {
                    Id = a.Id,
                    ApplicantName = $"{a.FirstName} {a.LastName}".Trim(),
                    NationalId = a.NationalId,
                    Email = a.Email,
                    Phone = a.Phone,
                    RoomTypeName = a.RoomType?.Name,
                    PricePerMonth = a.RoomType?.PricePerMonth,
                    SquareMeters = a.RoomType?.SquareMeters,
                    Status = a.Status.ToString(),
                    CreatedUtc = a.CreatedUtc,
                    IdSasUrl = string.IsNullOrWhiteSpace(a.IdDocumentPath) ? null :
                               GetReadSasUrl(_idContainer, a.IdDocumentPath, TimeSpan.FromMinutes(5)),
                    BankSasUrl = string.IsNullOrWhiteSpace(a.BankStatementPath) ? null :
                                 GetReadSasUrl(_bankContainer, a.BankStatementPath, TimeSpan.FromMinutes(5))
                };
                vm.Items.Add(card);
            }

            return View(vm);
        }

        // === minimal SAS helper (same logic you used before) ===
        private static (string account, string key) ParseNameKey(string connectionString)
        {
            string name = null, key = null;
            foreach (var part in connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                if (kv[0].Equals("AccountName", StringComparison.OrdinalIgnoreCase)) name = kv[1];
                if (kv[0].Equals("AccountKey", StringComparison.OrdinalIgnoreCase)) key = kv[1];
            }
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Storage connection string missing AccountName/AccountKey.");
            return (name, key);
        }

        private string GetReadSasUrl(string container, string blobName, TimeSpan lifetime)
        {
            var cs = ConfigurationManager.AppSettings["Azure:Storage:ConnectionString"];
            var (account, key) = ParseNameKey(cs);

            var blobEndpoint = $"https://{account}.blob.core.windows.net";
            var blobUri = new Uri($"{blobEndpoint}/{container}/{blobName}");

            var sas = new BlobSasBuilder
            {
                BlobContainerName = container,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime)
            };
            sas.SetPermissions(BlobSasPermissions.Read);

            var cred = new StorageSharedKeyCredential(account, key);
            var query = sas.ToSasQueryParameters(cred).ToString();
            return $"{blobUri}?{query}";
        }


        // Aprove or Reject Applications//

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Approve(int id)
        {
            var a = await _db.RentalApplications.FindAsync(id);
            if (a == null) return HttpNotFound();

            if (a.Status == ApplicationStatus.Approved)
                return Json(new { ok = true, message = "Already approved." });

            if (string.IsNullOrWhiteSpace(a.Email))
                return Json(new { ok = false, message = "Applicant has no email address on file." });

            // Generate token + expiry
            var token = Guid.NewGuid().ToString("N");
            int daysValid = 14;
            int.TryParse(ConfigurationManager.AppSettings["Lease:LinkDaysValid"], out daysValid);
            var expires = DateTime.UtcNow.AddDays(daysValid);

            a.LeaseToken = token;
            a.LeaseTokenExpiresUtc = expires;
            a.Status = ApplicationStatus.Approved;
            a.ApprovedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Build absolute lease link
            var leaseUrl = Url.Action("Start", "Lease", new { token = token }, protocol: Request?.Url?.Scheme);

            var viewingAddress = ConfigurationManager.AppSettings["Property:ViewingAddress"] ?? "[Property Address]";
            var subject = "Your Rental Application – Approved (Viewing Pending)";
            var html = $@"
        <p>Hi {HttpUtility.HtmlEncode(a.FirstName)},</p>
        <p>Great news! Your application has been <strong>approved</strong> for viewing.</p>
        <p><strong>Viewing address:</strong><br/>{HttpUtility.HtmlEncode(viewingAddress)}</p>
        <p>If you’re satisfied after the viewing, please use the secure link below to sign the lease and pay your first month’s rent:</p>
        <p><a href=""{leaseUrl}"">{leaseUrl}</a></p>
        <p><small>This link is valid for {daysValid} days (until {expires.ToLocalTime():yyyy-MM-dd HH:mm}).</small></p>
        <p>Regards,<br/>Fountain Court Management</p>";

            try
            {
                Mailer.SendHtml(a.Email, subject, html);
            }
            catch (Exception ex)
            {
                // Email failed but status already updated; you may choose to revert or log
                return Json(new { ok = true, message = "Approved, but email failed to send: " + ex.Message, status = "Approved" });
            }

            return Json(new { ok = true, message = "Approved and email sent.", status = "Approved", leaseUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Reject(int id)
        {
            var a = await _db.RentalApplications.FindAsync(id);
            if (a == null) return HttpNotFound();

            if (a.Status == ApplicationStatus.Rejected)
                return Json(new { ok = true, message = "Already rejected." });

            a.Status = ApplicationStatus.Rejected;
            a.RejectedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(a.Email))
            {
                var subject = "Your Rental Application – Decision";
                var html = $@"
            <p>Hi {HttpUtility.HtmlEncode(a.FirstName)},</p>
            <p>Thank you for your interest in Fountain Court. After careful consideration, we’re sorry to let you know that we won’t be proceeding with your application at this time.</p>
            <p>We appreciate the time you took to apply and wish you the best in your housing search.</p>
            <p>Warm regards,<br/>Fountain Court Management</p>";
                try { Mailer.SendHtml(a.Email, subject, html); } catch { /* best effort */ }
            }

            return Json(new { ok = true, message = "Rejected and email sent (if address available).", status = "Rejected" });
        }


        ///Tenant view///
        //public async Task<ActionResult> Tenants(string q = null)
        //{
        //    // Load all rooms with their types
        //    var rooms = await _db.Rooms
        //                         .Include(r => r.RoomType)
        //                         .OrderBy(r => r.RoomType.Name)
        //                         .ThenBy(r => r.Number)
        //                         .ToListAsync();

        //    // Load latest app per room (if any). If multiple, take most recent Approved/Screening first.
        //    var apps = await _db.RentalApplications
        //                        .Include(a => a.RoomType)
        //                        .Where(a => a.RoomId != null)
        //                        .OrderByDescending(a => a.CreatedUtc)
        //                        .ToListAsync();

        //    // Build quick lookup: for each roomId, pick the "best" application (latest, prefer not Rejected)
        //    var appByRoom = apps
        //        .GroupBy(a => a.RoomId.Value)
        //        .ToDictionary(
        //            g => g.Key,
        //            g => g.OrderByDescending(a => a.Status != ApplicationStatus.Rejected) // prefer non-rejected
        //                  .ThenByDescending(a => a.CreatedUtc)
        //                  .First()
        //        );

        //    var vm = new LandlordTenantsVM();

        //    // Group rooms by RoomType
        //    var groups = rooms.GroupBy(r => r.RoomTypeId)
        //                      .OrderBy(g => g.First().RoomType.Name);

        //    foreach (var grp in groups)
        //    {
        //        var rt = grp.First().RoomType;
        //        var groupVm = new RoomTypeGroupVM
        //        {
        //            RoomTypeId = rt.Id,
        //            RoomTypeName = rt.Name,
        //            TotalUnits = rt.TotalUnits
        //        };

        //        foreach (var room in grp)
        //        {
        //            var tr = new TenantRoomVM
        //            {
        //                RoomId = room.Id,
        //                RoomNumber = room.Number,
        //                IsOccupied = room.IsOccupied
        //            };

        //            if (appByRoom.TryGetValue(room.Id, out var app))
        //            {
        //                tr.ApplicationId = app.Id;
        //                tr.TenantName = (app.FirstName + " " + app.LastName).Trim();
        //                tr.Email = app.Email;
        //                tr.Phone = app.Phone;
        //                tr.Status = app.Status.ToString();
        //            }

        //            groupVm.Rooms.Add(tr);
        //        }

        //        groupVm.OccupiedCount = groupVm.Rooms.Count(r => r.IsOccupied);
        //        vm.Groups.Add(groupVm);
        //    }

        //    vm.TotalRooms = rooms.Count;
        //    vm.TotalOccupied = rooms.Count(r => r.IsOccupied);

        //    // Optional server-side filter by tenant name if q provided
        //    if (!string.IsNullOrWhiteSpace(q))
        //    {
        //        var query = q.Trim();
        //        foreach (var g in vm.Groups)
        //        {
        //            g.Rooms = g.Rooms
        //                      .Where(r => string.IsNullOrWhiteSpace(r.TenantName)
        //                                ? false
        //                                : r.TenantName.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
        //                      .ToList();
        //        }
        //        vm.Groups = vm.Groups.Where(g => g.Rooms.Any()).ToList();
        //        vm.Search = q;
        //    }

        //    return View(vm);
        //}

        // Tenant view
        //public async Task<ActionResult> Tenants(string q = null)
        //{
        //    // Load all rooms with their types
        //    var rooms = await _db.Rooms
        //                         .Include(r => r.RoomType)
        //                         .OrderBy(r => r.RoomType.Name)
        //                         .ThenBy(r => r.Number)
        //                         .ToListAsync();

        //    // Load latest app per room (if any). If multiple, take most recent Approved/Screening first.
        //    var apps = await _db.RentalApplications
        //                        .Include(a => a.RoomType)
        //                        .Where(a => a.RoomId != null)
        //                        .OrderByDescending(a => a.CreatedUtc)
        //                        .ToListAsync();

        //    // Build quick lookup: for each roomId, pick the "best" application (latest, prefer not Rejected)
        //    var appByRoom = apps
        //        .GroupBy(a => a.RoomId.Value)
        //        .ToDictionary(
        //            g => g.Key,
        //            g => g.OrderByDescending(a => a.Status != ApplicationStatus.Rejected) // prefer non-rejected
        //                  .ThenByDescending(a => a.CreatedUtc)
        //                  .First()
        //        );

        //    // === Load payments for all applications in one query ===
        //    var appIds = appByRoom.Values.Select(a => a.Id).ToList();
        //    var payments = await _db.Payments
        //        .Where(p => appIds.Contains(p.ApplicationId))
        //        .OrderByDescending(p => p.CreatedUtc)
        //        .ToListAsync();

        //    var paymentsByApp = payments
        //        .GroupBy(p => p.ApplicationId)
        //        .ToDictionary(
        //            g => g.Key,
        //            g => g.Select(p => new PaymentVM
        //            {
        //                CreatedUtc = p.CreatedUtc,
        //                Amount = p.Amount,
        //                Status = p.Status,
        //                Reference = p.Reference,
        //                BillingYear = p.BillingYear,
        //                BillingMonth = p.BillingMonth
        //            }).ToList()
        //        );

        //    var vm = new LandlordTenantsVM();

        //    // Group rooms by RoomType
        //    var groups = rooms.GroupBy(r => r.RoomTypeId)
        //                      .OrderBy(g => g.First().RoomType.Name);

        //    foreach (var grp in groups)
        //    {
        //        var rt = grp.First().RoomType;
        //        var groupVm = new RoomTypeGroupVM
        //        {
        //            RoomTypeId = rt.Id,
        //            RoomTypeName = rt.Name,
        //            TotalUnits = rt.TotalUnits
        //        };

        //        foreach (var room in grp)
        //        {
        //            var tr = new TenantRoomVM
        //            {
        //                RoomId = room.Id,
        //                RoomNumber = room.Number,
        //                IsOccupied = room.IsOccupied
        //            };

        //            if (appByRoom.TryGetValue(room.Id, out var app))
        //            {
        //                tr.ApplicationId = app.Id;
        //                tr.TenantName = (app.FirstName + " " + app.LastName).Trim();
        //                tr.Email = app.Email;
        //                tr.Phone = app.Phone;
        //                tr.Status = app.Status.ToString();

        //                // Attach payments if any
        //                if (paymentsByApp.TryGetValue(app.Id, out var payList))
        //                {
        //                    tr.Payments = payList;
        //                }
        //            }

        //            groupVm.Rooms.Add(tr);
        //        }

        //        groupVm.OccupiedCount = groupVm.Rooms.Count(r => r.IsOccupied);
        //        vm.Groups.Add(groupVm);
        //    }

        //    vm.TotalRooms = rooms.Count;
        //    vm.TotalOccupied = rooms.Count(r => r.IsOccupied);


        //    // Optional server-side filter by tenant name if q provided
        //    if (!string.IsNullOrWhiteSpace(q))
        //    {
        //        var query = q.Trim();
        //        foreach (var g in vm.Groups)
        //        {
        //            g.Rooms = g.Rooms
        //                      .Where(r => string.IsNullOrWhiteSpace(r.TenantName)
        //                                ? false
        //                                : r.TenantName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        //                      .ToList();
        //        }
        //        vm.Groups = vm.Groups.Where(g => g.Rooms.Any()).ToList();
        //        vm.Search = q;
        //    }

        //    return View(vm);
        //}

        public async Task<ActionResult> Tenants(string q = null)
        {
            // Load all occupied rooms
            var rooms = await _db.Rooms
                                 .Include(r => r.RoomType)
                                 .Where(r => r.IsOccupied) // <-- only occupied
                                 .OrderBy(r => r.RoomType.Name)
                                 .ThenBy(r => r.Number)
                                 .ToListAsync();

            var apps = await _db.RentalApplications
                                .Include(a => a.RoomType)
                                .Where(a => a.RoomId != null)
                                .OrderByDescending(a => a.CreatedUtc)
                                .ToListAsync();

            var appByRoom = apps
                .GroupBy(a => a.RoomId.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.Status != ApplicationStatus.Rejected)
                          .ThenByDescending(a => a.CreatedUtc)
                          .First()
                );

            // Load payments for these applications
            var appIds = appByRoom.Values.Select(a => a.Id).ToList();
            var payments = await _db.Payments
                                    .Where(p => appIds.Contains(p.ApplicationId))
                                    .OrderByDescending(p => p.CreatedUtc)
                                    .ToListAsync();

            var paymentsByApp = payments
                .GroupBy(p => p.ApplicationId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new PaymentVM
                    {
                        CreatedUtc = p.CreatedUtc,
                        Amount = p.Amount,
                        Status = p.Status,
                        Reference = p.Reference,
                        BillingYear = p.BillingYear,
                        BillingMonth = p.BillingMonth
                    }).ToList()
                );

            var vm = new LandlordTenantsVM();

            var groups = rooms.GroupBy(r => r.RoomTypeId)
                              .OrderBy(g => g.First().RoomType.Name);

            foreach (var grp in groups)
            {
                var rt = grp.First().RoomType;
                var groupVm = new RoomTypeGroupVM
                {
                    RoomTypeId = rt.Id,
                    RoomTypeName = rt.Name,
                    TotalUnits = rt.TotalUnits
                };

                foreach (var room in grp)
                {
                    if (!appByRoom.TryGetValue(room.Id, out var app)) continue; // skip rooms with no application

                    var tr = new TenantRoomVM
                    {
                        RoomId = room.Id,
                        RoomNumber = room.Number,
                        IsOccupied = true, // guaranteed
                        ApplicationId = app.Id,
                        TenantName = (app.FirstName + " " + app.LastName).Trim(),
                        Email = app.Email,
                        Phone = app.Phone,
                        Status = app.Status.ToString()
                    };

                    if (paymentsByApp.TryGetValue(app.Id, out var payList))
                    {
                        tr.Payments = payList;
                    }

                    groupVm.Rooms.Add(tr);
                }

                groupVm.OccupiedCount = groupVm.Rooms.Count;
                if (groupVm.Rooms.Any())
                    vm.Groups.Add(groupVm);
            }

            vm.TotalRooms = rooms.Count;
            vm.TotalOccupied = rooms.Count;

            // Server-side search
            if (!string.IsNullOrWhiteSpace(q))
            {
                var query = q.Trim();
                foreach (var g in vm.Groups)
                {
                    g.Rooms = g.Rooms
                              .Where(r => r.TenantName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                              .ToList();
                }
                vm.Groups = vm.Groups.Where(g => g.Rooms.Any()).ToList();
                vm.Search = q;
            }

            return View(vm);
        }




        ///NOTICES
        public async Task<ActionResult> Notices()
        {
            var list = await _db.SiteNotices
                .OrderByDescending(n => n.CreatedUtc)
                .ToListAsync();

            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateNotice(string title, string body, DateTime? expiresUtc)
        {
            title = (title ?? "").Trim();
            body = (body ?? "").Trim();

            if (string.IsNullOrWhiteSpace(body))
            {
                TempData["err"] = "Please enter a message.";
                return RedirectToAction("Notices");
            }

            // Keep only 2 active notices. If already 2+, remove the oldest active one.
            var active = await _db.SiteNotices
                .Where(n => n.IsActive && (n.ExpiresUtc == null || n.ExpiresUtc > DateTime.UtcNow))
                .OrderByDescending(n => n.CreatedUtc)
                .ToListAsync();

            if (active.Count >= 2)
            {
                var oldest = active.OrderBy(n => n.CreatedUtc).First();
                // You can either hard-delete or set inactive; choose one:
                _db.SiteNotices.Remove(oldest); // hard-delete
                                                // oldest.IsActive = false;  // (alternative) soft-disable
            }

            _db.SiteNotices.Add(new SiteNotice
            {
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Body = body,
                ExpiresUtc = expiresUtc,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            TempData["ok"] = "Notice posted.";
            return RedirectToAction("Notices");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteNotice(int id)
        {
            var n = await _db.SiteNotices.FindAsync(id);
            if (n != null)
            {
                _db.SiteNotices.Remove(n);   // or n.IsActive = false;
                await _db.SaveChangesAsync();
            }
            TempData["ok"] = "Notice removed.";
            return RedirectToAction("Notices");
        }

        //Delete tenant
        [HttpGet]
        public ActionResult DisableTenant(int applicationId, int roomId)
        {
            var app = _db.RentalApplications
                .Include(a => a.Room)
                .Include(a => a.Room.RoomType)
                .FirstOrDefault(a => a.Id == applicationId);

            if (app == null)
                return HttpNotFound();

            // 1) Update application status
            app.Status = ApplicationStatus.Disabled;
            app.UpdatedUtc = DateTime.UtcNow;

            // 2) Unassign room
            if (app.Room != null && app.Room.Id == roomId)
            {
                app.Room.IsOccupied = false;

                // 3) Increase availability on the RoomType
                if (app.Room.RoomType != null)
                {
                    app.Room.RoomType.UnitsAvailable += 1;
                }

                app.RoomId = null;
            }

            // 4) Disable the Identity account if exists
            if (!string.IsNullOrEmpty(app.UserId))
            {
                var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var user = userManager.FindByIdAsync(app.UserId).Result;
                if (user != null)
                {
                    user.LockoutEnabled = true;
                    user.LockoutEndDateUtc = DateTime.MaxValue; // Effectively disables login
                    userManager.Update(user);
                }
            }

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Tenant has been removed and disabled.";
            return RedirectToAction("Tenants"); // or ManageTenants if that's your action name
        }
    }
}
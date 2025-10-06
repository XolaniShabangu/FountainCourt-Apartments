using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas; 
using FountainCourtResidents.Models;
using FountainCourtResidents.Models.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace FountainCourtResidents.Controllers
{
    public class ApplicationsController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        // Containers (from Web.config appSettings; falls back to sensible defaults)
        private readonly string _idContainer = ConfigurationManager.AppSettings["Azure:Storage:IdContainer"] ?? "ids";
        private readonly string _bankContainer = ConfigurationManager.AppSettings["Azure:Storage:BankContainer"] ?? "bank-statements";

        // ========= PUBLIC APPLY =========

        [AllowAnonymous]
        
        public ActionResult Apply()
        {
            var vm = new ApplicationCreateVM
            {
                RoomTypes = GetRoomTypeSelectItems()
            };
            return View(vm);
        }

        // Helper to build the dropdown text: "Studio — R6,500 / month — 28 m² (12 available)"
        private IEnumerable<SelectListItem> GetRoomTypeSelectItems()
        {
            var za = new CultureInfo("en-ZA");

            return _db.RoomTypes
                .AsNoTracking()
                .Where(rt => rt.IsActive)
                .OrderBy(rt => rt.PricePerMonth)
                .ToList()
                .Select(rt => new SelectListItem
                {
                    Value = rt.Id.ToString(),
                    // Removed the " — {2} m²" portion; kept name, price, availability
                    Text = string.Format(
                        za,
                        "{0} — R{1:N0} / month — ({2} available)",
                        rt.Name,
                        rt.PricePerMonth,
                        rt.UnitsAvailable
                    ),
                    Disabled = rt.UnitsAvailable == 0
                });
        }



        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> Apply(ApplicationCreateVM vm, HttpPostedFileBase idFile, HttpPostedFileBase bankFile)
        {
            // 0) Basic form/file checks
            if (string.IsNullOrWhiteSpace(vm.FirstName))
                ModelState.AddModelError(nameof(vm.FirstName), "First name is required.");
            if (string.IsNullOrWhiteSpace(vm.LastName))
                ModelState.AddModelError(nameof(vm.LastName), "Last name is required.");
            if (string.IsNullOrWhiteSpace(vm.NationalId))
                ModelState.AddModelError(nameof(vm.NationalId), "ID Number is required.");

            if ((idFile == null || idFile.ContentLength == 0) && string.IsNullOrWhiteSpace(vm.IdDocumentPath))
                ModelState.AddModelError("", "Please upload your ID document (or scan it first).");

            if (bankFile == null || bankFile.ContentLength == 0)
                ModelState.AddModelError("", "Please upload your bank statement.");

            if (!ModelState.IsValid) return View(vm);

            // 1) Duplicate guard (by NationalId, except Rejected)
            var dup = _db.RentalApplications.Any(a =>
                a.NationalId == vm.NationalId && a.Status != ApplicationStatus.Rejected);
            if (dup)
            {
                ModelState.AddModelError("", "An active application with this ID Number already exists.");
                vm.RoomTypes = GetRoomTypeSelectItems();
                return View(vm);
            }

            // 2) Ensure we have an ID blob name and OCR text to validate against
            //    - If user scanned earlier: vm.IdDocumentPath should be set (blob exists)
            //    - If not: upload the posted idFile to canonical path now
            string idBlobName = vm.IdDocumentPath;
            string idOcrText;

            // Upload if needed
            if (string.IsNullOrWhiteSpace(idBlobName))
            {
                if (idFile == null || idFile.ContentLength == 0)
                {
                    ModelState.AddModelError("", "Please reselect your ID file, then submit.");
                    vm.RoomTypes = GetRoomTypeSelectItems();
                    return View(vm);
                }
                var idExt = GetSafeExt(idFile, idFile.ContentType);
                idBlobName = BuildCanonicalBlobName(vm.NationalId, idExt);
                await UploadToBlobAsync(_idContainer, idBlobName, idFile.InputStream, idFile.ContentType);
            }

            // OCR from blob (via short-lived SAS URL)
            var idSasUrl = GetReadSasUrl(_idContainer, idBlobName, TimeSpan.FromMinutes(5));
            var cv = GetCvClient();
            idOcrText = await OcrFromUrlAsync(cv, idSasUrl);

            // 3) Strict ID validation vs form fields (name + id number)
            var fullName = $"{vm.FirstName} {vm.LastName}".Trim();
            if (!ValidateIDDetails(fullName, vm.NationalId, idOcrText, out var idValidationMessage))
            {
                ModelState.AddModelError("", idValidationMessage);
                vm.RoomTypes = GetRoomTypeSelectItems();
                return View(vm);
            }

            // 4) Upload bank statement under canonical name
            var bankExt = GetSafeExt(bankFile, bankFile.ContentType);
            var bankBlobName = BuildCanonicalBlobName(vm.NationalId, bankExt);
            bankFile.InputStream.Position = 0;
            await UploadToBlobAsync(_bankContainer, bankBlobName, bankFile.InputStream, bankFile.ContentType);

            // 5) Persist application
            var app = new RentalApplication
            {
                UserId = User.Identity.IsAuthenticated ? User.Identity.GetUserId() : null,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                NationalId = vm.NationalId,
                Email = vm.Email,
                Phone = vm.Phone,
                RoomTypeId = vm.SelectedRoomTypeId.Value,
                IdDocumentPath = idBlobName,
                BankStatementPath = bankBlobName,
                Status = ApplicationStatus.New,
                CreatedUtc = DateTime.UtcNow
            };

            _db.RentalApplications.Add(app);
            _db.SaveChanges(); // sync is fine

            return RedirectToAction("Submitted", new { id = app.Id });
        }

        [AllowAnonymous]
        public ActionResult Submitted(int id)
        {
            var exists = _db.RentalApplications.Any(a => a.Id == id);
            if (!exists) return HttpNotFound();

            ViewBag.RedirectUrl = Url.Action("Index", "Home");
            ViewBag.DelayMs = 3000; // 3 seconds
            return View(model: id);
        }


        

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanId(HttpPostedFileBase idFile)
        {
            if (idFile == null || idFile.ContentLength == 0)
                return Json(new { ok = false, message = "Please choose an ID file." });

            var ct = (idFile.ContentType ?? "").ToLowerInvariant();
            if (!(ct.StartsWith("image/") || ct == "application/pdf"))
                return Json(new { ok = false, message = "Please upload an image or PDF of your ID." });
            if (idFile.ContentLength > 40 * 1024 * 1024)
                return Json(new { ok = false, message = "File too large. Max 40 MB." });

            try
            {
                var container = GetContainer(_idContainer);

                // 1) temp upload under unverified/
                var ext = GetSafeExt(idFile, idFile.ContentType);
                var tempName = $"unverified/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
                var tempBlob = container.GetBlobClient(tempName);
                using (var s = idFile.InputStream) { await tempBlob.UploadAsync(s, overwrite: true); }

                // NEW: set content type on the temp blob so browsers can render inline
                try
                {
                    await tempBlob.SetHttpHeadersAsync(
                        new Azure.Storage.Blobs.Models.BlobHttpHeaders
                        {
                            ContentType = string.IsNullOrWhiteSpace(idFile.ContentType) ? "application/octet-stream" : idFile.ContentType
                        });
                }
                catch { /* best effort */ }

                // 2) OCR via SAS URL
                var sasUrl = GetReadSasUrl(_idContainer, tempName, TimeSpan.FromMinutes(5));
                var cv = GetCvClient();
                var ocrText = await OcrFromUrlAsync(cv, sasUrl);

                // DEBUG dump
                System.Diagnostics.Debug.WriteLine("=== OCR TEXT START ===");
                System.Diagnostics.Debug.WriteLine(ocrText);
                System.Diagnostics.Debug.WriteLine("=== OCR TEXT END ===");

                var looksLikeSaId = (ocrText ?? "").IndexOf("REPUBLIC OF SOUTH AFRICA", StringComparison.OrdinalIgnoreCase) >= 0
                                    || (ocrText ?? "").IndexOf("IDENTITY", StringComparison.OrdinalIgnoreCase) >= 0;

                var surname = GetOcrField(ocrText, Label_Surname, Stop_Labels);
                var names = GetOcrField(ocrText, Label_Names, Stop_Labels);
                if (string.Equals(names, "For", StringComparison.OrdinalIgnoreCase)) names = string.Empty;

                if (string.IsNullOrWhiteSpace(surname))
                    surname = Regex.Match(ocrText ?? "", @"\bSurname\s*:?\s*([A-Z][A-Za-z' -]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(names))
                    names = Regex.Match(ocrText ?? "", @"\bNames?\s*:?\s*([A-Z][A-Za-z' -]+(?:\s+[A-Z][A-Za-z' -]+)*)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();

                string idNumber = "";
                foreach (Match m in Regex.Matches(ocrText ?? "", @"\b(\d{13})\b"))
                {
                    var c = m.Groups[1].Value;
                    if (LuhnOk(c)) { idNumber = c; break; }
                }

                string firstName = "", lastName = "";
                if (!string.IsNullOrWhiteSpace(surname)) lastName = CleanName(surname);
                if (!string.IsNullOrWhiteSpace(names))
                {
                    var parts = names.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(CleanName).ToArray();
                    if (parts.Length > 0) firstName = parts[0];
                }

                bool alreadyExists = false; string existingStatus = null; string finalBlobName = tempName;

                if (!string.IsNullOrWhiteSpace(idNumber))
                {
                    // move to canonical path
                    var canonical = BuildCanonicalBlobName(idNumber, ext);
                    var dst = container.GetBlobClient(canonical);
                    await dst.StartCopyFromUriAsync(tempBlob.Uri);

                    // wait briefly for copy
                    for (int i = 0; i < 20; i++)
                    {
                        var props = await dst.GetPropertiesAsync();
                        if (props?.Value?.CopyStatus != Azure.Storage.Blobs.Models.CopyStatus.Pending) break;
                        await Task.Delay(150);
                    }
                    await tempBlob.DeleteIfExistsAsync();
                    finalBlobName = canonical;

                    // NEW: set content type on the destination blob (critical for inline viewing)
                    try
                    {
                        await dst.SetHttpHeadersAsync(
                            new Azure.Storage.Blobs.Models.BlobHttpHeaders
                            {
                                ContentType = string.IsNullOrWhiteSpace(idFile.ContentType) ? "application/octet-stream" : idFile.ContentType
                            });
                    }
                    catch { /* best effort */ }

                    // duplicate check
                    var app = _db.RentalApplications
                                 .AsNoTracking()
                                 .FirstOrDefault(a => a.NationalId == idNumber && a.Status != ApplicationStatus.Rejected);
                    if (app != null) { alreadyExists = true; existingStatus = app.Status.ToString(); }
                }

                if (!looksLikeSaId && string.IsNullOrWhiteSpace(idNumber))
                    return Json(new { ok = false, message = "This doesn’t look like a South African ID document. Please upload a clearer image/PDF of your ID." });

                return Json(new
                {
                    ok = true,
                    message = alreadyExists ? $"Existing application found (Status: {existingStatus})." : "ID scanned.",
                    data = new
                    {
                        firstName,
                        lastName,
                        nationalId = idNumber,
                        idDocumentPath = finalBlobName,
                        alreadyExists,
                        existingStatus
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Failed to scan ID. " + ex.Message });
            }
        }


        // ========= HELPERS =========

        private static string GetSafeExt(HttpPostedFileBase file, string contentType)
        {
            // Prefer the file’s actual extension if present
            var ext = System.IO.Path.GetExtension(file?.FileName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(ext)) return ext.ToLowerInvariant();

            // Fallback from content-type
            switch ((contentType ?? "").ToLowerInvariant())
            {
                case "application/pdf": return ".pdf";
                case "image/jpeg": return ".jpg";
                case "image/png": return ".png";
                case "image/gif": return ".gif";
                case "image/tiff": return ".tif";
                default: return ".bin";
            }
        }


        // EF context cleanup
        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }

        // Canonical blob naming under a National ID
        private static string BuildCanonicalBlobName(string nationalId, string ext)
        {
            var safeExt = string.IsNullOrWhiteSpace(ext) ? ".bin" : (ext.StartsWith(".") ? ext : "." + ext);
            return $"{nationalId}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{safeExt.ToLowerInvariant()}";
        }

        // Minimal upload helper
        private async Task UploadToBlobAsync(string containerName, string blobName, Stream stream, string contentType)
        {
            var container = GetContainer(containerName);
            var blob = container.GetBlobClient(blobName);
            stream.Position = 0;
            await blob.UploadAsync(stream, overwrite: true);
            // Optionally set content type after upload:
            try { await blob.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" }); }
            catch { /* best effort */ }
        }

        // === Azure helpers ===
        private ComputerVisionClient GetCvClient()
        {
            var endpoint = ConfigurationManager.AppSettings["Azure:ComputerVision:Endpoint"]; // https://<name>.cognitiveservices.azure.com/
            var key = ConfigurationManager.AppSettings["Azure:ComputerVision:Key"];
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Computer Vision endpoint/key not configured.");
            return new ComputerVisionClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
        }

        private BlobContainerClient GetContainer(string containerName)
        {
            var cs = ConfigurationManager.AppSettings["Azure:Storage:ConnectionString"];
            var svc = new BlobServiceClient(cs);
            var container = svc.GetBlobContainerClient(containerName);
            container.CreateIfNotExists(); // private by default
            return container;
        }

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

        // === OCR helpers ===

        private static string GetOcrField(string ocr, string[] labelCandidates, string[] stopLabels = null)
        {
            if (string.IsNullOrWhiteSpace(ocr)) return string.Empty;
            var lines = ocr.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var compare = CultureInfo.InvariantCulture.CompareInfo;

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i] ?? string.Empty;
                var line = Regex.Replace(raw.Trim(), @"\s{2,}", " "); // collapse double spaces

                foreach (var label in labelCandidates)
                {
                    // Whole-word label match to avoid "Name" matching inside "Surname"
                    var labelPattern = @"\b" + Regex.Escape(label) + @"\b";
                    var lm = Regex.Match(line, labelPattern, RegexOptions.IgnoreCase);
                    if (lm.Success)
                    {
                        // Take everything after the label on the same line
                        var val = line.Substring(lm.Index + lm.Length).Trim().Trim(':').Trim();

                        // If value too short (e.g., only "For" from "Forenames"), try next line
                        if (val.Length < 2 && i + 1 < lines.Length)
                        {
                            var next = Regex.Replace((lines[i + 1] ?? string.Empty).Trim(), @"\s{2,}", " ");
                            if (!LooksLikeAnotherLabel(next, labelCandidates, stopLabels))
                                val = next;
                        }

                        // Cut off if another label got glued onto same line
                        if (stopLabels != null && !string.IsNullOrWhiteSpace(val))
                        {
                            foreach (var stop in stopLabels)
                            {
                                var cut = Regex.Match(val, @"\b" + Regex.Escape(stop) + @"\b", RegexOptions.IgnoreCase);
                                if (cut.Success && cut.Index > 0)
                                {
                                    val = val.Substring(0, cut.Index).Trim();
                                }
                            }
                        }

                        // Clean noisy spaces
                        val = Regex.Replace(val, @"\s{2,}", " ");
                        return val;
                    }
                }

            }
            return string.Empty;
        }

        private static bool LooksLikeAnotherLabel(string text, string[] labelCandidates, string[] stopLabels)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            foreach (var l in labelCandidates)
                if (compare.IndexOf(text, l, CompareOptions.IgnoreCase) >= 0) return true;
            if (stopLabels != null)
                foreach (var s in stopLabels)
                    if (compare.IndexOf(text, s, CompareOptions.IgnoreCase) >= 0) return true;
            return false;
        }

        // Label sets (English + Afrikaans + common variants seen in smart cards and OCR)
        private static readonly string[] Label_Surname = new[] {
         "Surname", "Sumame", "Surname/Van", "Van", "Surnames"
        };

        private static readonly string[] Label_Names = new[] {
         "Names", "Names/Name", "Forenames", "Given names", "Given name"
        };

        private static readonly string[] Stop_Labels = new[] {
            "Sex", "Geslag", "Identity Number", "Identiteitsnommer", "Date of Birth", "Geboortedatum",
            "Nationality", "Status", "RSA"
        };

        private async Task<string> OcrFromUrlAsync(ComputerVisionClient cv, string url)
        {
            try
            {
                var headers = await cv.ReadAsync(url);
                var opId = headers.OperationLocation.Substring(headers.OperationLocation.LastIndexOf('/') + 1);

                ReadOperationResult result;
                do
                {
                    await Task.Delay(750);
                    result = await cv.GetReadResultAsync(Guid.Parse(opId));
                }
                while (result.Status == OperationStatusCodes.Running || result.Status == OperationStatusCodes.NotStarted);

                if (result.Status != OperationStatusCodes.Succeeded)
                    throw new InvalidOperationException("OCR failed. Status: " + result.Status);

                var lines = result.AnalyzeResult.ReadResults.SelectMany(p => p.Lines).Select(l => l.Text);
                return string.Join("\n", lines);
            }
            catch (ComputerVisionErrorResponseException ex)
            {
                var code = ex.Body?.Error?.Code ?? ex.Response?.ReasonPhrase ?? "UnknownError";
                var msg = ex.Body?.Error?.Message ?? ex.Message;
                throw new InvalidOperationException($"Computer Vision error: {code} - {msg}");
            }
        }

        // === Parsing/validation ===
        private static string CleanName(string s)
        {
            // Hyphen placed at end to avoid "range" in char class; allow letters, apostrophes, spaces, hyphen.
            return Regex.Replace(s ?? "", @"[^A-Za-z'\s-]", "").Trim();
        }

        private static bool LuhnOk(string digits)
        {
            if (string.IsNullOrEmpty(digits) || digits.Length != 13 || !digits.All(char.IsDigit)) return false;
            int sum = 0; bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (alt) { d *= 2; if (d > 9) d -= 9; }
                sum += d; alt = !alt;
            }
            return sum % 10 == 0;
        }

        // Strict validation against OCR text for final submit
        private bool ValidateIDDetails(string fullName, string idNumber, string idText, out string msg)
        {
            msg = string.Empty;
            if (string.IsNullOrWhiteSpace(idText))
            {
                msg = "No text detected on the ID image.";
                return false;
            }

            // Light presence check for SA ID keywords
            if (idText.IndexOf("REPUBLIC OF SOUTH AFRICA", StringComparison.OrdinalIgnoreCase) < 0 &&
                idText.IndexOf("IDENTITY", StringComparison.OrdinalIgnoreCase) < 0)
            {
                msg = "This does not look like a South African ID document.";
                return false;
            }

            string ExtractBetween(string start, string end)
            {
                var pattern = $"{Regex.Escape(start)}\\s*:?\\s*(.*?)\\s*{Regex.Escape(end)}";
                var m = Regex.Match(idText, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
            }

            var extractedSurname = ExtractBetween("Surname", "Names");
            var extractedNames = ExtractBetween("Names", "Sex");

            if (string.IsNullOrWhiteSpace(extractedSurname))
                extractedSurname = Regex.Match(idText, @"\bSurname\s*:?\s*([A-Z][A-Za-z' -]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(extractedNames))
                extractedNames = Regex.Match(idText, @"\bNames?\s*:?\s*([A-Z][A-Za-z' -]+(?:\s+[A-Z][A-Za-z' -]+)*)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();

            var idMatch = Regex.Match(idText, @"\b(\d{13})\b");
            var extractedID = idMatch.Success ? idMatch.Groups[1].Value : string.Empty;

            // Compare names flexibly
            var fullParts = (fullName ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var nameParts = (extractedNames ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool surnameOk = fullParts.Length > 0 &&
                             !string.IsNullOrWhiteSpace(extractedSurname) &&
                             fullParts.Last().Equals(extractedSurname, StringComparison.OrdinalIgnoreCase);

            bool firstOk = fullParts.Any(p => nameParts.Contains(p, StringComparer.OrdinalIgnoreCase));

            bool idOk = !string.IsNullOrWhiteSpace(extractedID) &&
                        !string.IsNullOrWhiteSpace(idNumber) &&
                        extractedID == idNumber &&
                        LuhnOk(idNumber);

            if (!surnameOk || !firstOk || !idOk)
            {
                msg = $"ID validation failed. Expected: {fullName} / {idNumber}. " +
                      $"Found: Surname: {extractedSurname}, Names: {extractedNames}, Identity Number: {extractedID}.";
                return false;
            }
            return true;
        }
    }
}

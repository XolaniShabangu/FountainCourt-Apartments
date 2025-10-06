using FountainCourtResidents.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Data.Entity;


namespace FountainCourtResidents.Services
{
    /// <summary>
    /// Picks the least-busy active repairman (fewest Open/InProgress tickets),
    /// then breaks ties by higher Rating, then lowest Id (stable).
    /// </summary>
    public static class AutoAssignService
    {
        public static async Task<bool> TryAutoAssignAsync(ApplicationDbContext db, MaintenanceTicket ticket)
        {
            // Active repairmen
            var repairmen = await db.Repairmen
                .Where(r => r.IsActive)
                .Select(r => new { r.Id, r.Rating })
                .ToListAsync();

            if (!repairmen.Any()) return false;

            // Current workloads (only Open / InProgress tickets count)
            var openStatuses = new[] { MaintenanceStatus.Open, MaintenanceStatus.InProgress };
            var workloads = await db.MaintenanceTickets
                .Where(t => t.AssignedRepairmanId != null && openStatuses.Contains(t.Status))
                .GroupBy(t => t.AssignedRepairmanId)
                .Select(g => new { RepairmanId = g.Key.Value, Count = g.Count() })
                .ToListAsync();

            // Merge -> default 0 if none
            var candidates = repairmen
                .GroupJoin(
                    workloads,
                    r => r.Id,
                    w => w.RepairmanId,
                    (r, wj) => new
                    {
                        r.Id,
                        r.Rating,
                        Workload = wj.Select(x => x.Count).DefaultIfEmpty(0).FirstOrDefault()
                    })
                .OrderBy(c => c.Workload)        // least busy first
                .ThenByDescending(c => c.Rating) // better rating preferred
                .ThenBy(c => c.Id)               // stable
                .ToList();

            var chosen = candidates.First();
            ticket.AssignedRepairmanId = chosen.Id;

            // If you want to flip Open -> InProgress immediately, uncomment:
            // if (ticket.Status == MaintenanceStatus.Open) ticket.Status = MaintenanceStatus.InProgress;

            return true;
        }
    }
}
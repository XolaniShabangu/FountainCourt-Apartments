using FountainCourtResidents.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace FountainCourtResidents
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // Global.asax Application_Start
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            SeedAdminUser();

        }

        // EXACT STYLE YOU ASKED FOR, adapted to Landlord/Tenant
        private static void SeedAdminUser()
        {
            using (var context = new ApplicationDbContext())
            {
                // ensure DB exists
                context.Database.CreateIfNotExists();

                // roles
                var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(context));
                foreach (var roleName in new[] { "Landlord", "Tenant" })
                {
                    if (!roleManager.RoleExists(roleName))
                        roleManager.Create(new IdentityRole(roleName));
                }

                // default landlord user (change these!)
                var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(context));
                const string email = "landlord@fountaincourt.local";   // ← change
                const string password = "Landlord@12345";               // ← change

                var user = userManager.FindByEmail(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        LockoutEnabled = false
                    };
                    var create = userManager.Create(user, password);
                    if (!create.Succeeded)
                        throw new Exception(string.Join(" | ", create.Errors));
                }

                if (!userManager.IsInRole(user.Id, "Landlord"))
                    userManager.AddToRole(user.Id, "Landlord");

                // (optional) make sure the landlord is not in Tenant:
                if (userManager.IsInRole(user.Id, "Tenant"))
                    userManager.RemoveFromRole(user.Id, "Tenant");

                context.SaveChanges();
            }
        }
    }
}

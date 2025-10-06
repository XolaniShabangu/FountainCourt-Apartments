using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(FountainCourtResidents.Startup))]
namespace FountainCourtResidents
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}

using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(StudentConnect.Startup))]
namespace StudentConnect
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
        }
    }
}
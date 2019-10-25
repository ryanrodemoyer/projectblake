using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ProjectBlake.Startup))]

namespace ProjectBlake
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {

        }
    }
}

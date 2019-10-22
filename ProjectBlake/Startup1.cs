using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ProjectBlake.Startup1))]

namespace ProjectBlake
{
    public class Startup1
    {
        private const string DS_INTEGRATION_KEY = "5b79f171-12d7-4d81-87a9-da99cd122ff7";

        public void Configuration(IAppBuilder app)
        {

        }
    }
}

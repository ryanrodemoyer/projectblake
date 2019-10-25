using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace ProjectBlake
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            GlobalFilters.Filters.Add(new SiteLayoutActionFilter(), 0);
        }
    }

    public class SiteLayoutActionFilter : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            string baseUrl = ConfigurationManager.AppSettings["baseUrl"];
            string integrationId = ConfigurationManager.AppSettings["integrationId"];
            string scopes = ConfigurationManager.AppSettings["scopes"];
            string callback = ConfigurationManager.AppSettings["callback"];

            filterContext.Controller.ViewBag.ConsentUrl = $"https://{baseUrl}/oauth/auth?response_type=code&scope={scopes}&client_id={integrationId}&redirect_uri={callback}";
        }
    }
}

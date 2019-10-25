using System.Configuration;
using System.Web.Mvc;

namespace ProjectBlake.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        public ActionResult Consent()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ConsentRedirect()
        {
            string baseUrl = ConfigurationManager.AppSettings["baseUrl"];
            string integrationId = ConfigurationManager.AppSettings["integrationId"];
            string scopes = ConfigurationManager.AppSettings["scopes"];
            string callback = ConfigurationManager.AppSettings["callback"];

            string uri = $"https://{baseUrl}/oauth/auth?response_type=code&scope={scopes}&client_id={integrationId}&redirect_uri={callback}";

            return Redirect(uri);
        }

        [HttpPost]
        public ActionResult Logout()
        {
            Session.Clear();

            return RedirectToAction("Index", "Home");
            //return View("Index");
        }
    }
}

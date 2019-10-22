using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Web.Http;

namespace ProjectBlake.Controllers
{
    public class CallbackController : ApiController
    {
        [HttpGet]
        [Route("api/callback/auth")]
        public IHttpActionResult AuthCallback([FromUri] string code)
        {
            var obj = new {code};
            return Ok(obj);
        }
    }
}

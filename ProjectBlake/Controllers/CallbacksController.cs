using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using ProjectBlake.Models;

namespace ProjectBlake.Models
{
    //new { codeLocal, authGrantTokenContent, userContent, jwtResult.token, jwtResult.content }
    public class DocusignViewModel
    {
        public DocusignViewModel(string code, string authGrantTokenContent, string userContent, string jwtToken, string jwtPostContent)
        {
            Code = code;
            AuthGrantTokenContent = authGrantTokenContent;
            UserContent = userContent;
            JwtToken = jwtToken;
            JwtPostContent = jwtPostContent;
        }

        public string Code { get; set; }
        public string AuthGrantTokenContent { get; set; }
        public string UserContent { get; set; }
        public string JwtToken { get; set; }
        public string JwtPostContent { get; set; }
    }
}

namespace ProjectBlake.Controllers
{
    public class CallbacksController : Controller
    {
        public async Task<ActionResult> Docusign([FromUri] string code)
        {
            // what is this "code" value they are sending us after the "impersonation" scope??
            // per DS, we can ignore `code` if using the JWT flow


            ViewBag.Title = "Docusign JWT Callback";

            string codeLocal = code;
            string userContent = null;
            string authGrantTokenContent = null;

            bool isLoggedIn = Convert.ToBoolean(Session["isLoggedIn"]);
            if (isLoggedIn)
            {
                codeLocal = null;
                authGrantTokenContent = Session["authGrantTokenResponse"].ToString();
            }
            else
            {
                authGrantTokenContent = await GetAuthGrantAccessToken(code);

                Session["authGrantTokenResponse"] = authGrantTokenContent;
                Session["isLoggedIn"] = true;
            }

            if (!string.IsNullOrWhiteSpace(authGrantTokenContent))
            {
                userContent = await GetOauthUser(authGrantTokenContent);
                var userJToken = JToken.Parse(userContent);
                string userId = userJToken["sub"].ToString();

                dynamic jwtResult = await BuildJwtAndExchangeWithDocusign(userId);

                authGrantTokenContent = JToken.Parse(authGrantTokenContent).ToString(Formatting.Indented);
                return View(new DocusignViewModel(codeLocal, authGrantTokenContent, userContent, jwtResult.token, jwtResult.content));
            }

            return RedirectToAction("Index", "Home");
        }

        private async Task<dynamic> BuildJwtAndExchangeWithDocusign(string userId)
        {
            string clientId = ConfigurationManager.AppSettings["integrationId"]; // integration id
            string oauthBasePath = ConfigurationManager.AppSettings["baseUrl"];
            string privateKeyB64 = ConfigurationManager.AppSettings["privateKey"];
            byte[] privateKeyBytes = Convert.FromBase64String(privateKeyB64);

            int expiresInHours = 1;

            using (var http = new HttpClient())
            {
                string token = RsaUtils.RequestJWTUserToken(clientId, userId, oauthBasePath, privateKeyBytes, expiresInHours);

                var @params = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                    new KeyValuePair<string, string>("assertion", token)
                };

                var data = new FormUrlEncodedContent(@params);

                var response = await http.PostAsync($"https://{oauthBasePath}/oauth/token", data);
                string content = await response.Content.ReadAsStringAsync();
                content = JToken.Parse(content).ToString(Formatting.Indented);
                
                return new {token, content};
            }
        }

        private async Task<string> GetOauthUser(string token)
        {
            string baseUrl = ConfigurationManager.AppSettings["baseUrl"];

            JObject jobj = JObject.Parse(token);
            string accessToken = jobj["access_token"].ToString();

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var userResponse = await http.GetAsync($"https://{baseUrl}/oauth/userinfo");
                string userContent = await userResponse.Content.ReadAsStringAsync();
                userContent = JToken.Parse(userContent).ToString(Formatting.Indented);

                return userContent;

                // https://account-d.docusign.com/oauth/userinfo 
                //https://demo.docusign.net/restapi/v2/accounts/8db1b68f-f51a-4c63-a7df-40f65d327b29/users?email=ryan.rodemoyer@mortgagecadence.com
                // https://account-d.docusign.com/oauth/userinfo
            }
        }

        private async Task<string> GetAuthGrantAccessToken(string code)
        {
            string integrationId = ConfigurationManager.AppSettings["integrationId"];
            string dsSecret = ConfigurationManager.AppSettings["dsSecret"];
            string baseUrl = ConfigurationManager.AppSettings["baseUrl"];

            string basic = $"{integrationId}:{dsSecret}";
            byte[] bytes = Encoding.UTF8.GetBytes(basic);
            string base64 = Convert.ToBase64String(bytes);

            // data "grant_type=authorization_code&code=YOUR_AUTHORIZATION_CODE"
            
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64);

                var @params = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code)
                };

                var data = new FormUrlEncodedContent(@params);

                var response = await http.PostAsync($"https://{baseUrl}/oauth/token", data);

                string content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }
    }

    /// <summary>
    /// this code extracted from the Docusign C# Api
    /// Docusign C# API is MIT License!!!
    /// https://github.com/docusign/docusign-csharp-client/blob/7185c466bf9ccd93599637e1901022bc1c5eead6/sdk/src/DocuSign.eSign/Client/ApiClient.cs#L966
    /// ApiClient.RequestJWTUserToken
    /// </summary>
    public class RsaUtils
    {
        public static string RequestJWTUserToken(string clientId, string userId, string oauthBasePath, byte[] privateKeyBytes, int expiresInHours, List<string> scopes = null)
        {
            string privateKey = Encoding.UTF8.GetString(privateKeyBytes);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler
            {
                SetDefaultTimesOnTokenCreation = false
            };

            SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor()
            {
                Expires = DateTime.UtcNow.AddHours(expiresInHours),
                IssuedAt = DateTime.UtcNow,
            };

            scopes = scopes ?? new List<string> { "signature" };

            descriptor.Subject = new ClaimsIdentity();
            descriptor.Subject.AddClaim(new Claim("scope", String.Join(" ", scopes)));
            descriptor.Subject.AddClaim(new Claim("aud", oauthBasePath));
            descriptor.Subject.AddClaim(new Claim("iss", clientId));

            if (!string.IsNullOrEmpty(userId))
            {
                descriptor.Subject.AddClaim(new Claim("sub", userId));
            }
            else
            {
                throw new Exception("User Id not supplied or is invalid!");
            }

            if (!string.IsNullOrEmpty(privateKey))
            {
                var rsa = CreateRSAKeyFromPem(privateKey);
                RsaSecurityKey rsaKey = new RsaSecurityKey(rsa);
                descriptor.SigningCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256Signature);
            }
            else
            {
                throw new Exception("Private key not supplied or is invalid!");
            }

            var token = handler.CreateToken(descriptor);
            string jwtToken = handler.WriteToken(token);

            return jwtToken;
        }

        private static RSA CreateRSAKeyFromPem(string key)
        {
            TextReader reader = new StringReader(key);
            PemReader pemReader = new PemReader(reader);

            object result = pemReader.ReadObject();

            RSA provider = RSA.Create();

            if (result is AsymmetricCipherKeyPair keyPair)
            {
                var rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)keyPair.Private);
                provider.ImportParameters(rsaParams);
                return provider;
            }
            else if (result is RsaKeyParameters keyParameters)
            {
                var rsaParams = DotNetUtilities.ToRSAParameters(keyParameters);
                provider.ImportParameters(rsaParams);
                return provider;
            }

            throw new Exception("Unexpected PEM type");
        }
    }
}

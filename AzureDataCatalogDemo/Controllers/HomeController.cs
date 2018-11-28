using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.IdentityModel.Clients.ActiveDirectory; //Install-Package Microsoft.IdentityModel.Clients.ActiveDirectory in Package Manager if not existing 


namespace AzureDataCatalogDemo.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult AzureDCSearch(FormCollection formData)
        {
            //ADCRequester adcRequester = new ADCRequester(); 
           // string upn = ADCRequester.AccessToken().Result.UserInfo.DisplayableId;

            if(!String.IsNullOrEmpty(formData["CatalogName"]))
            {
                ADCRequester.catalogName = formData["CatalogName"]; 
            }
            ViewBag.Message = ADCRequester.SearchDataAsset(formData["SearchTerms"]);

            return View("SearchResult");
        }
    }

    /// <summary>
    /// This is my class to handle all Azure Data Catalog Requests 
    /// </summary>
    public static class ADCRequester
    {
        static string clientIDFromAzureAppRegistration = "{ClientID}"; // THIS IS where we need to provide ClientID from Data Catalog Registration 

        static AuthenticationResult authResult = null;
        public static string catalogName = "KeyBankDataCatalog"; // DefaultCatalog

        static async Task<AuthenticationResult> AccessToken()
        {
            if (authResult == null)
            {
                //Resource Uri for Data Catalog API
                string resourceUri = "https://api.azuredatacatalog.com";

                //To learn how to register a client app and get a Client ID, see https://msdn.microsoft.com/en-us/library/azure/mt403303.aspx#clientID   
                string clientId = clientIDFromAzureAppRegistration;

                //A redirect uri gives AAD more details about the specific application that it will authenticate.
                //Since a client app does not have an external service to redirect to, this Uri is the standard placeholder for a client app.
                string redirectUri = "https://login.live.com/oauth20_desktop.srf";

                // Create an instance of AuthenticationContext to acquire an Azure access token
                // OAuth2 authority Uri
                string authorityUri = "https://login.windows.net/common/oauth2/authorize";
                AuthenticationContext authContext = new AuthenticationContext(authorityUri);

                // Call AcquireToken to get an Azure token from Azure Active Directory token issuance endpoint
                //  AcquireToken takes a Client Id that Azure AD creates when you register your client app.
                authResult = await authContext.AcquireTokenAsync(resourceUri, clientId, new Uri(redirectUri), new PlatformParameters(PromptBehavior.Always));
            }

            return authResult;
        }

        /*public static async Task<AuthenticationResult> AccessToken()
        {
            if (authResult == null)
            {
                //Resource Uri for Data Catalog API
                string resourceUri = "https://api.azuredatacatalog.com";

                //To learn how to register a client app and get a Client ID, see https://msdn.microsoft.com/en-us/library/azure/mt403303.aspx#clientID   
                string clientId = clientIDFromAzureAppRegistration;

                //A redirect uri gives AAD more details about the specific application that it will authenticate.
                //Since a client app does not have an external service to redirect to, this Uri is the standard placeholder for a client app.
                string redirectUri = "https://login.live.com/oauth20_desktop.srf";

                // Create an instance of AuthenticationContext to acquire an Azure access token
                // OAuth2 authority Uri
                string authorityUri = "https://login.windows.net/common/oauth2/authorize";
                AuthenticationContext authContext = new AuthenticationContext(authorityUri);

                // Call AcquireToken to get an Azure token from Azure Active Directory token issuance endpoint
                //  AcquireToken takes a Client Id that Azure AD creates when you register your client app.
                // authResult = await authContext.AcquireTokenAsync(resourceUri, clientId, new Uri(redirectUri), new PlatformParameters(PromptBehavior.Always));
                //authResult = await authContext.AcquireTokenAsync(resourceUri, clientId, new Uri(redirectUri), new PlatformParameters(PromptBehavior.Never));
                authResult = await authContext.AcquireTokenAsync(resourceUri, clientId, new UserPasswordCredential("shaikaz@kbisolation.onmicrosoft.com", "Azimshaik1991*#$"));
            }

            return authResult;
        }*/

        public static string SearchDataAsset(string searchTerm)
        {
            string responseContent = string.Empty;

            //NOTE: To find the Catalog Name, sign into Azure Data Catalog, and choose User. You will see a list of Catalog names.          
            string fullUri =
                string.Format("https://api.azuredatacatalog.com/catalogs/{0}/search/search?searchTerms={1}&count=10&api-version=2016-03-30", catalogName, searchTerm);

            //Create a GET WebRequest
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullUri);
            request.Method = "GET";

            try
            {
                //Get HttpWebResponse from GET request
                using (HttpWebResponse httpResponse = SetRequestAndGetResponse(request))
                {
                    //Get StreamReader that holds the response stream
                    using (StreamReader reader = new System.IO.StreamReader(httpResponse.GetResponseStream()))
                    {
                        responseContent = reader.ReadToEnd();
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Status);
                if (ex.Response != null)
                {
                    // can use ex.Response.Status, .StatusDescription
                    if (ex.Response.ContentLength != 0)
                    {
                        using (var stream = ex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                Console.WriteLine(reader.ReadToEnd());
                            }
                        }
                    }
                }
                return null;
            }

            return responseContent;
        }

        private static HttpWebResponse SetRequestAndGetResponse(HttpWebRequest request, string payload = null)
        {
            while (true)
            {
                //To authorize the operation call, you need an access token which is part of the Authorization header
                request.Headers.Add("Authorization", AccessToken().Result.CreateAuthorizationHeader());
                //Set to false to be able to intercept redirects
                request.AllowAutoRedirect = false;

                if (!string.IsNullOrEmpty(payload))
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(payload);
                    request.ContentLength = byteArray.Length;
                    request.ContentType = "application/json";
                    //Write JSON byte[] into a Stream
                    request.GetRequestStream().Write(byteArray, 0, byteArray.Length);
                }
                else
                {
                    request.ContentLength = 0;
                }

                HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                // Requests to **Azure Data Catalog (ADC)** may return an HTTP 302 response to indicate
                // redirection to a different endpoint. In response to a 302, the caller must re-issue
                // the request to the URL specified by the Location response header. 
                if (response.StatusCode == HttpStatusCode.Redirect)
                {
                    string redirectedUrl = response.Headers["Location"];
                    HttpWebRequest nextRequest = WebRequest.Create(redirectedUrl) as HttpWebRequest;
                    nextRequest.Method = request.Method;
                    request = nextRequest;
                }
                else
                {
                    return response;
                }
            }
        }
    }
}
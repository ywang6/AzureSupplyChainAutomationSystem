using GdcoTicket.Model;
using gmail.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace GdcoTicket
{
    public class GDCOExternalTicketHandler
    {
        #region Initialize Auth-Related Fields
        /// <summary>
        /// The AAD instance to get the auth token.
        /// </summary>
        private static string aadInstance = "https://login.gmailonline.com/{0}";
        private static string tenant = "gmail.ongmail.com";
        private static string authority = string.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        /// <summary>
        /// AAD Web APP Credentials
        /// </summary>

        //private static string clientId = ConnectionHandler.GdcoPPEClientId;
        //private static string appKey = ConnectionHandler.GdcoPPEAppKey;
        private static string clientId = ConnectionHandler.GdcoClientId;
        private static string appKey = ConnectionHandler.GdcoAppKey;

        /// <summary>
        /// Authentication context.
        /// </summary>
        private static AuthenticationContext authContext = null;
        #endregion

        #region TicketingService Related fields
        /// <summary>
        /// Ticketing service endpoint and AAD resourceId
        /// </summary>
        //private static string ticketingServiceResourceId = "https://mcio.ongmail.com/GDCOTicketingServicePPE"; //PPE
        //private static string ticketingServiceTestEndpoint = "https://gdcoticketingppe.trafficmanager.net"; //PPE
        private static string ticketingServiceResourceId = "https://mcio.ongmail.com/GDCOTicketingService"; // PROD
        private static string ticketingServiceTestEndpoint = "https://gdcoticketing.trafficmanager.net"; // PROD
        #endregion

        /// <summary>
        /// The HttpClient to submit calls to the ticketing service.
        /// </summary>
        private static HttpClient client = null;

        public GDCOExternalTicketHandler()
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Initialize the AuthenticationContext for the AAD tenant.
            authContext = new AuthenticationContext(authority, TokenCache.DefaultShared);

            // Get an authentication token from AAD
            var authResult = ObtainAuthToken();

            // Create an http client with the authentication header
            client = new HttpClient();
            if (authResult != null && authResult.Result.AccessToken != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Result.AccessToken);
            }
            else
            {
                throw new Exception("GdcoTicketHandler(): Could not obtain auth token after multiple retries.");
            }
        }

        public Model.GdcoTicket PatchTicket(ExternalTicket ticket)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var gdcoTicket = new Model.GdcoTicket();
            if (ticket == null || ticket.Properties == null || !ticket.Properties.Any())
            {
                gdcoTicket.Error = "Invalid Input";
                return gdcoTicket;
            }

            // Get the GDCO ticket
            try
            {
                var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticket.TicketId}";
                var getResponse = client.GetAsync(requestUri).Result;
                if (getResponse.IsSuccessStatusCode)
                {
                    var resultString = getResponse.Content.ReadAsStringAsync().Result;
                    gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                    if(!gdcoTicket.Fields.ContainsKey("System.WorkItemType"))
                    {
                        gdcoTicket.Error = "GDCO ticket has Missing System.WorkItemType field";
                        return gdcoTicket;
                    }

                    string ticketType = (string)gdcoTicket.Fields["System.WorkItemType"];
                    if(string.IsNullOrEmpty(ticketType))
                    {
                        gdcoTicket.Error = "GDCO ticket System.WorkItemType field is empty";
                        return gdcoTicket;
                    }

                    // Update the result in GdcoTicketTable
                    try
                    {
                        var patchRequestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/${ticketType}";
                        var patchTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
                        foreach(var property in ticket.Properties)
                        {
                            patchTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                            {
                                op = "Add",
                                Path = $"/fields/{property.Key}",
                                Value = property.Value
                            });
                        }

                        var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(patchTicketJsonPatchDocumentList);
                        var patchMethod = new HttpMethod("PATCH");
                        var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
                        var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
                        {
                            Content = payload
                        };
                        var response = client.SendAsync(patchMessage).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(response.Content.ReadAsStringAsync().Result);
                            gdcoTicket.Error = "Success";
                        }
                        else
                        {
                            switch (response.StatusCode)
                            {
                                case HttpStatusCode.Unauthorized:
                                    gdcoTicket.Error = "Sorry, you don't have access to the Service.";
                                    break;
                                case HttpStatusCode.NotFound:
                                    gdcoTicket.Error = "Sorry, requested item was not found.";
                                    break;
                                default:
                                    gdcoTicket.Error = "Sorry, an error occurred accessing the endpoint. Please try again. " + response.ReasonPhrase;
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        gdcoTicket.Error = "Exception in Updating GDCO Ticket " + ex.ToString();
                    }
                }
                else
                {
                    switch (getResponse.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            gdcoTicket.Error = "Sorry, you don't have access to the Service.";
                            break;
                        case HttpStatusCode.NotFound:
                            gdcoTicket.Error = "Sorry, requested item was not found.";
                            break;
                        default:
                            gdcoTicket.Error = "Sorry, an error occurred accessing the endpoint. Please try again. " + getResponse.ReasonPhrase;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                gdcoTicket.Error = "Exception in Fetching GDCO Ticket. " + ex.ToString();
            }
            return gdcoTicket;
        }

        public Model.GdcoTicket GetTicket(long ticketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var gdcoTicket = new Model.GdcoTicket();

            // Get the GDCO ticket
            try
            {
                var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
                var getResponse = client.GetAsync(requestUri).Result;
                if (getResponse.IsSuccessStatusCode)
                {
                    var resultString = getResponse.Content.ReadAsStringAsync().Result;
                    gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);
                }
                else
                {
                    switch (getResponse.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            gdcoTicket.Error = "Sorry, you don't have access to the Service.";
                            break;
                        case HttpStatusCode.NotFound:
                            gdcoTicket.Error = "Sorry, requested item was not found.";
                            break;
                        default:
                            gdcoTicket.Error = "Sorry, an error occurred accessing the endpoint. Please try again. " + getResponse.ReasonPhrase;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                gdcoTicket.Error = "Exception in Fetching GDCO Ticket. " + ex.ToString();
            }
            return gdcoTicket;
        }

        public string CreateTicket(string ticketType, string errorCode, string errorTitle, string errorDescription, string dcCode, string requestOwner, string severity)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var gdcoTicket = new Model.GdcoTicket();
            var gdcoMessage = string.Empty;

            if (ticketType == null || string.IsNullOrEmpty(ticketType.Trim()) ||
                errorCode == null || string.IsNullOrEmpty(errorCode.Trim()) ||
                errorTitle == null || string.IsNullOrEmpty(errorTitle.Trim()) ||
                errorDescription == null || string.IsNullOrEmpty(errorDescription.Trim()) ||
                dcCode == null || string.IsNullOrEmpty(dcCode.Trim()) ||
                requestOwner == null || string.IsNullOrEmpty(requestOwner.Trim()) ||
                severity == null || string.IsNullOrEmpty(severity.Trim()))
            {
                gdcoTicket.Error = "One or more mandatory parameters - errorCode, errorDescription, dcCode, requestOwner, severity missing to create GDCO Ticket.";
                gdcoMessage = "Failed: " + gdcoTicket.Error;
                return gdcoMessage;
            }

            ticketType = ticketType.Trim();
            errorCode = errorCode.Trim();
            errorTitle = errorTitle.Trim();
            errorDescription = errorDescription.Trim();
            dcCode = dcCode.Trim();
            requestOwner = requestOwner.Trim();
            severity = severity.Trim();

            if(!requestOwner.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                requestOwner += "@gmail.com";
            }

            // Trim errorDescription if it is very lengthy
            int maxErrorDescriptionLength = 50000;
            errorDescription = errorDescription.Length > maxErrorDescriptionLength ? errorDescription.Substring(0, maxErrorDescriptionLength) : errorDescription;
            

            // Create the GDCO ticket
            var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/${ticketType}";
            var createTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.Code",
                Value = errorCode
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/System.Title",
                Value = errorTitle
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/System.Description",
                Value = errorDescription
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.DatacenterCode",
                Value = dcCode
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.RequestOwner",
                Value = requestOwner
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.Severity",
                Value = severity
            });


            var createTicketJsonPatchDocument = JsonConvert.SerializeObject(createTicketJsonPatchDocumentList);
            var patchMethod = new HttpMethod("PATCH");
            var payload = new StringContent(createTicketJsonPatchDocument, Encoding.UTF8, "application/json");
            var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
            {
                Content = payload
            };
            var response = client.SendAsync(patchMessage).Result;
            var resultString = response.Content.ReadAsStringAsync().Result;

            // Wait for 1s after each call
            Thread.Sleep(1000);

            if (response.IsSuccessStatusCode)
            {
                gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);
            }
            else
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        gdcoTicket.Error = "Sorry, you don't have access to the Service.";
                        break;
                    case HttpStatusCode.NotFound:
                        gdcoTicket.Error = "Sorry, requested item was not found.";
                        break;
                    default:
                        gdcoTicket.Error = "Sorry, an error occurred accessing the endpoint. Please try again. " + resultString;
                        break;
                }
            }
            
            if(string.IsNullOrEmpty(gdcoTicket.Error) && gdcoTicket.Id > 0)
            {
                gdcoMessage = "Success. TicketId:" + gdcoTicket.Id;
            }
            else
            {
                gdcoMessage = "Failed. " + gdcoTicket.Error;
            }

            return gdcoMessage;
        }

        /// <summary>
        /// Obtains authentication token with retries.
        /// </summary>
        /// <returns>An authentication result.</returns>
        private async Task<AuthenticationResult> ObtainAuthToken()
        {
            AuthenticationResult result = null;
            var retryCount = 0;
            var retry = false;
            do
            {
                retry = false;
                try
                {
                    // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    var clientCredential = new ClientCredential(clientId, appKey);
                    result = await authContext.AcquireTokenAsync(ticketingServiceResourceId, clientCredential);
                }
                catch (AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(2000);
                    }
                }

            } while ((retry == true) && (retryCount < 3));

            return result;
        }
    }
}

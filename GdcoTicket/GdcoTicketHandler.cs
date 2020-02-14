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
using Utilities;

namespace GdcoTicket
{
    public class GdcoTicketHandler
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

        private static string clientId = ConnectionHandler.GdcoClientId;
        private static string appKey = ConnectionHandler.GdcoAppKey;
        //private static string clientId = ConnectionHandler.GdcoPPEClientId;
        //private static string appKey = ConnectionHandler.GdcoPPEAppKey;

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

        public GdcoTicketHandler()
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

        public Model.GdcoTicket CreateTicket(string projectId, string currentWorkOrder, string errorCode, string errorTitle, string errorDescription, 
            string dcCode, string requestOwner, string severity, string parentTicketId, string mode, string source)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTicket = new Model.GdcoTicket();
            if(string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(errorCode) || string.IsNullOrEmpty(errorDescription) || string.IsNullOrEmpty(dcCode))
            {
                gdcoTicket.Error = "Missing Inputs to create a GDCO ticket.";
                return gdcoTicket;
            }

            if(!ErrorCodeMapping.GfsErrorToGdcoFaultCode.ContainsKey(errorCode) && !ErrorCodeMapping.CisErrorToGdcoFaultCode.ContainsKey(errorCode))
            {
                gdcoTicket.Error = "Unsupported Error Code - " + errorCode;
                return gdcoTicket;
            }

            // Trim errorDescription if it is very lengthy
            int maxErrorDescriptionLength = 50000;
            errorDescription = errorDescription.Length > maxErrorDescriptionLength ? errorDescription.Substring(0, maxErrorDescriptionLength) : errorDescription;

            // Add request owner and request severity
            if(string.IsNullOrEmpty(requestOwner))
            {
                requestOwner = "oadpm";
            }
            severity = "3";
            string gdcoErrorCode = null;
            string ticketType = null;
            if(mode.Equals("P", StringComparison.OrdinalIgnoreCase) || mode.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                ticketType = "GFSDeploymentIncident";
                gdcoErrorCode = ErrorCodeMapping.GfsErrorToGdcoFaultCode[errorCode];
            }
            else if(mode.Equals("M", StringComparison.OrdinalIgnoreCase) || mode.Equals("B", StringComparison.OrdinalIgnoreCase) || mode.Equals("D", StringComparison.OrdinalIgnoreCase))
            {
                ticketType = "DeploymentIncident";
                gdcoErrorCode = ErrorCodeMapping.CisErrorToGdcoFaultCode[errorCode];
            }
            else if (mode.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                ticketType = "DeploymentIncident";
                gdcoErrorCode = ErrorCodeMapping.CisErrorToGdcoFaultCode[errorCode];
            }
            else
            {
                gdcoTicket.Error = "Unsupported Mode to Create Ticket - " + mode;
                return gdcoTicket;
            }

            if(!ErrorCodeMapping.GdcoFaultCodeToDescription.ContainsKey(gdcoErrorCode))
            {
                gdcoTicket.Error = "Unsupported GDCO Error Code to Create Ticket - " + gdcoErrorCode;
                return gdcoTicket;
            }

            if (ErrorCodeMapping.GdcoFaultCodeToDescription.ContainsKey(gdcoErrorCode) && string.IsNullOrEmpty(errorTitle))
            {
                errorTitle = ErrorCodeMapping.GdcoFaultCodeToDescription[gdcoErrorCode];
            }

            // Exception case for BM
            if(!string.IsNullOrEmpty(errorTitle) && (errorTitle.Equals("SCEDO Bare Metal Task Activated Early") || errorTitle.Equals("GFSD Discover Serial Task Activated Early") ||
                errorTitle.Equals("DS Bare Metal Task Activated Early") || errorTitle.Equals("DS OA Task Activated Early")))
            {
                errorTitle = errorTitle + string.Empty;
            }

            string requestUri = null;
            var gdcoTableTicket = GetTicketFromTable(projectId, gdcoErrorCode, dcCode, errorTitle, errorDescription, severity, source);
            if(gdcoTableTicket != null)
            {
                var ticketId = JsonConvert.DeserializeObject<Model.GdcoTicket>(gdcoTableTicket.GdcoTicket).Id;
                requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
                var getResponse = client.GetAsync(requestUri).Result;
                if (getResponse.IsSuccessStatusCode)
                {
                    var resultString = getResponse.Content.ReadAsStringAsync().Result;
                    gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                    // Update the result in GdcoTicketTable
                    try
                    {
                        new GdcoTicketTableHandler().UpdateTicket(gdcoTableTicket.Id, resultString);
                        var state = (string)gdcoTicket.Fields["System.State"];
                        if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            goto CreateTicket;
                        }
                    }
                    catch (Exception ex)
                    {
                        gdcoTicket.Error = ex.ToString();
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
                return gdcoTicket;
            }

            CreateTicket:
            // Create the GDCO ticket
            requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/${ticketType}";
            var createTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.Code",
                Value = gdcoErrorCode
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
                Path = "/fields/GDCOTicketing.Custom.Severity",
                Value = severity
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.RequestOwner",
                Value = requestOwner + "@gmail.com"
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
                Path = "/fields/System.Description",
                Value = errorDescription
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.MDMId",
                Value = projectId
            });

            var createTicketJsonPatchDocument = JsonConvert.SerializeObject(createTicketJsonPatchDocumentList);
            var patchMethod = new HttpMethod("PATCH");
            var payload = new StringContent(createTicketJsonPatchDocument, Encoding.UTF8, "application/json");
            var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
            {
                Content = payload
            };
            var response = client.SendAsync(patchMessage).Result;
            if (response.IsSuccessStatusCode)
            {
                var resultString = response.Content.ReadAsStringAsync().Result;
                gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                // Insert the result in GdcoTicketTable
                try
                {
                    new GdcoTicketTableHandler().InsertTicket(projectId, resultString, source);
                }
                catch (Exception ex)
                {
                    gdcoTicket.Error = ex.ToString();
                }
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

            return gdcoTicket;
        }

        public Model.GdcoTicket CreateErrorTicket(string projectId, string currentWorkOrder, string internalErrorCode, string errorTitle, string errorDescription,
            string dcCode, string requestOwner, string severity, string parentTicketId, string mode, string source)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTicket = new Model.GdcoTicket();
            var gdcoFaultCode = "";
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(internalErrorCode) || string.IsNullOrEmpty(errorDescription) || string.IsNullOrEmpty(dcCode))
            {
                gdcoTicket.Error = "Missing Inputs to create a GDCO ticket.";
                return gdcoTicket;
            }

            if (!ErrorCodeMapping.InternalFcToGdcoFc.ContainsKey(internalErrorCode))
            {
                gdcoTicket.Error = "Unsupported Error Code - " + internalErrorCode;
                return gdcoTicket;
            }

            // Trim errorDescription if it is very lengthy
            int maxErrorDescriptionLength = 50000;
            errorDescription = errorDescription.Length > maxErrorDescriptionLength ? errorDescription.Substring(0, maxErrorDescriptionLength) : errorDescription;

            // Add request owner and request severity
            if (string.IsNullOrEmpty(requestOwner))
            {
                requestOwner = "oadpm";
            }
            severity = "3";
            string gdcoErrorCode = null;
            string ticketType = null;
            if (mode.Equals("P", StringComparison.OrdinalIgnoreCase) || mode.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                ticketType = "GFSDeploymentIncident";
                if (!(currentWorkOrder.IndexOf("Deployment Pre-RTEG Quality Check", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    internalErrorCode = "PreQC";
                }
                gdcoErrorCode = ErrorCodeMapping.InternalFcToGdcoFc[internalErrorCode];
            }
            else if (mode.Equals("M", StringComparison.OrdinalIgnoreCase) || mode.Equals("B", StringComparison.OrdinalIgnoreCase) || mode.Equals("D", StringComparison.OrdinalIgnoreCase) || mode.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                ticketType = "DeploymentIncident";
                gdcoErrorCode = ErrorCodeMapping.InternalFcToGdcoFc[internalErrorCode];
            }
            else
            {
                gdcoTicket.Error = "Unsupported Mode to Create Ticket - " + mode;
                return gdcoTicket;
            }

            if (!ErrorCodeMapping.GdcoFaultCodeToDescription.ContainsKey(gdcoErrorCode))
            {
                gdcoTicket.Error = "Unsupported GDCO Error Code to Create Ticket - " + gdcoErrorCode;
                return gdcoTicket;
            }

            if (ErrorCodeMapping.GdcoFaultCodeToDescription.ContainsKey(gdcoErrorCode))
            {
                gdcoFaultCode = ErrorCodeMapping.InternalFcToGdcoFc[internalErrorCode];
            }
            if (string.IsNullOrEmpty(errorTitle))
            {
                errorTitle = ErrorCodeMapping.GdcoFaultCodeToDescription[gdcoErrorCode];
            }

            // Exception case for BM
            if (!string.IsNullOrEmpty(errorTitle) && (errorTitle.Equals("SCEDO Bare Metal Task Activated Early") || errorTitle.Equals("GFSD Discover Serial Task Activated Early") ||
                errorTitle.Equals("DS Bare Metal Task Activated Early") || errorTitle.Equals("DS OA Task Activated Early")))
            {
                errorTitle = errorTitle + string.Empty;
            }

            string requestUri = null;
            var gdcoTableTicket = GetTicketFromTable(projectId, gdcoErrorCode, dcCode, errorTitle, errorDescription, severity, source);
            if (gdcoTableTicket != null)
            {
                var ticketId = JsonConvert.DeserializeObject<Model.GdcoTicket>(gdcoTableTicket.GdcoTicket).Id;
                requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
                var getResponse = client.GetAsync(requestUri).Result;
                if (getResponse.IsSuccessStatusCode)
                {
                    var resultString = getResponse.Content.ReadAsStringAsync().Result;
                    gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                    // Update the result in GdcoTicketTable
                    try
                    {
                        new GdcoTicketTableHandler().UpdateTicket(gdcoTableTicket.Id, resultString);
                        var state = (string)gdcoTicket.Fields["System.State"];
                        if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            goto CreateTicket;
                        }
                    }
                    catch (Exception ex)
                    {
                        gdcoTicket.Error = ex.ToString();
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
                return gdcoTicket;
            }

            CreateTicket:
            // Create the GDCO ticket
            requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/${ticketType}";
            var createTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.Code",
                Value = gdcoErrorCode
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
                Path = "/fields/GDCOTicketing.Custom.Severity",
                Value = severity
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.RequestOwner",
                Value = requestOwner + "@gmail.com"
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
                Path = "/fields/System.Description",
                Value = errorDescription
            });
            createTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/GDCOTicketing.Custom.MDMId",
                Value = projectId
            });

            var createTicketJsonPatchDocument = JsonConvert.SerializeObject(createTicketJsonPatchDocumentList);
            var patchMethod = new HttpMethod("PATCH");
            var payload = new StringContent(createTicketJsonPatchDocument, Encoding.UTF8, "application/json");
            var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
            {
                Content = payload
            };
            var response = client.SendAsync(patchMessage).Result;
            if (response.IsSuccessStatusCode)
            {
                var resultString = response.Content.ReadAsStringAsync().Result;
                gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                // Insert the result in GdcoTicketTable
                try
                {
                    new GdcoTicketTableHandler().InsertTicket(projectId, resultString, source);
                }
                catch (Exception ex)
                {
                    gdcoTicket.Error = ex.ToString();
                }
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

            return gdcoTicket;
        }

        public List<Model.GdcoTicket> UpdateTicket(string projectId, string source)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTickets = new List<Model.GdcoTicket>();
            if (string.IsNullOrEmpty(projectId))
            {
                gdcoTickets.Add(new Model.GdcoTicket
                {
                    Error = "Missing Inputs to update a GDCO ticket."
                });
                return gdcoTickets;
            }

            var tableTickets = new GdcoTicketTableHandler().GetTickets(projectId, source);
            var tickets = new List<GdcoTableTicket>();
            foreach (var tableTicket in tableTickets)
            {
                var ticket = JsonConvert.DeserializeObject<GdcoTicket.Model.GdcoTicket>(tableTicket.GdcoTicket);
                if (ticket != null && ticket.Fields.ContainsKey("System.State"))
                {
                    var state = ticket.Fields["System.State"].ToString();
                    if (!(state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        tickets.Add(tableTicket);
                    }
                }
            }

            foreach (var ticket in tickets)
            {
                string requestUri = null;
                if (ticket != null)
                {
                    var gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(ticket.GdcoTicket);
                    requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{gdcoTicket.Id}";
                    var getResponse = client.GetAsync(requestUri).Result;
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var resultString = getResponse.Content.ReadAsStringAsync().Result;
                        gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);
                        var state = (string)gdcoTicket.Fields["System.State"];
                        if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Update the result in GdcoTicketTable
                            try
                            {
                                new GdcoTicketTableHandler().UpdateTicket(ticket.Id, resultString);
                            }
                            catch (Exception ex)
                            {
                                gdcoTicket.Error = ex.ToString();
                            }
                        }
                        else
                        {
                            requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{gdcoTicket.Id}";
                            var updateTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
                            string ticketStatus = null;
                            if(state.Equals("Created", StringComparison.OrdinalIgnoreCase))
                            {
                                ticketStatus = "Canceled";
                            }
                            else
                            {
                                continue;
                            }
                            updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                            {
                                op = "Add",
                                Path = "/fields/System.State",
                                Value = ticketStatus
                            });
                            updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                            {
                                op = "Add",
                                Path = "/fields/GDCOTicketing.Custom.ResolutionDescription",
                                Value = "Completed"
                            });
                            var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(updateTicketJsonPatchDocumentList);
                            var patchMethod = new HttpMethod("PATCH");
                            var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
                            var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
                            {
                                Content = payload
                            };
                            var response = client.SendAsync(patchMessage).Result;
                            if (response.IsSuccessStatusCode)
                            {
                                resultString = response.Content.ReadAsStringAsync().Result;
                                gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                                // Update the result in GdcoTicketTable
                                try
                                {
                                    new GdcoTicketTableHandler().UpdateTicket(ticket.Id, resultString);
                                }
                                catch (Exception ex)
                                {
                                    gdcoTicket.Error = ex.ToString();
                                }
                            }
                            else
                            {
                                var responseContent = response.Content.ReadAsStringAsync().Result;
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
                    gdcoTickets.Add(gdcoTicket);
                }
            }
            return gdcoTickets;
        }

        public List<Model.GdcoTicket> UpdateFailedTicket(string projectId, string source, HashSet<String> failedDescriptions)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTickets = new List<Model.GdcoTicket>();
            if (string.IsNullOrEmpty(projectId))
            {
                gdcoTickets.Add(new Model.GdcoTicket
                {
                    Error = "Missing Inputs to update a GDCO ticket."
                });
                return gdcoTickets;
            }

            var tableTickets = new GdcoTicketTableHandler().GetTickets(projectId, source);

            if(tableTickets == null || !tableTickets.Any())
            {
                return gdcoTickets;
            }

            foreach (var ticket in tableTickets)
            {
                string requestUri = null;
                if (ticket != null)
                {
                    var gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(ticket.GdcoTicket);
                    requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{gdcoTicket.Id}";
                    var getResponse = client.GetAsync(requestUri).Result;
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var resultString = getResponse.Content.ReadAsStringAsync().Result;
                        gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);

                        // If the current active ticket's description is not in the QC run errors, we close current ticket
                        String description = "";
                        if (gdcoTicket.Fields.ContainsKey("System.Description"))
                        {
                            description = gdcoTicket.Fields["System.Description"].ToString();
                            description = description.Length > 1000 ? description.Substring(0, 1000) : description;
                        }

                        if (!failedDescriptions.Contains(description))
                        {
                            // Resolve the current ticket
                            ResolveTicket(gdcoTicket.Id);
                        }

                        try
                        {
                            new GdcoTicketTableHandler().UpdateTicket(ticket.Id, resultString);
                        }
                        catch (Exception ex)
                        {
                            gdcoTicket.Error = ex.ToString();
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
                    gdcoTickets.Add(gdcoTicket);
                }
            }
            return gdcoTickets;
        }

        public Dictionary<string, List<Model.GdcoTicket>> GetTickets(List<string> projectIds, string source)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTicketsDictionary = new Dictionary<string, List<Model.GdcoTicket>>();
            var gdcoTableTicketsDictionary = new GdcoTicketTableHandler().GetTickets(projectIds, source);
            if(gdcoTableTicketsDictionary == null || !gdcoTableTicketsDictionary.Any())
            {
                return gdcoTicketsDictionary;
            }
            foreach (var projectId in gdcoTableTicketsDictionary.Keys)
            {
                var gdcoTableTickets = gdcoTableTicketsDictionary[projectId];
                foreach (var gdcoTableTicket in gdcoTableTickets)
                {
                    var gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(gdcoTableTicket.GdcoTicket);
                    var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{gdcoTicket.Id}";
                    var getResponse = client.GetAsync(requestUri).Result;
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var resultString = getResponse.Content.ReadAsStringAsync().Result;
                        // Update the result in GdcoTicketTable
                        try
                        {
                            new GdcoTicketTableHandler().UpdateTicket(gdcoTableTicket.Id, resultString);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            foreach (var projectId in gdcoTableTicketsDictionary.Keys)
            {
                var gdcoTableTickets = gdcoTableTicketsDictionary[projectId];
                foreach(var gdcoTableTicket in gdcoTableTickets)
                {
                    var gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(gdcoTableTicket.GdcoTicket);
                    if (!gdcoTicketsDictionary.ContainsKey(projectId))
                    {
                        gdcoTicketsDictionary[projectId] = new List<Model.GdcoTicket> { gdcoTicket };
                    }
                    else
                    {
                        var gdcoTicketList = gdcoTicketsDictionary[projectId];
                        gdcoTicketList.Add(gdcoTicket);
                    }
                }
            }
            return gdcoTicketsDictionary;
        }

        public Dictionary<string, List<TicketingObject>> GetTicketsByFulfillmentId(List<string> FulfillmentIdList)
        {
            StringBuilder FilterContent = new StringBuilder();
            int retryMax = 3;
            int retryCount = 0;

            if (FulfillmentIdList != null && FulfillmentIdList.Any())
            {
                int i = 0;
                for (; i < FulfillmentIdList.Count - 1; i++)
                {
                    string content = String.Format("FulfillmentId eq '{0}'", FulfillmentIdList[i]);
                    FilterContent.Append(content);
                    FilterContent.Append(" or ");
                }
                FilterContent.Append(String.Format("FulfillmentId eq '{0}'", FulfillmentIdList[i]));
            }

            RequestDoc request = new RequestDoc()
            {
                Count = true,
                Filter = FilterContent.ToString(),
            };
            
            // Need to return <FulfillmentId, ListOfTicketingObject>
            Dictionary<string, List<TicketingObject>> resDictionary = new Dictionary<string, List<TicketingObject>>();

            try
            {
                var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/search";
                var queryJsonObject = JsonConvert.SerializeObject(request);
                var patchMethod = new HttpMethod("POST");
                var payload = new StringContent(queryJsonObject, Encoding.UTF8, "application/json");
                var postMessage = new HttpRequestMessage(patchMethod, requestUri)
                {
                    Content = payload
                };
                var response = client.SendAsync(postMessage).Result;
                Dictionary<string, TicketingObject> ticketDictionary = new Dictionary<string, TicketingObject>();

                if (response.IsSuccessStatusCode)
                {
                    var resultString = response.Content.ReadAsStringAsync().Result;
                    var tickets = JsonConvert.DeserializeObject<TicketingList>(resultString);

                    // Use pagination to get all of the tickets
                    var totalTicketCount = tickets.count;
                    var currentTicketCount = tickets.Tickets.Count;
                    List<TicketingObject> ticketList = tickets.Tickets;

                    while (currentTicketCount < totalTicketCount)
                    {
                        request.Skip = currentTicketCount;
                        queryJsonObject = JsonConvert.SerializeObject(request);
                        patchMethod = new HttpMethod("POST");
                        payload = new StringContent(queryJsonObject, Encoding.UTF8, "application/json");
                        postMessage = new HttpRequestMessage(patchMethod, requestUri)
                        {
                            Content = payload
                        };
                        response = client.SendAsync(postMessage).Result;
                        resultString = response.Content.ReadAsStringAsync().Result;
                        tickets = JsonConvert.DeserializeObject<TicketingList>(resultString);

                        if (response.IsSuccessStatusCode)
                        {
                            resultString = response.Content.ReadAsStringAsync().Result;
                            tickets = JsonConvert.DeserializeObject<TicketingList>(resultString);
                            ticketList.AddRange(tickets.Tickets);
                            currentTicketCount = ticketList.Count;
                            retryCount = 0;
                        }
                        else
                        {
                            if (retryCount < retryMax)
                            {
                                retryCount++;
                                Thread.Sleep(2000);
                            }
                            else
                            {
                                throw new Exception("Error in getting tickets, response code: " + response.Content.ReadAsStringAsync());
                            }
                        }
                    }

                    // Put <TicketID, List<TicketingObject>> to dictionary
                    foreach (var ticket in ticketList)
                    {
                        ticketDictionary[ticket.TicketId] = ticket;
                    }

                    // Get rest of Ticket information from GDCOTicketStoreHandler
                    GDCOTicketStoreHandler ticketStoreHandler = new GDCOTicketStoreHandler();
                    Dictionary<string, TicketStoreV2> ticketStoreDic = ticketStoreHandler.GetTicketStore(ticketDictionary.Keys.ToList());

                    if (ticketDictionary != null && ticketDictionary.Any() && ticketStoreDic != null && ticketStoreDic.Any())
                    {
                        // Fill in the rest of information we missed from Ticketing system
                        foreach (var ticketId in ticketStoreDic.Keys.ToList())
                        {
                            var DeliveryNumber = ticketStoreDic[ticketId].DeliveryNumber;
                            var TemplateType = ticketStoreDic[ticketId].TemplateType;
                            var WasSLABreached = ticketStoreDic[ticketId].WasSLABreached;

                            if (ticketDictionary.ContainsKey(ticketId))
                            {
                                ticketDictionary[ticketId].DeliveryNumber = DeliveryNumber;
                                ticketDictionary[ticketId].TemplateType = TemplateType;
                                ticketDictionary[ticketId].WasSLABreached = WasSLABreached;

                                ticketDictionary[ticketId].CreatedDate = ticketStoreDic[ticketId].CreatedDate;
                                ticketDictionary[ticketId].AssignedDate = ticketStoreDic[ticketId].AssignedDate;
                                ticketDictionary[ticketId].ResolvedDate = ticketStoreDic[ticketId].ResolvedDate;
                                ticketDictionary[ticketId].DueDate = ticketStoreDic[ticketId].DueDate;
                            }
                        }
                    }
                    // Create <FulfillmentId, List<TicketingObject>> dictionary
                    foreach (var ticket in ticketDictionary.Values.ToList())
                    {
                        if (resDictionary.ContainsKey(ticket.FulfillmentId))
                        {
                            resDictionary[ticket.FulfillmentId].Add(ticket);
                        }
                        else
                        {
                            resDictionary[ticket.FulfillmentId] = new List<TicketingObject>();
                            resDictionary[ticket.FulfillmentId].Add(ticket);
                        }
                    }
                }
                else
                {
                    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, "Error in GetTicketsByFulfillmentId", response.Content.ReadAsStringAsync().Result);
                }
            }
            catch (Exception ex)
            {
                SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.automationTeam, "Exception in GetTicketsByFulfillmentId", ex.ToString());
            }


            return resDictionary;
        }

        public Model.GdcoTicket BlockTicket(long parentTicketId, long childTicketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            // If child ticket in resolved or cancelled state, dont block the ticket
            var childTicket = GetGdcoTicket(childTicketId);
            if (childTicket != null && childTicket.Fields != null && childTicket.Fields.ContainsKey("System.State"))
            {
                var state = (string)childTicket.Fields["System.State"];
                if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return GetGdcoTicket(parentTicketId);
                }
            }

            // Block the ticket by adding parent-child relationship
            var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{childTicketId}";
            var updateTicketJsonPatchDocumentList = new List<GdcoTicketProperty2>();
            updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty2
            {
                op = "Add",
                Path = "/relations/-",
                Value = new Value { Id = parentTicketId, Rel = "System.LinkTypes.Hierarchy-Reverse" }
            });
            var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(updateTicketJsonPatchDocumentList);
            var patchMethod = new HttpMethod("PATCH");
            var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
            HttpResponseMessage response = null;
            var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
            {
                Content = payload
            };
            try
            {
                response = client.SendAsync(patchMessage).Result;
            }
            catch(Exception ex)
            {
                //if (response != null && !response.IsSuccessStatusCode)
                //{
                //    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.OADPMAccount, "Error in Blocking Parent Ticket", $"{response.Content.ReadAsStringAsync().Result} ParentTicket: {parentTicketId} ChildTicket: {childTicketId} Exception: {ex.ToString()}");
                //}
            }

            //if (response != null && !response.IsSuccessStatusCode)
            //{
            //    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.OADPMAccount, "Error in Blocking Parent Ticket", $"{response.Content.ReadAsStringAsync().Result} ParentTicket: {parentTicketId} ChildTicket: {childTicketId}");
            //}

            var assignTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
            assignTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
            {
                op = "Add",
                Path = "/fields/System.AssignedTo",
                Value = Constants.serviceAccount
            });
            var assignTicketJsonPatchDocument = JsonConvert.SerializeObject(assignTicketJsonPatchDocumentList);
            var assignPatchMessage = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
            {
                Content = new StringContent(assignTicketJsonPatchDocument, Encoding.UTF8, "application/json")
            };
            try
            {
                var assignTicketResponse = client.SendAsync(patchMessage).Result;
                //if (assignTicketResponse != null && !assignTicketResponse.IsSuccessStatusCode)
                //{
                //    SendEmail.SendExoSkuMsAssetReportEmail(Constants.serviceAccount, Constants.OADPMAccount, "Error in Assigning oadpm to the Blocking Child Ticket", $"{assignTicketResponse.Content.ReadAsStringAsync().Result} ParentTicket: {parentTicketId} ChildTicket: {childTicketId}");
                //}
            }
            catch (Exception)
            {
            }

            return GetGdcoTicket(parentTicketId);
        }

        public void AssignParent(string projectId, long ticketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                var assetTicket = new GfsAccess().GetMsAssetTicket(projectId);
                if (assetTicket != null && !string.IsNullOrEmpty(assetTicket.MsAssetTicketNumber))
                {
                    AssignParent(Convert.ToInt64(assetTicket.MsAssetTicketNumber), ticketId);
                }
            }
            catch (Exception)
            {
            }
        }

        public void AssignParent(long parentTicketId, long childTicketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            try
            {
                var gdcoTicket = GetGdcoTicket(childTicketId);
                if (gdcoTicket != null && gdcoTicket.Relations != null && gdcoTicket.Relations.Any())
                {
                    foreach (var relation in gdcoTicket.Relations)
                    {
                        if (relation.Rel.IndexOf("Dependency-Reverse", StringComparison.OrdinalIgnoreCase) >= 0 || relation.Rel.IndexOf("Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return;
                        }
                    }
                }

                var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{childTicketId}";
                var updateTicketJsonPatchDocumentList = new List<GdcoTicketProperty2>();
                updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty2
                {
                    op = "Add",
                    Path = "/relations/-",
                    Value = new Value { Id = parentTicketId, Rel = "System.LinkTypes.Dependency-Reverse" }
                });
                var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(updateTicketJsonPatchDocumentList);
                var patchMethod = new HttpMethod("PATCH");
                var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
                var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
                {
                    Content = payload
                };
                var response = client.SendAsync(patchMessage).Result;
            }
            catch (Exception)
            {
            }
        }

        public Model.GdcoTicket GetGdcoTicket(long ticketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
            var getResponse = client.GetAsync(requestUri).Result;
            if (getResponse.IsSuccessStatusCode)
            {
                var resultString = getResponse.Content.ReadAsStringAsync().Result;
                var gdcoTicket = JsonConvert.DeserializeObject<Model.GdcoTicket>(resultString);
                return gdcoTicket;
            }
            else
            {
                return null;
            }
        }

        public Model.GdcoTicket ResolveTicket(long ticketId)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTicket = GetGdcoTicket(ticketId);
            if (gdcoTicket != null && gdcoTicket.Fields != null && gdcoTicket.Fields.ContainsKey("System.State"))
            {
                var state = (string)gdcoTicket.Fields["System.State"];
                if (state.Equals("Created", StringComparison.OrdinalIgnoreCase))
                {
                    // update the ticket and assign it to someone.
                    var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
                    var updateTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
                    updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                    {
                        op = "Add",
                        Path = "/fields/System.AssignedTo",
                        Value = Constants.serviceAccount
                    });
                    updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                    {
                        op = "Add",
                        Path = "/fields/System.State",
                        Value = "InProgress"
                    });
                    var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(updateTicketJsonPatchDocumentList);
                    var patchMethod = new HttpMethod("PATCH");
                    var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
                    var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
                    {
                        Content = payload
                    };
                    try
                    {
                        var response = client.SendAsync(patchMessage).Result;
                    }
                    catch (Exception)
                    {
                    }
                }

                //Fetch the updated Ticket
                gdcoTicket = GetGdcoTicket(ticketId);
                if (gdcoTicket != null && gdcoTicket.Fields != null && gdcoTicket.Fields.ContainsKey("System.State"))
                {
                    state = (string)gdcoTicket.Fields["System.State"];
                    if (state.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
                    {
                        // update the ticket and change it to InProgress
                        var requestUri = $"{ticketingServiceTestEndpoint}/api/Tickets/{ticketId}";
                        var updateTicketJsonPatchDocumentList = new List<GdcoTicketProperty>();
                        updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                        {
                            op = "Add",
                            Path = "/fields/System.State",
                            Value = "Resolved"
                        });

                        updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                        {
                            op = "Add",
                            Path = "/fields/GDCOTicketing.Custom.ResolutionCode",
                            Value = "22001"
                        });
                        
                        updateTicketJsonPatchDocumentList.Add(new GdcoTicketProperty
                        {
                            op = "Add",
                            Path = "/fields/System.AssignedTo",
                            Value = Constants.serviceAccount
                        });

                        var updateTicketJsonPatchDocument = JsonConvert.SerializeObject(updateTicketJsonPatchDocumentList);
                        var patchMethod = new HttpMethod("PATCH");
                        var payload = new StringContent(updateTicketJsonPatchDocument, Encoding.UTF8, "application/json");
                        var patchMessage = new HttpRequestMessage(patchMethod, requestUri)
                        {
                            Content = payload
                        };
                        try
                        {
                            var response = client.SendAsync(patchMessage).Result;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            return GetGdcoTicket(ticketId);
        }

        public GdcoTableTicket GetTicketFromTable(string projectId, string gdcoErrorCode, string dcCode, string errorTitle, string errorDescription, string severity, string source)
        {
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var gdcoTicket = new Model.GdcoTicket();
            var tickets = new List<GdcoTableTicket>();
            try
            {
                tickets = new GdcoTicketTableHandler().GetTickets(projectId, source);
            }
            catch(Exception ex)
            {
                return null;
            }

            if(tickets == null || !tickets.Any())
            {
                return null;
            }

            foreach(var ticket in tickets)
            {
                var gdcoTicketObject = JsonConvert.DeserializeObject<Model.GdcoTicket>(ticket.GdcoTicket);
                if (gdcoTicketObject.Fields.ContainsKey("System.State"))
                {
                    var state = (string)gdcoTicketObject.Fields["System.State"];
                    if (state.IndexOf("Canceled", StringComparison.OrdinalIgnoreCase) >= 0 || state.IndexOf("Resolved", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                }
                string gdcoTicketErrorCode = null;
                if(gdcoTicketObject.Fields.ContainsKey("GDCOTicketing.Custom.Code"))
                {
                    gdcoTicketErrorCode = gdcoTicketObject.Fields["GDCOTicketing.Custom.Code"].ToString();
                }

                string gdcoTicketDcCode = null;
                if (gdcoTicketObject.Fields.ContainsKey("GDCOTicketing.Custom.DatacenterCode"))
                {
                    gdcoTicketDcCode = gdcoTicketObject.Fields["GDCOTicketing.Custom.DatacenterCode"].ToString();
                }

                string gdcoTicketTitle = null;
                if (gdcoTicketObject.Fields.ContainsKey("System.Title"))
                {
                    gdcoTicketTitle = gdcoTicketObject.Fields["System.Title"].ToString();
                }

                string gdcoTicketDescription = null;
                if (gdcoTicketObject.Fields.ContainsKey("System.Description"))
                {
                    gdcoTicketDescription = gdcoTicketObject.Fields["System.Description"].ToString();
                }

                var isSubsetError = gdcoTicketDescription.IndexOf(errorDescription, StringComparison.OrdinalIgnoreCase) >= 0;
                errorDescription = errorDescription.Length > 100 ? errorDescription.Substring(0, 100) : errorDescription;
                gdcoTicketDescription = gdcoTicketDescription.Length > 100 ? gdcoTicketDescription.Substring(0, 100) : gdcoTicketDescription;

                if (gdcoErrorCode.Trim().Equals(gdcoTicketErrorCode.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    dcCode.Trim().Equals(gdcoTicketDcCode.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    errorTitle.Trim().Equals(gdcoTicketTitle.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    (isSubsetError || StringSimilarity.CalculateSimilarity(errorDescription.Trim(), gdcoTicketDescription.Trim()) >= 0.90))
                {
                    return ticket;
                }
            }
            return null;
        }

        /// <summary>
        /// Obtains authentication token with retries.
        /// </summary>
        /// <returns>An authentication result.</returns>
        private async System.Threading.Tasks.Task<AuthenticationResult> ObtainAuthToken()
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

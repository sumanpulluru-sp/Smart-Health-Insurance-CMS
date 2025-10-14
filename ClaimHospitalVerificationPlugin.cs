using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using System.Net.Http;
using Newtonsoft.Json;

namespace Smart_Health_Insurance_CMS
{
    public class ClaimHospitalVerificationPlugin : IPlugin
    {
        private readonly string _flowEndpoint;

        // This constructor gets called by the Plugin Registration Tool when you pass config strings
        public ClaimHospitalVerificationPlugin(string unsecureConfig, string secureConfig)
        {
            // Expect unsecureConfig to contain the Flow HTTP POST URL.
            _flowEndpoint = unsecureConfig;
        }

        // Parameterless constructor for tooling or tests
        public ClaimHospitalVerificationPlugin() { }
        public void Execute(IServiceProvider serviceProvider)
        {
            //your plugin code goes here

            // Obtain the execution context
            IPluginExecutionContext context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the tracing service to log errors
            ITracingService tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the organization service factory and service
            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            IOrganizationService service =
                serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                    return;

                var target = (Entity)context.InputParameters["Target"];

                // Adjust schema name if your hospital field is different
                if (!target.Contains("sp_hospitalname"))
                    return;

                var hospitalName = (target["sp_hospitalname"] ?? string.Empty).ToString();
                if (string.IsNullOrWhiteSpace(hospitalName))
                    return;

                // Get endpoint from constructor config, fallback to environment variable if null
                var endpoint = _flowEndpoint ?? Environment.GetEnvironmentVariable("HOSPITAL_VERIFY_ENDPOINT");
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    tracingService.Trace("Hospital verification endpoint not configured.");
                    // Option: allow creation, or block. We'll block with friendly message:
                    throw new InvalidPluginExecutionException("Hospital verification service is not configured. Contact administrator.");
                }

                // Prepare HTTP request payload
                var payload = new { hospitalName = hospitalName };
                var jsonPayload = JsonConvert.SerializeObject(payload);

                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(10); // short timeout
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    // Send synchronously (block) - safe in plugin but watch latency
                    var resp = http.PostAsync(endpoint, content).Result;

                    if (!resp.IsSuccessStatusCode)
                    {
                        tracingService.Trace($"Hospital verification service returned non-success: {resp.StatusCode}");
                        throw new InvalidPluginExecutionException("Hospital verification service returned an error. Try again later.");
                    }

                    var respContent = resp.Content.ReadAsStringAsync().Result;
                    dynamic result = JsonConvert.DeserializeObject(respContent);

                    bool verified = false;
                    string hospitalId = null;
                    try
                    {
                        verified = result?.verified ?? false;
                        hospitalId = result?.hospitalId;
                    }
                    catch
                    {
                        tracingService.Trace("Hospital verification response parsing failed.");
                        throw new InvalidPluginExecutionException("Invalid response from hospital verification service.");
                    }

                    if (!verified)
                    {
                        // Block creation with user-friendly message
                        throw new InvalidPluginExecutionException("Hospital could not be verified. Claim creation aborted.");
                    }

                    // If verified, set the hospital id on the target entity so it's persisted with the create
                    if (!string.IsNullOrWhiteSpace(hospitalId))
                    {
                        target["sp_hospitalid"] = hospitalId;
                        // No need to call Update — in PreOperation modifications to 'target' are persisted.
                    }
                }
            }
            catch (InvalidPluginExecutionException)
            {
                // Let CRM show friendly message to user
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Unexpected error in ClaimHospitalVerificationPlugin: " + ex.ToString());
                // Fail gracefully: provide meaningful message
                throw new InvalidPluginExecutionException("An error occurred while verifying the hospital. Please try again or contact support.");
            }
        }
    }
}

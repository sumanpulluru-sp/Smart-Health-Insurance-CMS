using System.Security.Policy;
using System;
using Microsoft.Xrm.Sdk;

namespace Smart_Health_Insurance_CMS
{
    public class claimvalidate : IPlugin
    {
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

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity)) return;
            var claim = (Entity)context.InputParameters["Target"];
            var claimDate = claim.Attributes.Contains("sp_claimdate") ? claim.GetAttributeValue<DateTime>("sp_claimdate") : DateTime.MinValue;

            tracingService.Trace("claimDate is: " + claimDate);
            tracingService.Trace("claimDate minvalue is: " + DateTime.MinValue);

            if (claimDate != DateTime.MinValue && claimDate.Date > DateTime.Now.Date)
            {
                tracingService.Trace("Entered into first expecetion");
                throw new InvalidPluginExecutionException("Claim Date cannot be in the future.");
            }
            if (!claim.Attributes.Contains("sp_claimamount") || ((Money)claim["sp_claimamount"])?.Value <= 0)
            {
                tracingService.Trace("Claim Amount is: "+ ((Money)claim["sp_claimamount"])?.Value);
                tracingService.Trace("Entered into second expecetion");
                throw new InvalidPluginExecutionException("Claim Amount must be greater than 0.");
            }
        }
    }
}

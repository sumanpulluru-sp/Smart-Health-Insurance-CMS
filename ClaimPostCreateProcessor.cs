using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;

namespace Smart_Health_Insurance_CMS
{
    public class ClaimPostCreateProcessor : IPlugin
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
            var claimId = claim.Id;
            tracingService.Trace("claimId: " + claimId);
            // Retrieve full claim record to get amount
            var retrieveClaimDetails = service.Retrieve("sp_spclaim", claimId, new Microsoft.Xrm.Sdk.Query.ColumnSet("sp_claimamount"));

            var amount = retrieveClaimDetails.Attributes.Contains("sp_claimamount") ? retrieveClaimDetails.GetAttributeValue<Money>("sp_claimamount")?.Value : 0;

            tracingService.Trace("amount: " + amount);
            if (amount > 50000)
            {
                tracingService.Trace("Insie if amount condition:" + amount);
                // 1) Update claim status to Under Review
                Entity claimRecord = new Entity("sp_spclaim"); //{ Id = claimId };
                claimRecord.Id = claimId;
                claimRecord["statuscode"] = new OptionSetValue(ClaimStatusCodes.UnderReview);
                service.Update(claimRecord);
                tracingService.Trace("Insie if amount condition:" + amount);
            }
            tracingService.Trace("Before audit history");
            var audit = new Entity("sp_audithistory");
            audit["sp_name"] = "Audit for " + claimId;
            audit["sp_spclaim"] = new EntityReference("sp_spclaim", claimId);
            audit["sp_action"] = "Auto-UnderReview";
            //audit["sp_description"] = $"Auto-set to Under Review because amount {amount:C}";
            audit["sp_actiondate"] = DateTime.Now.Date;
            service.Create(audit);
            tracingService.Trace("After audit history");
        }
    }
}

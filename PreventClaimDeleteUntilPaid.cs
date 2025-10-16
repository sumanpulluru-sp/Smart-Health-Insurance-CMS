using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;

namespace Smart_Health_Insurance_CMS
{
    public class PreventClaimDeleteUntilPaid : IPlugin
        {
            public void Execute(IServiceProvider serviceProvider)
            {
                // 1. Setup Context
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                // 2. Ensure this is on Delete message
                if (context.MessageName.ToLower() != "delete" || context.InputParameters["Target"] == null)
                    return;

                EntityReference claimRef = (EntityReference)context.InputParameters["Target"];
                tracingService.Trace("Deleting Claim Record ID: {0}", claimRef.Id);

            try
                {
                    // 3. Retrieve related Claim Settlement records
                    QueryExpression query = new QueryExpression("sp_spclaimsettlement")
                    {
                        ColumnSet = new ColumnSet("sp_settlementstatus"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                        {
                            new ConditionExpression("sp_claimdetails", ConditionOperator.Equal, claimRef.Id)
                        }
                        }
                    };

                tracingService.Trace("Executing query to retrieve related Claim Settlement record...");
                EntityCollection settlements = service.RetrieveMultiple(query);
                tracingService.Trace("Settlements found: {0}", settlements.Entities.Count);

                if (settlements.Entities.Count > 0)
                    {
                        foreach (var settlement in settlements.Entities)
                        {
                            if (settlement.Contains("sp_settlementstatus"))
                            {
                                int status = (int)settlement["sp_settlementstatus"];

                                // Check if not Paid (Paid = 126530002)
                                if (status != 126530002)
                                {
                                    tracingService.Trace("Settlement not Paid. Throwing business exception.");
                                    throw new InvalidPluginExecutionException(" Cannot delete Claim. All related Claim Settlements must be fully Paid before deletion.");
                                }
                            }
                        }
                    }
                tracingService.Trace("Settlement is Paid. Deletion allowed.");
            }
                catch (InvalidPluginExecutionException)
                {
                    throw; // rethrow custom validation message
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException("Error in PreventClaimDeleteUntilPaid Plugin: " + ex.Message);
                }
            }
    }
   
}






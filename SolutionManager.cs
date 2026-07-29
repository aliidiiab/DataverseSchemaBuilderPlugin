using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages; // AddSolutionComponentRequest lives here, not Microsoft.Xrm.Sdk.Messages

namespace DataverseSchemaBuilderPlugin
{
    /// <summary>
    /// Creates a solution (and resolves its publisher) if it doesn't already exist,
    /// and adds table/field/relationship components to it.
    /// </summary>
    public static class SolutionManager
    {
        // Documented Dataverse solutioncomponent "componenttype" values.
        // Verify against Microsoft's current "componenttype Choices/Options" reference
        // if AddSolutionComponentRequest rejects them on your SDK version.
        public const int ComponentTypeEntity = 1;
        public const int ComponentTypeAttribute = 2;
        public const int ComponentTypeEntityRelationship = 10;

        public static void EnsureSolutionExists(IOrganizationService service, SolutionDefinition solution, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(solution.UniqueName))
                throw new InvalidOperationException("SolutionDefinition.UniqueName is required.");

            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, solution.UniqueName);

            var existing = service.RetrieveMultiple(query);
            if (existing.Entities.Count > 0)
            {
                log("Solution '" + solution.UniqueName + "' already exists — using it.");
                return;
            }

            var publisherId = GetPublisherId(service, solution.PublisherUniqueName);

            var solutionEntity = new Entity("solution");
            solutionEntity["uniquename"] = solution.UniqueName;
            solutionEntity["friendlyname"] = string.IsNullOrWhiteSpace(solution.FriendlyName) ? solution.UniqueName : solution.FriendlyName;
            solutionEntity["publisherid"] = new EntityReference("publisher", publisherId);
            solutionEntity["version"] = string.IsNullOrWhiteSpace(solution.Version) ? "1.0.0.0" : solution.Version;

            service.Create(solutionEntity);
            log("Solution '" + solution.UniqueName + "' created.");
        }

        private static Guid GetPublisherId(IOrganizationService service, string publisherUniqueName)
        {
            var query = new QueryExpression("publisher")
            {
                ColumnSet = new ColumnSet("publisherid"),
                Criteria = new FilterExpression()
            };

            if (!string.IsNullOrWhiteSpace(publisherUniqueName))
            {
                query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, publisherUniqueName);
            }
            else
            {
                query.TopCount = 1; // fall back to whichever publisher is found first
            }

            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
                throw new InvalidOperationException(
                    "Could not find a publisher" +
                    (string.IsNullOrWhiteSpace(publisherUniqueName) ? "." : " with unique name '" + publisherUniqueName + "'.") +
                    " Create a publisher first, or set SolutionDefinition.PublisherUniqueName to an existing one.");

            return result.Entities[0].Id;
        }

        public static void AddComponent(IOrganizationService service, Guid componentId, int componentType, string solutionUniqueName)
        {
            var request = new AddSolutionComponentRequest
            {
                ComponentId = componentId,
                ComponentType = componentType,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = false
            };
            service.Execute(request);
        }
    }
}

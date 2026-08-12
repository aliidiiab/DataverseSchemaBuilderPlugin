using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Messages;

namespace DataverseSchemaBuilderPlugin
{
    public static class DataverseSchemaBuilder
    {
        /// <summary>
        /// Creates the table (entity) described in the workbook, if it doesn't already exist.
        /// Pass a solutionUniqueName to also add the table to that solution (existing or newly created).
        /// </summary>
        public static void CreateTable(IOrganizationService service, TableDefinition table, string solutionUniqueName, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(table.LogicalName))
                throw new InvalidOperationException(
                    "TableDefinition.LogicalName is empty — check the 'Table Schema/Logical Name' column on the 'Tables' sheet.");

            var existingId = GetEntityMetadataId(service, table.LogicalName);
            if (existingId.HasValue)
            {
                log("Table '" + table.LogicalName + "' already exists — skipping creation.");
                AddToSolutionIfRequested(service, existingId.Value, SolutionManager.ComponentTypeEntity, solutionUniqueName, table.LogicalName, log);
                return;
            }

            var request = new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = table.LogicalName,
                    DisplayName = new Label(table.DisplayName, 1033),
                    DisplayCollectionName = new Label(table.PluralDisplayName, 1033),
                    OwnershipType = OwnershipTypes.UserOwned,
                    IsActivity = false,
                    HasNotes = false,
                    HasActivities = false,
                },
                PrimaryAttribute = new StringAttributeMetadata
                {
                    SchemaName = table.Prefix + "name",
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    MaxLength = 100,
                    DisplayName = new Label("Name", 1033),
                }
            };

            var response = (CreateEntityResponse)service.Execute(request);
            log("Table '" + table.LogicalName + "' created.");

            AddToSolutionIfRequested(service, response.EntityId, SolutionManager.ComponentTypeEntity, solutionUniqueName, table.LogicalName, log);
        }

        /// <summary>
        /// Creates every field described in the workbook against the given table.
        /// Lookups are created last (via a one-to-many relationship) since the
        /// related table must already exist.
        /// </summary>
        public static void CreateFields(IOrganizationService service, string tableLogicalName, List<FieldDefinition> fields, string solutionUniqueName, Action<string> log)
        {
            foreach (var field in fields.Where(f => !IsLookupType(f.DataType)))
            {
                CreateSingleField(service, tableLogicalName, field, solutionUniqueName, log);
            }

            foreach (var field in fields.Where(f => IsLookupType(f.DataType)))
            {
                CreateLookupField(service, tableLogicalName, field, solutionUniqueName, log);
            }
        }

        private static bool IsLookupType(string dataType)
        {
            return string.Equals(dataType, "Lookup", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(dataType, "Customer", StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateSingleField(IOrganizationService service, string tableLogicalName, FieldDefinition field, string solutionUniqueName, Action<string> log)
        {
            var existingId = GetAttributeMetadataId(service, tableLogicalName, field.LogicalName);
            if (existingId.HasValue)
            {
                log("Field '" + field.LogicalName + "' already exists — skipping.");
                AddToSolutionIfRequested(service, existingId.Value, SolutionManager.ComponentTypeAttribute, solutionUniqueName, field.LogicalName, log);
                return;
            }

            AttributeMetadata attribute;
            var requiredLevel = MapRequiredLevel(field.RequiredLevel);

            switch (field.DataType.Trim())
            {
                case "String":
                    attribute = new StringAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        MaxLength = field.MaxLengthOrPrecision ?? 100,
                        FormatName = StringFormatName.Text
                    };
                    break;

                case "Memo":
                    attribute = new MemoAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        MaxLength = field.MaxLengthOrPrecision ?? 2000
                    };
                    break;

                case "Integer":
                    attribute = new IntegerAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        Format = IntegerFormat.None
                    };
                    break;

                case "BigInt":
                    attribute = new BigIntAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel
                    };
                    break;

                case "Decimal":
                    attribute = new DecimalAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        Precision = field.MaxLengthOrPrecision ?? 2
                    };
                    break;

                case "Double":
                    attribute = new DoubleAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        Precision = field.MaxLengthOrPrecision ?? 2
                    };
                    break;

                case "Money":
                    attribute = new MoneyAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        Precision = field.MaxLengthOrPrecision ?? 2
                    };
                    break;

                case "Boolean":
                    attribute = new BooleanAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        OptionSet = new BooleanOptionSetMetadata(
                            new OptionMetadata(new Label("Yes", 1033), 1),
                            new OptionMetadata(new Label("No", 1033), 0))
                    };
                    break;

                case "DateTime":
                    attribute = new DateTimeAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        Format = DateTimeFormat.DateAndTime
                    };
                    break;

                //case "Uniqueidentifier":
                //    attribute = new UniqueIdentifierAttributeMetadata
                //    {
                //        SchemaName = field.LogicalName,
                //        DisplayName = new Label(field.DisplayName, 1033)
                //    };
                //    break;

                //case "EntityName":
                //    attribute = new EntityNameAttributeMetadata
                //    {
                //        SchemaName = field.LogicalName,
                //        DisplayName = new Label(field.DisplayName, 1033)
                //    };
                //    break;

                case "Picklist":
                    attribute = new PicklistAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        OptionSet = BuildOptionSet(field.OptionSetValues)
                    };
                    break;

                case "MultiSelectPicklist":
                    attribute = new MultiSelectPicklistAttributeMetadata
                    {
                        SchemaName = field.LogicalName,
                        DisplayName = new Label(field.DisplayName, 1033),
                        RequiredLevel = requiredLevel,
                        OptionSet = BuildOptionSet(field.OptionSetValues)
                    };
                    break;

                default:
                    throw new NotSupportedException(
                        string.Format("Unsupported data type '{0}' for field '{1}'.", field.DataType, field.LogicalName));
            }

            var request = new CreateAttributeRequest
            {
                EntityName = tableLogicalName,
                Attribute = attribute
            };

            var response = (CreateAttributeResponse)service.Execute(request);
            log("Field '" + field.LogicalName + "' (" + field.DataType + ") created.");

            AddToSolutionIfRequested(service, response.AttributeId, SolutionManager.ComponentTypeAttribute, solutionUniqueName, field.LogicalName, log);
        }

        private static void CreateLookupField(IOrganizationService service, string tableLogicalName, FieldDefinition field, string solutionUniqueName, Action<string> log)
        {
            var relationshipSchemaName = field.Prefix + field.RelatedTableLogicalName + "_" + tableLogicalName;
            var existingAttrId = GetAttributeMetadataId(service, tableLogicalName, field.LogicalName);

            if (existingAttrId.HasValue)
            {
                log("Lookup field '" + field.LogicalName + "' already exists — skipping.");
                AddToSolutionIfRequested(service, existingAttrId.Value, SolutionManager.ComponentTypeAttribute, solutionUniqueName, field.LogicalName, log);

                var existingRelId = GetRelationshipMetadataId(service, relationshipSchemaName);
                if (existingRelId.HasValue)
                    AddToSolutionIfRequested(service, existingRelId.Value, SolutionManager.ComponentTypeEntityRelationship, solutionUniqueName, relationshipSchemaName, log);
                return;
            }

            if (string.IsNullOrWhiteSpace(field.RelatedTableLogicalName))
                throw new InvalidOperationException(
                    "Field '" + field.LogicalName + "' is a Lookup but no Related Table Logical Name was provided in the workbook.");

            var request = new CreateOneToManyRequest
            {
                Lookup = new LookupAttributeMetadata
                {
                    SchemaName = field.LogicalName,
                    DisplayName = new Label(field.DisplayName, 1033),
                    RequiredLevel = MapRequiredLevel(field.RequiredLevel)
                },
                OneToManyRelationship = new OneToManyRelationshipMetadata
                {
                    ReferencedEntity = field.RelatedTableLogicalName,   // "one" side (parent table)
                    ReferencingEntity = tableLogicalName,                // "many" side (this table)
                    SchemaName = relationshipSchemaName
                }
            };

            var response = (CreateOneToManyResponse)service.Execute(request);
            log("Lookup field '" + field.LogicalName + "' -> '" + field.RelatedTableLogicalName + "' created.");

            AddToSolutionIfRequested(service, response.AttributeId, SolutionManager.ComponentTypeAttribute, solutionUniqueName, field.LogicalName, log);
            // NOTE: verify this property name against your installed SDK version - documented
            // as RelationshipId on CreateOneToManyResponse; some SDK builds have historically
            // exposed it as OneToManyRelationshipId instead.
            AddToSolutionIfRequested(service, response.RelationshipId, SolutionManager.ComponentTypeEntityRelationship, solutionUniqueName, relationshipSchemaName, log);
        }

        private static OptionSetMetadata BuildOptionSet(List<string> values)
        {
            var optionSet = new OptionSetMetadata { OptionSetType = OptionSetType.Picklist, IsGlobal = false };
            int value = 100000000; // safe custom starting value; adjust to your org's numbering convention
            foreach (var label in values)
            {
                optionSet.Options.Add(new OptionMetadata(new Label(label, 1033), value));
                value++;
            }
            return optionSet;
        }

        private static AttributeRequiredLevelManagedProperty MapRequiredLevel(string level)
        {
            AttributeRequiredLevel mapped;

            switch ((level ?? "None").Trim())
            {
                case "Recommended":
                    mapped = AttributeRequiredLevel.Recommended;
                    break;
                case "Business Required":
                    mapped = AttributeRequiredLevel.ApplicationRequired;
                    break;
                case "System Required":
                    mapped = AttributeRequiredLevel.SystemRequired;
                    break;
                default:
                    mapped = AttributeRequiredLevel.None;
                    break;
            }

            return new AttributeRequiredLevelManagedProperty(mapped);
        }

        private static Guid? GetEntityMetadataId(IOrganizationService service, string logicalName)
        {
            try
            {
                var req = new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = EntityFilters.Entity };
                var resp = (RetrieveEntityResponse)service.Execute(req);
                return resp.EntityMetadata.MetadataId;
            }
            catch
            {
                return null;
            }
        }

        private static Guid? GetAttributeMetadataId(IOrganizationService service, string entityLogicalName, string attributeLogicalName)
        {
            try
            {
                var req = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityLogicalName,
                    LogicalName = attributeLogicalName
                };
                var resp = (RetrieveAttributeResponse)service.Execute(req);
                return resp.AttributeMetadata.MetadataId;
            }
            catch
            {
                return null;
            }
        }

        private static Guid? GetRelationshipMetadataId(IOrganizationService service, string relationshipSchemaName)
        {
            try
            {
                var req = new RetrieveRelationshipRequest { Name = relationshipSchemaName };
                var resp = (RetrieveRelationshipResponse)service.Execute(req);
                return resp.RelationshipMetadata.MetadataId;
            }
            catch
            {
                return null;
            }
        }

        private static void AddToSolutionIfRequested(IOrganizationService service, Guid? componentId, int componentType, string solutionUniqueName, string componentName, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName) || !componentId.HasValue) return;

            SolutionManager.AddComponent(service, componentId.Value, componentType, solutionUniqueName);
            log("  -> added '" + componentName + "' to solution '" + solutionUniqueName + "'.");
        }
    }
}

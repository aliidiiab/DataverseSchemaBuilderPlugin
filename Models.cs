using System.Collections.Generic;

namespace DataverseSchemaBuilderPlugin
{
    public class TableDefinition
    {
        public string DisplayName { get; set; }
        public string PluralDisplayName { get; set; }
        public string LogicalName { get; set; }
        public string Prefix { get; set; }
    }

    public class FieldDefinition
    {
        public string TableLogicalName { get; set; }   // which table this field belongs to
        public string DisplayName { get; set; }
        public string LogicalName { get; set; }
        public string DataType { get; set; }            // matches header dropdown values
        public string Prefix { get; set; }
        public string RelatedTableLogicalName { get; set; } // Lookup only
        public List<string> OptionSetValues { get; set; } = new List<string>(); // Picklist / MultiSelectPicklist
        public int? MaxLengthOrPrecision { get; set; }
        public string RequiredLevel { get; set; }        // None | Recommended | Business Required | System Required
    }

    public class SolutionDefinition
    {
        public string UniqueName { get; set; }
        public string FriendlyName { get; set; }
        /// <summary>Optional. If blank, the first publisher found in the org is used.</summary>
        public string PublisherUniqueName { get; set; }
        public string Version { get; set; } = "1.0.0.0";
    }
}

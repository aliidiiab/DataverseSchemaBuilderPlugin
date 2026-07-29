using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace DataverseSchemaBuilderPlugin
{
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "Dataverse Schema Builder")]
    [ExportMetadata("Description", "Create Dataverse tables and fields in bulk from an Excel workbook.")]
    // Optional visual metadata - replace or remove as you like:
    [ExportMetadata("SmallImageBase64", null)]
    [ExportMetadata("BigImageBase64", null)]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public class DataverseSchemaBuilderPluginDescriptor : PluginBase, IGitHubPlugin, IHelpPlugin
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SchemaBuilderControl();
        }

        // ----- IGitHubPlugin / IHelpPlugin (optional - shown in the tool's About/Help menu) -----
        public string RepositoryName => "DataverseSchemaBuilderPlugin";
        public string UserName => "aliidiiab";
        public string HelpUrl => "https://github.com/aliidiiab/DataverseSchemaBuilderPlugin";
    }
}

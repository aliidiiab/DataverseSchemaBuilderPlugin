# Dataverse Schema Builder

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that creates Dataverse tables, fields, and lookup relationships in bulk from an Excel workbook — connect, download a template, fill it in, upload it, and watch the log as it builds and publishes your schema.

## Why

Creating tables and columns one at a time through the maker portal is slow and error-prone for anything beyond a handful of fields. This tool lets you define an entire schema — one or more tables, each with its own fields, option sets, and lookups — as a spreadsheet, review it like any other document, and apply it in one run.

## Features

- **Download a blank template** directly from the tool — two data-entry sheets (`Tables`, `Fields`) plus a hidden `Lists` sheet that drives the dropdown validation.
- **Multiple tables per workbook** — each field row is routed to its table via a `Table Logical Name` column.
- **All common field types** — text, memo, whole/decimal/float numbers, currency, yes/no, date/time, GUID, lookups, customer lookups, and single/multi-select choice fields with their option values.
- **Solution-aware** — pick an existing solution from a dropdown populated from your connected environment, or create a new one on the fly (with publisher).
- **Idempotent** — re-running against a workbook whose tables/fields already exist skips creation but still ensures everything ends up in the target solution.
- **Live log** — every step streams into an on-screen console in real time, with the UI staying responsive throughout (processing runs on a background thread).
- **Publishes automatically** at the end of the run.

## Requirements

- [XrmToolBox](https://www.xrmtoolbox.com/) installed
- A Dataverse / Dynamics 365 environment to connect to
- To build from source: Visual Studio, targeting **.NET Framework 4.6.2+**

## Installation

### From source

1. Clone this repository.
2. Restore NuGet packages:
   - `XrmToolBoxPackage`
   - `Microsoft.CrmSdk.CoreAssemblies`
   - `ClosedXML`
3. Build the solution.
4. Copy **every DLL** from the build output (`bin\Debug` or `bin\Release`) into a new subfolder under:
   ```
   %AppData%\MscrmTools\XrmToolBox\Plugins\DataverseSchemaBuilder\
   ```
5. Make sure XrmToolBox is fully closed (check the system tray) before copying, then relaunch it.
6. Connect to an environment — "Dataverse Schema Builder" should now appear in your local tools list.

## Usage

1. **Connect** to an organization through XrmToolBox as usual.
2. Click **Download Template** and save the blank workbook.
3. Fill in the **Tables** sheet — one row per table (Display Name, Plural Name, Schema/Logical Name, Prefix).
4. Fill in the **Fields** sheet — one row per field, with `Table Logical Name` pointing to the table it belongs to. Pick `Data Type` from the dropdown; fill `Related Table Logical Name` for lookups, or `Option Set Values` (comma-separated) for choice fields.
5. Save the workbook, then **Browse...** to it in the tool.
6. Choose a **Solution** from the dropdown, or select `<Create a new solution...>` and fill in the new solution's details.
7. Click **Create Tables & Fields** and watch the log. The tool creates each table, then its fields (non-lookups first, then lookups), adds everything to the chosen solution, and publishes at the end.

> **Note:** if a lookup field points to another table also being created in the same run, put that referenced table **earlier** in the Tables sheet — tables are processed top-to-bottom.

## Project structure

| File | Responsibility |
|---|---|
| `Plugin.cs` | MEF-exported plugin descriptor (name/description metadata, returns the UI control). |
| `SchemaBuilderControl.cs` | The plugin's UI and orchestration logic. |
| `Models.cs` | `TableDefinition`, `FieldDefinition`, `SolutionDefinition`. |
| `ExcelTemplateBuilder.cs` | Generates the blank workbook. |
| `SchemaExcelReader.cs` | Reads and validates a filled-in workbook. |
| `DataverseSchemaBuilder.cs` | Creates entities, attributes, and relationships against Dataverse. |
| `SolutionManager.cs` | Resolves/creates the target solution and adds components to it. |

A full technical write-up of the architecture, workbook column reference, and data type mapping lives in [`docs/Dataverse_Schema_Builder_Technical_Documentation.docx`](docs/Dataverse_Schema_Builder_Technical_Documentation.docx).

## Known limitations

- Option set values are assigned sequential IDs starting at `100000000` — not aligned to any org-specific numbering convention.
- No support yet for editing or deleting existing fields/tables — only creating and adding to a solution.
- Global option sets aren't supported; choice fields are always created as local option sets.

## Contributing

Issues and pull requests are welcome. If you're changing the Excel reading logic, please keep the "every filled row is real data, no example/placeholder detection" principle intact — it replaced an earlier, more clever approach that repeatedly misfired in both directions.

## Author

Ali Diab

## License

Add your preferred license here (e.g. MIT).

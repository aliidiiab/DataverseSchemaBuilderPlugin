using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace DataverseSchemaBuilderPlugin
{
    public static class SchemaExcelReader
    {
        /// <summary>
        /// Reads every table row from the 'Tables' sheet. One row = one table.
        /// Every row with a Display Name filled in is treated as real data - no
        /// example-row guessing, no content matching.
        /// </summary>
        public static List<TableDefinition> ReadTables(string filePath)
        {
            var result = new List<TableDefinition>();

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = wb.Worksheet("Tables");

                var headerRow = 4;
                var lastRow = ws.LastRowUsed().RowNumber();

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var displayName = ws.Cell(r, 1).GetString().Trim();

                    if (displayName.StartsWith("Notes", StringComparison.OrdinalIgnoreCase))
                        break; // hit the notes section - stop scanning

                    if (string.IsNullOrWhiteSpace(displayName)) continue; // blank row - keep scanning

                    var prefix = ws.Cell(r, 4).GetString().Trim();
                    var logicalNameCell = ws.Cell(r, 3).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(logicalNameCell))
                        throw new InvalidOperationException(
                            "Table row found (Display Name = '" + displayName + "') but no Prefix or Schema/Logical Name was provided. " +
                            "Fill in 'Prefix' (e.g. new_) on the 'Tables' sheet, row " + r + ".");

                    var logicalName = logicalNameCell.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(logicalName))
                    {
                        logicalName = BuildLogicalName(prefix, displayName);
                    }

                    if (string.IsNullOrWhiteSpace(logicalName))
                        throw new InvalidOperationException(
                            "Table row found (Display Name = '" + displayName + "') but could not determine a Schema/Logical Name. " +
                            "Fill in the 'Table Schema/Logical Name (with prefix)' column on the 'Tables' sheet, row " + r + ".");

                    if (result.Any(t => string.Equals(t.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException(
                            "Duplicate table logical name '" + logicalName + "' found on the 'Tables' sheet (row " + r + "). " +
                            "Each table must have a unique Schema/Logical Name.");

                    result.Add(new TableDefinition
                    {
                        DisplayName = displayName,
                        PluralDisplayName = string.IsNullOrWhiteSpace(ws.Cell(r, 2).GetString().Trim())
                            ? displayName + "s"
                            : ws.Cell(r, 2).GetString().Trim(),
                        LogicalName = logicalName,
                        Prefix = prefix
                    });
                }
            }

            if (result.Count == 0)
                throw new InvalidOperationException(
                    "No table definitions found on the 'Tables' sheet. Fill in at least one row with a 'Table Display Name'.");

            return result;
        }

        /// <summary>
        /// Reads every field row from the 'Fields' sheet. Each row's "Table Logical Name"
        /// must match one of the tables passed in (from ReadTables).
        /// </summary>
        public static List<FieldDefinition> ReadFields(string filePath, List<TableDefinition> knownTables)
        {
            var result = new List<FieldDefinition>();
            var knownTableNames = new HashSet<string>(
                knownTables.Select(t => t.LogicalName), StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(filePath))
            {
                var ws = wb.Worksheet("Fields");

                var headerRow = 4;
                var lastRow = ws.LastRowUsed().RowNumber();

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    var tableLogicalName = ws.Cell(r, 1).GetString().Trim().ToLowerInvariant();
                    var displayName = ws.Cell(r, 2).GetString().Trim();
                    var dataType = ws.Cell(r, 4).GetString().Trim();

                    if (tableLogicalName.StartsWith("notes", StringComparison.OrdinalIgnoreCase))
                        break; // hit the notes section - stop scanning

                    if (string.IsNullOrWhiteSpace(tableLogicalName) && string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(dataType))
                        continue; // blank row - keep scanning

                    if (string.IsNullOrWhiteSpace(tableLogicalName))
                        throw new InvalidOperationException(
                            "Row " + r + " on the 'Fields' sheet ('" + displayName + "') has no Table Logical Name. " +
                            "Fill in which table (from the 'Tables' sheet) this field belongs to.");
                    if (string.IsNullOrWhiteSpace(displayName))
                        throw new InvalidOperationException(
                            "Row " + r + " on the 'Fields' sheet has a Data Type but no Field Display Name. Fill in both or leave the row blank.");
                    if (string.IsNullOrWhiteSpace(dataType))
                        throw new InvalidOperationException(
                            "Row " + r + " on the 'Fields' sheet ('" + displayName + "') has no Data Type selected. Pick one from the dropdown.");
                    if (!knownTableNames.Contains(tableLogicalName))
                        throw new InvalidOperationException(
                            "Row " + r + " on the 'Fields' sheet references Table Logical Name '" + tableLogicalName +
                            "', which doesn't match any table defined on the 'Tables' sheet. Check spelling, or add that table there first.");

                    var prefix = ws.Cell(r, 5).GetString().Trim();
                    var logicalNameCell = ws.Cell(r, 3).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(logicalNameCell))
                        throw new InvalidOperationException(
                            "Row " + r + " on the 'Fields' sheet ('" + displayName + "') has no Prefix or Schema/Logical Name. " +
                            "Fill in 'Prefix' (e.g. new_) for this field.");

                    var logicalName = logicalNameCell.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(logicalName))
                    {
                        logicalName = BuildLogicalName(prefix, displayName);
                    }

                    var optionSetRaw = ws.Cell(r, 7).GetString().Trim();
                    var maxLenRaw = ws.Cell(r, 8).GetString().Trim();

                    int parsedMaxLen;
                    int? maxLenOrPrecision = int.TryParse(maxLenRaw, out parsedMaxLen) ? (int?)parsedMaxLen : null;

                    result.Add(new FieldDefinition
                    {
                        TableLogicalName = tableLogicalName,
                        DisplayName = displayName,
                        LogicalName = logicalName,
                        DataType = dataType,
                        Prefix = prefix,
                        RelatedTableLogicalName = ws.Cell(r, 6).GetString().Trim().ToLowerInvariant(),
                        OptionSetValues = string.IsNullOrWhiteSpace(optionSetRaw)
                            ? new List<string>()
                            : optionSetRaw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
                        MaxLengthOrPrecision = maxLenOrPrecision,
                        RequiredLevel = ws.Cell(r, 9).GetString().Trim()
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Builds "prefix + sanitized display name" (lowercase, letters/digits/underscore only,
        /// spaces removed) without doubling the prefix if the display name already starts with it.
        /// </summary>
        private static string BuildLogicalName(string prefix, string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;

            var chars = displayName.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
            var sanitized = new string(chars);

            if (string.IsNullOrWhiteSpace(sanitized)) return null;

            var normalizedPrefix = prefix.Trim();
            if (!normalizedPrefix.EndsWith("_")) normalizedPrefix += "_";

            return sanitized.StartsWith(normalizedPrefix) ? sanitized : normalizedPrefix + sanitized;
        }
    }
}

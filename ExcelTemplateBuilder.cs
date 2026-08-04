using System.Linq;
using ClosedXML.Excel;

namespace DataverseSchemaBuilderPlugin
{
    /// <summary>
    /// Builds the blank "Dataverse_Table_Field_Definition.xlsx" template that the user
    /// downloads, fills in, and re-uploads. No example data rows are pre-filled - only
    /// headers, blank input rows, and a written example in the Notes section for reference.
    /// </summary>
    public static class ExcelTemplateBuilder
    {
        public static void CreateTemplate(string outputPath)
        {
            using (var wb = new XLWorkbook())
            {
                IXLRange dataTypeRange;
                IXLRange requiredLevelRange;
                BuildListsSheet(wb, out dataTypeRange, out requiredLevelRange);
                BuildTablesSheet(wb);
                BuildFieldsSheet(wb, dataTypeRange, requiredLevelRange);
                wb.SaveAs(outputPath);
            }
        }

        /// <summary>
        /// Holds the dropdown option values on their own (hidden) sheet, and the two
        /// Fields-sheet dropdowns reference these cells directly instead of using an
        /// inline literal comma-separated list. Inline literal lists via ClosedXML's
        /// List(string) overload have been unreliable in some versions/configurations
        /// (producing "Removed Feature: Data validation..." on open); referencing real
        /// cells is the standard, most robust way to do Excel dropdowns.
        /// </summary>
        private static void BuildListsSheet(XLWorkbook wb, out IXLRange dataTypeRange, out IXLRange requiredLevelRange)
        {
            var ws = wb.Worksheets.Add("Lists");

            var dataTypes = new[]
            {
                "String", "Memo", "Integer", "BigInt", "Decimal", "Double", "Money",
                "Boolean", "DateTime", "Lookup", "Customer", "Owner", "Picklist",
                "MultiSelectPicklist",  "EntityName"
            };
            for (int i = 0; i < dataTypes.Length; i++)
                ws.Cell(i + 1, 1).Value = dataTypes[i];
            dataTypeRange = ws.Range(1, 1, dataTypes.Length, 1);

            var requiredLevels = new[] { "None", "Recommended", "Business Required", "System Required" };
            for (int i = 0; i < requiredLevels.Length; i++)
                ws.Cell(i + 1, 2).Value = requiredLevels[i];
            requiredLevelRange = ws.Range(1, 2, requiredLevels.Length, 2);

            ws.Visibility = XLWorksheetVisibility.VeryHidden;
        }

        private static void BuildTablesSheet(XLWorkbook wb)
        {
            var ws = wb.Worksheets.Add("Tables");

            ws.Cell(1, 1).Value = "Dataverse Table Definitions";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E78");
            ws.Range(1, 1, 1, 4).Merge();

            ws.Cell(2, 1).Value = "One row per table you want created. Fill in the yellow cells only.";
            StyleNote(ws.Cell(2, 1));
            ws.Range(2, 1, 2, 4).Merge();

            var headers = new[] { "Table Display Name", "Table Plural Display Name", "Table Schema/Logical Name (with prefix)", "Prefix" };
            const int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(headerRow, i + 1).Value = headers[i];
            StyleHeaderRow(ws, headerRow, headers.Length);

            const int inputStart = headerRow + 1;
            const int inputRows = 15;
            for (int r = inputStart; r < inputStart + inputRows; r++)
            {
                for (int c = 1; c <= headers.Length; c++)
                    StyleInputCell(ws.Cell(r, c));
            }

            ws.Column(1).Width = 26;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 34;
            ws.Column(4).Width = 12;

            int lastRow = inputStart + inputRows;
            int noteRow = lastRow + 2;
            ws.Cell(noteRow, 1).Value = "Notes:";
            ws.Cell(noteRow, 1).Style.Font.Bold = true;

            var notes = new[]
            {
                "- One row = one table. Leave rows blank if you don't need more tables.",
                "- Table Schema/Logical Name must be all lowercase, start with your chosen prefix, no spaces.",
                "- If Table Schema/Logical Name is left blank, it's auto-generated from Prefix + Table Display Name.",
                "- On the 'Fields' sheet, each field must reference one of the Table Schema/Logical Names entered here."
            };
            for (int i = 0; i < notes.Length; i++)
            {
                var cell = ws.Cell(noteRow + 1 + i, 1);
                cell.Value = notes[i];
                StyleNote(cell);
                ws.Range(noteRow + 1 + i, 1, noteRow + 1 + i, 4).Merge();
            }

            ws.SheetView.FreezeRows(headerRow);
        }

        private static void BuildFieldsSheet(XLWorkbook wb, IXLRange dataTypeRange, IXLRange requiredLevelRange)
        {
            var ws = wb.Worksheets.Add("Fields");

            ws.Cell(1, 1).Value = "Field Definitions";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E78");
            ws.Range(1, 1, 1, 9).Merge();

            ws.Cell(2, 1).Value = "One row per field. 'Table Logical Name' must match a Table Schema/Logical Name from the 'Tables' sheet. " +
                                   "Pick Data Type from the dropdown (matches Dataverse AttributeTypeCode values used by CreateAttributeRequest).";
            StyleNote(ws.Cell(2, 1));
            ws.Range(2, 1, 2, 9).Merge();

            var headers = new[]
            {
                "Table Logical Name (must match 'Tables' sheet)",
                "Field Display Name",
                "Field Schema/Logical Name (with prefix)",
                "Data Type",
                "Prefix",
                "Related Table Logical Name (Lookup only)",
                "Option Set Values (Choice/Picklist only, comma-separated)",
                "Max Length / Precision (Text, Memo, Decimal, Double)",
                "Required Level"
            };
            const int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(headerRow, i + 1).Value = headers[i];
            StyleHeaderRow(ws, headerRow, headers.Length);

            const int inputStart = headerRow + 1;
            const int inputRows = 40;
            for (int r = inputStart; r < inputStart + inputRows; r++)
            {
                for (int c = 1; c <= headers.Length; c++)
                    StyleInputCell(ws.Cell(r, c));
            }

            var colWidths = new[] { 30, 22, 30, 18, 10, 30, 32, 24, 20 };
            for (int i = 0; i < colWidths.Length; i++)
                ws.Column(i + 1).Width = colWidths[i];

            int lastRow = inputStart + inputRows;

            // Data Type dropdown (matches Dataverse AttributeTypeCode names) and Required
            // Level dropdown - both reference the hidden 'Lists' sheet rather than an
            // inline literal list (see BuildListsSheet for why).
            var dtRange = ws.Range(inputStart, 4, lastRow, 4);
            dtRange.SetDataValidation().List(dataTypeRange, true);

            var rlRange = ws.Range(inputStart, 9, lastRow, 9);
            rlRange.SetDataValidation().List(requiredLevelRange, true);

            int noteRow = lastRow + 2;
            ws.Cell(noteRow, 1).Value = "Notes / Data Type Reference:";
            ws.Cell(noteRow, 1).Style.Font.Bold = true;

            var notes = new[]
            {
                "String = single line text | Memo = multi-line text | Integer/BigInt = whole number | Decimal/Double = decimal number",
                "Money = currency | Boolean = two options (yes/no) | DateTime = date and time | Uniqueidentifier = GUID",
                "Lookup = single reference to another table -> fill Related Table Logical Name | Customer = lookup to account/contact",
                "Picklist = single-select choice -> fill Option Set Values | MultiSelectPicklist = multi-select choice -> fill Option Set Values",
                "Owner = ownership field (user/team) | EntityName = stores a table logical name as text",
                "Required Level maps to Dataverse RequiredLevel: None, Recommended, ApplicationRequired (Business Required), SystemRequired",
                "Leave 'Related Table Logical Name' and 'Option Set Values' blank unless the Data Type needs them.",
                "If a field's table is created in this same run, list that table on the 'Tables' sheet using the same logical name."
            };
            for (int i = 0; i < notes.Length; i++)
            {
                var cell = ws.Cell(noteRow + 1 + i, 1);
                cell.Value = notes[i];
                StyleNote(cell);
                ws.Range(noteRow + 1 + i, 1, noteRow + 1 + i, 9).Merge();
            }

            ws.SheetView.FreezeRows(headerRow);
        }

        private static void StyleHeaderRow(IXLWorksheet ws, int row, int colCount)
        {
            var range = ws.Range(row, 1, row, colCount);
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Alignment.WrapText = true;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private static void StyleInputCell(IXLCell cell)
        {
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private static void StyleNote(IXLCell cell)
        {
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.FromHtml("#808080");
        }
    }
}

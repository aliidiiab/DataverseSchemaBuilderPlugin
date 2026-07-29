using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using XrmToolBox.Extensibility;

namespace DataverseSchemaBuilderPlugin
{
    /// <summary>
    /// The plugin's user interface. Instantiated by DataverseSchemaBuilderPlugin (Plugin.cs)
    /// via GetControl() - do not add [Export]/[ExportMetadata] here, those belong on the
    /// Plugin descriptor class instead.
    /// </summary>
    public partial class SchemaBuilderControl : PluginControlBase
    {
        private class SolutionListItem
        {
            public string UniqueName; // null = the "create new solution" sentinel
            public string FriendlyName;
            public override string ToString()
            {
                return UniqueName == null ? FriendlyName : FriendlyName + "  (" + UniqueName + ")";
            }
        }

        private Label lblConnection;
        private Button btnDownloadTemplate;
        private TextBox txtFilePath;
        private Button btnBrowse;
        private ComboBox cmbSolution;
        private Button btnRefreshSolutions;
        private Panel newSolutionPanel;
        private TextBox txtNewSolutionUniqueName;
        private TextBox txtNewSolutionFriendlyName;
        private TextBox txtPublisherUniqueName;
        private Button btnRun;
        private RichTextBox txtLog;
        private ProgressBar progressBar;

        public SchemaBuilderControl()
        {
            BuildUi();
        }

        // ----- Connection -----
        public override void UpdateConnection(Microsoft.Xrm.Sdk.IOrganizationService newService, McTools.Xrm.Connection.ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);

            lblConnection.Text = detail != null
                ? "Connected to: " + detail.ConnectionName + "  (" + detail.WebApplicationUrl + ")"
                : "Not connected";
            btnRun.Enabled = detail != null && !string.IsNullOrWhiteSpace(txtFilePath.Text);

            if (Service != null) LoadSolutions();
        }

        private void LoadSolutions()
        {
            cmbSolution.Items.Clear();
            cmbSolution.Items.Add(new SolutionListItem { UniqueName = null, FriendlyName = "<Create a new solution...>" });

            try
            {
                var query = new QueryExpression("solution")
                {
                    ColumnSet = new ColumnSet("uniquename", "friendlyname"),
                    Criteria = new FilterExpression()
                };
                query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
                query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
                query.AddOrder("friendlyname", OrderType.Ascending);

                var result = Service.RetrieveMultiple(query);
                foreach (var e in result.Entities)
                {
                    cmbSolution.Items.Add(new SolutionListItem
                    {
                        UniqueName = e.GetAttributeValue<string>("uniquename"),
                        FriendlyName = e.GetAttributeValue<string>("friendlyname")
                    });
                }
            }
            catch (Exception ex)
            {
                Log("Could not load solutions: " + ex.Message);
            }

            cmbSolution.SelectedIndex = 0;
        }

        // ----- UI construction -----
        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            lblConnection = new Label
            {
                Text = "Not connected",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(3, 3, 3, 10)
            };
            layout.Controls.Add(lblConnection);

            // Row: Download template
            var templatePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            btnDownloadTemplate = new Button { Text = "1. Download Template", AutoSize = true };
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
            templatePanel.Controls.Add(btnDownloadTemplate);
            var lblTemplateHint = new Label
            {
                Text = "Fill in the Tables and Fields sheets, save, then upload it below.",
                AutoSize = true,
                Margin = new Padding(10, 8, 3, 3),
                ForeColor = Color.Gray
            };
            templatePanel.Controls.Add(lblTemplateHint);
            layout.Controls.Add(templatePanel);

            // Row: File picker
            var filePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(3, 10, 3, 3) };
            filePanel.Controls.Add(new Label { Text = "2. Filled workbook:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            txtFilePath = new TextBox { Width = 400, ReadOnly = true };
            filePanel.Controls.Add(txtFilePath);
            btnBrowse = new Button { Text = "Browse...", AutoSize = true };
            btnBrowse.Click += BtnBrowse_Click;
            filePanel.Controls.Add(btnBrowse);
            layout.Controls.Add(filePanel);

            // Row: Solution settings
            var solutionGroup = new GroupBox { Text = "3. Solution", AutoSize = true, Width = 650, Margin = new Padding(3, 10, 3, 3) };
            var solutionLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };

            solutionLayout.Controls.Add(new Label { Text = "3. Choose a solution:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, 0);
            cmbSolution = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSolution.SelectedIndexChanged += CmbSolution_SelectedIndexChanged;
            solutionLayout.Controls.Add(cmbSolution, 1, 0);
            btnRefreshSolutions = new Button { Text = "Refresh", AutoSize = true };
            btnRefreshSolutions.Click += (s, e) => { if (Service != null) LoadSolutions(); };
            solutionLayout.Controls.Add(btnRefreshSolutions, 2, 0);

            newSolutionPanel = new Panel { AutoSize = true, Visible = true, Margin = new Padding(0, 6, 0, 0) };
            var newSolutionLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
            newSolutionLayout.Controls.Add(new Label { Text = "New Solution Unique Name:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, 0);
            txtNewSolutionUniqueName = new TextBox { Width = 300 };
            newSolutionLayout.Controls.Add(txtNewSolutionUniqueName, 1, 0);
            newSolutionLayout.Controls.Add(new Label { Text = "New Solution Friendly Name:", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, 1);
            txtNewSolutionFriendlyName = new TextBox { Width = 300 };
            newSolutionLayout.Controls.Add(txtNewSolutionFriendlyName, 1, 1);
            newSolutionLayout.Controls.Add(new Label { Text = "Publisher Unique Name (optional):", AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, 2);
            txtPublisherUniqueName = new TextBox { Width = 300 };
            newSolutionLayout.Controls.Add(txtPublisherUniqueName, 1, 2);
            newSolutionPanel.Controls.Add(newSolutionLayout);
            solutionLayout.Controls.Add(newSolutionPanel, 0, 1);
            solutionLayout.SetColumnSpan(newSolutionPanel, 3);

            solutionGroup.Controls.Add(solutionLayout);
            layout.Controls.Add(solutionGroup);

            // Row: Run button + progress bar
            var runPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(3, 10, 3, 3) };
            btnRun = new Button { Text = "4. Create Tables && Fields", AutoSize = true, Enabled = false, Font = new Font(Font, FontStyle.Bold) };
            btnRun.Click += BtnRun_Click;
            runPanel.Controls.Add(btnRun);
            progressBar = new ProgressBar { Width = 300, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0, Margin = new Padding(15, 3, 3, 3) };
            runPanel.Controls.Add(progressBar);
            layout.Controls.Add(runPanel);

            // Row: Log
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font(FontFamily.GenericMonospace, 9),
                Margin = new Padding(3, 10, 3, 3)
            };
            layout.Controls.Add(txtLog);
        }

        // ----- Button handlers -----
        private void BtnDownloadTemplate_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "Dataverse_Table_Field_Definition.xlsx"
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    ExcelTemplateBuilder.CreateTemplate(sfd.FileName);
                    Log("Template saved to: " + sfd.FileName);
                    MessageBox.Show(this, "Template saved. Fill it in, save it, then upload it using Browse below.",
                        "Template Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not create the template: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CmbSolution_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = cmbSolution.SelectedItem as SolutionListItem;
            newSolutionPanel.Visible = selected == null || selected.UniqueName == null;
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;

                txtFilePath.Text = ofd.FileName;
                btnRun.Enabled = Service != null;
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            if (Service == null)
            {
                MessageBox.Show(this, "Connect to an organization first.", "Not connected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFilePath.Text) || !File.Exists(txtFilePath.Text))
            {
                MessageBox.Show(this, "Choose a valid workbook file first.", "No file selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedSolution = cmbSolution.SelectedItem as SolutionListItem;
            var isCreatingNewSolution = selectedSolution == null || selectedSolution.UniqueName == null;

            string solutionUniqueName;
            string solutionFriendlyName;
            string publisherUniqueName;

            if (isCreatingNewSolution)
            {
                solutionUniqueName = txtNewSolutionUniqueName.Text.Trim();
                solutionFriendlyName = txtNewSolutionFriendlyName.Text.Trim();
                publisherUniqueName = txtPublisherUniqueName.Text.Trim();

                if (string.IsNullOrWhiteSpace(solutionUniqueName))
                {
                    MessageBox.Show(this, "Enter a unique name for the new solution, or pick an existing one from the dropdown.",
                        "Solution required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                solutionUniqueName = selectedSolution.UniqueName;
                solutionFriendlyName = selectedSolution.FriendlyName;
                publisherUniqueName = null; // solution already exists - no publisher needed
            }

            var filePath = txtFilePath.Text;

            txtLog.Clear();
            btnRun.Enabled = false;
            btnBrowse.Enabled = false;
            btnDownloadTemplate.Enabled = false;
            progressBar.MarqueeAnimationSpeed = 30;

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Building Dataverse schema from workbook...",
                Work = (worker, args) =>
                {
                    Action<string> log = msg => worker.ReportProgress(0, msg);

                    var tables = SchemaExcelReader.ReadTables(filePath);
                    log("Found " + tables.Count + " table(s) to process.");
                    var fields = SchemaExcelReader.ReadFields(filePath, tables);
                    log("Found " + fields.Count + " field(s) to process.");

                    var solution = new SolutionDefinition
                    {
                        UniqueName = solutionUniqueName,
                        FriendlyName = string.IsNullOrWhiteSpace(solutionFriendlyName) ? solutionUniqueName : solutionFriendlyName,
                        PublisherUniqueName = string.IsNullOrWhiteSpace(publisherUniqueName) ? null : publisherUniqueName
                    };
                    SolutionManager.EnsureSolutionExists(Service, solution, log);

                    foreach (var table in tables)
                    {
                        log("--- Table: " + table.LogicalName + " ---");
                        DataverseSchemaBuilder.CreateTable(Service, table, solutionUniqueName, log);

                        var fieldsForThisTable = fields.Where(f => f.TableLogicalName == table.LogicalName).ToList();
                        DataverseSchemaBuilder.CreateFields(Service, table.LogicalName, fieldsForThisTable, solutionUniqueName, log);
                    }

                    log("Publishing customizations...");
                    Service.Execute(new Microsoft.Crm.Sdk.Messages.PublishAllXmlRequest());
                    log("Publish complete. All done.");
                },
                ProgressChanged = e2 =>
                {
                    if (e2.UserState != null) Log(e2.UserState.ToString());
                },
                PostWorkCallBack = e2 =>
                {
                    progressBar.MarqueeAnimationSpeed = 0;
                    btnRun.Enabled = true;
                    btnBrowse.Enabled = true;
                    btnDownloadTemplate.Enabled = true;

                    if (e2.Error != null)
                    {
                        Log("ERROR: " + e2.Error.Message);
                        MessageBox.Show(this, e2.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show(this, "Tables and fields created successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (Service != null) LoadSolutions(); // refresh in case a new solution was just created
                    }
                }
            });
        }

        private void Log(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(Log), message);
                return;
            }

            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }
    }
}

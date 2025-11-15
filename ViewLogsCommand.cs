using System;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimShady;

[Transaction(TransactionMode.Manual)]
public class ViewLogsCommand : IExternalCommand
{
    private static LogViewerForm? _logViewerInstance;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            BimShadyLogger.Log("Opening log viewer");

            // If log viewer is already open, just bring it to front
            if (_logViewerInstance != null && !_logViewerInstance.IsDisposed)
            {
                _logViewerInstance.BringToFront();
                _logViewerInstance.Focus();
                return Result.Succeeded;
            }

            // Create new modeless log viewer
            _logViewerInstance = new LogViewerForm();
            _logViewerInstance.FormClosed += (s, e) => _logViewerInstance = null;
            _logViewerInstance.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            BimShadyLogger.LogError("Error opening log viewer", ex);
            message = $"Error opening log viewer: {ex.Message}";
            return Result.Failed;
        }
    }
}

public class LogViewerForm : System.Windows.Forms.Form
{
    private System.Windows.Forms.TextBox _logTextBox;
    private System.Windows.Forms.Button _refreshButton;
    private System.Windows.Forms.Button _clearButton;
    private System.Windows.Forms.Button _copyButton;
    private System.Windows.Forms.Button _openFolderButton;
    private System.Windows.Forms.CheckBox _autoScrollCheckbox;
    private System.Windows.Forms.CheckBox _autoRefreshCheckbox;
    private System.Windows.Forms.Label _statusLabel;
    private System.Windows.Forms.Timer _autoRefreshTimer;

    public LogViewerForm()
    {
        InitializeComponents();
        LoadLogs();
    }

    private void InitializeComponents()
    {
        this.Text = "BimShady Log Viewer";
        this.Width = 1000;
        this.Height = 700;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Icon = null; // Use default icon

        // Log text box
        _logTextBox = new System.Windows.Forms.TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new System.Drawing.Font("Consolas", 9),
            Left = 10,
            Top = 10,
            Width = this.ClientSize.Width - 20,
            Height = this.ClientSize.Height - 80,
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220)
        };
        this.Controls.Add(_logTextBox);

        int buttonY = this.ClientSize.Height - 60;

        // Refresh button
        _refreshButton = new System.Windows.Forms.Button
        {
            Text = "Refresh",
            Left = 10,
            Top = buttonY,
            Width = 80,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _refreshButton.Click += (s, e) => LoadLogs();
        this.Controls.Add(_refreshButton);

        // Clear button
        _clearButton = new System.Windows.Forms.Button
        {
            Text = "Clear Logs",
            Left = 100,
            Top = buttonY,
            Width = 80,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _clearButton.Click += ClearButton_Click;
        this.Controls.Add(_clearButton);

        // Copy button
        _copyButton = new System.Windows.Forms.Button
        {
            Text = "Copy All",
            Left = 190,
            Top = buttonY,
            Width = 80,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _copyButton.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_logTextBox.Text))
            {
                Clipboard.SetText(_logTextBox.Text);
                UpdateStatus("Logs copied to clipboard");
            }
        };
        this.Controls.Add(_copyButton);

        // Open folder button
        _openFolderButton = new System.Windows.Forms.Button
        {
            Text = "Open Log Folder",
            Left = 280,
            Top = buttonY,
            Width = 120,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _openFolderButton.Click += (s, e) =>
        {
            try
            {
                string logFolder = Path.GetDirectoryName(BimShadyLogger.LogFilePath)!;
                if (Directory.Exists(logFolder))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        this.Controls.Add(_openFolderButton);

        // Auto-scroll checkbox
        _autoScrollCheckbox = new System.Windows.Forms.CheckBox
        {
            Text = "Auto-scroll",
            Left = 410,
            Top = buttonY + 5,
            Width = 100,
            Checked = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(_autoScrollCheckbox);

        // Auto-refresh checkbox
        _autoRefreshCheckbox = new System.Windows.Forms.CheckBox
        {
            Text = "Auto-refresh (2s)",
            Left = 510,
            Top = buttonY + 5,
            Width = 120,
            Checked = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _autoRefreshCheckbox.CheckedChanged += (s, e) =>
        {
            if (_autoRefreshCheckbox.Checked)
            {
                _autoRefreshTimer.Start();
                UpdateStatus("Auto-refresh enabled");
            }
            else
            {
                _autoRefreshTimer.Stop();
                UpdateStatus("Auto-refresh disabled");
            }
        };
        this.Controls.Add(_autoRefreshCheckbox);

        // Status label
        _statusLabel = new System.Windows.Forms.Label
        {
            Text = "",
            Left = 640,
            Top = buttonY + 5,
            Width = 300,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(_statusLabel);

        // Setup auto-refresh timer
        _autoRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000 // 2 seconds
        };
        _autoRefreshTimer.Tick += (s, e) => LoadLogs();
    }

    private void LoadLogs()
    {
        try
        {
            if (File.Exists(BimShadyLogger.LogFilePath))
            {
                string content = File.ReadAllText(BimShadyLogger.LogFilePath);

                // Only update if content changed
                if (_logTextBox.Text != content)
                {
                    _logTextBox.Text = content;

                    if (_autoScrollCheckbox.Checked)
                    {
                        _logTextBox.SelectionStart = _logTextBox.Text.Length;
                        _logTextBox.ScrollToCaret();
                    }

                    var fileInfo = new FileInfo(BimShadyLogger.LogFilePath);
                    UpdateStatus($"Loaded {fileInfo.Length / 1024} KB - {DateTime.Now:HH:mm:ss}");
                }
            }
            else
            {
                _logTextBox.Text = "No log file found.\n\n" +
                    "Logs will be created when you use BimShady API commands.\n\n" +
                    "Log file location: " + BimShadyLogger.LogFilePath;
                UpdateStatus("No log file");
            }
        }
        catch (Exception ex)
        {
            _logTextBox.Text = $"Error loading logs: {ex.Message}";
            UpdateStatus("Error loading logs");
        }
    }

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all logs?",
            "Clear Logs",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            try
            {
                if (File.Exists(BimShadyLogger.LogFilePath))
                {
                    File.WriteAllText(BimShadyLogger.LogFilePath, "");
                }
                LoadLogs();
                UpdateStatus("Logs cleared");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateStatus(string text)
    {
        _statusLabel.Text = text;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        base.OnFormClosed(e);
    }
}

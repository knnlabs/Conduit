using Terminal.Gui;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Keys;

public class KeyEditDialog : Dialog
{
    private TextField _keyNameField;
    private TextField _maxBudgetField;
    private TextField _dailyBudgetField;
    private TextView _allowedModelsField;
    private CheckBox _isEnabledCheckbox;
    // private CheckBox _logPromptsCheckbox; // Not supported in current API
    
    public CreateVirtualKeyRequest? Result { get; private set; }

    public KeyEditDialog(VirtualKeyDto? existing) : base(
        existing == null ? "Add Virtual Key" : "Edit Virtual Key", 
        60, 18)
    {
        InitializeUI(existing);
    }

    private void InitializeUI(VirtualKeyDto? existing)
    {
        // Key Name
        var keyNameLabel = new Label("Key Name:") { X = 1, Y = 1 };
        _keyNameField = new TextField(existing?.KeyName ?? "")
        {
            X = 16,
            Y = 1,
            Width = Dim.Fill(1)
        };

        // Max Budget (optional)
        var maxBudgetLabel = new Label("Max Budget ($):") { X = 1, Y = 3 };
        _maxBudgetField = new TextField(existing?.MaxBudget.ToString("F2") ?? "")
        {
            X = 16,
            Y = 3,
            Width = 20
        };

        // Daily Budget (optional)
        var dailyBudgetLabel = new Label("Daily Budget:") { X = 1, Y = 5 };
        _dailyBudgetField = new TextField("") // Daily budget not supported in current API
        {
            X = 16,
            Y = 5,
            Width = 20
        };

        // Allowed Models (optional, comma-separated)
        var allowedModelsLabel = new Label("Allowed Models:") { X = 1, Y = 7 };
        var allowedModelsHelp = new Label("(comma-separated, leave empty for all)") 
        { 
            X = 1, 
            Y = 8,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };
        _allowedModelsField = new TextView()
        {
            X = 16,
            Y = 7,
            Width = Dim.Fill(1),
            Height = 3,
            Text = existing?.AllowedModels != null ? string.Join(", ", existing.AllowedModels) : ""
        };

        // Is Enabled
        _isEnabledCheckbox = new CheckBox("Enabled")
        {
            X = 1,
            Y = 11,
            Checked = existing?.IsEnabled ?? true
        };

        // Log Prompts - removed, not supported in current API
        // _logPromptsCheckbox = new CheckBox("Log Prompts and Completions")
        // {
        //     X = Pos.Right(_isEnabledCheckbox) + 2,
        //     Y = 11,
        //     Checked = false
        // };

        // Current spend display (read-only, for edit mode)
        if (existing != null)
        {
            var spendLabel = new Label($"Current Spend: ${existing.CurrentSpend:F2}")
            {
                X = 1,
                Y = 13,
                ColorScheme = new ColorScheme
                {
                    Normal = Application.Driver.MakeAttribute(Color.Cyan, Color.Black)
                }
            };
            Add(spendLabel);
        }

        // Buttons
        var saveButton = new Button("Save")
        {
            X = Pos.Center() - 10,
            Y = 15
        };
        saveButton.Clicked += OnSave;

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Center() + 2,
            Y = 15
        };
        cancelButton.Clicked += OnCancel;

        // Add all controls
        Add(keyNameLabel, _keyNameField,
            maxBudgetLabel, _maxBudgetField,
            dailyBudgetLabel, _dailyBudgetField,
            allowedModelsLabel, allowedModelsHelp, _allowedModelsField,
            _isEnabledCheckbox, // _logPromptsCheckbox,
            saveButton, cancelButton);
    }

    private void OnSave()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_keyNameField.Text.ToString()))
        {
            MessageBox.ErrorQuery("Validation Error", "Key name is required", "OK");
            return;
        }

        // Parse optional budget fields
        decimal? maxBudget = null;
        if (!string.IsNullOrWhiteSpace(_maxBudgetField.Text.ToString()))
        {
            if (decimal.TryParse(_maxBudgetField.Text.ToString(), out var mb))
            {
                maxBudget = mb;
            }
            else
            {
                MessageBox.ErrorQuery("Validation Error", "Max budget must be a valid number", "OK");
                return;
            }
        }

        decimal? dailyBudget = null;
        if (!string.IsNullOrWhiteSpace(_dailyBudgetField.Text.ToString()))
        {
            if (decimal.TryParse(_dailyBudgetField.Text.ToString(), out var db))
            {
                dailyBudget = db;
            }
            else
            {
                MessageBox.ErrorQuery("Validation Error", "Daily budget must be a valid number", "OK");
                return;
            }
        }

        // Parse allowed models
        List<string>? allowedModels = null;
        var modelsText = _allowedModelsField.Text.ToString();
        if (!string.IsNullOrWhiteSpace(modelsText))
        {
            allowedModels = modelsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();
        }

        Result = new CreateVirtualKeyRequest
        {
            KeyName = _keyNameField.Text.ToString()!,
            MaxBudget = maxBudget,
            AllowedModels = allowedModels != null ? string.Join(",", allowedModels) : null
        };

        Running = false;
    }

    private void OnCancel()
    {
        Result = null;
        Running = false;
    }
}
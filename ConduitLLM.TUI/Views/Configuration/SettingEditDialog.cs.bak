using Terminal.Gui;

namespace ConduitLLM.TUI.Views.Configuration;

public class SettingEditDialog : Dialog
{
    private TextView _valueField;
    private Label _keyLabel;
    private Label _typeLabel;
    private Label _descriptionLabel;
    
    public string? Result { get; private set; }

    public SettingEditDialog(SettingDto setting) : base($"Edit Setting: {setting.Key}", 60, 16)
    {
        InitializeUI(setting);
    }

    private void InitializeUI(SettingDto setting)
    {
        // Key (read-only)
        _keyLabel = new Label($"Key: {setting.Key}")
        {
            X = 1,
            Y = 1
        };

        // Type
        _typeLabel = new Label($"Type: {setting.DataType}")
        {
            X = 1,
            Y = 2
        };

        // Description
        _descriptionLabel = new Label(setting.Description ?? "No description")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1)
        };

        // Value input
        var valueLabel = new Label("Value:")
        {
            X = 1,
            Y = 5
        };

        _valueField = new TextView()
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = 4,
            Text = setting.Value ?? "",
            WordWrap = true
        };

        // Validation hint based on type
        var hintLabel = new Label(GetTypeHint(setting.DataType))
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill(1),
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };

        // Buttons
        var saveButton = new Button("Save")
        {
            X = Pos.Center() - 10,
            Y = 12
        };
        saveButton.Clicked += () => OnSave(setting);

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Center() + 2,
            Y = 12
        };
        cancelButton.Clicked += OnCancel;

        Add(_keyLabel, _typeLabel, _descriptionLabel, valueLabel, _valueField, hintLabel, saveButton, cancelButton);
    }

    private string GetTypeHint(string dataType)
    {
        return dataType.ToLower() switch
        {
            "int" or "integer" => "Enter a whole number",
            "bool" or "boolean" => "Enter true or false",
            "decimal" or "double" or "float" => "Enter a decimal number",
            "string" => "Enter text",
            "json" => "Enter valid JSON",
            "url" => "Enter a valid URL",
            "email" => "Enter a valid email address",
            _ => $"Enter a value of type {dataType}"
        };
    }

    private void OnSave(SettingDto setting)
    {
        var value = _valueField.Text.ToString()?.Trim();
        
        // Basic validation based on type
        if (!ValidateValue(value, setting.DataType, out var error))
        {
            MessageBox.ErrorQuery("Validation Error", error, "OK");
            return;
        }

        Result = value;
        Running = false;
    }

    private bool ValidateValue(string? value, string dataType, out string error)
    {
        error = string.Empty;
        
        if (string.IsNullOrEmpty(value))
        {
            error = "Value cannot be empty";
            return false;
        }

        switch (dataType.ToLower())
        {
            case "int":
            case "integer":
                if (!int.TryParse(value, out _))
                {
                    error = "Value must be a valid integer";
                    return false;
                }
                break;
                
            case "bool":
            case "boolean":
                if (!bool.TryParse(value, out _))
                {
                    error = "Value must be true or false";
                    return false;
                }
                break;
                
            case "decimal":
            case "double":
            case "float":
                if (!decimal.TryParse(value, out _))
                {
                    error = "Value must be a valid decimal number";
                    return false;
                }
                break;
                
            case "url":
                if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                {
                    error = "Value must be a valid URL";
                    return false;
                }
                break;
                
            case "email":
                if (!value.Contains('@') || !value.Contains('.'))
                {
                    error = "Value must be a valid email address";
                    return false;
                }
                break;
                
            case "json":
                try
                {
                    System.Text.Json.JsonDocument.Parse(value);
                }
                catch
                {
                    error = "Value must be valid JSON";
                    return false;
                }
                break;
        }

        return true;
    }

    private void OnCancel()
    {
        Result = null;
        Running = false;
    }
}
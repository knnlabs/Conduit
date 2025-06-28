using Terminal.Gui;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Providers;

public class ProviderEditDialog : Dialog
{
    private TextField _providerNameField = null!;
    private TextView _apiKeyField = null!;
    private TextField _baseUrlField = null!;
    private TextField _orgIdField = null!;
    private CheckBox _isEnabledCheckbox = null!;
    private ComboBox _providerTypeCombo = null!;
    
    public CreateProviderCredentialDto? CreateResult { get; private set; }
    public UpdateProviderCredentialDto? UpdateResult { get; private set; }

    // Provider types that support configuration
    private readonly string[] _providerTypes = new[]
    {
        "OpenAI",
        "Anthropic",
        "Google",
        "Mistral",
        "Groq",
        "Perplexity",
        "Cohere",
        "TogetherAI",
        "Fireworks",
        "OpenRouter",
        "DeepSeek",
        "Replicate",
        "MiniMax",
        "xAI",
        "Amazon",
        "Cloudflare",
        "HuggingFace",
        "Azure"
    };

    public ProviderEditDialog(ProviderCredentialDto? existing) : base(
        existing == null ? "Add Provider" : "Edit Provider", 
        60, 18)
    {
        InitializeUI(existing);
    }

    private void InitializeUI(ProviderCredentialDto? existing)
    {
        // Provider Type
        var providerTypeLabel = new Label("Provider Type:") { X = 1, Y = 1 };
        _providerTypeCombo = new ComboBox()
        {
            X = 16,
            Y = 1,
            Width = Dim.Fill(1),
            Height = 1
        };
        _providerTypeCombo.SetSource(_providerTypes);
        
        // Provider Name
        var providerNameLabel = new Label("Provider Name:") { X = 1, Y = 3 };
        _providerNameField = new TextField(existing?.ProviderName ?? "")
        {
            X = 16,
            Y = 3,
            Width = Dim.Fill(1)
        };

        // API Key
        var apiKeyLabel = new Label("API Key:") { X = 1, Y = 5 };
        _apiKeyField = new TextView()
        {
            X = 16,
            Y = 5,
            Width = Dim.Fill(1),
            Height = 2,
            Text = existing?.ApiKey ?? ""
        };

        // Base URL (optional)
        var baseUrlLabel = new Label("Base URL:") { X = 1, Y = 8 };
        _baseUrlField = new TextField(existing?.ApiEndpoint ?? "")
        {
            X = 16,
            Y = 8,
            Width = Dim.Fill(1)
        };

        // Organization ID (optional)
        var orgIdLabel = new Label("Org ID:") { X = 1, Y = 10 };
        _orgIdField = new TextField(existing?.OrganizationId ?? "")
        {
            X = 16,
            Y = 10,
            Width = Dim.Fill(1)
        };

        // Is Enabled
        _isEnabledCheckbox = new CheckBox("Enabled")
        {
            X = 1,
            Y = 12,
            Checked = existing?.IsEnabled ?? true
        };

        // Buttons
        var saveButton = new Button("Save")
        {
            X = Pos.Center() - 10,
            Y = 14
        };
        saveButton.Clicked += OnSave;

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Center() + 2,
            Y = 14
        };
        cancelButton.Clicked += OnCancel;

        // Add all controls
        Add(providerTypeLabel, _providerTypeCombo,
            providerNameLabel, _providerNameField,
            apiKeyLabel, _apiKeyField,
            baseUrlLabel, _baseUrlField,
            orgIdLabel, _orgIdField,
            _isEnabledCheckbox,
            saveButton, cancelButton);

        // Set initial values
        if (existing != null)
        {
            var typeIndex = Array.IndexOf(_providerTypes, existing.ProviderName);
            if (typeIndex >= 0)
            {
                _providerTypeCombo.SelectedItem = typeIndex;
            }
            _providerNameField.ReadOnly = true; // Can't change provider name when editing
            _providerTypeCombo.Enabled = false; // Can't change provider type when editing
        }
        else
        {
            // For new providers, update name when type changes
            _providerTypeCombo.SelectedItemChanged += (e) =>
            {
                if (e.Item >= 0 && e.Item < _providerTypes.Length)
                {
                    _providerNameField.Text = _providerTypes[e.Item];
                }
            };
            _providerTypeCombo.SelectedItem = 0;
        }
    }

    private void OnSave()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_providerNameField.Text.ToString()))
        {
            MessageBox.ErrorQuery("Validation Error", "Provider name is required", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiKeyField.Text.ToString()))
        {
            MessageBox.ErrorQuery("Validation Error", "API key is required", "OK");
            return;
        }

        // Check if we're editing (provider name field is readonly)
        if (_providerNameField.ReadOnly)
        {
            UpdateResult = new UpdateProviderCredentialDto
            {
                ApiKey = _apiKeyField.Text.ToString()!,
                ApiEndpoint = string.IsNullOrWhiteSpace(_baseUrlField.Text.ToString()) ? null : _baseUrlField.Text.ToString(),
                OrganizationId = string.IsNullOrWhiteSpace(_orgIdField.Text.ToString()) ? null : _orgIdField.Text.ToString(),
                IsEnabled = _isEnabledCheckbox.Checked
            };
        }
        else
        {
            CreateResult = new CreateProviderCredentialDto
            {
                ProviderName = _providerNameField.Text.ToString()!,
                ApiKey = _apiKeyField.Text.ToString()!,
                ApiEndpoint = string.IsNullOrWhiteSpace(_baseUrlField.Text.ToString()) ? null : _baseUrlField.Text.ToString(),
                OrganizationId = string.IsNullOrWhiteSpace(_orgIdField.Text.ToString()) ? null : _orgIdField.Text.ToString(),
                IsEnabled = _isEnabledCheckbox.Checked
            };
        }

        Running = false;
    }

    private void OnCancel()
    {
        CreateResult = null;
        UpdateResult = null;
        Running = false;
    }
}
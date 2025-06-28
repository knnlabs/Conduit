using Terminal.Gui;
using ConduitLLM.TUI.Services;
using ConduitLLM.AdminClient.Models;

namespace ConduitLLM.TUI.Views.Models;

public class ModelMappingEditDialog : Dialog
{
    private TextField _requestedModelField;
    private ComboBox _providerCombo;
    private ComboBox _actualModelCombo;
    private TextField _contextWindowField;
    private TextField _maxOutputField;
    private CheckBox _isEnabledCheckbox;
    private StateManager _stateManager;
    
    public CreateModelProviderMappingDto? Result { get; private set; }

    public ModelMappingEditDialog(StateManager stateManager, ModelProviderMappingDto? existing) : base(
        existing == null ? "Add Model Mapping" : "Edit Model Mapping", 
        60, 16)
    {
        _stateManager = stateManager;
        InitializeUI(existing);
    }

    private void InitializeUI(ModelProviderMappingDto? existing)
    {
        // Requested Model
        var requestedModelLabel = new Label("Requested Model:") { X = 1, Y = 1 };
        _requestedModelField = new TextField(existing?.ModelId ?? "")
        {
            X = 18,
            Y = 1,
            Width = Dim.Fill(1)
        };

        // Provider
        var providerLabel = new Label("Provider:") { X = 1, Y = 3 };
        _providerCombo = new ComboBox()
        {
            X = 18,
            Y = 3,
            Width = Dim.Fill(1),
            Height = 1
        };
        
        var providers = _stateManager.Providers
            .Where(p => p.IsEnabled)
            .Select(p => p.ProviderName)
            .ToList();
        _providerCombo.SetSource(providers);
        
        // Actual Model
        var actualModelLabel = new Label("Actual Model:") { X = 1, Y = 5 };
        _actualModelCombo = new ComboBox()
        {
            X = 18,
            Y = 5,
            Width = Dim.Fill(1),
            Height = 1
        };

        // Context Window (optional)
        var contextWindowLabel = new Label("Context Window:") { X = 1, Y = 7 };
        _contextWindowField = new TextField(existing?.MaxContextLength?.ToString() ?? "")
        {
            X = 18,
            Y = 7,
            Width = 20
        };

        // Max Output Tokens (optional)
        var maxOutputLabel = new Label("Max Output:") { X = 1, Y = 9 };
        _maxOutputField = new TextField(existing?.MaxOutputTokens?.ToString() ?? "")
        {
            X = 18,
            Y = 9,
            Width = 20
        };

        // Is Enabled
        _isEnabledCheckbox = new CheckBox("Enabled")
        {
            X = 1,
            Y = 11,
            Checked = existing?.IsEnabled ?? true
        };

        // Buttons
        var saveButton = new Button("Save")
        {
            X = Pos.Center() - 10,
            Y = 13
        };
        saveButton.Clicked += OnSave;

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Center() + 2,
            Y = 13
        };
        cancelButton.Clicked += OnCancel;

        // Add all controls
        Add(requestedModelLabel, _requestedModelField,
            providerLabel, _providerCombo,
            actualModelLabel, _actualModelCombo,
            contextWindowLabel, _contextWindowField,
            maxOutputLabel, _maxOutputField,
            _isEnabledCheckbox,
            saveButton, cancelButton);

        // Set up provider selection change
        _providerCombo.SelectedItemChanged += OnProviderChanged;

        // Set initial values
        if (existing != null)
        {
            var providerIndex = providers.FindIndex(p => p == existing.ProviderId);
            if (providerIndex >= 0)
            {
                _providerCombo.SelectedItem = providerIndex;
            }
            _requestedModelField.ReadOnly = true; // Can't change requested model when editing
        }
        else if (providers.Any())
        {
            _providerCombo.SelectedItem = 0;
        }
    }

    private void OnProviderChanged(ListViewItemEventArgs e)
    {
        var providers = _stateManager.Providers.Where(p => p.IsEnabled).ToList();
        
        if (e.Item >= 0 && e.Item < providers.Count)
        {
            var selectedProvider = providers[e.Item];
            
            // Get models for selected provider
            if (_stateManager.ModelCapabilities.TryGetValue(selectedProvider.ProviderName, out var models))
            {
                var modelIds = models.Select(m => m.ModelId).ToList();
                _actualModelCombo.SetSource(modelIds);
                
                if (modelIds.Any())
                {
                    _actualModelCombo.SelectedItem = 0;
                }
            }
            else
            {
                _actualModelCombo.SetSource(new List<string>());
            }
        }
    }

    private void OnSave()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_requestedModelField.Text.ToString()))
        {
            MessageBox.ErrorQuery("Validation Error", "Requested model is required", "OK");
            return;
        }

        var providers = _stateManager.Providers.Where(p => p.IsEnabled).ToList();
        if (_providerCombo.SelectedItem < 0 || _providerCombo.SelectedItem >= providers.Count)
        {
            MessageBox.ErrorQuery("Validation Error", "Please select a provider", "OK");
            return;
        }

        var selectedProvider = providers[_providerCombo.SelectedItem];
        var actualModel = _actualModelCombo.Text.ToString();
        
        if (string.IsNullOrWhiteSpace(actualModel))
        {
            MessageBox.ErrorQuery("Validation Error", "Please select an actual model", "OK");
            return;
        }

        // Parse optional numeric fields
        int? contextWindow = null;
        if (!string.IsNullOrWhiteSpace(_contextWindowField.Text.ToString()))
        {
            if (int.TryParse(_contextWindowField.Text.ToString(), out var cw))
            {
                contextWindow = cw;
            }
            else
            {
                MessageBox.ErrorQuery("Validation Error", "Context window must be a number", "OK");
                return;
            }
        }

        int? maxOutputTokens = null;
        if (!string.IsNullOrWhiteSpace(_maxOutputField.Text.ToString()))
        {
            if (int.TryParse(_maxOutputField.Text.ToString(), out var mot))
            {
                maxOutputTokens = mot;
            }
            else
            {
                MessageBox.ErrorQuery("Validation Error", "Max output tokens must be a number", "OK");
                return;
            }
        }

        Result = new CreateModelProviderMappingDto
        {
            ModelId = _requestedModelField.Text.ToString()!,
            ProviderId = selectedProvider.ProviderName, // Using provider name as ID
            ProviderModelId = actualModel,
            MaxContextLength = contextWindow,
            IsEnabled = _isEnabledCheckbox.Checked
        };

        Running = false;
    }

    private void OnCancel()
    {
        Result = null;
        Running = false;
    }
}
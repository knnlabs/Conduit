using Terminal.Gui;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Constants;
using ConduitLLM.TUI.Utils;

namespace ConduitLLM.TUI.Views.Configuration;

/// <summary>
/// Extended base class for configuration tabs with common loading patterns.
/// </summary>
public abstract class ConfigurationTabBaseExtended<TConfig> : ConfigurationTabBase 
    where TConfig : class, new()
{
    protected TConfig? _currentConfig;
    protected bool _isLoading;

    protected ConfigurationTabBaseExtended(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// Loads configuration data with standard error handling and status updates.
    /// </summary>
    /// <param name="configName">The name of the configuration being loaded.</param>
    /// <param name="loadFunc">The function to load the configuration.</param>
    /// <param name="populateFunc">The function to populate UI fields from the configuration.</param>
    /// <param name="storeFunc">The function to store the configuration in state manager.</param>
    /// <param name="setLoadingFlag">The function to set the loading flag in state manager.</param>
    /// <param name="setLastUpdated">The function to set the last updated time in state manager.</param>
    protected async Task LoadConfigurationAsync(
        string configName,
        Func<Task<TConfig?>> loadFunc,
        Action<TConfig> populateFunc,
        Action<TConfig> storeFunc,
        Action<bool> setLoadingFlag,
        Action<DateTime> setLastUpdated)
    {
        try
        {
            _isLoading = true;
            UpdateStatus($"{UIConstants.StatusMessages.Loading} {configName} configuration...");
            setLoadingFlag(true);
            
            // Note: Configuration not yet implemented in AdminApiService
            TConfig? config = await loadFunc();
            _currentConfig = config;
            
            if (config != null)
            {
                storeFunc(config);
                populateFunc(config);
            }
            else
            {
                // Reset to defaults - implement in derived class
                Application.MainLoop.Invoke(() => UpdateStatus("No configuration found, using defaults"));
            }

            setLastUpdated(DateTime.UtcNow);
            UpdateStatus($"{configName} {UIConstants.StatusMessages.ConfigurationLoaded}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, string.Format(UIConstants.ErrorMessages.LoadFailed, $"{configName} configuration"));
            UpdateStatus(string.Format(UIConstants.ErrorMessages.ErrorFormat, ex.Message));
        }
        finally
        {
            setLoadingFlag(false);
            _isLoading = false;
        }
    }

    /// <summary>
    /// Saves configuration with standard error handling.
    /// </summary>
    /// <param name="configName">The name of the configuration being saved.</param>
    /// <param name="saveFunc">The function to save the configuration.</param>
    protected async Task SaveConfigurationAsync(string configName, Func<Task> saveFunc)
    {
        try
        {
            UpdateStatus($"Saving {configName} configuration...");
            await saveFunc();
            UpdateStatus($"{configName} {UIConstants.StatusMessages.ConfigurationSaved}");
        }
        catch (NotImplementedException)
        {
            // Note: Configuration save not yet fully implemented
            UpdateStatus($"{configName} {UIConstants.StatusMessages.SaveNotImplemented}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, string.Format(UIConstants.ErrorMessages.SaveFailed, $"{configName} configuration"));
            UpdateStatus(string.Format(UIConstants.ErrorMessages.ErrorFormat, ex.Message));
        }
    }

    /// <summary>
    /// Tests connection with standard error handling.
    /// </summary>
    /// <param name="configName">The name of the configuration being tested.</param>
    /// <param name="testFunc">The function to test the connection.</param>
    protected async Task TestConnectionAsync(string configName, Func<Task> testFunc)
    {
        UpdateStatus($"{UIConstants.StatusMessages.TestingConnection} {configName}...");
        
        try
        {
            await testFunc();
            UpdateStatus($"{configName} {UIConstants.StatusMessages.TestCompleted}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{configName} test failed");
            UpdateStatus($"{UIConstants.StatusMessages.TestFailed}: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows help dialog using the DialogHelper.
    /// </summary>
    /// <param name="title">The help dialog title.</param>
    /// <param name="content">The help content.</param>
    protected void ShowHelp(string title, string content)
    {
        DialogHelper.ShowHelp(title, content);
    }

    /// <summary>
    /// Creates standard configuration buttons (Save, Reset, Test).
    /// </summary>
    /// <param name="onSave">Action to execute on save.</param>
    /// <param name="onReset">Action to execute on reset.</param>
    /// <param name="onTest">Optional action to execute on test.</param>
    /// <param name="includeTest">Whether to include the test button.</param>
    /// <returns>An array of buttons.</returns>
    protected Button[] CreateStandardButtons(Action onSave, Action onReset, Action? onTest = null, bool includeTest = true)
    {
        var buttons = new List<Button>();

        var saveButton = new Button(UIConstants.ButtonLabels.Save) { X = 0, Y = 0 };
        saveButton.Clicked += onSave;
        buttons.Add(saveButton);

        var resetButton = new Button(UIConstants.ButtonLabels.ResetToDefaults) { X = 10, Y = 0 };
        resetButton.Clicked += onReset;
        buttons.Add(resetButton);

        if (includeTest && onTest != null)
        {
            var testButton = new Button(UIConstants.ButtonLabels.TestConnection) { X = 30, Y = 0 };
            testButton.Clicked += onTest;
            buttons.Add(testButton);
        }

        return buttons.ToArray();
    }

    /// <summary>
    /// Adds a label and field combination with standard spacing.
    /// </summary>
    protected TextField AddLabelAndField(View parent, string label, int x, int y, string defaultValue = "", int fieldWidth = 40)
    {
        return UIHelper.AddLabelAndField(parent, label, x, y, fieldWidth, defaultValue);
    }
}
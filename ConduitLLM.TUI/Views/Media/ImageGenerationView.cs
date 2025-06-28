using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.TUI.Views.Media;

public class ImageGenerationView : View
{
    private readonly CoreApiService _coreApiService;
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<ImageGenerationView> _logger;
    
    private TextView _promptField;
    private ComboBox _modelSelector;
    private ComboBox _sizeSelector;
    private ComboBox _qualitySelector;
    private ComboBox _styleSelector;
    private TextField _numImagesField;
    private Button _generateButton;
    private ListView _resultsList;
    private Label _statusLabel;
    private ProgressBar _progressBar;
    
    private List<string> _generatedUrls = new();
    private string? _currentTaskId;

    // Model options
    private readonly string[] _imageModels = new[] { "dall-e-3", "dall-e-2", "minimax-image" };
    private readonly string[] _imageSizes = new[] { "1024x1024", "1792x1024", "1024x1792", "512x512", "256x256" };
    private readonly string[] _imageQualities = new[] { "standard", "hd" };
    private readonly string[] _imageStyles = new[] { "vivid", "natural" };

    public ImageGenerationView(IServiceProvider serviceProvider)
    {
        _coreApiService = serviceProvider.GetRequiredService<CoreApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ImageGenerationView>>();

        InitializeUI();
        SetupEventHandlers();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Settings panel
        var settingsFrame = new FrameView("Image Generation Settings")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 10
        };

        // Prompt input
        var promptLabel = new Label("Prompt:") { X = 1, Y = 0 };
        _promptField = new TextView()
        {
            X = 9,
            Y = 0,
            Width = Dim.Fill(1),
            Height = 3,
            WordWrap = true
        };

        // Model selection
        var modelLabel = new Label("Model:") { X = 1, Y = 4 };
        _modelSelector = new ComboBox()
        {
            X = 9,
            Y = 4,
            Width = 20
        };
        _modelSelector.SetSource(_imageModels);
        _modelSelector.SelectedItem = 0;

        // Size selection
        var sizeLabel = new Label("Size:") { X = Pos.Right(_modelSelector) + 2, Y = 4 };
        _sizeSelector = new ComboBox()
        {
            X = Pos.Right(sizeLabel) + 1,
            Y = 4,
            Width = 15
        };
        _sizeSelector.SetSource(_imageSizes);
        _sizeSelector.SelectedItem = 0;

        // Quality selection (DALL-E 3 only)
        var qualityLabel = new Label("Quality:") { X = 1, Y = 6 };
        _qualitySelector = new ComboBox()
        {
            X = 9,
            Y = 6,
            Width = 10
        };
        _qualitySelector.SetSource(_imageQualities);
        _qualitySelector.SelectedItem = 0;

        // Style selection (DALL-E 3 only)
        var styleLabel = new Label("Style:") { X = Pos.Right(_qualitySelector) + 2, Y = 6 };
        _styleSelector = new ComboBox()
        {
            X = Pos.Right(styleLabel) + 1,
            Y = 6,
            Width = 10
        };
        _styleSelector.SetSource(_imageStyles);
        _styleSelector.SelectedItem = 0;

        // Number of images
        var numLabel = new Label("Count:") { X = Pos.Right(_styleSelector) + 2, Y = 6 };
        _numImagesField = new TextField("1")
        {
            X = Pos.Right(numLabel) + 1,
            Y = 6,
            Width = 5
        };

        settingsFrame.Add(promptLabel, _promptField, modelLabel, _modelSelector, 
            sizeLabel, _sizeSelector, qualityLabel, _qualitySelector, 
            styleLabel, _styleSelector, numLabel, _numImagesField);

        // Generate button
        _generateButton = new Button("Generate Images")
        {
            X = 0,
            Y = Pos.Bottom(settingsFrame),
            Width = 20
        };
        _generateButton.Clicked += GenerateImages;

        // Progress bar
        _progressBar = new ProgressBar()
        {
            X = Pos.Right(_generateButton) + 2,
            Y = Pos.Bottom(settingsFrame),
            Width = Dim.Fill(1),
            Height = 1,
            Visible = false
        };

        // Status label
        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.Bottom(_generateButton) + 1,
            Width = Dim.Fill()
        };

        // Results list
        var resultsFrame = new FrameView("Generated Images (Click URL to open)")
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _resultsList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _resultsList.OpenSelectedItem += OnImageUrlClicked;

        resultsFrame.Add(_resultsList);

        Add(settingsFrame, _generateButton, _progressBar, _statusLabel, resultsFrame);

        // Update UI based on model selection
        _modelSelector.SelectedItemChanged += OnModelChanged;
    }

    private void SetupEventHandlers()
    {
        _signalRService.ImageGenerationStatusUpdated += OnImageGenerationStatusUpdated;
    }

    private void OnModelChanged(ListViewItemEventArgs e)
    {
        var selectedModel = _imageModels[e.Item];
        
        // Update available sizes based on model
        if (selectedModel == "dall-e-3")
        {
            _sizeSelector.SetSource(new[] { "1024x1024", "1792x1024", "1024x1792" });
            _qualitySelector.Enabled = true;
            _styleSelector.Enabled = true;
            _numImagesField.Text = "1"; // DALL-E 3 only supports 1 image
            _numImagesField.ReadOnly = true;
        }
        else if (selectedModel == "dall-e-2")
        {
            _sizeSelector.SetSource(new[] { "1024x1024", "512x512", "256x256" });
            _qualitySelector.Enabled = false;
            _styleSelector.Enabled = false;
            _numImagesField.ReadOnly = false;
        }
        else if (selectedModel == "minimax-image")
        {
            _sizeSelector.SetSource(new[] { "1024x1024", "1920x1080", "1080x1920", "1280x720", "720x1280" });
            _qualitySelector.Enabled = false;
            _styleSelector.Enabled = false;
            _numImagesField.ReadOnly = false;
        }
        
        _sizeSelector.SelectedItem = 0;
    }

    private async void GenerateImages()
    {
        if (string.IsNullOrWhiteSpace(_promptField.Text.ToString()))
        {
            MessageBox.ErrorQuery("Validation Error", "Please enter a prompt", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_stateManager.SelectedVirtualKey))
        {
            MessageBox.ErrorQuery("No Virtual Key", "Please select a virtual key from the Virtual Keys view (F7)", "OK");
            return;
        }

        if (!int.TryParse(_numImagesField.Text.ToString(), out var numImages) || numImages < 1 || numImages > 10)
        {
            MessageBox.ErrorQuery("Validation Error", "Number of images must be between 1 and 10", "OK");
            return;
        }

        _generateButton.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Fraction = 0;
        UpdateStatus("Generating images...");

        try
        {
            var request = new ImageGenerationRequest
            {
                Prompt = _promptField.Text.ToString()!,
                Model = _imageModels[_modelSelector.SelectedItem],
                Size = ParseImageSize(_sizeSelector.Text.ToString()),
                N = numImages
            };

            // Add quality and style for DALL-E 3
            if (request.Model == "dall-e-3")
            {
                request.Quality = ParseImageQuality(_qualitySelector.Text.ToString());
                request.Style = ParseImageStyle(_styleSelector.Text.ToString());
            }

            var response = await _coreApiService.CreateImageGenerationAsync(request);
            
            // Image generation returns a direct response, not async
            if (response != null && response.Data != null)
            {
                // Direct response with URLs
                Application.MainLoop.Invoke(() =>
                {
                    _generatedUrls.Clear();
                    foreach (var image in response.Data)
                    {
                        if (!string.IsNullOrEmpty(image.Url))
                        {
                            _generatedUrls.Add(image.Url);
                        }
                    }
                    UpdateResultsList();
                    UpdateStatus($"Generated {response.Data.Count()} images");
                    _progressBar.Visible = false;
                    _generateButton.Enabled = true;
                });
            }
            else
            {
                Application.MainLoop.Invoke(() =>
                {
                    UpdateStatus("No images generated");
                    _progressBar.Visible = false;
                    _generateButton.Enabled = true;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate images");
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Error: {ex.Message}");
                _progressBar.Visible = false;
                _generateButton.Enabled = true;
            });
        }
    }

    private void OnImageGenerationStatusUpdated(object? sender, ImageGenerationStatusDto e)
    {
        if (e.TaskId != _currentTaskId)
            return;

        Application.MainLoop.Invoke(() =>
        {
            UpdateTaskStatus(e);
        });
    }

    public void UpdateTaskStatus(ImageGenerationStatusDto status)
    {
        _progressBar.Fraction = (float)(status.Progress / 100.0);
        UpdateStatus($"Status: {status.Status} ({status.Progress:F0}%)");

        if (status.Status == "completed" && status.ImageUrls.Any())
        {
            _generatedUrls = status.ImageUrls;
            UpdateResultsList();
            UpdateStatus($"Generated {status.ImageUrls.Count} images");
            _progressBar.Visible = false;
            _generateButton.Enabled = true;
            
            if (!string.IsNullOrEmpty(_currentTaskId))
            {
                Task.Run(async () => await _signalRService.LeaveImageGenerationGroupAsync(_currentTaskId!));
                _currentTaskId = null;
            }
        }
        else if (status.Status == "failed")
        {
            UpdateStatus($"Generation failed: {status.ErrorMessage}");
            _progressBar.Visible = false;
            _generateButton.Enabled = true;
            
            if (!string.IsNullOrEmpty(_currentTaskId))
            {
                Task.Run(async () => await _signalRService.LeaveImageGenerationGroupAsync(_currentTaskId!));
                _currentTaskId = null;
            }
        }
    }

    private void UpdateResultsList()
    {
        var items = _generatedUrls.Select((url, index) => $"Image {index + 1}: {url}").ToList();
        _resultsList.SetSource(items);
    }

    private void OnImageUrlClicked(ListViewItemEventArgs e)
    {
        if (e.Item >= 0 && e.Item < _generatedUrls.Count)
        {
            var url = _generatedUrls[e.Item];
            
            // Show URL in a dialog with copy instructions
            var dialog = new Dialog("Image URL", 70, 10);
            
            var urlText = new TextView()
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = 3,
                Text = url,
                ReadOnly = true
            };
            
            var instructionLabel = new Label("Copy this URL to view the image in your browser")
            {
                X = Pos.Center(),
                Y = 5
            };
            
            var okButton = new Button("OK") { X = Pos.Center(), Y = 7 };
            okButton.Clicked += () => dialog.Running = false;
            
            dialog.Add(urlText, instructionLabel, okButton);
            Application.Run(dialog);
        }
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }

    private ImageSize ParseImageSize(string sizeText)
    {
        return sizeText switch
        {
            "256x256" => ImageSize.Size256x256,
            "512x512" => ImageSize.Size512x512,
            "1024x1024" => ImageSize.Size1024x1024,
            "1792x1024" => ImageSize.Size1792x1024,
            "1024x1792" => ImageSize.Size1024x1792,
            _ => ImageSize.Size1024x1024
        };
    }

    private ImageQuality ParseImageQuality(string qualityText)
    {
        return qualityText.ToLower() switch
        {
            "hd" => ImageQuality.Hd,
            _ => ImageQuality.Standard
        };
    }

    private ImageStyle ParseImageStyle(string styleText)
    {
        return styleText.ToLower() switch
        {
            "natural" => ImageStyle.Natural,
            _ => ImageStyle.Vivid
        };
    }
}
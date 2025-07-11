using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.TUI.Views.Media;

public class VideoGenerationView : View
{
    private readonly CoreApiService _coreApiService;
    private readonly StateManager _stateManager;
    private readonly SignalRService _signalRService;
    private readonly ILogger<VideoGenerationView> _logger;
    
    private TextView _promptField = null!;
    private ComboBox _modelSelector = null!;
    private ComboBox _resolutionSelector = null!;
    private TextField _durationField = null!;
    private Button _generateButton = null!;
    private Button _checkStatusButton = null!;
    private ListView _tasksList = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;
    
    private Dictionary<string, VideoTaskInfo> _activeTasks = new();
    
    // Model options
    private readonly string[] _videoModels = new[] { "minimax-video", "video-01" };
    private readonly string[] _videoResolutions = new[] 
    { 
        "1280x720",   // 720p landscape
        "1920x1080",  // 1080p landscape
        "720x1280",   // 720p portrait
        "1080x1920",  // 1080p portrait
        "720x480"     // SD landscape
    };

    private class VideoTaskInfo
    {
        public string TaskId { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string Status { get; set; } = "";
        public double Progress { get; set; }
        public string? VideoUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public VideoGenerationView(IServiceProvider serviceProvider)
    {
        _coreApiService = serviceProvider.GetRequiredService<CoreApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _signalRService = serviceProvider.GetRequiredService<SignalRService>();
        _logger = serviceProvider.GetRequiredService<ILogger<VideoGenerationView>>();

        InitializeUI();
        SetupEventHandlers();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Settings panel
        var settingsFrame = new FrameView("Video Generation Settings")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 9
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
        _modelSelector.SetSource(_videoModels);
        _modelSelector.SelectedItem = 0;

        // Resolution selection
        var resolutionLabel = new Label("Resolution:") { X = Pos.Right(_modelSelector) + 2, Y = 4 };
        _resolutionSelector = new ComboBox()
        {
            X = Pos.Right(resolutionLabel) + 1,
            Y = 4,
            Width = 15
        };
        _resolutionSelector.SetSource(_videoResolutions);
        _resolutionSelector.SelectedItem = 1; // Default to 1080p

        // Duration
        var durationLabel = new Label("Duration (s):") { X = 1, Y = 6 };
        _durationField = new TextField("6")
        {
            X = 14,
            Y = 6,
            Width = 5
        };

        var durationHelp = new Label("(max 6 seconds)")
        {
            X = Pos.Right(_durationField) + 1,
            Y = 6,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.DarkGray, Color.Black)
            }
        };

        settingsFrame.Add(promptLabel, _promptField, modelLabel, _modelSelector,
            resolutionLabel, _resolutionSelector, durationLabel, _durationField, durationHelp);

        // Buttons
        _generateButton = new Button("Generate Video")
        {
            X = 0,
            Y = Pos.Bottom(settingsFrame),
            Width = 20
        };
        _generateButton.Clicked += GenerateVideo;

        _checkStatusButton = new Button("Check Status")
        {
            X = Pos.Right(_generateButton) + 2,
            Y = Pos.Bottom(settingsFrame),
            Width = 15,
            Enabled = false
        };
        _checkStatusButton.Clicked += CheckSelectedTaskStatus;

        // Progress bar
        _progressBar = new ProgressBar()
        {
            X = Pos.Right(_checkStatusButton) + 2,
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

        // Tasks list
        var tasksFrame = new FrameView("Video Generation Tasks (Click URL to open)")
        {
            X = 0,
            Y = Pos.Bottom(_statusLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _tasksList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _tasksList.SelectedItemChanged += OnTaskSelected;
        _tasksList.OpenSelectedItem += OnVideoUrlClicked;

        tasksFrame.Add(_tasksList);

        Add(settingsFrame, _generateButton, _checkStatusButton, _progressBar, _statusLabel, tasksFrame);
    }

    private void SetupEventHandlers()
    {
        _signalRService.VideoGenerationStatusUpdated += OnVideoGenerationStatusUpdated;
    }

    private async void GenerateVideo()
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

        if (!int.TryParse(_durationField.Text.ToString(), out var duration) || duration < 1 || duration > 6)
        {
            MessageBox.ErrorQuery("Validation Error", "Duration must be between 1 and 6 seconds", "OK");
            return;
        }

        _generateButton.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Fraction = 0;
        UpdateStatus("Starting video generation...");

        try
        {
            // Use async video generation API
            var request = new AsyncVideoGenerationRequest
            {
                Prompt = _promptField.Text.ToString()!,
                Model = _videoModels[_modelSelector.SelectedItem],
                Size = _resolutionSelector.Text.ToString(),
                Duration = duration
            };

            var response = await _coreApiService.CreateAsyncVideoGenerationAsync(request);
            
            if (!string.IsNullOrEmpty(response.TaskId))
            {
                var taskInfo = new VideoTaskInfo
                {
                    TaskId = response.TaskId,
                    Prompt = request.Prompt,
                    Status = "pending",
                    Progress = 0,
                    CreatedAt = DateTime.Now
                };
                
                _activeTasks[response.TaskId] = taskInfo;
                
                // Join SignalR group for this task
                await _signalRService.JoinVideoGenerationGroupAsync(response.TaskId);
                
                Application.MainLoop.Invoke(() =>
                {
                    UpdateTasksList();
                    UpdateStatus($"Video generation task started: {response.TaskId}");
                    _progressBar.Visible = false;
                    _generateButton.Enabled = true;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate video");
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Error: {ex.Message}");
                _progressBar.Visible = false;
                _generateButton.Enabled = true;
            });
        }
    }

    private async void CheckSelectedTaskStatus()
    {
        if (_tasksList.SelectedItem < 0)
            return;

        var taskId = _activeTasks.Values.ElementAt(_tasksList.SelectedItem).TaskId;
        
        try
        {
            UpdateStatus($"Checking status for task {taskId}...");
            var status = await _coreApiService.GetVideoGenerationStatusAsync(taskId);
            
            Application.MainLoop.Invoke(() =>
            {
                if (_activeTasks.TryGetValue(taskId, out var taskInfo))
                {
                    taskInfo.Status = status.Status.ToString();
                    taskInfo.Progress = status.Progress;
                    taskInfo.VideoUrl = status.Result?.Data?.FirstOrDefault()?.Url;
                    UpdateTasksList();
                    UpdateStatus($"Task {taskId}: {status.Status} ({status.Progress:F0}%)");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check video status");
            Application.MainLoop.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
        }
    }

    private void OnVideoGenerationStatusUpdated(object? sender, VideoGenerationStatusDto e)
    {
        Application.MainLoop.Invoke(() =>
        {
            UpdateTaskStatus(e);
        });
    }

    public void UpdateTaskStatus(VideoGenerationStatusDto status)
    {
        if (_activeTasks.TryGetValue(status.TaskId, out var taskInfo))
        {
            taskInfo.Status = status.Status;
            taskInfo.Progress = status.Progress;
            taskInfo.VideoUrl = status.VideoUrl;
            
            UpdateTasksList();
            
            // Update progress bar if this is the most recent task
            var mostRecentTask = _activeTasks.Values.OrderByDescending(t => t.CreatedAt).FirstOrDefault();
            if (mostRecentTask?.TaskId == status.TaskId && status.Status == "processing")
            {
                _progressBar.Visible = true;
                _progressBar.Fraction = (float)(status.Progress / 100.0);
            }
            
            UpdateStatus($"Task {status.TaskId}: {status.Status} ({status.Progress:F0}%)");
            
            if (status.Status == "completed" || status.Status == "failed")
            {
                // Leave SignalR group when task is done
                Task.Run(async () => await _signalRService.LeaveVideoGenerationGroupAsync(status.TaskId));
                
                if (mostRecentTask?.TaskId == status.TaskId)
                {
                    _progressBar.Visible = false;
                }
            }
        }
    }

    private void UpdateTasksList()
    {
        var items = _activeTasks.Values
            .OrderByDescending(t => t.CreatedAt)
            .Select(t =>
            {
                var truncatedPrompt = t.Prompt.Length > 30 ? t.Prompt.Substring(0, 30) + "..." : t.Prompt;
                var statusIcon = t.Status switch
                {
                    "completed" => "✓",
                    "failed" => "✗",
                    "processing" => "⚡",
                    _ => "◷"
                };
                
                if (t.Status == "completed" && !string.IsNullOrEmpty(t.VideoUrl))
                {
                    return $"{statusIcon} {truncatedPrompt} - {t.VideoUrl}";
                }
                else
                {
                    return $"{statusIcon} {truncatedPrompt} - {t.Status} ({t.Progress:F0}%)";
                }
            })
            .ToList();
        
        _tasksList.SetSource(items);
    }

    private void OnTaskSelected(ListViewItemEventArgs e)
    {
        _checkStatusButton.Enabled = e.Item >= 0 && e.Item < _activeTasks.Count;
    }

    private void OnVideoUrlClicked(ListViewItemEventArgs e)
    {
        if (e.Item >= 0 && e.Item < _activeTasks.Count)
        {
            var task = _activeTasks.Values.ElementAt(e.Item);
            if (!string.IsNullOrEmpty(task.VideoUrl))
            {
                // Show URL in a dialog with copy instructions
                var dialog = new Dialog("Video URL", 70, 10);
                
                var urlText = new TextView()
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(1),
                    Height = 3,
                    Text = task.VideoUrl,
                    ReadOnly = true
                };
                
                var instructionLabel = new Label("Copy this URL to view the video in your browser")
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
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}
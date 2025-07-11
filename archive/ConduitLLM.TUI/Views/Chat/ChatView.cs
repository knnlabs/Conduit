using System.Text;
using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.TUI.Services;
using ConduitLLM.CoreClient.Models;

namespace ConduitLLM.TUI.Views.Chat;

public class ChatView : View
{
    private readonly CoreApiService _coreApiService;
    private readonly StateManager _stateManager;
    private readonly ILogger<ChatView> _logger;
    
    private TextView _chatHistory = null!;
    private TextView _inputField = null!;
    private ComboBox _modelSelector = null!;
    private Label _statusLabel = null!;
    private Button _sendButton = null!;
    private Button _clearButton = null!;
    private CheckBox _streamingCheckbox = null!;
    
    private List<ChatCompletionMessage> _messages = new();
    private bool _isProcessing = false;
    private CancellationTokenSource? _streamingCts;

    public ChatView(IServiceProvider serviceProvider)
    {
        _coreApiService = serviceProvider.GetRequiredService<CoreApiService>();
        _stateManager = serviceProvider.GetRequiredService<StateManager>();
        _logger = serviceProvider.GetRequiredService<ILogger<ChatView>>();

        InitializeUI();
        LoadAvailableModels();
    }

    private void InitializeUI()
    {
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Model selector and controls
        var topPanel = new FrameView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 3,
            Title = "Settings"
        };

        _modelSelector = new ComboBox()
        {
            X = 1,
            Y = 0,
            Width = 30,
            Height = 1
        };
        _modelSelector.SelectedItemChanged += (e) => _stateManager.SelectedModel = _modelSelector.Text.ToString();

        var modelLabel = new Label("Model:") { X = Pos.Left(_modelSelector) - 7, Y = 0 };

        _streamingCheckbox = new CheckBox("Stream responses")
        {
            X = Pos.Right(_modelSelector) + 2,
            Y = 0,
            Checked = true
        };

        _statusLabel = new Label("Ready")
        {
            X = Pos.Right(_streamingCheckbox) + 2,
            Y = 0,
            Width = Dim.Fill(1),
            TextAlignment = TextAlignment.Right
        };

        topPanel.Add(modelLabel, _modelSelector, _streamingCheckbox, _statusLabel);

        // Chat history
        var chatFrame = new FrameView("Chat History")
        {
            X = 0,
            Y = Pos.Bottom(topPanel),
            Width = Dim.Fill(),
            Height = Dim.Fill(5)
        };

        _chatHistory = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true
        };

        chatFrame.Add(_chatHistory);

        // Input area
        var inputFrame = new FrameView("Input")
        {
            X = 0,
            Y = Pos.Bottom(chatFrame),
            Width = Dim.Fill(),
            Height = 5
        };

        _inputField = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2,
            WordWrap = true
        };

        // Allow Ctrl+Enter to send
        _inputField.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == (Key.Enter | Key.CtrlMask))
            {
                e.Handled = true;
                SendMessage();
            }
        };

        var buttonPanel = new View()
        {
            X = 0,
            Y = Pos.Bottom(_inputField),
            Width = Dim.Fill(),
            Height = 1
        };

        _sendButton = new Button("Send [Ctrl+Enter]")
        {
            X = 0,
            Y = 0
        };
        _sendButton.Clicked += () => SendMessage();

        _clearButton = new Button("Clear")
        {
            X = Pos.Right(_sendButton) + 1,
            Y = 0
        };
        _clearButton.Clicked += ClearChat;

        var cancelButton = new Button("Cancel")
        {
            X = Pos.Right(_clearButton) + 1,
            Y = 0,
            Visible = false
        };
        cancelButton.Clicked += CancelStreaming;

        buttonPanel.Add(_sendButton, _clearButton, cancelButton);
        inputFrame.Add(_inputField, buttonPanel);

        Add(topPanel, chatFrame, inputFrame);
    }

    private async void LoadAvailableModels()
    {
        try
        {
            UpdateStatus("Loading models...");
            
            // Check if virtual key is selected
            if (string.IsNullOrEmpty(_stateManager.SelectedVirtualKey))
            {
                UpdateStatus("No virtual key selected");
                AppendToChat("System", "Please select a virtual key from the Virtual Keys view (F7) before using chat.");
                return;
            }

            var modelsResponse = await _coreApiService.GetModelsAsync();
            var modelNames = modelsResponse.Data.Select(m => m.Id).ToList();
            
            Application.MainLoop.Invoke(() =>
            {
                _modelSelector.SetSource(modelNames);
                if (modelNames.Any())
                {
                    _modelSelector.SelectedItem = 0;
                    _stateManager.SelectedModel = modelNames[0];
                }
                UpdateStatus("Ready");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load models");
            Application.MainLoop.Invoke(() =>
            {
                UpdateStatus($"Error: {ex.Message}");
                AppendToChat("System", $"Failed to load models: {ex.Message}");
            });
        }
    }

    private async void SendMessage()
    {
        if (_isProcessing || string.IsNullOrWhiteSpace(_inputField.Text.ToString()))
            return;

        var userMessage = _inputField.Text.ToString()!.Trim();
        _inputField.Text = "";
        
        _isProcessing = true;
        UpdateStatus("Processing...");
        AppendToChat("You", userMessage);
        
        _messages.Add(new ChatCompletionMessage { Role = "user", Content = userMessage });

        try
        {
            var request = new ChatCompletionRequest
            {
                Model = _stateManager.SelectedModel ?? "gpt-3.5-turbo",
                Messages = _messages,
                Stream = _streamingCheckbox.Checked
            };

            if (_streamingCheckbox.Checked)
            {
                await StreamResponse(request);
            }
            else
            {
                await GetNonStreamingResponse(request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            Application.MainLoop.Invoke(() =>
            {
                AppendToChat("System", $"Error: {ex.Message}");
                UpdateStatus("Error occurred");
            });
        }
        finally
        {
            _isProcessing = false;
            Application.MainLoop.Invoke(() => UpdateStatus("Ready"));
        }
    }

    private async Task StreamResponse(ChatCompletionRequest request)
    {
        _streamingCts = new CancellationTokenSource();
        var responseBuilder = new StringBuilder();
        
        Application.MainLoop.Invoke(() => AppendToChat("Assistant", ""));
        
        try
        {
            await foreach (var chunk in _coreApiService.CreateChatCompletionStreamAsync(request))
            {
                if (_streamingCts.Token.IsCancellationRequested)
                    break;
                    
                responseBuilder.Append(chunk);
                
                Application.MainLoop.Invoke(() =>
                {
                    UpdateLastAssistantMessage(responseBuilder.ToString());
                });
            }

            var completeResponse = responseBuilder.ToString();
            _messages.Add(new ChatCompletionMessage { Role = "assistant", Content = completeResponse });
        }
        catch (OperationCanceledException)
        {
            Application.MainLoop.Invoke(() =>
            {
                AppendToChat("System", "Response cancelled by user");
            });
        }
        finally
        {
            _streamingCts?.Dispose();
            _streamingCts = null;
        }
    }

    private async Task GetNonStreamingResponse(ChatCompletionRequest request)
    {
        var response = await _coreApiService.CreateChatCompletionAsync(request);
        var content = response.Choices.FirstOrDefault()?.Message?.Content ?? "No response";
        
        Application.MainLoop.Invoke(() =>
        {
            AppendToChat("Assistant", content);
        });
        
        _messages.Add(new ChatCompletionMessage { Role = "assistant", Content = content });
    }

    private void AppendToChat(string sender, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var formattedMessage = $"[{timestamp}] {sender}: {message}\n\n";
        
        _chatHistory.Text += formattedMessage;
        _chatHistory.MoveEnd();
    }

    private void UpdateLastAssistantMessage(string content)
    {
        var text = _chatHistory.Text.ToString() ?? string.Empty;
        var lastAssistantIndex = text.LastIndexOf("] Assistant: ");
        
        if (lastAssistantIndex != -1)
        {
            var messageStart = lastAssistantIndex + "] Assistant: ".Length;
            var nextMessageIndex = text.IndexOf("\n\n", messageStart);
            
            if (nextMessageIndex == -1)
            {
                _chatHistory.Text = text.Substring(0, messageStart) + content + "\n\n";
            }
            else
            {
                _chatHistory.Text = text.Substring(0, messageStart) + content + text.Substring(nextMessageIndex);
            }
            
            _chatHistory.MoveEnd();
        }
    }

    private void ClearChat()
    {
        _messages.Clear();
        _chatHistory.Text = "";
        UpdateStatus("Chat cleared");
    }

    private void CancelStreaming()
    {
        _streamingCts?.Cancel();
    }

    private void UpdateStatus(string status)
    {
        _statusLabel.Text = status;
    }
}
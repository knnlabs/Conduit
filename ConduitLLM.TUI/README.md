# Conduit TUI

A Terminal User Interface (TUI) for Conduit LLM Gateway that provides full functionality equivalent to the WebUI through a text-based interface.

## Features

- **Chat Interface**: Interactive chat with streaming support
- **Provider Management**: Create, edit, delete, and discover provider credentials
- **Model Mapping**: Manage model-to-provider mappings with real-time updates
- **Virtual Key Management**: Create and manage API keys with budgets and restrictions
- **Image Generation**: Generate images with progress tracking
- **Video Generation**: Generate videos with real-time status updates
- **System Health Monitoring**: View system status and component health
- **Configuration Management**: View and manage system settings
- **Real-time Updates**: SignalR integration for live updates across all views

## Prerequisites

- .NET 9.0 SDK or later
- Running Conduit Core API and Admin API services
- Master API key for authentication

## Installation

```bash
# Clone the repository
git clone https://github.com/knnlabs/Conduit.git
cd Conduit/ConduitLLM.TUI

# Build the project
dotnet build

# Run the TUI
dotnet run -- --master-key YOUR_MASTER_KEY
```

## Usage

### Command Line Options

```bash
conduit-tui --master-key <key> [options]

Options:
  -k, --master-key <key>     Master API key for authentication (required)
  -c, --core-api <url>       Core API URL (default: http://localhost:5000)
  -a, --admin-api <url>      Admin API URL (default: http://localhost:5002)
  -s, --show-virtual-key     Show the WebUI virtual key and exit
  -h, --help                 Show help information
```

### Keyboard Shortcuts

- **F1** - Help / Keyboard Shortcuts
- **F2** - Chat View
- **F3** - Model Mappings
- **F4** - Provider Credentials
- **F5** - Image Generation
- **F6** - Video Generation
- **F7** - Virtual Keys
- **F8** - System Health
- **F9** - Configuration Management
- **Ctrl+Q** - Quit Application
- **Tab** - Next Field
- **Shift+Tab** - Previous Field
- **Enter** - Select/Confirm
- **Esc** - Cancel/Back

### Using the Chat Interface

1. First, select a virtual key from the Virtual Keys view (F7)
2. Navigate to Chat view (F2)
3. Select a model from the dropdown
4. Type your message and press Ctrl+Enter to send
5. Enable/disable streaming with the checkbox

### Generating Images

1. Select a virtual key (F7)
2. Navigate to Image Generation (F5)
3. Enter your prompt
4. Select model, size, and other options
5. Click Generate Images
6. Click on generated URLs to view them

### Generating Videos

1. Select a virtual key (F7)
2. Navigate to Video Generation (F6)
3. Enter your prompt
4. Select resolution and duration
5. Click Generate Video
6. Monitor progress in real-time
7. Click on completed videos to view URLs

## Architecture

The TUI is built with:
- **Terminal.Gui** - Cross-platform terminal UI framework
- **ConduitLLM.AdminClient** - .NET SDK for Admin API
- **ConduitLLM.CoreClient** - .NET SDK for Core API
- **SignalR** - Real-time updates
- **Dependency Injection** - Service management

## Development

### Project Structure

```
ConduitLLM.TUI/
├── Configuration/       # App configuration
├── Services/           # API service wrappers
├── Views/              # UI components
│   ├── Chat/          # Chat interface
│   ├── Providers/     # Provider management
│   ├── Models/        # Model mappings
│   ├── Media/         # Image/Video generation
│   ├── Keys/          # Virtual key management
│   └── Monitoring/    # Health dashboard
├── Models/            # Data models
└── Utilities/         # Helper functions
```

### Building from Source

```bash
# Restore dependencies
dotnet restore

# Build in Debug mode
dotnet build

# Build in Release mode
dotnet build -c Release

# Run tests (when available)
dotnet test
```

## Platform Support

- **Linux/macOS**: Full support, optimized experience
- **Windows Terminal**: Full support
- **Legacy Windows Console**: Basic support

## Troubleshooting

### Connection Issues
- Ensure Core API and Admin API are running
- Verify API URLs are correct
- Check master key is valid

### Display Issues
- Use a modern terminal emulator
- Ensure terminal supports UTF-8
- Minimum terminal size: 80x24

### Virtual Key Selection
- Always select a virtual key before using chat/generation features
- Virtual keys are managed in the Virtual Keys view (F7)

## Authentication

The TUI uses a two-step authentication process:

1. **Master Key**: Used to authenticate with the Admin API
   - Pass via `--master-key` command line argument
   - Required for managing virtual keys, providers, and settings
   - In docker-compose.yml, this is typically set to `alpha`

2. **Virtual Key**: Used to authenticate with the Core API  
   - Required for chat, image/video generation, and SignalR connections
   - The TUI uses the shared WebUI virtual key stored in the Configuration table
   - This key is automatically created when the WebUI first starts
   - You can view this key using: `conduit-tui -k YOUR_MASTER_KEY --show-virtual-key`

**Note**: Virtual keys in the database are hashed for security. The only unencrypted virtual key is the WebUI key stored in the Configuration table, which is shared between the WebUI and TUI.

## License

MIT License - See LICENSE file in the repository root
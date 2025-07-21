using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SpectreColor = Spectre.Console.Color;
using Terminal.Gui;
using ConduitLLM.TUI.Configuration;
using ConduitLLM.TUI.Services;
using ConduitLLM.TUI.Views;
using ConduitLLM.AdminClient;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.CoreClient;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.TUI.Utils;

namespace ConduitLLM.TUI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Check for --help or -h before building the full command
        if (args.Any(arg => arg == "--help" || arg == "-h" || arg == "-?" || arg == "/?"))
        {
            ShowHelp();
            return 0;
        }

        var rootCommand = new RootCommand("Conduit TUI - Terminal User Interface for Conduit LLM");
        
        var masterKeyOption = new Option<string>(
            aliases: new[] { "--master-key", "-k" },
            description: "Master API key for authentication")
        {
            IsRequired = true
        };
        
        var coreApiUrlOption = new Option<string>(
            aliases: new[] { "--core-api", "-c" },
            getDefaultValue: () => "http://localhost:5000",
            description: "Core API URL");
        
        var adminApiUrlOption = new Option<string>(
            aliases: new[] { "--admin-api", "-a" },
            getDefaultValue: () => "http://localhost:5002",
            description: "Admin API URL");

        var helpOption = new Option<bool>(
            aliases: new[] { "--help", "-h" },
            description: "Show help and usage information");

        var showVirtualKeyOption = new Option<bool>(
            aliases: new[] { "--show-virtual-key", "-s" },
            description: "Show the currently selected virtual key and exit");

        rootCommand.AddOption(masterKeyOption);
        rootCommand.AddOption(coreApiUrlOption);
        rootCommand.AddOption(adminApiUrlOption);
        rootCommand.AddOption(helpOption);
        rootCommand.AddOption(showVirtualKeyOption);

        rootCommand.SetHandler(async (masterKey, coreApiUrl, adminApiUrl, help, showVirtualKey) =>
        {
            if (help)
            {
                ShowHelp();
                return;
            }
            if (showVirtualKey)
            {
                await ShowVirtualKey(masterKey, coreApiUrl, adminApiUrl);
                return;
            }
            await RunApplication(masterKey, coreApiUrl, adminApiUrl);
        }, masterKeyOption, coreApiUrlOption, adminApiUrlOption, helpOption, showVirtualKeyOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunApplication(string masterKey, string coreApiUrl, string adminApiUrl)
    {
        // Show splash screen
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Conduit TUI")
                .LeftJustified()
                .Color(SpectreColor.Blue));
        
        AnsiConsole.MarkupLine("[grey]Initializing...[/]");
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services, configuration, masterKey, coreApiUrl, adminApiUrl);
        var serviceProvider = services.BuildServiceProvider();
        
        // Get the log buffer for later use
        var logBuffer = serviceProvider.GetRequiredService<LogBuffer>();

        // Initialize services
        var stateManager = serviceProvider.GetRequiredService<StateManager>();
        var signalRService = serviceProvider.GetRequiredService<SignalRService>();
        var adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
        
        AnsiConsole.MarkupLine("[green]Checking Admin API connection...[/]");
        
        // Check if Admin API is available first
        bool adminApiAvailable = false;
        try
        {
            var virtualKeys = await adminApiService.GetVirtualKeysAsync();
            adminApiAvailable = true;
            AnsiConsole.MarkupLine("[green]✓ Admin API connected[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Admin API connection failed: {ex.Message}[/]");
            
            // Check if it's an authentication error
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[red]Authentication failed. Please check your master key.[/]");
                AnsiConsole.MarkupLine("[grey]The provided master key was rejected by the Admin API.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Tip:[/] Make sure you're using the correct master key.");
                AnsiConsole.MarkupLine("[grey]You can find it in your Admin API configuration.[/]");
                return; // Exit early - no point continuing without authentication
            }
            
            AnsiConsole.MarkupLine("[yellow]  SignalR will not be available without Admin API[/]");
        }
        
        if (adminApiAvailable)
        {
            AnsiConsole.MarkupLine("[green]Connecting to SignalR...[/]");
            
            try
            {
                await signalRService.ConnectAsync();
                AnsiConsole.MarkupLine("[green]✓ Connected to SignalR[/]");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
            {
                // 404 indicates missing SignalR endpoint - this is a critical error
                AnsiConsole.MarkupLine($"[red]✗ SignalR connection failed: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[red]  The SignalR endpoint is missing from the Core API.[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]This appears to be a version mismatch between the TUI and Core API.[/]");
                AnsiConsole.MarkupLine("[yellow]Please ensure both components are from compatible versions.[/]");
                return; // Exit early - cannot proceed without proper SignalR endpoints
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ SignalR connection failed: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[yellow]  Continuing without real-time updates[/]");
            }
        }

        await Task.Delay(1000); // Brief pause to show initialization
        
        // Start Terminal.Gui application
        Application.Init();
        
        try
        {
            var mainWindow = new MainWindow(serviceProvider);
            Application.Run(mainWindow);
        }
        finally
        {
            Application.Shutdown();
            await signalRService.DisposeAsync();
            
            // Dump logs to console before exiting
            DumpLogsToConsole(logBuffer);
            
            if (serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    static void ConfigureServices(IServiceCollection services, IConfiguration configuration, 
        string masterKey, string coreApiUrl, string adminApiUrl)
    {
        // Create and register log buffer
        var logBuffer = new LogBuffer(maxEntries: 1000);
        services.AddSingleton(logBuffer);
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            
            // Add TUI logger provider instead of console
            builder.AddProvider(new TuiLoggerProvider(logBuffer, LogLevel.Information));
            
            builder.SetMinimumLevel(LogLevel.Information);
            
            // Set specific log levels for noisy components
            builder.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Warning);
            builder.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Warning);
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            
            // Show more detail for our components
            builder.AddFilter("ConduitLLM.TUI", LogLevel.Debug);
        });

        // Configuration
        services.AddSingleton(new AppConfiguration
        {
            MasterKey = masterKey,
            CoreApiUrl = coreApiUrl,
            AdminApiUrl = adminApiUrl
        });

        // Admin Client
        services.AddSingleton<ConduitAdminClient>(provider =>
        {
            var config = new ConduitAdminClientConfiguration
            {
                MasterKey = masterKey,
                AdminApiUrl = adminApiUrl
            };
            return new ConduitAdminClient(config);
        });

        // Services
        services.AddSingleton<NavigationStateService>();
        services.AddSingleton<StateManager>();
        services.AddSingleton<ConfigurationStateManager>();
        services.AddSingleton<AdminApiService>();
        services.AddSingleton<CoreApiService>();
        services.AddSingleton<SignalRService>();
        
        // Views (as transient since Terminal.Gui manages their lifecycle)
        services.AddTransient<MainWindow>();
    }

    static async Task ShowVirtualKey(string masterKey, string coreApiUrl, string adminApiUrl)
    {
        AnsiConsole.Write(
            new FigletText("Conduit TUI")
                .LeftJustified()
                .Color(SpectreColor.Blue));
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Retrieving WebUI virtual key from configuration...[/]");
        
        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            // Setup minimal services needed
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });
            
            services.AddSingleton(new AppConfiguration
            {
                MasterKey = masterKey,
                CoreApiUrl = coreApiUrl,
                AdminApiUrl = adminApiUrl
            });

            services.AddSingleton<ConduitAdminClient>(provider =>
            {
                var config = new ConduitAdminClientConfiguration
                {
                    MasterKey = masterKey,
                    AdminApiUrl = adminApiUrl
                };
                return new ConduitAdminClient(config);
            });

            services.AddSingleton<NavigationStateService>();
            services.AddSingleton<CoreApiService>();
            services.AddSingleton<StateManager>();
            services.AddSingleton<ConfigurationStateManager>();
            services.AddScoped<AdminApiService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var adminApiService = serviceProvider.GetRequiredService<AdminApiService>();
            
            // Get the WebUI_VirtualKey from configuration
            try
            {
                AnsiConsole.MarkupLine($"[grey]Admin API URL: {adminApiUrl}[/]");
                AnsiConsole.MarkupLine($"[grey]Master Key: {masterKey.Substring(0, 5)}...[/]");
                
                var webUIKeySetting = await adminApiService.GetSettingByKeyAsync("WebUI_VirtualKey");
                var webUIKeyIdSetting = await adminApiService.GetSettingByKeyAsync("WebUI_VirtualKeyId");
                
                AnsiConsole.MarkupLine($"[grey]WebUI Key Setting: {(webUIKeySetting == null ? "null" : "found")}[/]");
                if (webUIKeySetting != null)
                {
                    AnsiConsole.MarkupLine($"[grey]WebUI Key Value: {(string.IsNullOrEmpty(webUIKeySetting.Value) ? "empty" : "present")}[/]");
                }
                
                if (webUIKeySetting != null && !string.IsNullOrEmpty(webUIKeySetting.Value))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]WebUI Virtual Key (from Configuration):[/]");
                    AnsiConsole.MarkupLine($"  [cyan]{webUIKeySetting.Value}[/]");
                    
                    if (webUIKeyIdSetting != null && !string.IsNullOrEmpty(webUIKeyIdSetting.Value))
                    {
                        AnsiConsole.MarkupLine($"  [grey]Key ID: {webUIKeyIdSetting.Value}[/]");
                        
                        // Try to get more info about the key
                        if (int.TryParse(webUIKeyIdSetting.Value, out var keyId))
                        {
                            var virtualKeys = await adminApiService.GetVirtualKeysAsync();
                            var keyInfo = virtualKeys?.FirstOrDefault(k => k.Id == keyId);
                            if (keyInfo != null)
                            {
                                AnsiConsole.MarkupLine($"  [grey]Name: {keyInfo.KeyName}[/]");
                                AnsiConsole.MarkupLine($"  [grey]Enabled: {(keyInfo.IsEnabled ? "Yes" : "No")}[/]");
                                
                                if (keyInfo.MaxBudget > 0)
                                {
                                    AnsiConsole.MarkupLine($"  [grey]Max Budget: ${keyInfo.MaxBudget:N2}[/]");
                                }
                                if (keyInfo.CurrentSpend > 0)
                                {
                                    AnsiConsole.MarkupLine($"  [grey]Spent: ${keyInfo.CurrentSpend:N2}[/]");
                                }
                            }
                        }
                    }
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]No WebUI virtual key found in configuration.[/]");
                    AnsiConsole.MarkupLine("[grey]The WebUI will create one automatically when it starts.[/]");
                }
            }
            catch (Exception settingsEx)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Note: Unable to retrieve configuration settings.[/]");
                AnsiConsole.MarkupLine($"[grey]Details: {settingsEx.Message}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (ex.Message.Contains("refused") || ex.Message.Contains("Unable to connect"))
            {
                AnsiConsole.MarkupLine("[grey]Make sure the Admin API is running at {0}[/]", adminApiUrl);
            }
        }
    }

    static void ShowHelp()
    {
        AnsiConsole.Write(
            new FigletText("Conduit TUI")
                .LeftJustified()
                .Color(SpectreColor.Blue));
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Conduit TUI[/] - Terminal User Interface for Conduit LLM Gateway");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]USAGE:[/]");
        AnsiConsole.MarkupLine("    conduit-tui --master-key [[KEY]] [[OPTIONS]]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]REQUIRED ARGUMENTS:[/]");
        AnsiConsole.MarkupLine("    [green]--master-key, -k [[KEY]][/]    Master API key for authentication");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]OPTIONS:[/]");
        AnsiConsole.MarkupLine("    [green]--core-api, -c [[URL]][/]      Core API URL (default: http://localhost:5000)");
        AnsiConsole.MarkupLine("    [green]--admin-api, -a [[URL]][/]     Admin API URL (default: http://localhost:5002)");
        AnsiConsole.MarkupLine("    [green]--show-virtual-key, -s[/]    Show the selected virtual key and exit");
        AnsiConsole.MarkupLine("    [green]--help, -h[/]                Show this help message");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]EXAMPLES:[/]");
        AnsiConsole.MarkupLine("    [grey]# Use with default API URLs[/]");
        AnsiConsole.MarkupLine("    conduit-tui --master-key YOUR_MASTER_KEY");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("    [grey]# Specify custom API URLs[/]");
        AnsiConsole.MarkupLine("    conduit-tui -k YOUR_MASTER_KEY -c http://api.example.com:5000 -a http://api.example.com:5002");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("    [grey]# Show the selected virtual key[/]");
        AnsiConsole.MarkupLine("    conduit-tui -k YOUR_MASTER_KEY --show-virtual-key");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]KEYBOARD SHORTCUTS:[/]");
        AnsiConsole.MarkupLine("    [green]F1[/]      Help / Keyboard Shortcuts");
        AnsiConsole.MarkupLine("    [green]F2[/]      Chat View");
        AnsiConsole.MarkupLine("    [green]F3[/]      Model Mappings");
        AnsiConsole.MarkupLine("    [green]F4[/]      Provider Credentials");
        AnsiConsole.MarkupLine("    [green]F5[/]      Image Generation");
        AnsiConsole.MarkupLine("    [green]F6[/]      Video Generation");
        AnsiConsole.MarkupLine("    [green]F7[/]      Virtual Keys");
        AnsiConsole.MarkupLine("    [green]F8[/]      System Health");
        AnsiConsole.MarkupLine("    [green]F9[/]      Configuration");
        AnsiConsole.MarkupLine("    [green]Ctrl+Q[/]  Quit Application");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]NOTES:[/]");
        AnsiConsole.MarkupLine("  • You must select a virtual key (F7) before using chat or generation features");
        AnsiConsole.MarkupLine("  • Real-time updates require SignalR connection to Core API");
        AnsiConsole.MarkupLine("  • Minimum terminal size: 80x24 characters");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("For more information, visit: [link]https://github.com/knnlabs/Conduit[/]");
    }
    
    static void DumpLogsToConsole(LogBuffer logBuffer)
    {
        var logs = logBuffer.GetEntries().ToList();
        if (logs.Count == 0)
            return;
            
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Session Logs[/]").LeftJustified());
        AnsiConsole.WriteLine();
        
        foreach (var log in logs)
        {
            var color = log.Level switch
            {
                LogLevel.Trace => "grey",
                LogLevel.Debug => "grey",
                LogLevel.Information => "white",
                LogLevel.Warning => "yellow",
                LogLevel.Error => "red",
                LogLevel.Critical => "red bold",
                _ => "white"
            };
            
            AnsiConsole.MarkupLine($"[{color}]{log.GetFormattedMessage().EscapeMarkup()}[/]");
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().LeftJustified());
    }
}
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace ConduitLLM.Tools.SignalRBackplaneTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("SignalR Redis Backplane Test Tool");
        Console.WriteLine("=================================\n");

        // Configuration
        var apiPorts = new[] { 5000, 5001, 5002 };
        var connections = new List<(int port, HubConnection connection)>();
        var messageLog = new List<(DateTime time, int port, string message)>();

        // Connect to all instances
        Console.WriteLine("Connecting to API instances...");
        foreach (var port in apiPorts)
        {
            try
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl($"http://localhost:{port}/hubs/navigation-state")
                    .WithAutomaticReconnect()
                    .Build();

                // Set up event handlers
                connection.On<object>("NavigationStateChanged", (state) =>
                {
                    var message = $"NavigationStateChanged: {System.Text.Json.JsonSerializer.Serialize(state)}";
                    messageLog.Add((DateTime.Now, port, message));
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Instance {port}: {message}");
                });

                connection.On<object>("ModelMappingChanged", (mapping) =>
                {
                    var message = $"ModelMappingChanged: {System.Text.Json.JsonSerializer.Serialize(mapping)}";
                    messageLog.Add((DateTime.Now, port, message));
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Instance {port}: {message}");
                });

                connection.On<object>("ProviderHealthChanged", (health) =>
                {
                    var message = $"ProviderHealthChanged: {System.Text.Json.JsonSerializer.Serialize(health)}";
                    messageLog.Add((DateTime.Now, port, message));
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Instance {port}: {message}");
                });

                connection.Closed += async (error) =>
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Instance {port}: Connection closed. Error: {error?.Message}");
                    await Task.Delay(new Random().Next(0, 5) * 1000);
                };

                // Connect
                await connection.StartAsync();
                connections.Add((port, connection));
                Console.WriteLine($"✓ Connected to instance on port {port}");

                // Request current state
                await connection.InvokeAsync("RequestCurrentState");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to connect to port {port}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nConnected to {connections.Count} instances");
        Console.WriteLine("\nMonitoring for SignalR messages. Press any key to run tests, or 'Q' to quit.\n");

        // Interactive test loop
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q)
                    break;

                await RunTests(connections, messageLog);
            }

            await Task.Delay(100);
        }

        // Disconnect all
        Console.WriteLine("\nDisconnecting...");
        foreach (var (port, connection) in connections)
        {
            await connection.DisposeAsync();
        }

        // Print summary
        PrintSummary(messageLog);
    }

    static async Task RunTests(List<(int port, HubConnection connection)> connections, List<(DateTime time, int port, string message)> messageLog)
    {
        Console.WriteLine("\n--- Running Tests ---");
        
        // Test 1: Message Propagation
        Console.WriteLine("\nTest 1: Message Propagation");
        Console.WriteLine("Trigger an event through Admin API and check if all instances receive it.");
        Console.WriteLine("(You need to manually trigger an event via Admin API or curl)");
        
        var countBefore = messageLog.Count;
        Console.WriteLine("Waiting 5 seconds for messages...");
        await Task.Delay(5000);
        
        var newMessages = messageLog.Skip(countBefore).ToList();
        if (newMessages.Any())
        {
            var uniquePorts = newMessages.Select(m => m.port).Distinct().Count();
            Console.WriteLine($"✓ Received {newMessages.Count} messages across {uniquePorts} instances");
            
            // Check if all instances received messages
            if (uniquePorts == connections.Count)
            {
                Console.WriteLine("✓ All instances received messages - Redis backplane is working!");
            }
            else
            {
                Console.WriteLine($"⚠ Only {uniquePorts}/{connections.Count} instances received messages");
            }
        }
        else
        {
            Console.WriteLine("✗ No messages received. Make sure to trigger an event via Admin API.");
        }

        // Test 2: Latency Measurement
        Console.WriteLine("\nTest 2: Message Latency");
        if (newMessages.Count >= 2)
        {
            var messageGroups = newMessages.GroupBy(m => m.message);
            foreach (var group in messageGroups)
            {
                var times = group.Select(g => g.time).OrderBy(t => t).ToList();
                if (times.Count > 1)
                {
                    var latency = (times.Last() - times.First()).TotalMilliseconds;
                    Console.WriteLine($"Message propagation latency: {latency:F2}ms");
                }
            }
        }

        // Test 3: Connection Status
        Console.WriteLine("\nTest 3: Connection Status");
        foreach (var (port, connection) in connections)
        {
            var state = connection.State;
            Console.WriteLine($"Instance {port}: {state}");
        }
    }

    static void PrintSummary(List<(DateTime time, int port, string message)> messageLog)
    {
        Console.WriteLine("\n--- Test Summary ---");
        Console.WriteLine($"Total messages received: {messageLog.Count}");
        
        var messagesByPort = messageLog.GroupBy(m => m.port);
        foreach (var portGroup in messagesByPort)
        {
            Console.WriteLine($"Instance {portGroup.Key}: {portGroup.Count()} messages");
        }

        if (messageLog.Any())
        {
            var uniqueMessages = messageLog.Select(m => m.message).Distinct().Count();
            Console.WriteLine($"Unique messages: {uniqueMessages}");
            
            // Calculate average propagation delay
            var messageGroups = messageLog.GroupBy(m => m.message);
            var delays = new List<double>();
            
            foreach (var group in messageGroups.Where(g => g.Count() > 1))
            {
                var times = group.Select(g => g.time).OrderBy(t => t).ToList();
                var delay = (times.Last() - times.First()).TotalMilliseconds;
                delays.Add(delay);
            }
            
            if (delays.Any())
            {
                Console.WriteLine($"Average propagation delay: {delays.Average():F2}ms");
                Console.WriteLine($"Max propagation delay: {delays.Max():F2}ms");
            }
        }
    }
}
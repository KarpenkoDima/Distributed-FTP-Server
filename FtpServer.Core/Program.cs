﻿using FtpServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FtpServer.Core.Services;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

// Step 1: Create a Host Builder using Host.CreateApplicationBuilder.
        // This method automatically sets up:
        // - Configuration: Loads appsettings.json, appsettings.{Environment}.json,
        //   environment variables, and command-line arguments (from 'args').
        //   It adds environment variables AFTER JSON files, ensuring they override.
        // - Logging: Console, Debug, EventSource
        // - Dependency Injection: Basic services like IConfiguration, IHostEnvironment
        var builder = Host.CreateApplicationBuilder(args);

        //  REMOVED: var appConfig = builder.Configuration.AddJsonFile("appsettings.json").Build();
        // This line was creating a separate IConfiguration instance that didn't include environment variables.
        // We will now rely on the IConfiguration instance built by the host.

        // Step 2: Configure services within the builder.
        // Add Redis connection
        builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            // Get the IConfiguration instance that includes all sources (including env vars)
            var config = provider.GetRequiredService<IConfiguration>();
            // Use a configurable connection string for Redis
            var connectionString = config["RedisConnectionString"] ?? "localhost:6379"; // Default fallback
            Console.WriteLine($"Connecting to Redis at: {connectionString}");
            return ConnectionMultiplexer.Connect(connectionString);
        });

        // Add HttpClient for Auth service
        builder.Services.AddHttpClient<AuthHttpClient>("AuthClient")
            .ConfigureHttpClient((provider, client) =>
            {
                // Get the IConfiguration instance that includes all sources
                var config = provider.GetRequiredService<IConfiguration>();
                // Use a configurable base address for the Auth service
                var authServiceUrl = config["authservice"] ?? "http://localhost:5160"; // Default fallback
                Console.WriteLine($"AuthHttpClient BaseAddress: {authServiceUrl}");
                client.BaseAddress = new Uri(authServiceUrl);
            });

        // Change MemorySessionManager to RedisSessionManager
        builder.Services.AddSingleton<ISessionManager, RedisSessionManager>();

        // Register your BasicFtpServer
        builder.Services.AddSingleton<BasicFtpServer>();

        // Step 3: Build the host. This finalizes the configuration and service collection.
        var host = builder.Build();

        // Step 4: Retrieve the IConfiguration instance from the built host's services.
        // THIS IS THE CORRECT appConfig: It now contains all loaded settings, including
        // overrides from environment variables.
        var appConfig = host.Services.GetRequiredService<IConfiguration>();
       
        // Step 5: Retrieve your FTP server instance from the service provider.
        var server = host.Services.GetRequiredService<BasicFtpServer>();

        // Step 6: Start the FTP server using the port obtained from the correct configuration.
        // appConfig["port"] will now correctly reflect the environment variable
        // if it was set, otherwise it will fall back to appsettings.json.
        await server.StartAsync(GetPortFromConfig(appConfig["port"]));

        Console.WriteLine("FTP Server Started. Press any key to stop ...");
        Console.ReadKey(); // Keep the console application running until a key is pressed
        server.Stop();

        // If your application is a long-running service that should not stop until
        // the host is explicitly shut down (e.g., via Ctrl+C), you might use:
        // await host.RunAsync();
        // and handle application shutdown via IHostApplicationLifetime.

static int GetPortFromConfig(string port)
{
    Console.WriteLine($"Config port {port}");
    // From configuration
    if (false == string.IsNullOrEmpty(port) && int.TryParse(port, out var configPort))
        return configPort;

    // Default
    return 2121;
}
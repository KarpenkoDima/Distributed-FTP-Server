using FtpServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FtpServer.Core.Services;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

var appConfig = builder.Configuration.AddJsonFile("appsettings.json").Build();

builder.Services.AddHttpClient<AuthHttpClient>("AuthClient")
    .ConfigureHttpClient(client => client.BaseAddress= new Uri(appConfig["authservice"]));

// Add SessionManager
builder.Services.AddSingleton<ISessionManager, MemorySessionManager>();

builder.Services.AddSingleton<BasicFtpServer>();

var host = builder.Build();

var server = host.Services.GetRequiredService<BasicFtpServer>();
await server.StartAsync(GetPortFromConfig(appConfig["port"]));

Console.WriteLine("FTP Server Started. Press any key to stop ...");
Console.ReadKey();
server.Stop();

static int GetPortFromConfig(string port)
{
    Console.WriteLine($"Config port {port}");
    // From configuration
    if (false == string.IsNullOrEmpty(port) && int.TryParse(port, out var configPort))
        return configPort;
    
    // Default
    return 2121;
}
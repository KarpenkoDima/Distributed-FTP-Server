using FtpServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using FtpServer.Core.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient<AuthHttpClient>("AuthClient")
    .ConfigureHttpClient(client => client.BaseAddress= new Uri("http://localhost:5160"));

builder.Services.AddSingleton<BasicFtpServer>();

var host = builder.Build();

var server = host.Services.GetRequiredService<BasicFtpServer>();
await server.StartAsync(2121);

Console.WriteLine("FTP Server Started. Press any key to stop ...");
Console.ReadKey();
server.Stop();
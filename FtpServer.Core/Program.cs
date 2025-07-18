using FtpServer.Core;

var server = new BasicFtpServer();
await server.StartAsync(2121);
server.Stop();
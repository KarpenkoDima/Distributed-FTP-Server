using FtpServer.Core;

BasicFtpServer ftpServer = new BasicFtpServer();
await ftpServer.StartAsync(2121);
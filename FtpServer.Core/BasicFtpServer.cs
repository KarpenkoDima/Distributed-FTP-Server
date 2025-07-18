using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FtpServer.Core
{
    internal class BasicFtpServer
    {
        private TcpListener _listener;
        private bool _isRunning;

        public async Task StartAsync(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"🚀 FTP Server started on port {port}");
            Console.WriteLine($"💡 Test with: telnet localhost {port}");

            while (_isRunning)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var clientEndPoint = tcpClient.Client.RemoteEndPoint?.ToString();
                    Console.WriteLine($"📞 New client connected: {clientEndPoint}");

                    // Handle each client - one Task
                    _ = Task.Run(() => HandleClientAsync(tcpClient));

                }
                catch (Exception ex)
                {

                    Console.WriteLine($"❌ Error accepting client: {ex.Message}");

                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient)
        {

        }
    }
}

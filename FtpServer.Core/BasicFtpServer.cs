using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

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
                    _ = Task.Run(() => HandleClientAsync(tcpClient, clientEndPoint));

                }
                catch (Exception ex)
                {

                    Console.WriteLine($"❌ Error accepting client: {ex.Message}");

                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, string? clientEndPoint)
        {
            try
            {
                using var networkStream = tcpClient.GetStream();
                using var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
                using var reader = new StreamReader(networkStream, Encoding.UTF8);

                // Send welcom message to cleint
                await SendResponseAsync(writer, "220",  "Welcome to Basic FTP Server v1.0");

                // Main cycle handle of commands
                await ProcessCommandAsync(reader, writer, clientEndPoint);

            }
            catch (Exception ex)
            {

                Console.WriteLine($"❌ Error handling client {clientEndPoint}: {ex.Message}");
            }
            finally
            {
                tcpClient.Close();
                Console.WriteLine($"📴 Client {clientEndPoint} disconnected");
            }
        }

        private async Task ProcessCommandAsync(StreamReader reader, StreamWriter writer, string clientEndPoint)
        {
            while (true)
            {
                try
                {
                    var command = await reader.ReadLineAsync();

                    if (command == null)
                    {
                        Console.WriteLine($"🔌 Client {clientEndPoint} disconnected");
                        break;
                    }

                    command = command.Trim();
                    if(string.IsNullOrEmpty(command))
                        continue;

                    Console.WriteLine($"📨 [{clientEndPoint}] Command: {command}");

                    // handle only one command QUIT
                    if (command.ToUpper() == "QUIT")
                    {
                        await SendResponseAsync(writer, "221", "Goodbye");
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        private async Task SendResponseAsync(StreamWriter writer, string code, string message)
        {
            var response = $"{code} {message}";
            await writer.WriteLineAsync(response);
            Console.WriteLine($"📤 Response: {response}");
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}

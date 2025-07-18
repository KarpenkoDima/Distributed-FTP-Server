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

                // Create ftp user
                var ftpUser = new FtpUser
                {                    
                    ClientEndPoint = clientEndPoint ?? "unknown",
                   
                };

                // Send welcom message to cleint
                await SendResponseAsync(writer, "220 Welcome to Basic FTP Server v1.0.\r\n Input \"USER demo\" or \"USER test\" and any password");

                // Main cycle handle of commands
                await ProcessCommandAsync(reader, writer, ftpUser);

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

        private async Task ProcessCommandAsync(StreamReader reader, StreamWriter writer, FtpUser ftpUser)
        {
            while (true)
            {
                try
                {
                    var command = await reader.ReadLineAsync();

                    if (command == null)
                    {
                        Console.WriteLine($"🔌 Client {ftpUser.ClientEndPoint} disconnected");
                        break;
                    }

                    command = command.Trim();
                    if (string.IsNullOrEmpty(command))
                        continue;

                    Console.WriteLine($"📨 [{ftpUser.ClientEndPoint}] Command: {command}");

                    var response = await HandleCommandAsync(command, ftpUser);
                    await SendResponseAsync(writer, response);

                    // Если клиент отправил QUIT, завершаем соединение
                    if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        private async Task<string> HandleCommandAsync(string commandLine, FtpUser ftpUser)
        {
            var parts = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToUpper();
            var args = parts.Length > 1 ? parts[1] : "";

            return command switch
            {
                "USER" => await HandleUserAsync(args, ftpUser),
                "PASS" => await HandlePassAsync(args, ftpUser),
                "QUIT" => "221 Goodbye",
                _ => $"502 Command '{command}' not implemented"
            };
        }

        private async Task<string> HandleUserAsync(string username, FtpUser ftpUser)
        {
            if (string.IsNullOrEmpty(username))
                return $"501 Username required";

            ftpUser.Username = username;
            Console.WriteLine($"👤 [{ftpUser.ClientEndPoint}] User: {username}");

            return $"331 Password required";
        }
        private async Task<string> HandlePassAsync(string password, FtpUser ftpUser)
        {
            if (string.IsNullOrEmpty(ftpUser.Username))
                return $"503 Login with USER first";

            // Простая проверка: любой пароль принимается для demo/test пользователей
            if (ftpUser.Username.Equals("demo", StringComparison.OrdinalIgnoreCase) ||
                ftpUser.Username.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                ftpUser.IsAuthenticated = true;
                

                Console.WriteLine($"✅ [{ftpUser.ClientEndPoint}] Authentication successful for {ftpUser.Username}");
                return $" 230 Login successful";
            }

            Console.WriteLine($"❌ [{ftpUser.ClientEndPoint}] Authentication failed for {ftpUser.Username}");
            return $"530 Login incorrect";
        }

        private async Task SendResponseAsync(StreamWriter writer, string response)
        {            
            await writer.WriteLineAsync(response);
            Console.WriteLine($"📤 Response: {response}");
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }

    public class FtpUser
    {
        public string ClientEndPoint { get; set; } = "";
        public string Username { get; set; } = "";
        public bool IsAuthenticated { get; set; }
    }
}

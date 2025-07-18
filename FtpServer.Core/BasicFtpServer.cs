using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
                var session = new FtpSession
                {
                    SessionId = Guid.NewGuid().ToString()[..8],
                    ClientEndpoint = clientEndPoint ?? "unknown",
                    NetworkStream = networkStream,
                    Reader = reader,
                    Writer = writer

                };

                // Send welcom message to cleint
                await SendResponseAsync(writer, "220", "Welcome to Basic FTP Server v1.0.\r\n Input \"USER demo\" or \"USER test\" and any password");

                // Main cycle handle of commands
                await ProcessCommandAsync(session);

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

        private async Task ProcessCommandAsync(FtpSession session)
        {
            while (true)
            {
                try
                {
                    var command = await session.Reader.ReadLineAsync();

                    if (command == null)
                    {
                        Console.WriteLine($"🔌 Client {session.ClientEndpoint} disconnected");
                        break;
                    }

                    command = command.Trim();
                    if (string.IsNullOrEmpty(command))
                        continue;

                    Console.WriteLine($"📨 [{session.SessionId}] Command: {command}");

                    var response = await HandleCommandAsync(command, session);
                    await SendResponseAsync(session.Writer, response.Code.ToString(), response.Message);

                    // Если клиент отправил QUIT, завершаем соединение
                    if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing command for {session.ClientEndpoint}: {ex.Message}");
                    break;
                }
            }
        }

        private async Task<FtpResponse> HandleCommandAsync(string commandLine, FtpSession session)
        {
            var parts = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToUpper();
            var args = parts.Length > 1 ? parts[1] : "";

            return command switch
            {
                "USER" => await HandleUserAsync(args, session),
                "PASS" => await HandlePassAsync(args, session),
                "QUIT" => new FtpResponse(221, $"Goodbye"),
                _ => new FtpResponse(502, $"Command '{command}' not implemented")
            };
        }

        private async Task<FtpResponse> HandleUserAsync(string username, FtpSession session)
        {
            if (string.IsNullOrEmpty(username))
                return new FtpResponse(501, $"Username required");

            session.Username = username;
            Console.WriteLine($"👤 [{session.SessionId}] User: {username}");

            return new FtpResponse(331, $"Password required");
        }
        private async Task<FtpResponse> HandlePassAsync(string password, FtpSession session)
        {
            if (string.IsNullOrEmpty(session.Username))
                return new FtpResponse(503, $"Login with USER first");

            // Простая проверка: любой пароль принимается для demo/test пользователей
            if (session.Username.Equals("demo", StringComparison.OrdinalIgnoreCase) ||
                session.Username.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                session.IsAuthenticated = true;
                session.CurrentDirectory = "/";

                Console.WriteLine($"✅ [{session.SessionId}] Authentication successful for {session.Username}");
                return new FtpResponse(230, $"Login successful");
            }

            Console.WriteLine($"❌ [{session.SessionId}] Authentication failed for {session.Username}");
            return new FtpResponse(530, $"Login incorrect");
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

    public class FtpSession
    {
        public string SessionId { get; set; } = "";
        public string ClientEndpoint { get; set; } = "";
        public string Username { get; set; } = "";
        public bool IsAuthenticated { get; set; }
        public string CurrentDirectory { get; set; } = "/";

        // Networking
        public NetworkStream NetworkStream { get; set; } = null!;
        public StreamReader Reader { get; set; } = null!;
        public StreamWriter Writer { get; set; } = null!;
    }

    public record FtpResponse(int Code, string Message);
}

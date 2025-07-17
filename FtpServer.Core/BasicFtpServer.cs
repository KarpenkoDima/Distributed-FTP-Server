// FtpServer.Core/Program.cs
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FtpServer.Core;

public class Program
{
    public static async Task Main(string[] args)
    {
        var server = new BasicFtpServer();
        await server.StartAsync(2121); // use port 2121 for development
    }
}

public class BasicFtpServer
{
    private TcpListener? _listener;
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
                var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString();

                Console.WriteLine($"📞 New client connected: {clientEndpoint}");

                // Handle any client in the Task
                _ = Task.Run(() => HandleClientAsync(tcpClient));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString();

        try
        {
            using var networkStream = client.GetStream();
            using var reader = new StreamReader(networkStream, Encoding.UTF8);
            using var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

            // Create session for the client
            var session = new FtpSession
            {
                SessionId = Guid.NewGuid().ToString()[..8],
                ClientEndPoint = clientEndpoint ?? "unknown",
                NetworkStream = networkStream,
                Reader = reader,
                Writer = writer
            };

            // Send Hello message
            await SendResponseAsync(writer, "220", "Welcome to Basic FTP Server v1.0");

            // Main cycle commands handler
            await ProcessCommandsAsync(session);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling client {clientEndpoint}: {ex.Message}");
        }
        finally
        {
            // session clear or close stream
            client.Close();
            Console.WriteLine($"📴 Client {clientEndpoint} disconnected");
        }
    }

    private async Task ProcessCommandsAsync(FtpSession session)
    {
        while (true)
        {
            try
            {
                var command = await session.Reader.ReadLineAsync();

                if (command == null)
                {
                    Console.WriteLine($"🔌 Client {session.ClientEndPoint} disconnected");
                    break;
                }

                command = command.Trim();
                if (string.IsNullOrEmpty(command))
                    continue;

                Console.WriteLine($"📨 [{session.SessionId}] Command: {command}");

                var response = await HandleCommandAsync(command, session);
                await SendResponseAsync(session.Writer, response.Code.ToString(), response.Message);

                // If the client send QUIT, close the session
                if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing command for {session.ClientEndPoint}: {ex.Message}");
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
            "PWD" => HandlePwd(session),
            "CWD" => HandleCwd(args, session),
            "SYST" => new FtpResponse(215, "UNIX Type: L8"),
            "FEAT" => HandleFeat(),
            "NOOP" => new FtpResponse(200, "OK"),
            "QUIT" => new FtpResponse(221, "Goodbye"),
            _ => new FtpResponse(502, $"Command '{command}' not implemented")
        };
    }

    private async Task<FtpResponse> HandleUserAsync(string username, FtpSession session)
    {
        if (string.IsNullOrEmpty(username))
            return new FtpResponse(501, "Username required");

        session.Username = username;
        Console.WriteLine($"👤 [{session.SessionId}] User: {username}");

        return new FtpResponse(331, "Password required");
    }

    private async Task<FtpResponse> HandlePassAsync(string password, FtpSession session)
    {
        if (string.IsNullOrEmpty(session.Username))
            return new FtpResponse(503, "Login with USER first");

        // Simple verify: any password for users demo/test
        if (session.Username.Equals("demo", StringComparison.OrdinalIgnoreCase) ||
            session.Username.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            session.IsAuthenticated = true;
            session.CurrentDirectory = "/";

            Console.WriteLine($"✅ [{session.SessionId}] Authentication successful for {session.Username}");
            return new FtpResponse(230, "Login successful");
        }

        Console.WriteLine($"❌ [{session.SessionId}] Authentication failed for {session.Username}");
        return new FtpResponse(530, "Login incorrect");
    }

    private FtpResponse HandlePwd(FtpSession session)
    {
        if (!session.IsAuthenticated)
            return new FtpResponse(530, "Not logged in");

        return new FtpResponse(257, $"\"{session.CurrentDirectory}\" is current directory");
    }

    private FtpResponse HandleCwd(string path, FtpSession session)
    {
        if (!session.IsAuthenticated)
            return new FtpResponse(530, "Not logged in");

        if (string.IsNullOrEmpty(path))
            return new FtpResponse(501, "Path required");

        // Simple navigation - set any path
        session.CurrentDirectory = path.StartsWith('/') ? path : $"{session.CurrentDirectory.TrimEnd('/')}/{path}";

        Console.WriteLine($"📁 [{session.SessionId}] Changed directory to: {session.CurrentDirectory}");
        return new FtpResponse(250, "Directory changed successfully");
    }

    private FtpResponse HandleFeat()
    {
        return new FtpResponse(211, "Features:\r\n UTF8\r\n211 End");
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

// Models


public record FtpResponse(int Code, string Message);
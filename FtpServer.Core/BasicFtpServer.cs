// FtpServer.Core/Program.cs
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    private string _rootDirectory;

    public BasicFtpServer()
    {
        // Create root directory for FTP server
        _rootDirectory = Path.Combine(Environment.CurrentDirectory, "ftp_root");
        Directory.CreateDirectory(_rootDirectory);

        // Createing guest directories and files
        InitializeTestFiles();
    }

    private void InitializeTestFiles()
    {
        var uploadsDir = Path.Combine(_rootDirectory, "uploads");
        var downloadsDir = Path.Combine(_rootDirectory, "downloads");

        Directory.CreateDirectory(uploadsDir);
        Directory.CreateDirectory(downloadsDir);

        // Create test file
        var testFile = Path.Combine(downloadsDir, "readme.txt");
        if (false == File.Exists(testFile))
        {
            File.WriteAllText(testFile, "Welcome to FTP Server!\nThis is a test file for download.\n");
        }

        var testFile2 = Path.Combine(downloadsDir, "sample.txt");
        if (false == File.Exists(testFile2))
        {
            File.WriteAllText(testFile2, "Sample file content\nLine 2]\nLine 3");
        }

        Console.WriteLine($"📁 FTP Root directory: {_rootDirectory}");
        Console.WriteLine($"📁 Upload to: {uploadsDir}");
        Console.WriteLine($"📁 Download from: {downloadsDir}");
    }

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
            using var reader = new StreamReader(networkStream, new UTF8Encoding(false));
            using var writer = new StreamWriter(networkStream, new UTF8Encoding(false)) { AutoFlush = true };

            // Create session for the client
            var session = new FtpSession
            {
                SessionId = Guid.NewGuid().ToString()[..8],
                ClientEndPoint = clientEndpoint ?? "unknown",
                NetworkStream = networkStream,
                Reader = reader,
                Writer = writer,
                // 
                CurrentDirectory = "/",
                RootDirectory = _rootDirectory
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

            "TYPE" => HandleType(args, session),
            "PASV" => await HandlePasvAsync(session),
            "LIST" => await HandleListAsync(session),
            /*"STOR" => await HandleStorAsync(args, session),
            "RETR" => await HandleRetrAsync(args, session),
            "SIZE" => HandleSize(session),*/

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

    private FtpResponse HandleType(string type, FtpSession session)
    {
        if (!session.IsAuthenticated)
            return new FtpResponse(530, "Not logged in");

        session.TransferType = type.ToUpper() switch
        {
            "A" => TransferType.ASCII,
            "I" => TransferType.Binary,
            _ => session.TransferType
        };

        return new FtpResponse(200, $"Type set to {session.TransferType}");
    }

    private async Task<FtpResponse> HandlePasvAsync(FtpSession session)
    {
        if (false == session.IsAuthenticated)
        {
            return new FtpResponse(530, "Not Logged in");
        }

        try
        {
            // Close previous data vonnection if open
            session.DataListener?.Stop();

            // Create new listener on any port
            session.DataListener = new TcpListener(IPAddress.Any, 0);
            session.DataListener.Start();

            var endPoint = (IPEndPoint)session.DataListener.LocalEndpoint;
            var port = endPoint.Port;

            // Get Server IP address (simple - localhost)
            var serverIP = IPAddress.Loopback;
            var ipBytes = serverIP.GetAddressBytes();

            // FTP PASV response format: (h1,h2,h3,h4,p1,p2)
            // where IP = h1.h2.h3.h4, port = p1*256 + p2
            var p1 = port / 256;
            var p2 = port % 256;

            var pasvResponse = $"227 Entering Passive Mode ({ipBytes[0]},{ipBytes[1]},{ipBytes[2]},{ipBytes[3]},{p1},{p2})";

            Console.WriteLine($"📡 [{session.SessionId}] Data connection prepared on port {port}");
            return new FtpResponse(227, pasvResponse.Substring(4)); // Remove response code
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error setting up passive mode: {ex.Message}");
            return new FtpResponse(425, "Can't open data connection");
        }
    }

    private async Task<FtpResponse> HandleListAsync(FtpSession session)
    {
        if (false == session.IsAuthenticated)
            return new FtpResponse(530, "Not logged in");

        if (session.DataListener == null)
            return new FtpResponse(425, "Use PASV first");

        try
        {
            var physicalPath = MapVirtualToPhysical(session.CurrentDirectory, session.RootDirectory);
            

            // Send response that beginning to send
            await SendResponseAsync(session.Writer, "150", "Opening data connection for directory list");
            Console.WriteLine($"📡 [{session.SessionId}] Waiting for data connection on port {((IPEndPoint)session.DataListener.LocalEndpoint).Port} for LIST command...");
            // connection acceptance
            var dataClient =  await session.DataListener.AcceptTcpClientAsync();
            Console.WriteLine($"📡 [{session.SessionId}] Data connection established for LIST");
            using (dataClient)
            {
                using (var dataStream = dataClient.GetStream())
                using (var dataWriter = new StreamWriter(dataStream, new UTF8Encoding(false)) { AutoFlush = true })
                {
                    // to listing directory
                    var entries = Directory.GetFileSystemEntries(physicalPath);

                    foreach (var entry in entries)
                    {
                        var info = new FileInfo(entry);
                        var isDirectory = Directory.Exists(entry);
                        var name = Path.GetFileName(entry);
                        var size = isDirectory ? 0 : info.Length;
                        var modified = info.LastWriteTime.ToString("MMM dd HH:mm");

                        // Simple UNIX-style listing
                        var permissions = isDirectory ? "drwxr-xr-x" : "-rw-r--r--";
                        var listLine = $"{permissions} 1 user group {size,10} {modified} {name}";

                        await dataWriter.WriteLineAsync(listLine);
                        Console.WriteLine($"📄 [{session.SessionId}] Listed: {name} ({(isDirectory ? "DIR" : size + " bytes")})");

                    }
                }
            }
            return new FtpResponse(226, "Directory listing completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in LIST command: {ex.Message}");
            return new FtpResponse(550, "Failed to list directory");
        }
        finally
        {
            session.DataListener?.Stop();
            session.DataListener = null;
        }
    }
    private async Task SendResponseAsync(StreamWriter writer, string code, string message)
    {
        var response = $"{code} {message}";
        await writer.WriteLineAsync(response);
        Console.WriteLine($"📤 Response: {response}");
    }

    private string MapVirtualToPhysical(string virtualPath, string rootDir)
    {
        var relativePath = virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(rootDir, relativePath);
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
    }
}

// Models


public record FtpResponse(int Code, string Message);
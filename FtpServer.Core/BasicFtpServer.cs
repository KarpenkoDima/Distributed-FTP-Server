using FtpServer.Core.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        private string _rootDirectory;

        // AuthService
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ISessionManager _sessionManager;
        private readonly IConfiguration _configuration;
        public BasicFtpServer(IHttpClientFactory httpClientFactory, 
            ISessionManager sessionManager,
            IConfiguration configuration)
        {
            // Create root directory for FTP server
            //_rootDirectory = Path.Combine(Environment.CurrentDirectory, "ftp_root");
            //Directory.CreateDirectory(_rootDirectory);

            

            _httpClientFactory = httpClientFactory;
            _sessionManager = sessionManager;
            _configuration = configuration;
            
            // Createing guest directories and files
            InitializeTestFiles();
        }

        public async Task<bool> GetUserByusernameAsync(string username)
        {
            // Create HttpClient from factory
            var httpClient = _httpClientFactory.CreateClient("AuthClient");
            // Use it for create our AuthHttpClient
            var authHttpClient = new AuthHttpClient(httpClient);

            var authResult = await authHttpClient.GetUserByUsernameAsync(username);
            return authResult.IsSuccess;
        }

        public async Task<bool> AuthenticateUserAsync(string username, string pasword)
        {
            // Create HttpClient from factory
            var httpClient = _httpClientFactory.CreateClient("AuthClient");
            // Use it for create our AuthHttpClient
            var authHttpClient = new AuthHttpClient(httpClient);

            var authResult = await authHttpClient.AuthenticateAsync(username, pasword);
            return authResult.IsSuccess;
        }

        private void InitializeTestFiles()
        {
            // NOW: use SHARED storage 
            var sharedStorageRoot = _configuration.GetValue<string>("SharedStoragePath", "/app/shared_storage");
            _rootDirectory = sharedStorageRoot;
            Console.WriteLine($"📁 Using SHARED storage: {_rootDirectory}");

            // Create directories for each user
            CreateUserDirectory("demo");
            CreateUserDirectory("admin");
            CreateUserDirectory("test");

            Console.WriteLine($"📁 FTP Root directory: {_rootDirectory}");
        }

        private void CreateUserDirectory(string username) 
        {
            var userDir = Path.Combine(_rootDirectory, username);
            var uploadsDir = Path.Combine(userDir, "uploads");
            var downloadsDir = Path.Combine(userDir, "downloads");

            Directory.CreateDirectory(userDir);
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
            FtpSession session = null;
            try
            {
                using var networkStream = tcpClient.GetStream();
                using var writer = new StreamWriter(networkStream, new UTF8Encoding(false)) { AutoFlush = true };
                using var reader = new StreamReader(networkStream, new UTF8Encoding(false));

                // Create ftp user
                 session = new FtpSession
                {
                    SessionId = Guid.NewGuid().ToString()[..8],
                    ClientEndpoint = clientEndPoint ?? "unknown",
                    NetworkStream = networkStream,
                    Reader = reader,
                    Writer = writer,
                    // 
                    CurrentDirectory = "/",
                    RootDirectory = _rootDirectory

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
                if (session != null && false == string.IsNullOrEmpty(session.SessionId))
                {
                    await _sessionManager.RemoveSesionAsync(session.SessionId);
                }
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
                    Console.WriteLine($"🔍 [{session.SessionId}] Waiting for next command...");
                    var command = await session.Reader.ReadLineAsync();

                    if (command == null)
                    {
                        Console.WriteLine($"🔌 [{session.SessionId}] Client {session.ClientEndpoint} disconnected (ReadLine returned null)");
                        break;
                    }

                    command = command.Trim();
                    if (string.IsNullOrEmpty(command))
                        continue;

                    Console.WriteLine($"📨 [{session.SessionId}] Command: {command}");

                    var response = await HandleCommandAsync(command, session);
                    await SendResponseAsync(session.Writer, response.Code.ToString(), response.Message);

                    // If client send QUIT, than close session
                    if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing command for {session.ClientEndpoint}: {ex.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}"); // Detail error
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
                "TYPE" => HandleType(args, session),
                "PASV" => await HandlePasvAsync(session),
                "LIST" => await HandleListAsync(session),               
                "QUIT" => new FtpResponse(221, $"Goodbye"),
                "STOR" => await HandleStorAsync(args, session),
                "RETR" => await HandleRetrAsync(args, session),
                "SIZE" => HandleSize(args, session),
                _ => new FtpResponse(502, $"Command '{command}' not implemented")
            };
        }

        private async Task<FtpResponse> HandleUserAsync(string username, FtpSession session)
        {
            if (string.IsNullOrEmpty(username))
                return new FtpResponse(501, $"Username required");
            bool res = await GetUserByusernameAsync(username);
            if (false == res)
                return new FtpResponse(530, $"Username incorrect");

            session.Username = username;
            Console.WriteLine($"👤 [{session.SessionId}] User: {username}");

            return new FtpResponse(331, $"Password required");
        }

        private async Task<FtpResponse> HandlePassAsync(string password, FtpSession session)
        {
            if (string.IsNullOrEmpty(session.Username))
                return new FtpResponse(503, $"Login with USER first");

            // Простая проверка: любой пароль принимается для demo/test пользователей
            bool result = await AuthenticateUserAsync(session.Username, password);
            if (result)
            {
                session.IsAuthenticated = true;

                // User virtual inside in root '/'                
                session.CurrentDirectory = "/";

                // But physically, their root is their folder
                session.RootDirectory = Path.Combine(_rootDirectory, session.Username);
                session.UserHomeDirectory = "/"; // For user root - their home

                // Save session in SessionManager (Redis in future)
                await _sessionManager.SaveSessionAsync(session);

                Console.WriteLine($"✅ [{session.SessionId}] Authentication successful for {session.Username}");
                Console.WriteLine($"🏠 [{session.SessionId}] User home: {session.UserHomeDirectory}");
                return new FtpResponse(230, $"Login successful");
            }

            Console.WriteLine($"❌ [{session.SessionId}] Authentication failed for {session.Username}");
            return new FtpResponse(530, $"Login incorrect");
        }

        private FtpResponse HandlePwd(FtpSession session)
        {
            if (!session.IsAuthenticated)
                return new FtpResponse(530, "Not logged in");

            return new FtpResponse(257, $"\"{session.CurrentDirectory}\" is current directory");
        }

        private FtpResponse HandleCwd(string path, FtpSession session)
        {
            if (false == session.IsAuthenticated)
                return new FtpResponse(530, "Not logged in");

            if (string.IsNullOrEmpty(path))
                return new FtpResponse(501, "Path required");

            var newPath = GetAbsolutePath(path, session.CurrentDirectory);

            // Security: Check that the user does not go outside their folder
            if (false == IsPathWithUserName(newPath, session.UserHomeDirectory))
            {
                Console.WriteLine($"🚫 [{session.SessionId}] Access denied: {newPath} (outside user home)");
                return new FtpResponse(530, "Access denied: Directory outside user space");
            }

            try
            {
                var physicalPath = MapVirtualToPhysical(newPath, session.RootDirectory);

                if (false == Directory.Exists(physicalPath))
                {
                    return new FtpResponse(550, "Directory not found");
                }

                session.CurrentDirectory = newPath;
                Console.WriteLine($"📁 [{session.SessionId}] Changed directory to: {session.CurrentDirectory}");
                return new FtpResponse(250, "Directory changed successfully");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"🚫 [{session.SessionId}] Security violation: {ex.Message}");
                return new FtpResponse(550, "Access denied");
            }
        }
        // For security
        private bool IsPathWithUserName(string requestedPath, string userHome)
        {
            // The user is isolated in their folder — they cannot go above “/”.      
            var normalizedPath = userHome.Replace("\\", "/").Trim('/');

            // We prohibit going above the root.
            return true;
        }

        private FtpResponse HandleFeat()
        {
            return new FtpResponse(211, "Features:\r\n UTF8\r\n211 End");
        }

        private FtpResponse HandleType(string type, FtpSession session)
        {
            if (false == session.IsAuthenticated)
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

                // Get configuration for PROXY mode
                var isBehindProxy = _configuration.GetValue<bool>("FtpBehindProxy", false);
                var externalIp = _configuration["FtpExternalIp"] ?? "127.0.0.1";
                var minPort = _configuration.GetValue<int>("FtpPasvMinPort", 50000);
                var maxPort = _configuration.GetValue<int>("FtpPasvMaxPort", 50010);

                Console.WriteLine($"🔧 [{session.SessionId}] PASV Config: Proxy={isBehindProxy}, IP={externalIp}, Range={minPort}-{maxPort}");

                int dataPort;               

                if (isBehindProxy)
                {
                    // in proxy mode use to port from range ports
                    dataPort = GetAvailablePortRange(minPort, maxPort);
                    Console.WriteLine($"🔄 [{session.SessionId}] Proxy mode: using port {dataPort} from range {minPort}-{maxPort}");
                }
                else
                {
                    // in direct mode use port from range
                    dataPort = GetAvailablePortRange(minPort, maxPort);
                    Console.WriteLine($"🔄 [{session.SessionId}] Direct mode: using port {dataPort} from range {minPort}-{maxPort}");
                }

                session.DataListener = new TcpListener(IPAddress.Any, dataPort);
                session.DataListener.Start();

                var endPoint = (IPEndPoint)session.DataListener.LocalEndpoint;
                var actualListenerPort = endPoint.Port;

                if (actualListenerPort != dataPort)
                {
                    Console.WriteLine($"❌ [{session.SessionId}] Port mismatch! Requested {dataPort}, got {actualListenerPort}");
                    session.DataListener.Stop();
                    return new FtpResponse(425, "Port allocated failed");
                }

                // Parsing external IP for PASV response
                if (false == IPAddress.TryParse(externalIp, out var parsedIp))
                {
                    Console.WriteLine($"⚠️ Invalid external IP: {externalIp}, using localhost");
                    parsedIp = IPAddress.Loopback;
                }

                // FTP PASV response format: (h1,h2,h3,h4,p1,p2)
                // where IP = h1.h2.h3.h4, port = p1*256 + p2
                var p1 = actualListenerPort / 256;
                var p2 = actualListenerPort % 256;
                var ipBytes = parsedIp.GetAddressBytes();

                var pasvResponse = $"227 Entering Passive Mode ({ipBytes[0]},{ipBytes[1]},{ipBytes[2]},{ipBytes[3]},{p1},{p2})";

                Console.WriteLine($"📡 [{session.SessionId}] Data connection prepared on port {actualListenerPort}");
                Console.WriteLine($"📡 [{session.SessionId}] nginx will proxy this to our internal port {actualListenerPort}");

                if (isBehindProxy)
                {
                    Console.WriteLine($"🔀 [{session.SessionId}] Nginx will proxy external:{actualListenerPort} -> internal:{actualListenerPort}");
                }
                else
                {
                    Console.WriteLine($"🔗 [{session.SessionId}] Direct connection expected on port {actualListenerPort}");
                }
                return new FtpResponse(227, pasvResponse.Substring(4)); // Remove response code
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error setting up passive mode: {ex.Message}");
                Console.WriteLine($"[{session.SessionId}] Stack trace {ex.StackTrace}");
                return new FtpResponse(425, "Can't open data connection");
            }
        }
        private int GetAvailablePortRange(int minPort, int maxPort)
        {
            Console.WriteLine($"🔍 Looking for available port in range {minPort}-{maxPort}");

            var busyPorts = GetBusyPorstsInRange(minPort, maxPort);

            if (busyPorts.Any())
            {
                Console.WriteLine($"📊 Busy ports in range: {string.Join(", ", busyPorts.OrderBy(p => p))}");
            }

            for (int port = minPort; port <= maxPort; port++)
            {
                if (false == busyPorts.Contains(port))
                {
                    try
                    {

                    
                    var testTcpListener = new TcpListener(IPAddress.Any, port);
                    testTcpListener.Start();
                    testTcpListener.Stop();
                    Console.WriteLine($"✅ Found available port: {port} (sequential search)");
                    return port;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Port {port} is busy");
                    }
                }
            }

            /*
             * We can go through the port range and create a TCP connection on each of them to find out if the port is free.
             */

            throw new InvalidOperationException($"No availale ports found in rrange {minPort} - {maxPort}");
        }

            private HashSet<int> GetBusyPorstsInRange(int minPort, int maxPort)
        {
            var busyPorts = new HashSet<int>();

            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

                // TCP listener
                foreach (var endPoint in ipGlobalProperties.GetActiveTcpListeners())
                {
                    if (endPoint.Port >=minPort && endPoint.Port <= maxPort)
                    {
                        busyPorts.Add(endPoint.Port);
                        Console.WriteLine($"🔍 Found busy TCP listener: {endPoint.Port}");
                    }
                }

                // TCP connections 
                foreach (var conn in ipGlobalProperties.GetActiveTcpConnections())
                {
                    if (conn.LocalEndPoint.Port >= minPort && conn.LocalEndPoint.Port <= maxPort)
                    {
                        busyPorts.Add(conn.LocalEndPoint.Port);
                        Console.WriteLine($"🔍 Found busy TCP connection: {conn.LocalEndPoint.Port}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not get system port information: {ex.Message}");
            }
            return busyPorts;
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
                
                // Add timeout for Data Connection
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    // connection acceptance
                    var dataClient = await session.DataListener.AcceptTcpClientAsync();
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
                                //var modified = info.LastWriteTime.ToString("MMM dd HH:mm");

                                // Simple UNIX-style listing
                                var permissions = isDirectory ? "drwxr-xr-x" : "-rw-r--r--";
                                var listLine = $"{permissions} 1 user group {size,10} {name}";

                                await dataWriter.WriteLineAsync(listLine);
                                Console.WriteLine($"📄 [{session.SessionId}] Listed: {name} ({(isDirectory ? "DIR" : size + " bytes")})");

                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"⏰ [{session.SessionId}] Data connection timeout for LIST");
                    return new FtpResponse(425, "Data connection timeout");
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
                // Safe close Data Listener
                try
                {
                    session.DataListener?.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error stopping DataListener: {ex.Message}");
                }

                session.DataListener = null;
            }
        }

        private async Task<FtpResponse> HandleStorAsync(string filename, FtpSession session)
        {
            if (false == session.IsAuthenticated)
                return new FtpResponse(530, "Not logged in");

            if (string.IsNullOrEmpty(filename))
                return new FtpResponse(501, "Filename required");

            if (session.DataListener == null)
                return new FtpResponse(425, "Use PASV first");

            try
            {
                var physicalPath = MapVirtualToPhysical(session.CurrentDirectory, session.RootDirectory);
                var filepath = Path.Combine(physicalPath, filename);

                await SendResponseAsync(session.Writer, "150", $"Opening data connection for {filename}");

                
                // Add timeout for Data Connection
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    // request data connection
                    var dataClient = await session.DataListener.AcceptTcpClientAsync();

                    Console.WriteLine($"📡 [{session.SessionId}] Data connection established for STOR {filename}");

                    using (dataClient)
                    using (var dataStream = dataClient.GetStream())
                    using (var fileStream = File.Create(filepath))
                    {
                        await dataStream.CopyToAsync(fileStream);
                        Console.WriteLine($"💾 [{session.SessionId}] File uploaded: {filename} ({fileStream.Length} bytes)");
                    }

                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"⏰ [{session.SessionId}] Data connection timeout for LIST");
                    return new FtpResponse(425, "Data connection timeout");
                }
                return new FtpResponse(226, "Transfer completed");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in STOR command: {ex.Message}");
                return new FtpResponse(550, "Failed to store file");
            }
            finally
            {
                // Safe close Data Listener
                try
                {
                    session.DataListener?.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error stopping DataListener: {ex.Message}");
                }

                session.DataListener = null;
            }
        }

        private async Task<FtpResponse> HandleRetrAsync(string filename, FtpSession session)
        {
            if (false == session.IsAuthenticated)
                return new FtpResponse(530, "Not logged in");

            if (string.IsNullOrEmpty(filename))
                return new FtpResponse(501, "Filename required");

            if (session.DataListener == null)
                return new FtpResponse(425, "Use PASV first");

            try
            {
                var physicalPath = MapVirtualToPhysical(session.CurrentDirectory, session.RootDirectory);
                var filepath = Path.Combine(physicalPath, filename);

                if (false == File.Exists(filepath))
                    return new FtpResponse(550, "File not found");

                await SendResponseAsync(session.Writer, "150", $"Opening data connection for {filename}");

                // Add timeout for Data Connection
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    // request data connection
                    var dataClient = await session.DataListener.AcceptTcpClientAsync();
                    Console.WriteLine($"📡 [{session.SessionId}] Data connection established for RETR {filename}");

                    using (dataClient)
                    using (var dataStream = dataClient.GetStream())
                    using (var fileStream = File.OpenRead(filepath))
                    {
                        await fileStream.CopyToAsync(dataStream);
                        Console.WriteLine($"💾 [{session.SessionId}] File uploaded: {filename} ({fileStream.Length} bytes)");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"⏰ [{session.SessionId}] Data connection timeout for LIST");
                    return new FtpResponse(425, "Data connection timeout");
                }
                return new FtpResponse(226, "Transfer completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in RETR command: {ex.Message}");
                return new FtpResponse(550, "Failed to retrieve file");
            }
            finally
            {
                // Safe close Data Listener
                try
                {
                    session.DataListener?.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error stopping DataListener: {ex.Message}");
                }
                
                session.DataListener = null;
            }
        }

        private FtpResponse HandleSize(string filename, FtpSession session)
        {
            if (false == session.IsAuthenticated)
                return new FtpResponse(530, "Not logged in");

            if (string.IsNullOrEmpty(filename))
                return new FtpResponse(501, "Filename required");

            try
            {
                var physicalPath = MapVirtualToPhysical(session.CurrentDirectory, session.RootDirectory);
                var filePath = Path.Combine(physicalPath, filename);

                if (false == File.Exists(filePath))
                    return new FtpResponse(550, "File not found");

                var fileInfo = new FileInfo(filePath);
                return new FtpResponse(213, "File not found");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in SIZE command: {ex.Message}");
                return new FtpResponse(550, "Failed to get file size");
            }

        }

        private string MapVirtualToPhysical(string virtualPath, string rootDir)
        {
            // Normalized virtual path
            var normalizedPath = virtualPath.Replace("\\", "/").Trim('/');

            // SECURITY: Forbiden absolute path
            if(normalizedPath.Contains(":"))
            {
                Console.WriteLine($"🚫 Blocked absolute path in mapping: {virtualPath}");
                throw new UnauthorizedAccessException($"Absolute paths not allowed: {virtualPath}");
            }

            // SECURITY: We prohibit exiting via ../..
            if(normalizedPath.Contains(".."))
            {
                Console.WriteLine($"🚫 Blocked parent directory in mapping: {virtualPath}");
                throw new UnauthorizedAccessException ($"Parent directory access not allowed: {virtualPath}");
            }

            // Create safe relative path
            var relativePath = normalizedPath.Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(rootDir, relativePath);

            // ADDITIONAL CHECK: the physical path must be inside rootDir
            var fullPhysicalPath = Path.GetFullPath(physicalPath);
            var fullRootDir = Path.GetFullPath(rootDir);

            if(false ==  fullPhysicalPath.StartsWith(fullRootDir, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"🚫 Path escape attempt: {fullPhysicalPath} outside {fullRootDir}");
                throw new UnauthorizedAccessException($"pATH OUTSIDE USER DIRECTORY {virtualPath}");
            }

            Console.WriteLine($"🔍 Path mapping: '{virtualPath}' → '{fullPhysicalPath}'");
            return fullPhysicalPath;
        }

        private string GetAbsolutePath(string path, string currentDir)
        {
            if (path.StartsWith('/'))
            {
                return path;
            }

            if (path == "..")
            {
                var parts = currentDir.TrimEnd('/').Split('/');
                return parts.Length > 1 ? string.Join("/", parts[..^1]) : "/";
            }

            return Path.Combine(currentDir, path).Replace('\\', '/');
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
        public string RootDirectory { get; set; } = "";
        public string UserHomeDirectory { get; set; } = "/";
        public TransferType TransferType { get; set; }

        // Networking
        public NetworkStream NetworkStream { get; set; } = null!;
        public StreamReader Reader { get; set; } = null!;
        public StreamWriter Writer { get; set; } = null!;
        // data Connection
        public TcpListener? DataListener { get; set; }
    }

    public enum TransferType
    {
        ASCII,
        Binary
    }

    public record FtpResponse(int Code, string Message);
}

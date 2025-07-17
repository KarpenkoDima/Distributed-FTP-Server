
using System.Net.Sockets;

namespace FtpServer.Core;

// Models
internal class FtpSession
{
    public string SessionId { get; set; } = "";
    public string SessionName { get; set; } = "";
    public string ClientEndPoint { get; set; } = "";
    public string Username { get; set; } = "";
    public string CurrentDirectory { get; set; } = "";
    public string RootDirectory { get; set; } = "";
    public bool IsAuthenticated { get; set; }

     // Networking
    public NetworkStream NetworkStream { get; set; } = null;
    public StreamReader Reader { get; set; } = null;
    public StreamWriter Writer { get; set; } = null;

    public TransferType TransferType { get; set; }

    // data Connection
    public TcpListener? DataListener { get; set; }
}

public enum TransferType
{
    ASCII,
    Binary
}
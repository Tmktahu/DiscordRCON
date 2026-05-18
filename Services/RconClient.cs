using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DiscordRcon.Services;

public class RconClient : IDisposable
{
  TcpClient _tcp;
  NetworkStream _stream;
  int _requestId;

  const int SERVERDATA_AUTH = 3;
  const int SERVERDATA_AUTH_RESPONSE = 2;
  const int SERVERDATA_EXECCOMMAND = 2;
  const int SERVERDATA_RESPONSE_VALUE = 0;

  public async Task ConnectAsync(string host, int port, string password, int timeoutMs = 5000)
  {
    _tcp = new TcpClient();
    var endpoints = await Dns.GetHostAddressesAsync(host);
    if (endpoints.Length == 0) throw new Exception($"Could not resolve RCON host: {host}");

    using var cts = new CancellationTokenSource(timeoutMs);
    await _tcp.ConnectAsync(endpoints[0], port, cts.Token);
    _stream = _tcp.GetStream();

    SendPacket(NextRequestId(), SERVERDATA_AUTH, password);

    while (true)
    {
      var (id, type, _) = await ReadPacketAsync();
      if (type == SERVERDATA_AUTH_RESPONSE)
      {
        if (id == -1) throw new Exception("RCON authentication failed");
        break;
      }
    }
  }

  public async Task<string> SendCommandAsync(string command, int timeoutMs = 10000)
  {
    SendPacket(NextRequestId(), SERVERDATA_EXECCOMMAND, command);

    using var cts = new CancellationTokenSource(timeoutMs);

    while (true)
    {
      var (_, type, body) = await ReadPacketAsync(cts.Token);
      if (type == SERVERDATA_RESPONSE_VALUE)
      {
        return body;
      }
    }
  }

  int NextRequestId() => Interlocked.Increment(ref _requestId);

  void SendPacket(int id, int type, string body)
  {
    var bodyBytes = Encoding.UTF8.GetBytes(body);
    var size = 4 + 4 + bodyBytes.Length + 2;
    var packet = new byte[4 + size];

    BitConverter.TryWriteBytes(packet.AsSpan(0), size);
    BitConverter.TryWriteBytes(packet.AsSpan(4), id);
    BitConverter.TryWriteBytes(packet.AsSpan(8), type);
    bodyBytes.CopyTo(packet, 12);

    _stream.Write(packet, 0, packet.Length);
  }

  async Task<(int id, int type, string body)> ReadPacketAsync(CancellationToken ct = default)
  {
    var sizeBuf = new byte[4];
    await ReadExactAsync(sizeBuf, ct);
    var size = BitConverter.ToInt32(sizeBuf, 0);

    if (size < 10 || size > 65536)
      throw new Exception($"Invalid RCON packet size: {size}");

    var data = new byte[size];
    await ReadExactAsync(data, ct);

    var id = BitConverter.ToInt32(data, 0);
    var type = BitConverter.ToInt32(data, 4);
    var bodyLen = size - 4 - 4 - 2;
    var body = bodyLen > 0 ? Encoding.UTF8.GetString(data, 8, bodyLen) : "";

    return (id, type, body);
  }

  async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
  {
    int offset = 0;
    while (offset < buffer.Length)
    {
      int read = await _stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
      if (read == 0) throw new Exception("RCON connection closed");
      offset += read;
    }
  }

  public void Dispose()
  {
    _stream?.Dispose();
    _tcp?.Dispose();
  }
}

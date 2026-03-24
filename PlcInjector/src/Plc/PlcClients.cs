using System.Net.Sockets;
using PlcInjector.Models;

namespace PlcInjector.Plc;

public interface IPlcClient : IDisposable
{
    Task<int>    ReadWordAsync(string address);
    Task<int[]>  ReadWordsAsync(string address, int count);
    Task         WriteWordAsync(string address, int value);
    Task         ConnectAsync();
    bool         IsConnected { get; }
}

// ── Mock ───────────────────────────────────────────────────────────────────────
public class MockPlcClient : IPlcClient
{
    private int  _v    = 1000;
    private int  _flag = 0;
    private int  _tick = 0;
    public bool IsConnected => true;

    public Task ConnectAsync() => Task.CompletedTask;

    public Task<int> ReadWordAsync(string address)
    {
        var up = address.ToUpperInvariant();
        if (up.Contains("FLAG") || up is "DM0201" or "D201")
        {
            _tick++;
            if (_tick % 5 == 0) _flag = 1;
            return Task.FromResult(_flag);
        }
        _v += Random.Shared.Next(-3, 8);
        return Task.FromResult(_v);
    }

    public Task<int[]> ReadWordsAsync(string address, int count)
        => Task.FromResult(Enumerable.Range(0, count).Select(_ => _v).ToArray());

    public Task WriteWordAsync(string address, int value)
    {
        var up = address.ToUpperInvariant();
        if (up.Contains("FLAG") || up is "DM0201" or "D201") _flag = value;
        return Task.CompletedTask;
    }

    public void Dispose() { }
}

// ── Keyence KV-8000 via Modbus TCP ────────────────────────────────────────────
public class KeyenceKVClient : IPlcClient
{
    private readonly string _ip;
    private readonly int    _port;
    private readonly int    _unit;
    private TcpClient?      _tcp;
    private NetworkStream?  _stream;
    private ushort          _txId = 0;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // KV-8000 Modbus 地址映射
    private static readonly Dictionary<string, (int fc, int baseAddr)> DevMap = new()
    {
        ["DM"]  = (3, 0x0000),   // Holding Register
        ["W"]   = (3, 0x0800),
        ["TN"]  = (3, 0x0C00),
        ["CN"]  = (3, 0x1000),
        ["MR"]  = (1, 0x0000),   // Coil
        ["CR"]  = (1, 0x0800),
        ["CIO"] = (4, 0x0000),   // Input Register
    };

    public bool IsConnected => _tcp?.Connected ?? false;

    public KeyenceKVClient(PlcConfig cfg)
    {
        _ip   = cfg.Ip;
        _port = cfg.Port;
        _unit = cfg.Unit;
    }

    public async Task ConnectAsync()
    {
        _tcp?.Dispose();
        _tcp    = new TcpClient { ReceiveTimeout = 3000, SendTimeout = 3000 };
        await _tcp.ConnectAsync(_ip, _port);
        _stream = _tcp.GetStream();
    }

    private (int fc, int addr) ParseAddress(string address)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            address.ToUpperInvariant(), @"^([A-Z]+)(\d+)$");
        if (!m.Success)
            throw new ArgumentException($"无效地址格式: {address}  示例: DM0100 / MR000");
        var dev = m.Groups[1].Value;
        var num = int.Parse(m.Groups[2].Value);
        if (!DevMap.TryGetValue(dev, out var info))
            throw new ArgumentException($"不支持的设备类型: {dev}  支持: {string.Join(", ", DevMap.Keys)}");
        return (info.fc, info.baseAddr + num);
    }

    // Modbus TCP frame builder
    private byte[] BuildRequest(byte fc, int addr, int count)
    {
        _txId++;
        var pdu = new byte[] { fc,
            (byte)(addr >> 8), (byte)(addr & 0xFF),
            (byte)(count >> 8), (byte)(count & 0xFF) };
        var frame = new byte[6 + pdu.Length];
        frame[0] = (byte)(_txId >> 8); frame[1] = (byte)(_txId & 0xFF);
        frame[2] = 0; frame[3] = 0;
        frame[4] = (byte)((pdu.Length + 1) >> 8); frame[5] = (byte)((pdu.Length + 1) & 0xFF);
        frame[6] = (byte)_unit;
        pdu.CopyTo(frame, 7);
        return frame;
    }

    private async Task<byte[]> SendReceiveAsync(byte[] frame)
    {
        await _lock.WaitAsync();
        try
        {
            if (!IsConnected) await ConnectAsync();
            await _stream!.WriteAsync(frame);
            var header = new byte[6];
            await _stream.ReadExactlyAsync(header, 0, 6);
            int len = (header[4] << 8) | header[5];
            var body = new byte[len];
            await _stream.ReadExactlyAsync(body, 0, len);
            return body;
        }
        finally { _lock.Release(); }
    }

    public async Task<int> ReadWordAsync(string address)
    {
        var (fc, addr) = ParseAddress(address);
        if (fc == 1)  // coil
        {
            var frame = BuildRequest(1, addr, 1);
            var resp  = await SendReceiveAsync(frame);
            return (resp[2] & 0x01);
        }
        else
        {
            var frame = BuildRequest((byte)fc, addr, 1);
            var resp  = await SendReceiveAsync(frame);
            return (resp[2] << 8) | resp[3];
        }
    }

    public async Task<int[]> ReadWordsAsync(string address, int count)
    {
        var (fc, addr) = ParseAddress(address);
        var frame = BuildRequest((byte)fc, addr, count);
        var resp  = await SendReceiveAsync(frame);
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = (resp[2 + i * 2] << 8) | resp[3 + i * 2];
        return result;
    }

    public async Task WriteWordAsync(string address, int value)
    {
        var (fc, addr) = ParseAddress(address);
        byte writeFc = fc == 1 ? (byte)5 : (byte)6;

        byte[] pdu;
        if (fc == 1)
            pdu = new byte[] { writeFc,
                (byte)(addr >> 8), (byte)(addr & 0xFF),
                (byte)(value != 0 ? 0xFF : 0x00), 0x00 };
        else
            pdu = new byte[] { writeFc,
                (byte)(addr >> 8), (byte)(addr & 0xFF),
                (byte)(value >> 8), (byte)(value & 0xFF) };

        _txId++;
        var frame = new byte[6 + pdu.Length];
        frame[0] = (byte)(_txId >> 8); frame[1] = (byte)(_txId & 0xFF);
        frame[4] = (byte)((pdu.Length + 1) >> 8); frame[5] = (byte)((pdu.Length + 1) & 0xFF);
        frame[6] = (byte)_unit;
        pdu.CopyTo(frame, 7);
        await SendReceiveAsync(frame);
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
    }
}

// ── Factory ────────────────────────────────────────────────────────────────────
public static class PlcFactory
{
    public static IPlcClient Create(PlcConfig cfg) => cfg.Brand switch
    {
        "keyence_kv" => new KeyenceKVClient(cfg),
        "mock"       => new MockPlcClient(),
        _            => throw new NotSupportedException($"未知 PLC 品牌: {cfg.Brand}")
    };
}

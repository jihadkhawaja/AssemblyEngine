using System.Buffers;
using System.Net.Sockets;
using System.Text.Json;

namespace AssemblyEngine.Networking;

internal sealed class MultiplayerConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public MultiplayerConnection(TcpClient client, JsonSerializerOptions serializerOptions)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        _serializerOptions = serializerOptions;
        EndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
    }

    public string? PeerId { get; set; }

    public string EndPoint { get; }

    public async Task SendAsync(MultiplayerEnvelope envelope, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(envelope, _serializerOptions);
        byte[] header = BitConverter.GetBytes(payload.Length);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(header, cancellationToken);
            await _stream.WriteAsync(payload, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task ReadLoopAsync(Func<MultiplayerEnvelope, Task> onMessage, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        byte[] headerBuffer = new byte[sizeof(int)];
        while (!cancellationToken.IsCancellationRequested)
        {
            await ReadExactlyAsync(headerBuffer, cancellationToken);
            int payloadLength = BitConverter.ToInt32(headerBuffer, 0);
            if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                throw new InvalidDataException($"Invalid multiplayer payload size '{payloadLength}'.");

            byte[] rented = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                var payload = rented.AsMemory(0, payloadLength);
                await ReadExactlyAsync(payload, cancellationToken);

                MultiplayerEnvelope envelope = JsonSerializer.Deserialize<MultiplayerEnvelope>(payload.Span, _serializerOptions)
                    ?? throw new InvalidDataException("Failed to deserialize multiplayer envelope.");

                await onMessage(envelope);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _stream.Close();
            _client.Close();
        }
        finally
        {
            _sendLock.Dispose();
            await Task.CompletedTask;
        }
    }

    private async Task ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            int chunk = await _stream.ReadAsync(buffer[bytesRead..], cancellationToken);
            if (chunk <= 0)
                throw new EndOfStreamException("Multiplayer connection closed while reading.");

            bytesRead += chunk;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
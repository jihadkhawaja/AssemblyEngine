using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace AssemblyEngine.Diagnostics;

internal static class RuntimeDiagnosticsPipe
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static Channel<RuntimeDiagnosticsMessage> CreateOutboundChannel()
    {
        return Channel.CreateUnbounded<RuntimeDiagnosticsMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public static RuntimeDiagnosticsMessage CreateEvent<TPayload>(string name, TPayload payload)
    {
        return CreateMessage(RuntimeDiagnosticsProtocol.EventKind, name, null, payload, null);
    }

    public static RuntimeDiagnosticsMessage CreateCommand<TPayload>(string name, long requestId, TPayload payload)
    {
        return CreateMessage(RuntimeDiagnosticsProtocol.CommandKind, name, requestId, payload, null);
    }

    public static RuntimeDiagnosticsMessage CreateResponse<TPayload>(string name, long requestId, TPayload payload)
    {
        return CreateMessage(RuntimeDiagnosticsProtocol.ResponseKind, name, requestId, payload, null);
    }

    public static RuntimeDiagnosticsMessage CreateErrorResponse(string name, long requestId, string error)
    {
        return CreateMessage<object?>(RuntimeDiagnosticsProtocol.ResponseKind, name, requestId, null, error);
    }

    public static T DeserializePayload<T>(RuntimeDiagnosticsMessage message)
    {
        return JsonSerializer.Deserialize<T>(message.Payload, SerializerOptions)
            ?? throw new InvalidOperationException($"The payload for '{message.Name}' was empty.");
    }

    public static async ValueTask WriteMessageAsync(Stream stream, RuntimeDiagnosticsMessage message, CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        await stream.WriteAsync(payload.AsMemory(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async ValueTask<RuntimeDiagnosticsMessage?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[sizeof(int)];
        if (!await ReadExactlyOrEofAsync(stream, header, cancellationToken))
            return null;

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength <= 0)
            throw new InvalidDataException("Diagnostics transport received an invalid message length.");

        byte[] payload = new byte[payloadLength];
        await stream.ReadExactlyAsync(payload.AsMemory(), cancellationToken);

        return JsonSerializer.Deserialize<RuntimeDiagnosticsMessage>(payload, SerializerOptions)
            ?? throw new InvalidDataException("Diagnostics transport received an unreadable message.");
    }

    private static RuntimeDiagnosticsMessage CreateMessage<TPayload>(string kind, string name, long? requestId, TPayload payload, string? error)
    {
        return new RuntimeDiagnosticsMessage
        {
            Kind = kind,
            Name = name,
            RequestId = requestId,
            Error = error,
            Payload = JsonSerializer.SerializeToElement(payload, SerializerOptions),
        };
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static async Task<bool> ReadExactlyOrEofAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                if (offset == 0)
                    return false;

                throw new EndOfStreamException("Diagnostics transport closed mid-message.");
            }

            offset += read;
        }

        return true;
    }
}
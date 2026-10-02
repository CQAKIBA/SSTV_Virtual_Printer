using System.Buffers.Binary;
using System.Text;

namespace SstvVPrinter.IppProbe.Ipp;

public static class IppMessageReader
{
    private const int Limit = 1024 * 1024;

    public static async ValueTask<IppMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[8];
        await ReadExactlyAsync(stream, header, cancellationToken);
        var version = new IppVersion(header[0], header[1]);
        ushort code = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        int requestId = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));

        try
        {
            IReadOnlyList<IppAttributeGroup> groups = await ReadGroupsAsync(stream, cancellationToken);
            return new IppMessage(version, code, requestId, groups);
        }
        catch (IppProtocolException exception) when (exception.RequestId is null)
        {
            throw new IppProtocolException(exception.Message, exception.StatusCode, exception, version, requestId);
        }
    }

    private static async ValueTask<IReadOnlyList<IppAttributeGroup>> ReadGroupsAsync(Stream stream, CancellationToken cancellationToken)
    {
        var groups = new List<IppAttributeGroup>();
        int used = 8;
        string? previous = null;
        byte tag = await ReadByteAsync(stream, cancellationToken);
        while (tag != IppDelimiterTags.EndOfAttributes)
        {
            if (!IppDelimiterTags.IsGroup(tag)) throw Bad($"Unknown delimiter 0x{tag:X2}");
            var attributes = new List<IppAttribute>();
            var indexes = new Dictionary<string, int>();
            byte next = await ReadByteAsync(stream, cancellationToken);
            while (!IppDelimiterTags.IsGroup(next) && next != IppDelimiterTags.EndOfAttributes)
            {
                ushort nameLength = await ReadUInt16Async(stream, cancellationToken);
                Check(used = checked(used + 3 + nameLength));
                string name;
                if (nameLength == 0) name = previous ?? throw Bad("Repeated value without name");
                else
                {
                    byte[] nameBytes = new byte[nameLength];
                    await ReadExactlyAsync(stream, nameBytes, cancellationToken);
                    name = Encoding.UTF8.GetString(nameBytes);
                    previous = name;
                }

                ushort valueLength = await ReadUInt16Async(stream, cancellationToken);
                Check(used = checked(used + 2 + valueLength));
                byte[] valueBytes = new byte[valueLength];
                await ReadExactlyAsync(stream, valueBytes, cancellationToken);
                var value = new IppValue(next, valueBytes);
                if (nameLength == 0)
                {
                    if (!indexes.TryGetValue(name, out int index)) throw Bad("Repeated name outside group");
                    attributes[index] = new(name, attributes[index].Values.Append(value).ToArray());
                }
                else
                {
                    indexes[name] = attributes.Count;
                    attributes.Add(new(name, [value]));
                }
                next = await ReadByteAsync(stream, cancellationToken);
            }
            groups.Add(new(tag, attributes));
            tag = next;
        }
        return groups;
    }

    private static void Check(int count)
    {
        if (count > Limit) throw Bad("Attributes exceed 1 MiB");
    }

    private static async ValueTask<byte> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] value = new byte[1];
        await ReadExactlyAsync(stream, value, cancellationToken);
        return value[0];
    }

    private static async ValueTask<ushort> ReadUInt16Async(Stream stream, CancellationToken cancellationToken)
    {
        byte[] value = new byte[2];
        await ReadExactlyAsync(stream, value, cancellationToken);
        return BinaryPrimitives.ReadUInt16BigEndian(value);
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try { await stream.ReadExactlyAsync(buffer, cancellationToken); }
        catch (EndOfStreamException exception) { throw Bad("Unexpected end", exception); }
    }

    private static IppProtocolException Bad(string message, Exception? inner = null) =>
        new(message, IppStatusCodes.ClientErrorBadRequest, inner);
}

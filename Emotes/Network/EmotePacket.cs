using ProtoBuf;

namespace Emotes;

[ProtoContract]
public class EmotePacket
{
    [ProtoMember(1)]
    public string Code { get; set; }
}

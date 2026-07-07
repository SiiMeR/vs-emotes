using ProtoBuf;

namespace Emotes;

[ProtoContract]
public class DisabledEmotesPacket
{
    [ProtoMember(1)]
    public string[] Codes { get; set; }
}

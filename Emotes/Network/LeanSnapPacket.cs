using ProtoBuf;

namespace Emotes;

[ProtoContract]
public class LeanSnapPacket
{
    [ProtoMember(1)]
    public float Yaw { get; set; }
}

using ProtoBuf;

namespace OobeRadio;

public sealed class ServerConfig
{
    public bool Enabled { get; set; } = true;
    public bool AllowHttpFallback { get; set; } = true;
}

public sealed class ClientConfig
{
    public int VolumePercent { get; set; } = 50;
    public int? MusicLevelBeforeRadio { get; set; }
}

[ProtoContract]
public sealed class StationRequest
{
    [ProtoMember(1)] public int Protocol { get; set; } = 1;
}

[ProtoContract]
public sealed class StationPolicy
{
    [ProtoMember(1)] public bool Enabled { get; set; }
    [ProtoMember(2)] public bool AllowHttpFallback { get; set; }
}

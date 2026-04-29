namespace Emotes;

public class CustomEmote
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Animation { get; set; }
    public bool StopOnMovement { get; set; } = true;
    public bool StopOnDamage { get; set; } = true;
    public bool StopAfterAnimation { get; set; } = false;
    public bool RequiresTarget { get; set; } = false;
    public float PairDistance { get; set; } = 0.5f;
}

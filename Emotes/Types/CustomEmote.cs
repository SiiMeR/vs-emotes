using Vintagestory.API.Common;

namespace Emotes;

public class CustomEmote
{
    public string Code { get; set; }
    public string AnimationCode { get; set; }
    public string Category { get; set; }
    public int CategoryOrder { get; set; }
    public string Name { get; set; }
    public string CategoryName { get; set; }
    public string Domain { get; set; }
    public string Source { get; set; }
    public bool StopOnMovement { get; set; } = true;
    public bool StopOnDamage { get; set; } = true;
    public bool StopAfterAnimation { get; set; }
    public bool RequiresTarget { get; set; }
    public float PairDistance { get; set; } = 0.5f;
    public bool SnapToWall { get; set; }
    public double? EyeHeight { get; set; }
    public AnimationMetaData Meta { get; set; }
    public Animation InjectedAnimation { get; set; }
}

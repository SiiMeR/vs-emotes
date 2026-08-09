using System.Collections.Generic;

namespace Emotes;

public class EmoteDefinition
{
    public string Code;
    public string Category;
    public int? CategoryOrder;
    public string Animation;
    public string AnimationShape;
    public string AnimationShapeCode;
    public Vintagestory.API.Common.Animation AnimationDefinition;
    public EmoteAnimationMeta AnimationMeta;
    public string Name;
    public string CategoryName;
    public bool StopOnMovement = true;
    public bool StopOnDamage = true;
    public bool StopAfterAnimation;
    public bool RequiresTarget;
    public float PairDistance = 0.5f;
    public bool SnapToWall;
    public double? EyeHeight;
}

public class EmoteAnimationMeta
{
    public string BlendMode;
    public float? Weight;
    public float? AnimationSpeed;
    public float? EaseInSpeed;
    public float? EaseOutSpeed;
    public float? WeightCapFactor;
    public float? HoldEyePosAfterEasein;
    public bool? SupressDefaultAnimation;
    public bool? AdjustCollisionBox;
    public Dictionary<string, float> ElementWeight;
    public Dictionary<string, string> ElementBlendMode;
}

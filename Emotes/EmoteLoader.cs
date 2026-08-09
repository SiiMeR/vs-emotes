using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace Emotes;

public class EmoteLoadResult
{
    public Dictionary<string, CustomEmote> Emotes = new(StringComparer.OrdinalIgnoreCase);
    public Animation[] InjectedAnimations = Array.Empty<Animation>();
    public int FileCount;
    public int SkippedCount;
}

public static class EmoteLoader
{
    private const string AssetPath = "config/emotes/";

    public static EmoteLoadResult Load(ICoreAPI api, ILogger logger)
    {
        var result = new EmoteLoadResult();
        if (api?.Assets == null) return result;

        var assets = api.Assets.GetMany(AssetPath);
        if (assets == null) return result;

        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var shapes = new Dictionary<string, Shape>(StringComparer.Ordinal);

        foreach (var asset in assets.OrderBy(a => a.Location.ToString(), StringComparer.Ordinal))
        {
            result.FileCount++;

            var source = asset.Location.ToString();

            EmoteDefinition[] definitions;
            try
            {
                definitions = ReadDefinitions(asset);
            }
            catch (Exception e)
            {
                logger.Error("Syntax error in json file '{0}': {1}", source, e.Message);
                result.SkippedCount++;
                continue;
            }

            foreach (var def in definitions)
            {
                if (def == null) continue;

                var emote = Build(api, asset.Location.Domain, source, def, shapes, out var error);
                if (emote == null)
                {
                    logger.Error("Skipped emote '{0}' from {1}: {2}", def.Code ?? "?", source, error);
                    result.SkippedCount++;
                    continue;
                }

                if (sources.TryGetValue(emote.Code, out var previous))
                    logger.Warning("Emote '{0}' from {1} replaces the one from {2}", emote.Code, source, previous);

                sources[emote.Code] = source;
                result.Emotes[emote.Code] = emote;

                logger.Debug("Emote '{0}' from {1}: animation '{2}' ({3})", emote.Code, source, emote.AnimationCode,
                    DescribeSource(def, emote));
            }
        }

        result.InjectedAnimations = result.Emotes.Values
            .Select(e => e.InjectedAnimation)
            .Where(a => a != null)
            .ToArray();

        return result;
    }

    private static EmoteDefinition[] ReadDefinitions(IAsset asset)
    {
        var token = asset.ToObject<JToken>();
        if (token == null) return Array.Empty<EmoteDefinition>();

        var domain = asset.Location.Domain;
        if (token is JArray array)
            return array.Select(t => t.ToObject<EmoteDefinition>(domain)).ToArray();

        return new[] { token.ToObject<EmoteDefinition>(domain) };
    }

    private static string DescribeSource(EmoteDefinition def, CustomEmote emote)
    {
        if (emote.InjectedAnimation == null) return "already on the player shape";
        return def.AnimationDefinition != null ? "inline definition" : "shape " + def.AnimationShape;
    }

    private static CustomEmote Build(ICoreAPI api, string domain, string source, EmoteDefinition def,
        Dictionary<string, Shape> shapes, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(def.Code))
        {
            error = "code is empty";
            return null;
        }

        var code = def.Code.Trim().ToLowerInvariant();
        var animationCode = string.IsNullOrWhiteSpace(def.Animation)
            ? domain + ":" + code
            : def.Animation.Trim().ToLowerInvariant();

        var animation = ResolveAnimation(api, domain, def, shapes, out error);
        if (error != null) return null;

        if (animation != null)
        {
            if (!Validate(animation, out error)) return null;
            animation = animation.Clone();
            animation.Code = animationCode;
        }

        var meta = BuildMeta(animationCode, def.AnimationMeta, out error);
        if (meta == null) return null;

        return new CustomEmote
        {
            Code = code,
            AnimationCode = animationCode,
            Category = string.IsNullOrWhiteSpace(def.Category) ? "misc" : def.Category.Trim().ToLowerInvariant(),
            CategoryOrder = def.CategoryOrder ?? int.MaxValue - 1,
            Name = def.Name,
            CategoryName = def.CategoryName,
            Domain = domain,
            Source = source,
            StopOnMovement = def.StopOnMovement,
            StopOnDamage = def.StopOnDamage,
            StopAfterAnimation = def.StopAfterAnimation,
            RequiresTarget = def.RequiresTarget,
            PairDistance = def.PairDistance,
            SnapToWall = def.SnapToWall,
            EyeHeight = def.EyeHeight,
            Meta = meta,
            InjectedAnimation = animation
        };
    }

    private static Animation ResolveAnimation(ICoreAPI api, string domain, EmoteDefinition def,
        Dictionary<string, Shape> shapes, out string error)
    {
        error = null;

        var hasShape = !string.IsNullOrWhiteSpace(def.AnimationShape);
        if (def.AnimationDefinition != null && hasShape)
        {
            error = "animationShape and animationDefinition are both set, use only one";
            return null;
        }

        if (def.AnimationDefinition != null) return def.AnimationDefinition;
        if (!hasShape) return null;

        var shapeLocation = AssetLocation.Create(def.AnimationShape.Trim(), domain)
            .WithPathPrefixOnce("shapes/")
            .WithPathAppendixOnce(".json");

        var key = shapeLocation.ToString();
        if (!shapes.TryGetValue(key, out var shape))
        {
            shape = Shape.TryGet(api, shapeLocation);
            shapes[key] = shape;
        }

        if (shape == null)
        {
            error = "animationShape '" + shapeLocation +
                    "' could not be loaded, the file is missing or the shape has no elements array";
            return null;
        }

        var animations = shape.Animations;
        if (animations == null || animations.Length == 0)
        {
            error = "animationShape '" + shapeLocation + "' contains no animations";
            return null;
        }

        if (string.IsNullOrWhiteSpace(def.AnimationShapeCode))
        {
            if (animations.Length == 1) return animations[0];
            error = "animationShape '" + shapeLocation + "' has " + animations.Length +
                    " animations, set animationShapeCode to pick one, found: " + Describe(animations);
            return null;
        }

        var wanted = NormalizeAnimationCode(def.AnimationShapeCode);
        var match = animations.FirstOrDefault(a => a != null && NormalizeAnimationCode(a.Code) == wanted);
        if (match == null)
        {
            error = "animationShape '" + shapeLocation + "' has no animation with code '" + def.AnimationShapeCode +
                    "', found: " + Describe(animations);
            return null;
        }

        return match;
    }

    private static string Describe(Animation[] animations)
    {
        return string.Join(", ", animations.Where(a => a != null).Select(a => a.Code ?? a.Name ?? "?"));
    }

    private static string NormalizeAnimationCode(string code)
    {
        return code == null ? "" : code.ToLowerInvariant().Replace(" ", "");
    }

    private static bool Validate(Animation animation, out string error)
    {
        error = null;

        if (animation.KeyFrames == null || animation.KeyFrames.Length == 0)
        {
            error = "animation has no keyframes";
            return false;
        }

        var highest = 0;
        for (var i = 0; i < animation.KeyFrames.Length; i++)
        {
            var frame = animation.KeyFrames[i];
            if (frame == null)
            {
                error = "keyframe " + i + " is empty";
                return false;
            }

            if (frame.Elements == null)
            {
                error = "keyframe " + i + " has no elements";
                return false;
            }

            if (i > 0 && frame.Frame <= animation.KeyFrames[i - 1].Frame)
            {
                error = "keyframes are not in ascending frame order, frame " + frame.Frame + " follows frame " +
                        animation.KeyFrames[i - 1].Frame;
                return false;
            }

            highest = frame.Frame;
        }

        if (animation.QuantityFrames <= highest)
        {
            error = "quantityframes is " + animation.QuantityFrames +
                    ", it must be greater than the highest keyframe frame " + highest;
            return false;
        }

        return true;
    }

    private static AnimationMetaData BuildMeta(string animationCode, EmoteAnimationMeta overlay, out string error)
    {
        error = null;

        var meta = new AnimationMetaData
        {
            Code = animationCode,
            Animation = animationCode,
            BlendMode = EnumAnimationBlendMode.Add,
            Weight = 1f,
            AnimationSpeed = 1f,
            EaseInSpeed = 999f,
            EaseOutSpeed = 2f,
            ClientSide = true,
            ElementWeight = new Dictionary<string, float>(),
            ElementBlendMode = new Dictionary<string, EnumAnimationBlendMode>(StringComparer.OrdinalIgnoreCase)
        };

        if (overlay == null) return meta.Init();

        if (!string.IsNullOrWhiteSpace(overlay.BlendMode))
        {
            if (!TryParseBlendMode(overlay.BlendMode, out var blendMode))
            {
                error = "unknown blendMode '" + overlay.BlendMode + "', expected Add, Average or AddAverage";
                return null;
            }

            meta.BlendMode = blendMode;
        }

        if (overlay.Weight.HasValue) meta.Weight = overlay.Weight.Value;
        if (overlay.AnimationSpeed.HasValue) meta.AnimationSpeed = overlay.AnimationSpeed.Value;
        if (overlay.EaseInSpeed.HasValue) meta.EaseInSpeed = overlay.EaseInSpeed.Value;
        if (overlay.EaseOutSpeed.HasValue) meta.EaseOutSpeed = overlay.EaseOutSpeed.Value;
        if (overlay.WeightCapFactor.HasValue) meta.WeightCapFactor = overlay.WeightCapFactor.Value;
        if (overlay.HoldEyePosAfterEasein.HasValue) meta.HoldEyePosAfterEasein = overlay.HoldEyePosAfterEasein.Value;
        if (overlay.SupressDefaultAnimation.HasValue) meta.SupressDefaultAnimation = overlay.SupressDefaultAnimation.Value;
        if (overlay.AdjustCollisionBox.HasValue) meta.AdjustCollisionBox = overlay.AdjustCollisionBox.Value;

        if (overlay.ElementWeight != null)
            foreach (var entry in overlay.ElementWeight)
                meta.ElementWeight[entry.Key] = entry.Value;

        if (overlay.ElementBlendMode != null)
            foreach (var entry in overlay.ElementBlendMode)
            {
                if (!TryParseBlendMode(entry.Value, out var blendMode))
                {
                    error = "unknown elementBlendMode '" + entry.Value + "' for element '" + entry.Key +
                            "', expected Add, Average or AddAverage";
                    return null;
                }

                meta.ElementBlendMode[entry.Key] = blendMode;
            }

        return meta.Init();
    }

    private static bool TryParseBlendMode(string value, out EnumAnimationBlendMode blendMode)
    {
        return Enum.TryParse(value?.Trim(), true, out blendMode) && Enum.IsDefined(typeof(EnumAnimationBlendMode), blendMode);
    }
}

using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Emotes;

public static class EmoteWallSnap
{
    private const int MaxWallDistance = 2;

    private static readonly (BlockFacing facing, float yaw)[] HorizontalFacings =
    {
        (BlockFacing.NORTH, 0f),
        (BlockFacing.SOUTH, GameMath.PI),
        (BlockFacing.EAST, -GameMath.PIHALF),
        (BlockFacing.WEST, GameMath.PIHALF)
    };

    public static float? TrySnap(Entity entity)
    {
        var world = entity?.World;
        if (world == null) return null;

        var pos = entity.Pos;
        var blockPos = pos.AsBlockPos;

        var wall = HorizontalFacings
            .Select(candidate => (candidate.facing, candidate.yaw,
                distance: WallDistance(world, blockPos, candidate.facing)))
            .Where(candidate => candidate.distance > 0)
            .OrderBy(candidate => LookAwayFrom(pos.Yaw, candidate.yaw))
            .ThenBy(candidate => candidate.distance)
            .FirstOrDefault();

        if (wall.facing == null) return null;

        var norm = wall.facing.Normali;
        var wallPos = blockPos.AddCopy(norm.X * wall.distance, 0, norm.Z * wall.distance);
        var gap = entity.Properties?.CollisionBoxSize?.X / 2.0 ?? 0.3;

        double snapX = pos.X, snapZ = pos.Z;
        if (norm.X != 0)
            snapX = wallPos.X + (norm.X > 0 ? -gap : 1.0 + gap);
        else
            snapZ = wallPos.Z + (norm.Z > 0 ? -gap : 1.0 + gap);

        entity.TeleportToDouble(snapX, pos.Y, snapZ);
        entity.Pos.Yaw = wall.yaw;
        return wall.yaw;
    }

    private static float LookAwayFrom(float entityYaw, float leanYaw)
    {
        return Math.Abs(GameMath.AngleRadDistance(entityYaw, leanYaw + GameMath.PI));
    }

    private static int WallDistance(IWorldAccessor world, BlockPos from, BlockFacing facing)
    {
        var norm = facing.Normali;

        for (var distance = 1; distance <= MaxWallDistance; distance++)
        {
            var candidate = from.AddCopy(norm.X * distance, 0, norm.Z * distance);
            if (IsSolidWall(world, candidate, facing)) return distance;
        }

        return 0;
    }

    private static bool IsSolidWall(IWorldAccessor world, BlockPos pos, BlockFacing facingToWall)
    {
        var playerSide = facingToWall.Opposite;
        var low = world.BlockAccessor.GetBlock(pos);
        var high = world.BlockAccessor.GetBlock(pos.AddCopy(0, 1, 0));
        return low.SideSolid[playerSide.Index] && high.SideSolid[playerSide.Index];
    }
}

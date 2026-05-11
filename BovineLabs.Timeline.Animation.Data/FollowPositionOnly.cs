using Unity.Entities;

namespace BovineLabs.Timeline.Animation
{
    public struct FollowPositionOnly : IComponentData
    {
        public Entity TargetBone;
    }
}
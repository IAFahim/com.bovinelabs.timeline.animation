using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation
{
    public struct RukhankaAnimationClipAnimated : IComponentData
    {
        public Hash128 AnimationHash;
    }
}
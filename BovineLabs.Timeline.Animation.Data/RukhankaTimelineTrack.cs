using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation
{
    public struct RukhankaTimelineTrack : IComponentData
    {
        public Hash128 ExitIdleClipHash;
        public float ExitTransitionDuration;
    }
}
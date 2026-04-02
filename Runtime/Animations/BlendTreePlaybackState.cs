using Unity.Entities;

namespace BovineLabs.Timeline.Animation
{
    public struct BlendTreePlaybackState : IComponentData
    {
        public float AccumulatedTime;
        public float PreviousAbsoluteTime;
        public bool IsInitialized;
    }
}

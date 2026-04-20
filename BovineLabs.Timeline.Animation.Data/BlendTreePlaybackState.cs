using Unity.Entities;

namespace BovineLabs.Timeline.Animation
{
    [InternalBufferCapacity(4)]
    public struct BlendTreePlaybackStateElement : IBufferElementData
    {
        public Entity Track;
        public float AccumulatedTime;
        public float PreviousAbsoluteTime;
        public bool IsInitialized;
    }
}
using Rukhanka;
using Unity.Entities;

namespace BovineLabs.Timeline.Animation
{
    [InternalBufferCapacity(8)]
    public struct BlendGroupEntry : IBufferElementData
    {
        public int LayerIndex;
        public Hash128 ClipHash;
        public float NormalizedTime;
        public float Weight;
        public Hash128 AvatarMaskHash;
        public AnimationBlendingMode BlendMode;
    }

    [InternalBufferCapacity(8)]
    public struct SmoothBlendGroupEntry : IBufferElementData
    {
        public int LayerIndex;
        public Hash128 ClipHash;
        public float NormalizedTime;
        public float CurrentWeight;
        public float TargetWeight;
        public AnimationBlendingMode BlendMode;
    }

    public struct BlendGroupTimer : IComponentData, IEnableableComponent
    {
        public float FallbackAccumulatedTime;
    }

    public struct BlendGroupFallBackForNoAnimationToProcessComponent : IComponentData
    {
        public Hash128 ClipHash;
        public float BlendInSpeed;
        public float BlendOutSpeed;
    }

    public struct TrackFallbackOverride : IComponentData
    {
        public Hash128 FallbackClipHash;
        public float BlendInSpeed;
        public float BlendOutSpeed;
    }
}
using Rukhanka;
using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;

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
        public uint MotionId;
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
        public Hash128 AvatarMaskHash;
        public uint MotionId;
    }

    public struct BlendGroupTimer : IComponentData, IEnableableComponent
    {
        public float FallbackAccumulatedTime;
    }

    public struct BlendGroupFallbackForNoAnimationToProcessComponent : IComponentData
    {
        public Hash128 ClipHash;
        public float BlendInSpeed;
        public float BlendOutSpeed;
        public FallbackPlaybackMode PlaybackMode;
        public int LayerIndex;
        public AnimationBlendingMode BlendMode;
        public Hash128 AvatarMaskHash;
    }

    public struct TrackFallbackOverride : IComponentData
    {
        public Hash128 FallbackClipHash;
        public float BlendInSpeed;
        public float BlendOutSpeed;
        public FallbackPlaybackMode PlaybackMode;
        public int LayerIndex;
        public AnimationBlendingMode BlendMode;
        public Hash128 AvatarMaskHash;
    }

    public enum FallbackPlaybackMode : byte
    {
        Loop = 0,
        Clamp = 1,
        Hold = 2
    }

    public struct AnimationDebugState : IComponentData
    {
        public int ActiveTrackCount;
        public int ActiveClipCount;
        public int FallbackTrackCount;
        public float FallbackWeight;
        public float BlendInSpeed;
        public float BlendOutSpeed;
        public FallbackPlaybackMode PlaybackMode;
    }
}

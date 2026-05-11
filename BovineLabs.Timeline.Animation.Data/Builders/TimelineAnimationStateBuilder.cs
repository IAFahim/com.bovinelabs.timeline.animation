using BovineLabs.Core.EntityCommands;
using Rukhanka;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Data.Builders
{
    public struct TimelineAnimationStateBuilder
    {
        private const float MinDuration = 0.001f;

        private Hash128 _fallbackClipHash;
        private float _blendInSpeed;
        private float _blendOutSpeed;
        private BlobAssetReference<AnimationClipBlob> _fallbackBlob;
        private Hash128 _fallbackBlobHash;
        private FallbackPlaybackMode _playbackMode;

        private float3 _positionOffset;
        private quaternion _rotationOffset;
        private bool _removeStartOffset;
        private bool _applyFootIK;

        public TimelineAnimationStateBuilder WithFallback(
            Hash128 clipHash,
            float blendInDuration,
            float blendOutDuration,
            FallbackPlaybackMode mode = FallbackPlaybackMode.Loop)
        {
            _fallbackClipHash = clipHash;
            _blendInSpeed = 1f / math.max(MinDuration, blendInDuration);
            _blendOutSpeed = 1f / math.max(MinDuration, blendOutDuration);
            _playbackMode = mode;
            return this;
        }

        public TimelineAnimationStateBuilder WithFallbackOffsets(float3 pos, quaternion rot, bool removeStart,
            bool footIK)
        {
            _positionOffset = pos;
            _rotationOffset = rot;
            _removeStartOffset = removeStart;
            _applyFootIK = footIK;
            return this;
        }

        public TimelineAnimationStateBuilder WithFallbackBlob(
            BlobAssetReference<AnimationClipBlob> blob,
            Hash128 hash)
        {
            _fallbackBlob = blob;
            _fallbackBlobHash = hash;
            return this;
        }

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new BlendGroupTimer { FallbackAccumulatedTime = 0f });

            var activeFallback = new FallbackBlend
            {
                ClipHash = _fallbackClipHash,
                BlendInSpeed = _blendInSpeed,
                BlendOutSpeed = _blendOutSpeed,
                PlaybackMode = _playbackMode,
                LayerIndex = 0,
                BlendMode = AnimationBlendingMode.Override,
                AvatarMaskHash = default,
                PositionOffset = _positionOffset,
                RotationOffset = _rotationOffset,
                RemoveStartOffset = _removeStartOffset,
                ApplyFootIK = _applyFootIK
            };

            builder.AddComponent(activeFallback);

            builder.AddComponent(new DefaultBlendGroupFallback
            {
                ClipHash = _fallbackClipHash,
                BlendInSpeed = _blendInSpeed,
                BlendOutSpeed = _blendOutSpeed,
                PlaybackMode = _playbackMode,
                LayerIndex = 0,
                BlendMode = AnimationBlendingMode.Override,
                AvatarMaskHash = default,
                PositionOffset = _positionOffset,
                RotationOffset = _rotationOffset,
                RemoveStartOffset = _removeStartOffset,
                ApplyFootIK = _applyFootIK
            });

            if (_fallbackBlob.IsCreated)
            {
                var dbBuffer = builder.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>();
                dbBuffer.Add(new NewBlobAssetDatabaseRecord<AnimationClipBlob>
                    { hash = _fallbackBlobHash, value = _fallbackBlob });
            }

            builder.AddBuffer<BlendGroupEntry>();
            builder.AddBuffer<SmoothBlendGroupEntry>();
            // Always added: any entity may be targeted by BlendTree2D tracks at runtime.
            // InternalBufferCapacity(4) means minimal overhead for single-clip-only entities.
            // The buffer is also used by the cleanup pass in DecomposeAndAppendBlendTreeJob.
            builder.AddBuffer<BlendTreePlaybackStateElement>();
        }
    }
}
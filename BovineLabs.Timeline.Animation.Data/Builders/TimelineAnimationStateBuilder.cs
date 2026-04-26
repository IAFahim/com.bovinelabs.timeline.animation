using BovineLabs.Core.EntityCommands;
using Rukhanka;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Data.Builders
{
    public struct TimelineAnimationStateBuilder
    {
        private Hash128 fallbackClipHash;
        private float blendInSpeed;
        private float blendOutSpeed;
        private BlobAssetReference<AnimationClipBlob> fallbackBlob;
        private Hash128 fallbackBlobHash;
        private FallbackPlaybackMode playbackMode;

        public TimelineAnimationStateBuilder WithFallback(
            Hash128 clipHash,
            float blendInDuration,
            float blendOutDuration,
            FallbackPlaybackMode mode = FallbackPlaybackMode.Loop)
        {
            fallbackClipHash = clipHash;
            blendInSpeed = 1f / math.max(0.001f, blendInDuration);
            blendOutSpeed = 1f / math.max(0.001f, blendOutDuration);
            playbackMode = mode;
            return this;
        }

        public TimelineAnimationStateBuilder WithFallbackBlob(
            BlobAssetReference<AnimationClipBlob> blob,
            Hash128 hash)
        {
            fallbackBlob = blob;
            fallbackBlobHash = hash;
            return this;
        }

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new BlendGroupTimer { FallbackAccumulatedTime = 0f });

            var activeFallback = new FallbackBlend
            {
                ClipHash = fallbackClipHash,
                BlendInSpeed = blendInSpeed,
                BlendOutSpeed = blendOutSpeed,
                PlaybackMode = playbackMode,
                LayerIndex = 0,
                BlendMode = AnimationBlendingMode.Override,
                AvatarMaskHash = default
            };

            builder.AddComponent(activeFallback);

            builder.AddComponent(new DefaultBlendGroupFallback
            {
                ClipHash = fallbackClipHash,
                BlendInSpeed = blendInSpeed,
                BlendOutSpeed = blendOutSpeed,
                PlaybackMode = playbackMode,
                LayerIndex = 0,
                BlendMode = AnimationBlendingMode.Override,
                AvatarMaskHash = default
            });

            if (fallbackBlob.IsCreated)
            {
                var dbBuffer = builder.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>();
                dbBuffer.Add(new NewBlobAssetDatabaseRecord<AnimationClipBlob>
                {
                    hash = fallbackBlobHash,
                    value = fallbackBlob
                });
            }

            builder.AddBuffer<BlendGroupEntry>();
            builder.AddBuffer<SmoothBlendGroupEntry>();
            builder.AddBuffer<BlendTreePlaybackStateElement>();
        }
    }
}
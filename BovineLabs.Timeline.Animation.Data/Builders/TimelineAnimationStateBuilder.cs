using BovineLabs.Core.EntityCommands;
using Rukhanka;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Animation.Data.Builders
{
    public struct TimelineAnimationStateBuilder
    {
        private Hash128 fallbackClipHash;
        private float blendInSpeed;
        private float blendOutSpeed;
        private BlobAssetReference<AnimationClipBlob> fallbackBlob;
        private Hash128 fallbackBlobHash;

        public TimelineAnimationStateBuilder WithFallback(
            Hash128 clipHash,
            float blendInDuration,
            float blendOutDuration)
        {
            fallbackClipHash = clipHash;
            blendInSpeed = 1f / math.max(0.001f, blendInDuration);
            blendOutSpeed = 1f / math.max(0.001f, blendOutDuration);
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

            builder.AddComponent(new BlendGroupFallBackForNoAnimationToProcessComponent
            {
                ClipHash = fallbackClipHash,
                BlendInSpeed = blendInSpeed,
                BlendOutSpeed = blendOutSpeed
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
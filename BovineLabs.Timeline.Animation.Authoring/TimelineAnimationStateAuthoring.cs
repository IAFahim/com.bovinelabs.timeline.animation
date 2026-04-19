using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Animation.Data.Builders;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public class TimelineAnimationStateAuthoring : MonoBehaviour
    {
        [Tooltip("The animation to play when no timeline clips are active.")]
        public AnimationClip fallbackAnimationClip;

        [Tooltip("Time in seconds to smoothly transition into a new timeline clip.")] [Min(0.001f)]
        public float blendInDuration = 0.25f;

        [Tooltip("Time in seconds to smoothly transition out of a timeline clip.")] [Min(0.001f)]
        public float blendOutDuration = 0.25f;

        public class Baker : Baker<TimelineAnimationStateAuthoring>
        {
            public override void Bake(TimelineAnimationStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var rigDef = GetComponent<RigDefinitionAuthoring>();
                var avatar = rigDef != null ? rigDef.GetAvatar() : null;

                var commands = new BakerCommands(this, entity);
                var builder = new TimelineAnimationStateBuilder()
                    .WithFallback(default, authoring.blendInDuration, authoring.blendOutDuration);

                if (authoring.fallbackAnimationClip != null)
                {
                    var (fallbackHash, fallbackBlob) = BakeFallbackAnimation(authoring, avatar, entity);
                    builder.WithFallback(fallbackHash, authoring.blendInDuration, authoring.blendOutDuration)
                        .WithFallbackBlob(fallbackBlob, fallbackHash);
                }

                builder.ApplyTo(ref commands);
            }

            private (Hash128 hash, BlobAssetReference<AnimationClipBlob> blob) BakeFallbackAnimation(
                TimelineAnimationStateAuthoring authoring, Avatar avatar, Entity entity)
            {
                var fallbackHash = BakingUtils.ComputeAnimationHash(authoring.fallbackAnimationClip, avatar);
                var animationBaker = new AnimationClipBaker();
                var bakedAnimations = animationBaker.BakeAnimations(this, new[] { authoring.fallbackAnimationClip },
                    avatar, authoring.gameObject);

                BlobAssetReference<AnimationClipBlob> fallbackBlob = default;

                if (bakedAnimations is { IsCreated: true, Length: > 0 } &&
                    bakedAnimations[0] != BlobAssetReference<AnimationClipBlob>.Null)
                    fallbackBlob = bakedAnimations[0];

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
                return (fallbackHash, fallbackBlob);
            }
        }
    }
}
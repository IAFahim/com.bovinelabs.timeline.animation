using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Animation;
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

                Hash128 fallbackHash = default;

                if (authoring.fallbackAnimationClip != null)
                    fallbackHash = BakeFallBackAnimationCLip(authoring, avatar, entity);

                AddComponent(entity, new BlendGroupTimer { FallbackAccumulatedTime = 0f });

                AddComponent(entity, new BlendGroupFallBackForNoAnimationToProcessComponent
                {
                    ClipHash = fallbackHash,
                    BlendInSpeed = 1f / Mathf.Max(0.001f, authoring.blendInDuration),
                    BlendOutSpeed = 1f / Mathf.Max(0.001f, authoring.blendOutDuration)
                });

                AddBuffer<BlendGroupEntry>(entity);
                AddBuffer<SmoothBlendGroupEntry>(entity);
            }

            private Hash128 BakeFallBackAnimationCLip(TimelineAnimationStateAuthoring authoring, Avatar avatar,
                Entity entity)
            {
                Hash128 fallbackHash;
                fallbackHash = BakingUtils.ComputeAnimationHash(authoring.fallbackAnimationClip, avatar);
                var animationBaker = new AnimationClipBaker();
                var bakedAnimations = animationBaker.BakeAnimations(this, new[] { authoring.fallbackAnimationClip },
                    avatar, authoring.gameObject);

                var dbBuffer = AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>(entity);
                if (bakedAnimations is { IsCreated: true, Length: > 0 } &&
                    bakedAnimations[0] != BlobAssetReference<AnimationClipBlob>.Null)
                    dbBuffer.Add(new NewBlobAssetDatabaseRecord<AnimationClipBlob>
                        { hash = fallbackHash, value = bakedAnimations[0] });

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
                return fallbackHash;
            }
        }
    }
}
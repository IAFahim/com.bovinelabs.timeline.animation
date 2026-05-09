using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.PlayerInputs.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public sealed class BlendTree2DClip : DOTSClip, ITimelineClipAsset
    {
        public float2 BlendParameter;
        public BlendDirectionReadKind ReadKind;
        public EntityLinkSchema ReadFrom;

        [Header("Clip Transform Offsets")]
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 eulerAnglesOffset = Vector3.zero;
        [Space][Tooltip("Removes the starting offset of the animation so it begins exactly at the track's offset.")]
        public bool removeStartOffset = true;
        public bool applyFootIK = true;

        public ClipCaps clipCaps => ClipCaps.All;

#if UNITY_EDITOR
        /// <summary>
        /// In edit mode, return an empty AnimationMixerPlayable as a dummy node.
        /// BlendTree2D clips are driven by ECS at runtime — there is no single clip
        /// to preview for the editor PlayableGraph. The track mixer still provides
        /// track-level offsets, but clip contents are DOTS-only.
        /// </summary>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (!Application.isPlaying)
            {
                // Return an empty mixer — no animation data to preview per-clip for blend trees
                return AnimationMixerPlayable.Create(graph, 0);
            }

            return base.CreatePlayable(graph, owner);
        }
#endif

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (!TryGetReadEntity(context, out var readEntity)) return;

            context.Baker.AddComponent(clipEntity, new BlendTree2DDirectionClipData
            {
                Value = BlendParameter,
                ReadKind = ReadKind,
                ReadEntity = readEntity,
                ClipIn = (float)context.Clip.clipIn,
                TimeScale = (float)context.Clip.timeScale,
                PositionOffset = positionOffset,
                RotationOffset = Quaternion.Euler(eulerAnglesOffset),
                RemoveStartOffset = removeStartOffset,
                ApplyFootIK = applyFootIK
            });

            base.Bake(clipEntity, context);
        }

        private bool TryGetReadEntity(BakingContext context, out Entity entity)
        {
            entity = context.Target;
            if (ReadKind == BlendDirectionReadKind.ClipValue) return true;
            if (ReadFrom == null)
            {
                Debug.LogError($"{nameof(BlendTree2DClip)} '{name}' needs {nameof(ReadFrom)}.");
                return false;
            }

            switch (ReadKind)
            {
                case BlendDirectionReadKind.PhysicsLinearVelocityNormalized:
                    return TryGetLinkedComponent<PhysicsBodyAuthoring>(context, out entity);
                case BlendDirectionReadKind.PlayerMoveInput:
                    return TryGetLinkedComponent<InputConsumerAuthoring>(context, out entity);
                default:
                    Debug.LogError($"{nameof(BlendTree2DClip)} '{name}' has invalid {nameof(ReadKind)}.");
                    return false;
            }
        }

        private bool TryGetLinkedComponent<T>(BakingContext context, out Entity entity) where T : Component
        {
            entity = Entity.Null;
            if (!context.TryResolveLinkComponent<T>(ReadFrom, out var component))
            {
                Debug.LogError($"{nameof(BlendTree2DClip)} '{name}' could not resolve '{ReadFrom.name}' with {typeof(T).Name}.");
                return false;
            }
            entity = context.Baker.GetEntity(component, TransformUsageFlags.None);
            return entity != Entity.Null;
        }
    }
}

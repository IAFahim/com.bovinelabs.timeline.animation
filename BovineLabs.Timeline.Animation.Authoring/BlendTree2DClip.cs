using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.PlayerInputs.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public sealed class BlendTree2DClip : DOTSClip, ITimelineClipAsset
    {
        public float2 BlendParameter;
        public BlendDirectionReadKind ReadKind;
        public EntityLinkSchema ReadFrom;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (!TryGetReadEntity(context, out var readEntity)) return;

            context.Baker.AddComponent(clipEntity, new BlendTree2DDirectionClipData
            {
                Value = BlendParameter,
                ReadKind = ReadKind,
                ReadEntity = readEntity
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

        private bool TryGetLinkedComponent<T>(BakingContext context, out Entity entity)
            where T : Component
        {
            entity = Entity.Null;

            if (!context.TryResolveLinkComponent<T>(ReadFrom, out var component))
            {
                Debug.LogError(
                    $"{nameof(BlendTree2DClip)} '{name}' could not resolve '{ReadFrom.name}' with {typeof(T).Name}.");
                return false;
            }

            entity = context.Baker.GetEntity(component, TransformUsageFlags.None);
            return entity != Entity.Null;
        }
    }
}
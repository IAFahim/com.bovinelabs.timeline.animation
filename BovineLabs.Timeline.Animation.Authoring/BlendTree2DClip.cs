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
            if (!this.TryGetReadEntity(context, out var readEntity))
            {
                return;
            }

            context.Baker.AddComponent(clipEntity, new BlendTree2DDirectionClipData
            {
                Value = this.BlendParameter,
                ReadKind = this.ReadKind,
                ReadEntity = readEntity
            });

            base.Bake(clipEntity, context);
        }

        private bool TryGetReadEntity(BakingContext context, out Entity entity)
        {
            entity = context.Target;

            if (this.ReadKind == BlendDirectionReadKind.ClipValue)
            {
                return true;
            }

            if (this.ReadFrom == null)
            {
                Debug.LogError($"{nameof(BlendTree2DClip)} '{this.name}' needs {nameof(this.ReadFrom)}.");
                return false;
            }

            switch (this.ReadKind)
            {
                case BlendDirectionReadKind.PhysicsLinearVelocityNormalized:
                    return this.TryGetLinkedComponent<PhysicsBodyAuthoring>(context, out entity);

                case BlendDirectionReadKind.PlayerMoveInput:
                    return this.TryGetLinkedComponent<InputConsumerAuthoring>(context, out entity);

                default:
                    Debug.LogError($"{nameof(BlendTree2DClip)} '{this.name}' has invalid {nameof(this.ReadKind)}.");
                    return false;
            }
        }

        private bool TryGetLinkedComponent<T>(BakingContext context, out Entity entity)
            where T : Component
        {
            entity = Entity.Null;

            if (!context.TryResolveLinkComponent<T>(this.ReadFrom, out var component))
            {
                Debug.LogError($"{nameof(BlendTree2DClip)} '{this.name}' could not resolve '{this.ReadFrom.name}' with {typeof(T).Name}.");
                return false;
            }

            entity = context.Baker.GetEntity(component, TransformUsageFlags.None);
            return entity != Entity.Null;
        }
    }
}

using System;
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
    public class BlendTree2DClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The X/Y direction to feed into the Blend Tree (e.g., Velocity X/Z). Used when ReadKind is ClipValue.")]
        public float2 BlendParameter;

        [Tooltip("How blend direction is resolved. ClipValue uses the BlendParameter field. PhysicsLinearVelocityNormalized reads from the linked entity's velocity. PlayerMoveInput reads from the linked entity's move input.")]
        public BlendDirectionReadKind ReadKind;

        [Tooltip("EntityLink schema to resolve the read target entity. Required when ReadKind is not ClipValue.")]
        public EntityLinkTagSchema ReadFrom;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var valueTarget = context.Target;

            if (ReadKind != BlendDirectionReadKind.ClipValue)
            {
                if (ReadFrom == null)
                {
                    Debug.LogError(
                        $"[BlendTree2DClip] {ReadKind} selected on clip '{name}' but ReadFrom schema is null. Skipping clip data.");
                    return;
                }

                var binding = context.Director.GetGenericBinding(context.Track);
                var targetGo = binding switch
                {
                    GameObject go => go,
                    Component comp => comp.gameObject,
                    _ => null
                };

                if (targetGo == null)
                {
                    Debug.LogError(
                        $"[BlendTree2DClip] No track binding found for clip '{name}'. Skipping clip data.");
                    return;
                }
                var root = targetGo.transform.root;

                var registry = root.GetComponent<EntityLinkRegistryAuthoring>();

                if (registry == null)
                {
                    Debug.LogError(
                        $"[BlendTree2DClip] Track binding '{root.name}' is missing EntityLinkRegistryAuthoring component. Add it directly to the bound GameObject. Skipping clip data.");
                    return;
                }

                EntityTagAuthoring linkedTag = null;
                foreach (var tagAuth in registry.entityTagAuthorings)
                {
                    if (tagAuth != null && tagAuth.entityLinkTagSchema == ReadFrom)
                    {
                        linkedTag = tagAuth;
                        break;
                    }
                }

                if (linkedTag == null)
                {
                    Debug.LogError(
                        $"[BlendTree2DClip] Required link '{ReadFrom.name}' missing in EntityLinkRegistryAuthoring on '{root.name}'. Add the link to the registry. Skipping clip data.");
                    return;
                }

                if (ReadKind == BlendDirectionReadKind.PhysicsLinearVelocityNormalized)
                {
                    if (linkedTag.GetComponent<PhysicsBodyAuthoring>() == null)
                    {
                        Debug.LogError(
                            $"[BlendTree2DClip] Linked object '{linkedTag.gameObject.name}' for {ReadFrom.name} is missing expected PhysicsBodyAuthoring. Skipping clip data.");
                        return;
                    }
                }
                else if (ReadKind == BlendDirectionReadKind.PlayerMoveInput)
                {
                    if (linkedTag.GetComponent<InputConsumerAuthoring>() == null)
                    {
                        Debug.LogError(
                            $"[BlendTree2DClip] Linked object '{linkedTag.gameObject.name}' for {ReadFrom.name} is missing expected InputConsumerAuthoring. Skipping clip data.");
                        return;
                    }
                }

                valueTarget = context.Baker.GetEntity(linkedTag.gameObject, TransformUsageFlags.None);
            }

            context.Baker.AddComponent(clipEntity, new BlendTree2DDirectionClipData
            {
                Value = BlendParameter,
                ReadKind = ReadKind,
                ReadEntity = valueTarget
            });

            base.Bake(clipEntity, context);
        }
    }
}

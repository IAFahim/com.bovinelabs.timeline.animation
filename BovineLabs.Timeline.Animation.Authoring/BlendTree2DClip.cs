using System;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring.Extensions;
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
        [Tooltip(
            "The X/Y direction to feed into the Blend Tree (e.g., Velocity X/Z). Used when ReadKind is ClipValue.")]
        public float2 BlendParameter;

        [Tooltip(
            "How blend direction is resolved. ClipValue uses the BlendParameter field. PhysicsLinearVelocityNormalized reads from the linked entity's velocity. PlayerMoveInput reads from the linked entity's move input.")]
        public BlendDirectionReadKind ReadKind;

        [Tooltip("EntityLink schema to resolve the read target entity. Required when ReadKind is not ClipValue.")]
        public SourceSchema ReadFrom;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var valueTarget = context.Target;

            if (ReadKind != BlendDirectionReadKind.ClipValue)
            {
                if (!context.TryResolveLink(ReadFrom, out var linkedGo))
                {
                    Debug.LogError($"[BlendTree2DClip] Missing link '{ReadFrom?.name}' on '{name}'. Skip.");
                    return;
                }

                if (ReadKind == BlendDirectionReadKind.PhysicsLinearVelocityNormalized &&
                    linkedGo.GetComponent<PhysicsBodyAuthoring>() == null)
                {
                    Debug.LogError($"[BlendTree2DClip] '{linkedGo.name}' missing PhysicsBodyAuthoring. Skip.");
                    return;
                }

                if (ReadKind == BlendDirectionReadKind.PlayerMoveInput &&
                    linkedGo.GetComponent<InputConsumerAuthoring>() == null)
                {
                    Debug.LogError($"[BlendTree2DClip] '{linkedGo.name}' missing InputConsumerAuthoring. Skip.");
                    return;
                }

                valueTarget = context.Baker.GetEntity(linkedGo, TransformUsageFlags.None);
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
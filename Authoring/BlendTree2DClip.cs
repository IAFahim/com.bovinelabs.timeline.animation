using System;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Animation;
using PlayerInputs.PlayerInputs.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public class BlendTree2DClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The X/Y direction to feed into the Blend Tree (e.g., Velocity X/Z)")]
        public float2 BlendParameter;

        public BlendDirectionComponentTarget blendDirectionComponentTarget;

        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var valueTarget = context.Target;
            switch (blendDirectionComponentTarget)
            {
                case BlendDirectionComponentTarget.BlendTree2DDirectionClipData:
                    break;
                case BlendDirectionComponentTarget.PhysicsLinearVelocityNormalized:
                {
                    var binding = context.Director.GetGenericBinding(context.Track);
                    var targetGo = binding switch
                    {
                        GameObject go => go,
                        Component comp => comp.gameObject,
                        _ => null
                    };
                    var physicsBody = targetGo?.transform.root.GetComponentInChildren<PhysicsBodyAuthoring>();
                    if (physicsBody != null)
                        valueTarget = context.Baker.GetEntity(physicsBody.gameObject, TransformUsageFlags.None);
                    break;
                }
                case BlendDirectionComponentTarget.PlayerMoveInput:
                {
                    var binding = context.Director.GetGenericBinding(context.Track);
                    var targetGo = binding switch
                    {
                        GameObject go => go,
                        Component comp => comp.gameObject,
                        _ => null
                    };
                    var playerInputAuth = targetGo?.transform.root.GetComponentInChildren<PlayerInputAuthoring>();
                    if (playerInputAuth != null)
                        valueTarget = context.Baker.GetEntity(playerInputAuth.gameObject, TransformUsageFlags.None);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            context.Baker.AddComponent(clipEntity, new BlendTree2DDirectionClipData
            {
                Value = BlendParameter,
                BlendDirectionComponentTarget = blendDirectionComponentTarget,
                BlendDirectionEntityTarget = valueTarget
            });

            base.Bake(clipEntity, context);
        }
    }
}
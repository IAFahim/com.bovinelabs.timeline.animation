To provide a robust Edit Mode preview for your designers, we don't actually need
to run the full ECS runtime simulation in the editor. Instead, we can hook
directly into Unity's native PlayableGraph system during Edit Mode. This allows
Timeline to preview the animations and move the GameObjects accurately while
scrubbing, exactly as the built-in Animation track does.

To achieve this:

1.  For RukhankaAnimationClip, we apply both the Track and Clip offsets to a
    Unity AnimationPlayableAsset during CreatePlayable().
2.  For BlendTree2DClip, instead of returning an empty mixer, we build a nested
    AnimationMixerPlayable containing all the motions of the blend tree.
3.  For BlendTree2DTrack, we inject a custom ScriptPlayable during
    CreateTrackMixer() that uses Rukhanka's static ScriptedAnimator math to
    calculate and apply the correct blend weights based on the clip's
    BlendParameter every frame in the Editor.

Here are the updated files to accomplish this:

1. BovineLabs.Timeline.Animation.Authoring/RukhankaAnimationClip.cs

Updates the clip to find its parent track and apply the combined offsets
natively in Edit Mode.

using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
public class RukhankaAnimationClip : DOTSClip, ITimelineClipAsset
{
[Tooltip("The animation clip to play when this timeline clip is active.")]
public AnimationClip animationClipHolder;

        [Header("Clip Transform Offsets")]
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 eulerAnglesOffset = Vector3.zero;
        
        [Space][Tooltip("Removes the starting offset of the animation so it begins exactly at the track's offset.")]
        public bool removeStartOffset = true;
        public bool applyFootIK = true;

        public override double duration => animationClipHolder != null ? animationClipHolder.length : base.duration;
        public ClipCaps clipCaps => ClipCaps.All;

#if UNITY_EDITOR
private RukhankaAnimationTrack GetParentTrack(GameObject owner)
{
var director = owner.GetComponent<PlayableDirector>();
if (director != null && director.playableAsset != null)
{
foreach (var track in director.playableAsset.GetOutputTracks())
{
if (track is RukhankaAnimationTrack tTrack)
{
foreach (var c in track.GetClips())
{
if (c.asset == this) return tTrack;
}
}
}
}
return null;
}

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (!Application.isPlaying && animationClipHolder != null)
            {
                var asset = ScriptableObject.CreateInstance<UnityEngine.Timeline.AnimationPlayableAsset>();
                asset.clip = animationClipHolder;
                asset.applyFootIK = applyFootIK;
                asset.removeStartOffset = removeStartOffset;

                var parentTrack = GetParentTrack(owner);
                if (parentTrack != null && parentTrack.trackOffset == TrackOffset.ApplyTransformOffsets)
                {
                    var trackRot = Quaternion.Euler(parentTrack.eulerAnglesOffset);
                    asset.position = parentTrack.positionOffset + (trackRot * positionOffset);
                    asset.eulerAngles = (trackRot * Quaternion.Euler(eulerAnglesOffset)).eulerAngles;
                }
                else
                {
                    asset.position = positionOffset;
                    asset.eulerAngles = eulerAnglesOffset;
                }

                return asset.CreatePlayable(graph, owner);
            }

            return base.CreatePlayable(graph, owner);
        }
#endif

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (animationClipHolder != null)
            {
                Avatar avatar = null;
                var rigDef = context.Director.ResolveRigDefinition(context.Track);
                if (rigDef != null) avatar = rigDef.GetAvatar();

                context.Baker.AddComponent(clipEntity, new RukhankaSingleClipData
                {
                    ClipHash = BakingUtils.ComputeAnimationHash(animationClipHolder, avatar),
                    ClipIn = (float)context.Clip.clipIn,
                    TimeScale = (float)context.Clip.timeScale,
                    PreExtrapolation = context.Clip.preExtrapolationMode,
                    PostExtrapolation = context.Clip.postExtrapolationMode,
                    
                    PositionOffset = positionOffset,
                    RotationOffset = Quaternion.Euler(eulerAnglesOffset),
                    RemoveStartOffset = removeStartOffset,
                    ApplyFootIK = applyFootIK
                });
            }

            base.Bake(clipEntity, context);
        }
    }
}

2. BovineLabs.Timeline.Animation.Authoring/BlendTree2DClip.cs

Constructs a native AnimationMixerPlayable mapping all target animations defined
in the parent track.

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
private BlendTree2DTrack GetParentTrack(GameObject owner)
{
var director = owner.GetComponent<PlayableDirector>();
if (director != null && director.playableAsset != null)
{
foreach (var track in director.playableAsset.GetOutputTracks())
{
if (track is BlendTree2DTrack tTrack)
{
foreach (var c in track.GetClips())
{
if (c.asset == this) return tTrack;
}
}
}
}
return null;
}

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (!Application.isPlaying)
            {
                var parentTrack = GetParentTrack(owner);
                if (parentTrack != null && parentTrack.Motions.Count > 0)
                {
                    var mixer = AnimationMixerPlayable.Create(graph, parentTrack.Motions.Count);

                    var finalPosOffset = positionOffset;
                    var finalRotOffset = Quaternion.Euler(eulerAnglesOffset);

                    if (parentTrack.trackOffset == TrackOffset.ApplyTransformOffsets)
                    {
                        var trackRot = Quaternion.Euler(parentTrack.eulerAnglesOffset);
                        finalPosOffset = parentTrack.positionOffset + (trackRot * positionOffset);
                        finalRotOffset = trackRot * finalRotOffset;
                    }

                    for (int i = 0; i < parentTrack.Motions.Count; i++)
                    {
                        var motion = parentTrack.Motions[i];
                        if (motion.clip != null)
                        {
                            var asset = ScriptableObject.CreateInstance<UnityEngine.Timeline.AnimationPlayableAsset>();
                            asset.clip = motion.clip;
                            asset.applyFootIK = applyFootIK;
                            asset.removeStartOffset = removeStartOffset;
                            asset.position = finalPosOffset;
                            asset.eulerAngles = finalRotOffset.eulerAngles;

                            var clipPlayable = asset.CreatePlayable(graph, owner);
                            graph.Connect(clipPlayable, 0, mixer, i);
                            mixer.SetInputWeight(i, 0f); // Default to 0 weight
                        }
                    }

                    return mixer;
                }

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

3. BovineLabs.Timeline.Animation.Authoring/BlendTree2DTrack.cs

Hooks in a ScriptPlayable Custom Behaviour during Edit Mode that evaluates the
dynamic blend-tree logic frame by frame and adjusts weights accordingly using
Rukhanka's static calculation methods.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BovineLabs.Core.PropertyDrawers;
using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Authoring
{
[Serializable][TrackClipType(typeof(BlendTree2DClip))][TrackColor(0.20f, 0.70f, 0.85f)]
[TrackBindingType(typeof(Animator))][DisplayName("BovineLabs/Animation/Blend Tree 2D")]
public class BlendTree2DTrack : DOTSTrack
{
[Tooltip("Blend tree algorithm: SimpleDirectional for 1D-like with a center, FreeformCartesian for 2D positions, FreeformDirectional for 2D with polar handling.")]
public MotionBlob.Type BlendTreeType = MotionBlob.Type.BlendTree2DSimpleDirectional;

        [Tooltip("Layer index for multi-track blending. 0 = base layer, 1+ = additive/override layers.")]
        public int LayerIndex;

        [Header("Track Offsets")]
        public TrackOffset trackOffset = TrackOffset.ApplyTransformOffsets;
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 eulerAnglesOffset = Vector3.zero;

        [Header("Avatar Mask")]
        public AvatarMask avatarMask;
        public bool applyAvatarMask = true;

        [Header("Exit / Fallback Override (Optional)")][Tooltip("Animation clip to play as fallback when no timeline clips are active on this track's target.")]
        public AnimationClip ExitIdleClip;

        [Tooltip("Time in seconds to blend into this fallback clip.")][Min(0.001f)]
        public float BlendInDuration = 0.25f;

        [Tooltip("Time in seconds to blend out of this fallback clip.")] [Min(0.001f)]
        public float BlendOutDuration = 0.25f;

        [Tooltip("How the fallback animation wraps.")]
        public FallbackPlaybackMode FallbackPlaybackMode = FallbackPlaybackMode.Loop;

        [Tooltip("Motion entries that define the blend tree. Each entry maps an animation clip to a 2D direction/position.")]
        public List<BlendTree2DMotionEntry> Motions = new();

#if UNITY_EDITOR
public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
{
if (!Application.isPlaying)
{
var mixer = AnimationMixerPlayable.Create(graph, inputCount);

                var director = go.GetComponent<PlayableDirector>();
                var binding = director != null ? director.GetGenericBinding(this) as Animator : null;
                if (binding != null)
                {
                    binding.cullingMode = 0; // AlwaysAnimate
                    var output = AnimationPlayableOutput.Create(graph, name, binding);
                    output.SetSourcePlayable(mixer);
                    output.SetWeight(1.0f);
                }

                // Add a script playable to drive the weights inside the nested BlendTree mixer during Edit Mode!
                var behaviour = new BlendTree2DTrackPreviewBehaviour { track = this, trackMixer = mixer };
                var scriptPlayable = ScriptPlayable<BlendTree2DTrackPreviewBehaviour>.Create(graph, behaviour);
                
                var dummyOutput = ScriptPlayableOutput.Create(graph, name + "_Dummy");
                dummyOutput.SetSourcePlayable(scriptPlayable);

                return mixer;
            }

            return base.CreateTrackMixer(graph, go, inputCount);
        }
#endif

        protected override void Bake(BakingContext context)
        {
            var director = context.Director;
            var rigDef = director.ResolveRigDefinition(this);

            if (rigDef == null)
            {
                Debug.LogWarning($"[BlendTree2DTrack] '{name}' has no RigDefinitionAuthoring binding — animation data will not be baked.");
                base.Bake(context);
                return;
            }

            var baker = context.Baker;
            var trackEntity = context.TrackEntity;
            var avatar = rigDef.GetAvatar();

            Hash128 avatarMaskHash = default;
            if (applyAvatarMask && avatarMask != null)
            {
                var maskBaker = new AvatarMaskBaker();
                var maskBlob = maskBaker.CreateAvatarMaskBlob(baker, avatarMask, rigDef);
                avatarMaskHash = maskBlob.Value.hash;
                baker.AddBuffer<AvatarMaskBakingData>(trackEntity).Add(new AvatarMaskBakingData { rigEntity = trackEntity, dataBlob = maskBlob });
            }

            baker.AddComponent(trackEntity, new BlendAnimationTree2DTrackData 
            { 
                BlendTreeType = BlendTreeType, 
                LayerIndex = LayerIndex,
                TrackPositionOffset = trackOffset == TrackOffset.ApplyTransformOffsets ? positionOffset : Vector3.zero,
                TrackRotationOffset = trackOffset == TrackOffset.ApplyTransformOffsets ? Quaternion.Euler(eulerAnglesOffset) : Quaternion.identity,
                ApplyAvatarMask = applyAvatarMask,
                AvatarMaskHash = avatarMaskHash
            });

            var motionBuffer = baker.AddBuffer<BlendTree2DMotionData>(trackEntity);
            var clipsToBake = new List<AnimationClip>();
            var index = 0;

            foreach (var motion in Motions)
            {
                if (motion.clip == null) continue;
                motion.CalcDirection();
                motionBuffer.Add(new BlendTree2DMotionData
                {
                    AnimationHash = BakingUtils.ComputeAnimationHash(motion.clip, avatar),
                    BlendTree2DMotionElement = new ScriptedAnimator.BlendTree2DMotionElement { pos = motion.directionCalc, motionIndex = index++ }
                });
                clipsToBake.Add(motion.clip);
            }

            if (ExitIdleClip != null)
            {
                baker.AddComponent(trackEntity, new TrackFallbackOverride
                {
                    FallbackClipHash = BakingUtils.ComputeAnimationHash(ExitIdleClip, avatar),
                    BlendInSpeed = 1f / Mathf.Max(0.001f, BlendInDuration),
                    BlendOutSpeed = 1f / Mathf.Max(0.001f, BlendOutDuration),
                    PlaybackMode = FallbackPlaybackMode,
                    LayerIndex = LayerIndex,
                    BlendMode = AnimationBlendingMode.Override,
                    AvatarMaskHash = avatarMaskHash,
                    PositionOffset = trackOffset == TrackOffset.ApplyTransformOffsets ? positionOffset : Vector3.zero,
                    RotationOffset = trackOffset == TrackOffset.ApplyTransformOffsets ? Quaternion.Euler(eulerAnglesOffset) : Quaternion.identity,
                    RemoveStartOffset = true,
                    ApplyFootIK = true
                });
                clipsToBake.Add(ExitIdleClip);
            }

            if (clipsToBake.Count > 0)
            {
                var bakedAnimations = new AnimationClipBaker().BakeAnimations(baker, clipsToBake.ToArray(), avatar, rigDef.gameObject);
                var e = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, name + "_BlendTreeAssets");
                var dbBuffer = baker.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>(e);
                dbBuffer.AddValidAnimations(bakedAnimations);

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
            }

            base.Bake(context);
        }

        [Serializable]
        public class BlendTree2DMotionEntry
        {
            [Tooltip("Animation clip for this motion entry.")]
            public AnimationClip clip;

            [Tooltip("Direction angle in degrees. 0 = forward, 90 = right, -90 = left, 180 = backward.")][Range(-180, 180)]
            public float degreeCalc;

            [Tooltip("Distance from origin in the blend space. Controls how far this motion extends.")]
            public float rangeCalc = 1;

            [Tooltip("Computed direction vector (auto-calculated from degree and range).")] [InspectorReadOnly]
            public Vector2 directionCalc;

            internal Vector2 CalcDirection()
            {
                var radians = degreeCalc * Mathf.Deg2Rad;
                directionCalc = new Vector2(Mathf.Sin(radians) * rangeCalc, Mathf.Cos(radians) * rangeCalc);
                return directionCalc;
            }
        }
    }

#if UNITY_EDITOR
public class BlendTree2DTrackPreviewBehaviour : PlayableBehaviour
{
public BlendTree2DTrack track;
public AnimationMixerPlayable trackMixer;
private Unity.Collections.NativeArray<ScriptedAnimator.BlendTree2DMotionElement> positions;

        public override void OnPlayableCreate(Playable playable)
        {
            if (track == null || track.Motions == null || track.Motions.Count == 0) return;

            positions = new Unity.Collections.NativeArray<ScriptedAnimator.BlendTree2DMotionElement>(
                track.Motions.Count, Unity.Collections.Allocator.Persistent);

            for (int m = 0; m < track.Motions.Count; m++)
            {
                track.Motions[m].CalcDirection();
                positions[m] = new ScriptedAnimator.BlendTree2DMotionElement
                {
                    pos = track.Motions[m].directionCalc,
                    motionIndex = m
                };
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (positions.IsCreated) positions.Dispose();
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (Application.isPlaying || !positions.IsCreated) return;

            int inputCount = trackMixer.GetInputCount();
            var clips = System.Linq.Enumerable.ToArray(track.GetClips());

            for (int i = 0; i < inputCount; i++)
            {
                if (i >= clips.Length) break;
                
                float trackInputWeight = trackMixer.GetInputWeight(i);
                if (trackInputWeight <= 0f) continue;

                var clipPlayable = trackMixer.GetInput(i);
                if (!clipPlayable.IsValid()) continue;

                AnimationMixerPlayable blendMixer = default;
                if (clipPlayable.IsPlayableOfType<AnimationMixerPlayable>()) blendMixer = (AnimationMixerPlayable)clipPlayable;
                else if (clipPlayable.GetInputCount() > 0)
                {
                    var inner = clipPlayable.GetInput(0);
                    if (inner.IsValid() && inner.IsPlayableOfType<AnimationMixerPlayable>()) blendMixer = (AnimationMixerPlayable)inner;
                }

                if (!blendMixer.IsValid() || !(clips[i].asset is BlendTree2DClip btClip)) continue;

                Unity.Collections.NativeList<ScriptedAnimator.MotionIndexAndWeight> weights = default;
                switch (track.BlendTreeType)
                {
                    case MotionBlob.Type.BlendTree2DSimpleDirectional:
                        weights = ScriptedAnimator.ComputeBlendTree2DSimpleDirectional(positions, btClip.BlendParameter);
                        break;
                    case MotionBlob.Type.BlendTree2DFreeformCartesian:
                        weights = ScriptedAnimator.ComputeBlendTree2DFreeformCartesian(positions, btClip.BlendParameter);
                        break;
                    case MotionBlob.Type.BlendTree2DFreeformDirectional:
                        weights = ScriptedAnimator.ComputeBlendTree2DFreeformDirectional(positions, btClip.BlendParameter);
                        break;
                }

                int blendInputCount = blendMixer.GetInputCount();
                for (int m = 0; m < blendInputCount; m++) blendMixer.SetInputWeight(m, 0f);

                if (weights.IsCreated)
                {
                    foreach (var w in weights)
                    {
                        if (w.motionIndex < blendInputCount) blendMixer.SetInputWeight(w.motionIndex, w.weight);
                    }
                    weights.Dispose();
                }
            }
        }
    }
#endif
}

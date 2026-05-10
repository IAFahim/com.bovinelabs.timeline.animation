#if UNITY_EDITOR
using Unity.Entities;
using BovineLabs.Timeline;
using Rukhanka;
using System.Collections.Generic;

namespace BovineLabs.Timeline.Animation.Editor
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(TimelineAnimationUnificationSystem))]
    public partial class EditorRukhankaAnimationGroup : ComponentSystemGroup
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(EditorRukhankaAnimationGroup))]
    [UpdateBefore(typeof(AnimationApplicationSystem))]
    public partial struct EditorAnimationProcessWrapper : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateBefore(typeof(TimelineAnimationUnificationSystem))]
    public partial struct DisableAnimationCullingInEditorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (cull, entity) in SystemAPI.Query<RefRW<CullAnimationsTag>>().WithEntityAccess())
            {
                SystemAPI.SetComponentEnabled<CullAnimationsTag>(entity, false);
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    [CreateAfter(typeof(TimelineComponentAnimationGroup))]
    public partial class EditorPreviewBootstrap : SystemBase
    {
        private readonly List<SystemHandle> registeredSystems = new();

        protected override void OnCreate()
        {
            var timelineGroup = World.GetOrCreateSystemManaged<TimelineComponentAnimationGroup>();

            // Timeline clip systems
            SystemHandle[] timelineSystems =
            {
                World.GetOrCreateSystem<DisableAnimationCullingInEditorSystem>(),
                World.GetOrCreateSystem<TimelineAnimationBlendTree2DTrackSystem>(),
                World.GetOrCreateSystem<TimelineSingleAnimationTrackSystem>(),
                World.GetOrCreateSystem<TimelineAnimationUnificationSystem>(),
            };

            foreach (var sys in timelineSystems)
            {
                timelineGroup.AddSystemToUpdateList(sys);
                registeredSystems.Add(sys);
            }

            // Rukhanka ECS bone pipeline in nested group with guaranteed ordering
            var rukhankaGroup = World.GetOrCreateSystemManaged<EditorRukhankaAnimationGroup>();
            timelineGroup.AddSystemToUpdateList((ComponentSystemBase)rukhankaGroup);
            registeredSystems.Add(rukhankaGroup.SystemHandle);

            var aps = World.GetOrCreateSystem<AnimationProcessSystem>();
            var eaw = World.GetOrCreateSystem<EditorAnimationProcessWrapper>();
            var aas = World.GetOrCreateSystem<AnimationApplicationSystem>();

            rukhankaGroup.AddSystemToUpdateList(aps);
            rukhankaGroup.AddSystemToUpdateList(eaw);
            rukhankaGroup.AddSystemToUpdateList(aas);
            rukhankaGroup.SortSystems();

            timelineGroup.SortSystems();
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (World.IsCreated && registeredSystems.Count > 0)
            {
                try
                {
                    var timelineGroup = World.GetExistingSystemManaged<TimelineComponentAnimationGroup>();
                    if (timelineGroup != null)
                    {
                        foreach (var sys in registeredSystems)
                            timelineGroup.RemoveSystemFromUpdateList(sys);
                    }
                }
                catch
                {
                }
            }
            registeredSystems.Clear();
        }

        
        protected override void OnUpdate() { }
    }
}
#endif

#if UNITY_EDITOR
using Unity.Entities;
using BovineLabs.Timeline;
using Rukhanka;

namespace BovineLabs.Timeline.Animation.Editor
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)][UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial class EditorPreviewBootstrap : SystemBase
    {
        protected override void OnCreate()
        {
            var timelineGroup = World.GetOrCreateSystemManaged<TimelineComponentAnimationGroup>();
            
            var blendTreeSys = World.GetOrCreateSystem<TimelineAnimationBlendTree2DTrackSystem>();
            var singleTrackSys = World.GetOrCreateSystem<TimelineSingleAnimationTrackSystem>();
            var unificSys = World.GetOrCreateSystem<TimelineAnimationUnificationSystem>();
            var disableCull = World.GetOrCreateSystem<DisableAnimationCullingInEditorSystem>();
            var aps = World.GetOrCreateSystem<AnimationProcessSystem>();
            var aas = World.GetOrCreateSystem<AnimationApplicationSystem>();
            
            timelineGroup.AddSystemToUpdateList(blendTreeSys);
            timelineGroup.AddSystemToUpdateList(singleTrackSys);
            timelineGroup.AddSystemToUpdateList(unificSys);
            timelineGroup.AddSystemToUpdateList(disableCull);
            timelineGroup.AddSystemToUpdateList(aps);
            timelineGroup.AddSystemToUpdateList(aas);

            timelineGroup.SortSystems();
            Enabled = false;
        }

        protected override void OnUpdate() { }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)][UpdateBefore(typeof(AnimationProcessSystem))]
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
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
}
#endif
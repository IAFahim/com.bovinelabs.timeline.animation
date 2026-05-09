using BovineLabs.Timeline.Animation.Authoring;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Editor
{
    [CustomTimelineEditor(typeof(RukhankaAnimationTrack))]
    public class RukhankaAnimationTrackEditor : TrackEditor
    {
        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.trackColor = new Color(0.16f, 0.54f, 0.88f); // Matches Unity's native animation blue
            return options;
        }
    }

    [CustomTimelineEditor(typeof(RukhankaAnimationClip))]
    public class RukhankaAnimationClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            var options = base.GetClipOptions(clip);
            var asset = clip.asset as RukhankaAnimationClip;
            
            // Set the tooltip to the name of the assigned animation clip
            if (asset != null && asset.animationClipHolder != null)
                options.tooltip = asset.animationClipHolder.name;
                
            return options;
        }
    }
}
using BovineLabs.Timeline.Animation.Authoring;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Animation.Editor
{
    [CustomEditor(typeof(RukhankaAnimationClip))]
    [CanEditMultipleObjects]
    public class RukhankaAnimationClipInspector : UnityEditor.Editor
    {
        private SerializedProperty m_AnimationClipHolder;
        private SerializedProperty m_ApplyFootIK;
        private SerializedProperty m_EulerAnglesOffset;
        private SerializedProperty m_PositionOffset;
        private SerializedProperty m_RemoveStartOffset;

        private void OnEnable()
        {
            m_AnimationClipHolder = serializedObject.FindProperty("animationClipHolder");
            m_PositionOffset = serializedObject.FindProperty("positionOffset");
            m_EulerAnglesOffset = serializedObject.FindProperty("eulerAnglesOffset");
            m_RemoveStartOffset = serializedObject.FindProperty("removeStartOffset");
            m_ApplyFootIK = serializedObject.FindProperty("applyFootIK");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_AnimationClipHolder, new GUIContent("Animation Clip"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clip Transform Offsets", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_PositionOffset, new GUIContent("Position"));
            EditorGUILayout.PropertyField(m_EulerAnglesOffset, new GUIContent("Rotation"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_RemoveStartOffset, new GUIContent("Remove Start Offset"));
            EditorGUILayout.PropertyField(m_ApplyFootIK, new GUIContent("Foot IK"));
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
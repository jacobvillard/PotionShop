using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PotiondexSettings))]
public sealed class PotiondexSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear Tracked Unlocks")) PotiondexProgress.ClearAll();
        if (GUILayout.Button("Test Brew Health Potion")) PotiondexProgress.UnlockForTesting("Health");
    }
}

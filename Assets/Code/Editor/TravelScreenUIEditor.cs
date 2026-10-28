using UnityEditor;
using UnityEngine;
using TravelApp;

[CustomEditor(typeof(TravelScreenUI))]
public sealed class TravelScreenUIEditor : Editor
{
    TravelPage preview;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Every screen is saved in this hierarchy. Preview a screen below, then select its text, images, inputs or buttons to edit them. The Initial Page field controls what opens when playing.", MessageType.Info);
        preview = (TravelPage)EditorGUILayout.EnumPopup("Preview screen", preview);
        if (GUILayout.Button("Show selected screen in Scene / Game view"))
        {
            var screen = (TravelScreenUI)target;
            var targets = screen.pages.CastObjects().ConcatObjects(screen,screen.heading,screen.subtitle);
            Undo.RecordObjects(targets, "Preview travel screen");
            screen.PreviewPage(preview);
            foreach (var obj in targets) { EditorUtility.SetDirty(obj); if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Open shared AppShell prefab")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/AppShell.prefab"));
        EditorGUILayout.Space(); DrawDefaultInspector();
    }
}
static class PreviewObjectExtensions
{
    public static UnityEngine.Object[] CastObjects(this GameObject[] objects) => System.Array.ConvertAll(objects, o => (UnityEngine.Object)o);
    public static UnityEngine.Object[] ConcatObjects(this UnityEngine.Object[] objects, params UnityEngine.Object[] more)
    {
        var result = new UnityEngine.Object[objects.Length+more.Length]; objects.CopyTo(result,0); more.CopyTo(result,objects.Length); return result;
    }
}

[CustomEditor(typeof(TravelItemList))]
public sealed class TravelItemListEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("The sample card previews the layout in edit mode. Open the item prefab to change the appearance of all real items.", MessageType.Info);
        if (GUILayout.Button("Open reusable item prefab"))
        {
            var list = (TravelItemList)target;
            if (list.itemPrefab != null) AssetDatabase.OpenAsset(list.itemPrefab.gameObject);
        }
        DrawDefaultInspector();
    }
}

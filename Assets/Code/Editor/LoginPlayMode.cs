using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only convenience; no runtime redirect, so direct scene testing remains supported.
[InitializeOnLoad]
public static class LoginPlayMode
{
    static LoginPlayMode()
    {
        EditorApplication.delayCall+=Restore;
        EditorApplication.playModeStateChanged+=state=>{
            if(state==PlayModeStateChange.ExitingEditMode && SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(component=>component!=null && component.GetType().FullName=="UnityEngine.TestTools.TestRunner.PlaymodeTestsController"))
                EditorSceneManager.playModeStartScene=null; // Let the test runner launch its bootstrap scene.
            if(state==PlayModeStateChange.EnteredEditMode)Restore();
        };
    }
    static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Login.unity");
    }
    [MenuItem("Travel App/Play Current Scene")]
    static void Current(){EditorSceneManager.playModeStartScene=null;EditorApplication.EnterPlaymode();}
    [MenuItem("Travel App/Play Current Scene",true)]
    static bool CurrentAvailable()=>!EditorApplication.isPlayingOrWillChangePlaymode;
}

using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TravelPlanning.UI.Shared
{
    /// <summary>Loads the screens once, binds before Start, and preserves the account session in Login.</summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class TravelSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private string[] featureScenes = { "Registration", "Home", "Flights", "Destinations", "Reviews", "SavedTrips", "Notifications" };
        private TravelSceneBindings bindings;
        public bool IsReady { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RedirectStandaloneScreen()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (new[] { "Registration", "Home", "Flights", "Destinations", "Reviews", "SavedTrips", "Notifications" }.Contains(scene)
                && !TravelSceneBindings.Find<TravelSceneBootstrap>())
                SceneManager.LoadScene("Login");
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += HideLoadedScreen;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HideLoadedScreen;
        }

        private static void HideLoadedScreen(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Canvas>()) root.SetActive(false);
        }

        private void StartupFailed(Exception error)
        {
            Debug.LogError("TRAVEL_SCENE_STARTUP_FAILED " + error.Message);
            var loginRoot = SceneManager.GetSceneByName("Login").GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
            if (loginRoot)
            {
                var label = loginRoot.transform.Find("Right Panel/LoginCard/AccountFeedback");
                if (label) label.GetComponent<TMP_Text>().text = "The travel screens could not be opened. Please restart the app.";
            }
        }

        private IEnumerator Start()
        {
            foreach (string sceneName in featureScenes)
                if (!SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    AsyncOperation loading = null;
                    try { loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
                    catch (Exception error) { StartupFailed(error); }
                    if (loading == null) yield break;
                    yield return loading;
                }
            try { bindings = BindLoadedScenesForEditor(); }
            catch (Exception error) { StartupFailed(error); }
            if (bindings == null) yield break;
            foreach (var canvas in bindings.Canvases) canvas.SetActive(canvas == bindings.LoginCanvas);
            bindings.Account.LoginPanel.SetActive(true);
            bindings.Account.RegistrationPanel.SetActive(false);
            bindings.Account.HomePanel.SetActive(false);
            bindings.EnableControllers();
            IsReady = true;
            Debug.Log("SCENE_BOOTSTRAP_READY loaded=" + bindings.Canvases.Length);
        }

        public static TravelSceneBindings BindLoadedScenesForEditor()
        {
            var result = new TravelSceneBindings();
            result.ConfigureAll();
            return result;
        }

        private void LateUpdate()
        {
            if (!IsReady) return;
            // Modals have higher canvas sorting orders than the screen beneath them.
            GameObject visible = null;
            int highestOrder = int.MinValue;
            foreach (var canvas in bindings.Canvases)
            {
                if (!canvas.activeInHierarchy) continue;
                int order = canvas.GetComponent<Canvas>().sortingOrder;
                if (order > highestOrder)
                {
                    highestOrder = order;
                    visible = canvas;
                }
            }
            if (visible && visible.scene.IsValid() && visible.scene.isLoaded && SceneManager.GetActiveScene() != visible.scene)
                SceneManager.SetActiveScene(visible.scene);
        }
    }
}

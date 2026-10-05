using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds account behavior to Kevin's imported scene, preserving his login layout.</summary>
    public static class AuthSetup
    {
        public const string ScenePath = "Assets/TravelPlanning/Scenes/Login.unity";
        public const string BuildPath = "Builds/Authentication/TravelPlannerAuthentication.exe";
#region Prepare
        [MenuItem("Travel Planning/Authentication/1 - Prepare Login Scene")]
        public static void Prepare()
        {
            if (MultiSceneSetup.OpenMigratedProject()) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            TravelSeedBuilder.BuildSeed();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<LoginPage>() != null)
            {
                // Include inactive registration cards when refreshing policy text in a prepared scene.
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        if (text.name == "PasswordPolicy" && text.transform.parent.name == "RegistrationCard")
                            text.text = "Use 10-128 characters. Spaces are welcome.\nThis account is stored only on this computer.";
                EditorSceneManager.SaveScene(scene);
                return;
            }

            Transform card = GameObject.Find("Canvas/Right Panel/LoginCard").transform;
            var email = card.Find("EmailInput").GetComponent<TMP_InputField>();
            var password = card.Find("PasswordInput").GetComponent<TMP_InputField>();
            email.characterLimit = 254;
            password.characterLimit = 128;
            password.contentType = TMP_InputField.ContentType.Password;
            var submit = card.Find("LoginButton").GetComponent<UnityEngine.UI.Button>();
            var create = TextButton(card.Find("Create an account"));
            var forgot = TextButton(card.Find("Forgot?"));
            var feedback = Text(card, "AccountFeedback", "", new Vector2(0, -600), new Vector2(580, 90), 22);
            var registration = UnityEngine.Object.Instantiate(card.gameObject, card.parent);
            registration.name = "RegistrationCard";
            Transform form = registration.transform;
            form.GetComponent<RectTransform>().sizeDelta = new Vector2(580, 800);
            form.Find("Welcome back").GetComponent<TMP_Text>().text = "Create account";
            form.Find("Subtitle").GetComponent<TMP_Text>().text = "Save your trips on this computer";
            UnityEngine.Object.DestroyImmediate(form.Find("Forgot?").gameObject);
            UnityEngine.Object.DestroyImmediate(form.Find("First Time?").gameObject);
            var registrationEmail = form.Find("EmailInput").GetComponent<TMP_InputField>();
            var registrationPassword = form.Find("PasswordInput").GetComponent<TMP_InputField>();
            Move(form.Find("EmailLabel"), -184);
            form.Find("EmailLabel").GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            Move(form.Find("PasswordLabel"), -285);
            form.Find("PasswordLabel").GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            Move(registrationPassword.transform, -330);
            var confirmation = UnityEngine.Object.Instantiate(registrationPassword, form);
            confirmation.name = "ConfirmationInput";
            Move(confirmation.transform, -425);
            var confirmationLabel = UnityEngine.Object.Instantiate(form.Find("PasswordLabel").gameObject, form);
            confirmationLabel.name = "ConfirmationLabel";
            confirmationLabel.GetComponent<TMP_Text>().text = "Confirm password";
            Move(confirmationLabel.transform, -380);
            confirmationLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            var registrationSubmit = form.Find("LoginButton").GetComponent<UnityEngine.UI.Button>();
            Move(registrationSubmit.transform, -550);
            registrationSubmit.GetComponentInChildren<TMP_Text>().text = "Create account";
            var back = form.Find("Create an account").GetComponent<UnityEngine.UI.Button>();
            back.GetComponent<TMP_Text>().text = "Back to sign in";
            Move(back.transform, -615);
            Text(form, "PasswordPolicy", "Use 10-128 characters. Spaces are welcome.\nThis account is stored only on this computer.", new Vector2(0,
                -465), new Vector2(580, 50), 20);
            var registerFeedback = form.Find("AccountFeedback").GetComponent<TMP_Text>();
            Move(registerFeedback.transform, -665);
            var signedIn = new GameObject("SignedInCard", typeof(RectTransform));
            signedIn.transform.SetParent(card.parent, false);
            CopyRect(card.GetComponent<RectTransform>(), signedIn.GetComponent<RectTransform>());
            Text(signedIn.transform, "Title", "Welcome", new Vector2(0, -65), new Vector2(580, 80), 48);
            var accountEmail = Text(signedIn.transform, "AccountEmail", "", new Vector2(0, -180), new Vector2(580, 80), 28);
            Text(signedIn.transform, "NextMilestone", "You are signed in.\nFlight search is the next milestone.", new Vector2(0, -290), new Vector2(580, 100), 26);
            var logout = UnityEngine.Object.Instantiate(submit, signedIn.transform);
            logout.name = "LogoutButton";
            logout.GetComponentInChildren<TMP_Text>().text = "Log out";
            Move(logout.transform, -460);
            var controller = new GameObject("AccountController").AddComponent<LoginPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "loginPanel", card.gameObject);
            Set(fields, "registrationPanel", registration);
            Set(fields, "signedInPanel", signedIn);
            Set(fields, "emailField", email);
            Set(fields, "passwordField", password);
            Set(fields, "registrationEmail", registrationEmail);
            Set(fields, "registrationPassword", registrationPassword);
            Set(fields, "confirmationField", confirmation);
            Set(fields, "feedback", feedback);
            Set(fields, "registrationFeedback", registerFeedback);
            Set(fields, "signedInEmail", accountEmail);
            Set(fields, "submitButton", submit);
            Set(fields, "switchButton", create);
            Set(fields, "registerButton", registrationSubmit);
            Set(fields, "backButton", back);
            Set(fields, "logoutButton", logout);
            Set(fields, "forgotButton", forgot);
            fields.ApplyModifiedPropertiesWithoutUndo();
            registration.SetActive(false);
            signedIn.SetActive(false);
            var events = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            foreach (var module in events.GetComponents<BaseInputModule>())
                UnityEngine.Object.DestroyImmediate(module);
            events.gameObject.AddComponent<StandaloneInputModule>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AUTH_SCENE_READY " + ScenePath);
        }

#endregion
#region BuildWindows
        [MenuItem("Travel Planning/Authentication/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            if (MultiSceneSetup.IsMigrated)
            {
                ReleaseSetup.BuildWindows();
                return;
            }
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Authentication build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("AUTH_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

#endregion
#region TextButton
        private static UnityEngine.UI.Button TextButton(Transform target)
        {
            var graphic = target.GetComponent<TMP_Text>();
            graphic.raycastTarget = true;
            var button = target.GetComponent<UnityEngine.UI.Button>() ?? target.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            return button;
        }

#endregion
#region Text
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            text.text = value;
            text.fontSize = fontSize;
            text.color = new Color32(35, 43, 57, 255);
            text.raycastTarget = false;
            return text;
        }

#endregion
#region Move
        private static void Move(Transform target, float y)
        {
            var r = target.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, y);
        }

#endregion
#region CopyRect
        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
        }

#endregion
#region Set
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
#endregion
    }
}

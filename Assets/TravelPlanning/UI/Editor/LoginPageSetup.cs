using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TravelPlanning.UI.Editor
{
    /// <summary>A one-time scene builder, like assembling a form from ready-made pieces.</summary>
    public static class LoginPageSetup
    {
        [MenuItem("Travel Planning/Create Login Scene")]
        public static void CreateScene()
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogError("Import TMP Essential Resources first (Window > TextMeshPro > Import TMP Essential Resources), then create the login scene again.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasObject = new GameObject("LoginCanvas", typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 800);
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(canvasObject.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(0.07f, 0.12f, 0.20f);
            background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;

            var form = new GameObject("LoginForm", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(LoginPage));
            form.transform.SetParent(canvasObject.transform, false);
            var rectangle = form.GetComponent<RectTransform>();
            rectangle.anchorMin = rectangle.anchorMax = new Vector2(0.5f, 0.5f);
            rectangle.sizeDelta = new Vector2(480, 690);
            form.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            var layout = form.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 24, 24);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text(form.transform, "Brand", "TRAVEL PLANNER", 20, 32);
            var heading = Text(form.transform, "Heading", "Welcome back", 30, 48);
            Text(form.transform, "LocalNote", "Your account stays on this computer.", 17, 32);
            var email = Input(form.transform, "Email", "Email address", false);
            var password = Input(form.transform, "Password", "Password (8-128 characters)", true);
            var confirmation = Input(form.transform, "Confirmation", "Confirm password", true);
            var submit = Button(form.transform, "Submit", "Log in");
            var switchForm = Button(form.transform, "SwitchForm", "New here? Create an account");
            var feedback = Text(form.transform, "Feedback", "", 18, 78);
            var logout = Button(form.transform, "Logout", "Log out");

            var controller = new SerializedObject(form.GetComponent<LoginPage>());
            Set(controller, "emailField", email);
            Set(controller, "passwordField", password);
            Set(controller, "confirmationField", confirmation);
            Set(controller, "heading", heading);
            Set(controller, "feedback", feedback);
            Set(controller, "submitButton", submit);
            Set(controller, "submitLabel", submit.GetComponentInChildren<TMP_Text>());
            Set(controller, "switchButton", switchForm);
            Set(controller, "switchLabel", switchForm.GetComponentInChildren<TMP_Text>());
            Set(controller, "logoutButton", logout);
            controller.ApplyModifiedPropertiesWithoutUndo();
            confirmation.gameObject.SetActive(false);
            logout.gameObject.SetActive(false);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            if (!AssetDatabase.IsValidFolder("Assets/TravelPlanning/Scenes"))
                AssetDatabase.CreateFolder("Assets/TravelPlanning", "Scenes");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TravelPlanning/Scenes/Login.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = form;
            Debug.Log("Login scene saved to " + path + ". Press Play to register or log in.");
        }

        private static TMP_Text Text(Transform parent, string name, string value, int size, int height)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.08f, 0.13f, 0.22f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.richText = false;
            Height(item, height);
            return text;
        }

        private static TMP_InputField Input(Transform parent, string name, string placeholder, bool password)
        {
            var item = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            item.name = name;
            item.transform.SetParent(parent, false);
            Height(item, 52);
            var input = item.GetComponent<TMP_InputField>();
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.characterLimit = password ? 128 : 254;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.fontSize = 18;
            input.textComponent.richText = false;
            var label = (TMP_Text)input.placeholder;
            label.text = placeholder;
            label.fontSize = 17;
            return input;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, string label)
        {
            var item = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            item.name = name;
            item.transform.SetParent(parent, false);
            Height(item, 48);
            var text = item.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 18;
            text.color = Color.white;
            text.raycastTarget = false;
            item.GetComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.30f, 0.52f);
            return item.GetComponent<UnityEngine.UI.Button>();
        }

        private static void Height(GameObject item, float height)
        {
            var layout = item.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
        }

        private static void Stretch(RectTransform rectangle)
        {
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject target, string field, Object value)
        {
            target.FindProperty(field).objectReferenceValue = value;
        }
    }
}

using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TravelPlanning.Tests
{
    public sealed class AuthenticationSceneTests
    {
#region KevinLoginGeometryAndLegacyInputArePreserved
        [Test]
        public void KevinLoginGeometryAndLegacyInputArePreserved()
        {
            ApplicationSceneFixture.Open();
            var card = GameObject.Find("Canvas/Right Panel/LoginCard").transform;
            Assert.That(card.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(580, 700)));
            Assert.That(card.Find("EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
            Assert.That(card.Find("PasswordInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -355)));
            Assert.That(card.Find("LoginButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -460)));
            Assert.That(card.Find("Welcome back").GetComponent<TMP_Text>().text.Trim(), Is.EqualTo("Welcome back"));
            Assert.That(GameObject.Find("Canvas").GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(Object.FindFirstObjectByType<EventSystem>().GetComponent<StandaloneInputModule>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>().GetComponents<BaseInputModule>().Length, Is.EqualTo(1));
        }

#endregion
#region AccountControllerHasEveryReferenceAndLivesOutsideSwitchablePanels
        [Test]
        public void AccountControllerHasEveryReferenceAndLivesOutsideSwitchablePanels()
        {
            ApplicationSceneFixture.Open();
            var page = Object.FindFirstObjectByType<LoginPage>();
            Assert.That(page.transform.parent, Is.Null);
            var serialized = new SerializedObject(page);
            foreach (string name in new[]
            {
                "loginPanel",
                "registrationPanel",
                "signedInPanel",
                "emailField",
                "passwordField",
                "registrationEmail",
                "registrationPassword",
                "confirmationField",
                "feedback",
                "registrationFeedback",
                "signedInEmail",
                "submitButton",
                "switchButton",
                "registerButton",
                "backButton",
                "logoutButton",
                "forgotButton"
            }

            )
                Assert.That(serialized.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }

#endregion
#region FormSwitchingAndEmptySubmissionStayResponsive
        [UnityTest]
        public IEnumerator FormSwitchingAndEmptySubmissionStayResponsive()
        {
            ApplicationSceneFixture.Open();
            string file = "auth-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = file;
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var page = Object.FindFirstObjectByType<LoginPage>();
            // Enter/Exit Play mode reloads the C# domain; recover the serialized test file before cleanup.
            string activeTestFile = new SerializedObject(page).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(activeTestFile, Does.StartWith("auth-editor-"));
            Assert.That(System.IO.Path.GetFileName(activeTestFile), Is.EqualTo(activeTestFile));
            float deadline = Time.realtimeSinceStartup + 30;
            while (!page.IsReady && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(page.IsReady, Is.True);
            var right = GameObject.Find("Canvas/Right Panel").transform;
            var login = right.Find("LoginCard");
            var registration = page.RegistrationPanel.transform;
            login.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(registration.gameObject.activeSelf, Is.True);
            Assert.That(login.gameObject.activeSelf, Is.False);
            registration.Find("LoginButton").GetComponent<Button>().onClick.Invoke();
            while (page.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(page.IsBusy, Is.False);
            Assert.That(registration.Find("AccountFeedback").GetComponent<TMP_Text>().text, Is.Not.Empty);
            Assert.That(page.SignedInEmail, Is.Null);
            registration.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(login.gameObject.activeSelf, Is.True);
            Object.Destroy(page.gameObject);
            yield return null;
            login.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(registration.gameObject.activeSelf, Is.False, "Destroyed controllers must detach their button listeners.");
            string testPath = System.IO.Path.Combine(Application.persistentDataPath, activeTestFile);
            Assert.That(System.IO.File.Exists(testPath), Is.True, "The isolated test database should have been created.");
            System.IO.File.Delete(testPath);
            yield return new ExitPlayMode();
        }
#endregion
    }
}

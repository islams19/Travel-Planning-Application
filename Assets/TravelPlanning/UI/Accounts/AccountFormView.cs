using TMPro;
using UnityEngine;

namespace TravelPlanning.UI.Accounts
{
    /// <summary>Changes account panels and input state; account operations remain in LoginPage.</summary>
    internal sealed class AccountFormView
    {
        private readonly GameObject loginPanel, registrationPanel, signedInPanel;
        private readonly TMP_InputField[] loginFields, registrationFields;
        private readonly TMP_Text feedback, registrationFeedback, signedInEmail;
        private readonly UnityEngine.UI.Button[] buttons;
        public AccountFormView(GameObject loginPanel, GameObject registrationPanel, GameObject signedInPanel,
            TMP_InputField[] loginFields, TMP_InputField[] registrationFields, TMP_Text feedback, TMP_Text registrationFeedback,
            TMP_Text signedInEmail, UnityEngine.UI.Button[] buttons)
        {
            this.loginPanel = loginPanel;
            this.registrationPanel = registrationPanel;
            this.signedInPanel = signedInPanel;
            this.loginFields = loginFields;
            this.registrationFields = registrationFields;
            this.feedback = feedback;
            this.registrationFeedback = registrationFeedback;
            this.signedInEmail = signedInEmail;
            this.buttons = buttons;
        }

        public void SetBusy(bool busy)
        {
            foreach (var button in buttons)
            {
                button.interactable = !busy;
            }

            foreach (var field in loginFields)
            {
                field.interactable = !busy;
            }

            foreach (var field in registrationFields)
            {
                field.interactable = !busy;
            }
        }

        public void ClearPasswords()
        {
            loginFields[1].text = "";
            registrationFields[1].text = "";
            registrationFields[2].text = "";
        }

        public void ShowRegistration(bool show)
        {
            ClearPasswords();
            registrationFields[0].text = loginFields[0].text;
            feedback.text = registrationFeedback.text = "";
            loginPanel.SetActive(!show);
            registrationPanel.SetActive(show);
            signedInPanel.SetActive(false);
            ShowCanvas(show ? registrationPanel : loginPanel);
        }

        private void ShowCanvas(GameObject selected)
        {
            var target = selected.GetComponentInParent<Canvas>(true);
            foreach (var panel in new[] { loginPanel, registrationPanel, signedInPanel })
            {
                var canvas = panel.GetComponentInParent<Canvas>(true);
                if (canvas && target && canvas != target)
                    canvas.gameObject.SetActive(false);
            }
            if (target) target.gameObject.SetActive(true);
        }

        public void ShowSignedIn(string email)
        {
            signedInEmail.text = email;
            loginPanel.SetActive(false);
            registrationPanel.SetActive(false);
            signedInPanel.SetActive(true);
            ShowCanvas(signedInPanel);
        }
    }
}

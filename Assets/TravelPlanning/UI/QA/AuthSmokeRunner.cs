using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TravelPlanning.UI.QA
{
    /// <summary>Development-only account checks use the real form controls and never log credentials.</summary>
    internal sealed class AuthSmokeRunner
    {
        private readonly LoginPage account;
        private readonly GameObject loginPanel, signedInPanel;
        private readonly TMP_InputField[] loginFields, registrationFields;
        private readonly UnityEngine.UI.Button switchButton, registerButton, submitButton, logoutButton;
        private readonly CancellationToken lifetime;
        public AuthSmokeRunner(LoginPage account, GameObject loginPanel, GameObject signedInPanel, TMP_InputField[] loginFields,
            TMP_InputField[] registrationFields, UnityEngine.UI.Button switchButton, UnityEngine.UI.Button registerButton,
            UnityEngine.UI.Button submitButton, UnityEngine.UI.Button logoutButton, CancellationToken lifetime)
        {
            this.account = account;
            this.loginPanel = loginPanel;
            this.signedInPanel = signedInPanel;
            this.loginFields = loginFields;
            this.registrationFields = registrationFields;
            this.switchButton = switchButton;
            this.registerButton = registerButton;
            this.submitButton = submitButton;
            this.logoutButton = logoutButton;
            this.lifetime = lifetime;
        }

        public async Task RunAsync(bool register)
        {
            try
            {
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                const string email = "ui-smoke@example.test";
                const string password = "Travel demo smoke phrase 2027!";
                if (register)
                {
                    switchButton.onClick.Invoke();
                    registrationFields[0].text = email;
                    registrationFields[1].text = registrationFields[2].text = password;
                    registerButton.onClick.Invoke();
                    await WaitForRequest();
                    if (!loginPanel.activeSelf || account.SignedInEmail != null)
                    {
                        throw new InvalidOperationException();
                    }
                }

                loginFields[0].text = email;
                loginFields[1].text = "Definitely an incorrect password";
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (account.SignedInEmail != null || !loginPanel.activeSelf)
                {
                    throw new InvalidOperationException();
                }

                loginFields[1].text = password;
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (account.SignedInEmail != email || !signedInPanel.activeSelf || loginFields[1].text.Length != 0)
                {
                    throw new InvalidOperationException();
                }

                logoutButton.onClick.Invoke();
                if (account.SignedInEmail != null || !loginPanel.activeSelf)
                {
                    throw new InvalidOperationException();
                }

                Debug.Log("AUTH_UI_SMOKE_PASS registration=" + register + " elapsedMs=" + elapsed.ElapsedMilliseconds);
                Application.Quit(0);
            }
            catch (Exception)
            {
                Debug.LogError("AUTH_UI_SMOKE_FAIL");
                Application.Quit(1);
            }
        }

        private async Task WaitForRequest()
        {
            for (int i = 0; account.IsBusy && i < 3000; i++)
            {
                await Task.Delay(10, lifetime);
            }

            if (account.IsBusy)
            {
                throw new TimeoutException();
            }
        }
    }
}

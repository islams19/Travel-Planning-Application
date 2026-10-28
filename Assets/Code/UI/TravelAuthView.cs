using TMPro;
using UnityEngine;
namespace TravelApp
{
    public sealed class TravelAuthView : MonoBehaviour
    {
        public TMP_InputField loginUsername, loginPassword, registerUsername, registerEmail, registerPassword;
        TravelScreenUI screen;
        public void Bind(TravelScreenUI owner) { screen = owner; }
        public void Login()
        {
            var result = screen.Services.Accounts.Login(loginUsername.text, loginPassword.text); screen.Report(result);
            if (result.success) { ClearPasswords(); TravelAppRuntime.Instance.Navigate("HomeScreen"); }
        }
        public void Register()
        {
            var result = screen.Services.Accounts.Register(registerUsername.text, registerEmail.text, registerPassword.text); screen.Report(result);
            if (result.success) { ClearPasswords(); TravelAppRuntime.Instance.Navigate("HomeScreen"); }
        }
        public void ClearPasswords() { loginPassword.text = ""; registerPassword.text = ""; }
    }
}

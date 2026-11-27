using TMPro;
using System.Linq;
using TravelApp;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class DesktopLoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField username;
    [SerializeField] private TMP_InputField password;
    [SerializeField] private TMP_InputField email;
    [SerializeField] private GameObject emailRow;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private TMP_Text submitLabel;
    [SerializeField] private TMP_Text switchLabel;
    [SerializeField] private TMP_Text status;
    [SerializeField] private UnityEngine.UI.Button submit;
    [SerializeField] private UnityEngine.UI.Button switchMode;
    [SerializeField] private RectTransform passwordLabel;
    [SerializeField] private GameObject firstTime;
    private bool registering;
    private UnityEngine.UI.Button guest;
    private FormFieldFeedback usernameFeedback,emailFeedback,passwordFeedback;

    private void Awake()
    {
        heading.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,580);
        heading.textWrappingMode=TextWrappingModes.NoWrap;
        password.contentType = TMP_InputField.ContentType.Password;
        submit.onClick.AddListener(Submit);
        switchMode.onClick.AddListener(SwitchMode);
        password.onSubmit.AddListener(_ => Submit());
        AlignField(username, "Your username");
        AlignField(password, "Your password");
        AlignField(email, "you@example.com");
        usernameFeedback=FormFieldFeedback.Attach(username,()=>UsernameError());
        emailFeedback=FormFieldFeedback.Attach(email,()=>registering && !EmailAddressValidator.IsValid(email.text)?"Enter a valid email address.":null);
        passwordFeedback=FormFieldFeedback.Attach(password,()=>PasswordError());
        foreach(var field in new[]{username,email,password})field.onValueChanged.AddListener(_=>{
            if(!usernameFeedback.HasError&&!passwordFeedback.HasError&&(!registering||!emailFeedback.HasError))status.text="";});
        username.characterLimit = 40;
        email.characterLimit = 120;
        var guestObject = Instantiate(switchMode.gameObject, switchMode.transform.parent);
        guestObject.name = "Continue as guest";
        guest = guestObject.GetComponent<UnityEngine.UI.Button>();
        guest.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        guest.onClick.AddListener(ContinueAsGuest);
        var guestLabel = guestObject.GetComponent<TMP_Text>();
        guestLabel.text = "Continue as guest";
        guestLabel.alignment = TextAlignmentOptions.Center;
        var guestRect = (RectTransform)guestObject.transform;
        guestRect.anchorMin = guestRect.anchorMax = new Vector2(0.5f, 1);
        guestRect.pivot = new Vector2(0.5f, 0.5f);
        guestRect.anchoredPosition = new Vector2(0, -655);
        guestRect.sizeDelta = new Vector2(580, 44);
        var focus=username.GetComponentInParent<Canvas>().gameObject.AddComponent<KeyboardFocusOrder>();
        focus.Configure(username,email,password,submit,switchMode,guest);
        Refresh();
    }

    private static void AlignField(TMP_InputField field, string placeholderText)
    {
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 128;
        foreach (var label in new[] { field.textComponent, (TMP_Text)field.placeholder })
        {
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 22;
            label.enableAutoSizing = false;
            label.margin = Vector4.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var viewport = field.textViewport;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(18, 0);
        viewport.offsetMax = new Vector2(-18, 0);
        ((TMP_Text)field.placeholder).text = placeholderText;
        field.customCaretColor = true;
        field.caretColor = new Color32(47, 80, 247, 255);
    }

    private void ContinueAsGuest()
    {
        TravelAppRuntime.Instance.Services.Accounts.Logout();
        guest.interactable = false;
        SceneManager.LoadSceneAsync("DesktopLayout");
    }

    private void SwitchMode()
    {
        registering = !registering;
        ClearErrors();
        password.text = "";
        status.text = "";
        Refresh();
    }

    private void Refresh()
    {
        emailRow.SetActive(registering);
        guest.gameObject.SetActive(!registering);
        passwordLabel.anchoredPosition = new Vector2(0, registering ? -420 : -325);
        ((RectTransform)password.transform).anchoredPosition = new Vector2(0, registering ? -450 : -355);
        ((RectTransform)submit.transform).anchoredPosition = new Vector2(0, registering ? -545 : -460);
        firstTime.SetActive(!registering);
        var switchRect = (RectTransform)switchMode.transform;
        switchRect.anchoredPosition = registering ? new Vector2(95, -620) : new Vector2(240, -535);
        switchRect.sizeDelta = new Vector2(registering ? 430 : 250, 50);
        status.rectTransform.anchoredPosition = new Vector2(0, registering ? -670 : -590);
        heading.text = registering ? "Create your account" : "Welcome back";
        subtitle.text = registering ? "Start planning your next adventure." : "Sign in to continue planning your next adventure.";
        submitLabel.text = registering ? "Create account" : "Sign In";
        switchLabel.text = registering ? "Already registered? Sign in" : "Create an account";
    }


    private string UsernameError()
    {
        string value=username.text.Trim();
        if(value.Length==0)return "Enter your username.";
        return registering && (value.Length<3||value.Length>40||value.Any(char.IsControl))?"Use 3–40 characters.":null;
    }
    private string PasswordError()=>string.IsNullOrEmpty(password.text)?"Enter your password.":
        registering && (password.text.Length<4||password.text.Length>128)?"Use 4–128 characters.":null;
    private void ClearErrors()
    {usernameFeedback?.SetError(null);emailFeedback?.SetError(null);passwordFeedback?.SetError(null);}

    private void Submit()
    {
        var runtime = TravelAppRuntime.Instance;
        if (runtime?.Services == null || runtime.IsNavigating)
            return;
        bool validUser=usernameFeedback.Validate();
        bool validEmail=!registering||emailFeedback.Validate();
        bool validPassword=passwordFeedback.Validate();
        if(!validUser||!validEmail||!validPassword)
        {status.text="Check the highlighted fields.";FormFieldFeedback.Focus(!validUser?username:!validEmail?email:password);return;}
        var result = registering
            ? runtime.Services.Accounts.Register(username.text, email.text, password.text)
            : runtime.Services.Accounts.Login(username.text, password.text);
        status.text = result.message;
        if (!result.success)
        {
            if(result.message.Contains("username is already")){usernameFeedback.SetError("That username is already in use.");FormFieldFeedback.Focus(username);}
            else if(result.message.Contains("incorrect"))
            {usernameFeedback.SetError("Check username and password.");passwordFeedback.SetError("Check username and password.");FormFieldFeedback.Focus(username);}
            return;
        }
        ClearErrors();
        if (registering)
        {
            runtime.Services.Accounts.Logout();
            registering = false;
            Refresh();
            status.text = "Account created. Click Sign In to continue.";
            return;
        }
        password.text = "";
        runtime.Navigate("DesktopLayout");
    }
}

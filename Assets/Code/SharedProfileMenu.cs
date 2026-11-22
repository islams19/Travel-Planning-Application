using System;
using System.Linq;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static DateSelectionPage;

// One header menu shared by the desktop pages. AccountService remains the identity source.
public sealed class SharedProfileMenu : MonoBehaviour
{
    private static readonly Color Ink = new Color32(29, 40, 59, 255);
    private static readonly Color Muted = new Color32(96, 113, 139, 255);
    private static readonly Color Blue = new Color32(47, 80, 247, 255);
    private static readonly Color Pale = new Color32(235, 240, 255, 255);
    private GameObject overlay;
    private GameObject actions;
    private GameObject details;
    private TMP_Text identity;
    private TMP_Text email;
    private TMP_Text profileIdentity;
    private TMP_Text profileEmail;
    private TMP_Text signOutLabel;
    private AppServices services;
    public bool IsOpen => overlay != null && overlay.activeSelf;

    public void Build(DateSelectionPage style, SidebarNavigation navigation, Canvas canvas)
    {
        services = TravelAppRuntime.Instance.Services;
        var avatar = canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(image => image.name == "header_avatar");
        var trigger = avatar.GetComponent<UnityEngine.UI.Button>() ?? avatar.gameObject.AddComponent<UnityEngine.UI.Button>();
        avatar.raycastTarget = true;
        trigger.targetGraphic = avatar;
        trigger.onClick.AddListener(Toggle);
        var colors = trigger.colors;
        colors.highlightedColor = new Color32(225, 234, 255, 255);
        colors.pressedColor = new Color32(203, 218, 250, 255);
        trigger.colors = colors;

        var rect = (RectTransform)new GameObject("Shared profile overlay", typeof(RectTransform)).transform;
        rect.gameObject.layer = avatar.gameObject.layer;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay = rect.gameObject;
        var dismiss = Group("Dismiss profile menu", rect);
        var blocker = dismiss.AddComponent<UnityEngine.UI.Image>();
        blocker.color = Color.clear;
        dismiss.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(Close);

        var panel = style.Panel("Profile dropdown", rect, 0, 0, 380, 382);
        var outer = (RectTransform)panel.parent;
        outer.anchorMin = outer.anchorMax = outer.pivot = Vector2.one;
        outer.anchoredPosition = new Vector2(-40, -132);
        panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        identity = Label("Signed-in name", panel, 24, 18, 332, 34, 23, Ink, true);
        email = Label("Signed-in email", panel, 24, 55, 332, 30, 18, Muted);
        style.Shape("Divider", panel, 24, 100, 332, 1.5f, new Color32(221, 228, 239, 255));
        actions = Group("Profile actions", panel);
        style.Control("Profile", actions.transform, 18, 115, 344, 49, "Profile", Pale, Blue, 20, 2)
            .onClick.AddListener(ShowProfile);
        style.Control("Settings", actions.transform, 18, 173, 344, 49, "Settings", Pale, Ink, 20, 2)
            .onClick.AddListener(navigation.OpenSettings);
        style.Control("Saved Trips", actions.transform, 18, 231, 344, 49, "My Trips", Pale, Ink, 20, 2)
            .onClick.AddListener(navigation.OpenSavedTrips);
        var signOut = style.Control("Sign Out", actions.transform, 18, 300, 344, 49, "Sign Out", Color.white,
            new Color32(166, 51, 67, 255), 20, 2);
        signOutLabel = signOut.GetComponentInChildren<TMP_Text>();
        signOut.onClick.AddListener(SignOut);
        details = Group("Profile details", panel);
        Text("Profile heading", details.transform, 24, 116, 332, 35, "Your profile", 23, Ink, true, TextAlignmentOptions.Left);
        Text("Username label", details.transform, 24, 164, 332, 24, "Username", 16, Muted, false, TextAlignmentOptions.Left);
        profileIdentity = Label("Profile username", details.transform, 24, 190, 332, 30, 20, Ink);
        Text("Email label", details.transform, 24, 232, 332, 24, "Email", 16, Muted, false, TextAlignmentOptions.Left);
        profileEmail = Label("Profile email", details.transform, 24, 258, 332, 30, 18, Ink);
        style.Control("Back to profile menu", details.transform, 18, 310, 344, 49, "‹  Back", Pale, Blue, 20, 2)
            .onClick.AddListener(() => { details.SetActive(false); actions.SetActive(true); });
        services.Changed += Refresh;
        Close();
    }

    public void Toggle()
    {
        if (IsOpen) { Close(); return; }
        Refresh();
        details.SetActive(false);
        actions.SetActive(true);
        overlay.transform.SetAsLastSibling();
        overlay.SetActive(true);
    }

    public void Close() { if (overlay != null) overlay.SetActive(false); }
    private void ShowProfile() { actions.SetActive(false); details.SetActive(true); }
    private void Refresh()
    {
        string username = services.Accounts.Username ?? "Guest User";
        string address = services.Accounts.Email;
        identity.text = profileIdentity.text = username;
        email.text = profileEmail.text = string.IsNullOrWhiteSpace(address) ? "Not signed in" : address;
        signOutLabel.text = services.Accounts.IsLoggedIn ? "Sign Out" : "Sign In";
    }

    private void SignOut()
    {
        Close();
        services.Accounts.Logout();
        SceneManager.LoadSceneAsync("Login");
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
    private void OnDestroy()
    {
        if (services != null) services.Changed -= Refresh;
        if (overlay != null) Destroy(overlay);
    }
    private static GameObject Group(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect.gameObject;
    }
    private static TMP_Text Label(string name, Transform parent, float x, float y, float width, float height,
        float size, Color color, bool bold = false)
    {
        var label = Text(name, parent, x, y, width, height, "", size, color, bold, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
}

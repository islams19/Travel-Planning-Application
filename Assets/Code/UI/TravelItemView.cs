using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TravelApp
{
    // Shared card presentation. Edit its prefab to style every catalog, cart or booking item.
    public sealed class TravelItemView : MonoBehaviour
    {
        public TMP_Text eyebrow, title, description, price, primaryLabel, secondaryLabel;
        public Image image;
        public UnityEngine.UI.Button primaryButton, secondaryButton;
        Action primary, secondary;
        void Awake() { primaryButton.onClick.AddListener(Primary); secondaryButton.onClick.AddListener(Secondary); }
        void Primary() { primary?.Invoke(); }
        void Secondary() { secondary?.Invoke(); }
        public void Set(string category, string name, string text, string amount, Sprite sprite, string action, Action callback, string secondAction = null, Action secondCallback = null)
        {
            eyebrow.text = category; title.text = name; description.text = text; price.text = amount;
            image.sprite = sprite; image.enabled = sprite != null;
            primary = callback; secondary = secondCallback;
            primaryButton.gameObject.SetActive(callback != null); primaryLabel.text = action;
            secondaryButton.gameObject.SetActive(secondCallback != null); secondaryLabel.text = secondAction;
        }
    }
}

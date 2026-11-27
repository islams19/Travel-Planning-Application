using System;
using UnityEngine;
using UnityEngine.UI;
using static DateSelectionPage;

public sealed class AppNotification : MonoBehaviour
{
    RectTransform panel;
    TMPro.TMP_Text message;
    UnityEngine.UI.Button action;
    Action onAction;
    float expires;
    public bool IsVisible => panel!=null&&panel.parent.gameObject.activeSelf;
    public static AppNotification Get(DateSelectionPage style,Transform owner)
    {
        var canvas=owner.GetComponentInParent<Canvas>();
        var view=canvas.GetComponent<AppNotification>();if(view!=null)return view;
        view=canvas.gameObject.AddComponent<AppNotification>();
        view.panel=style.Panel("Application notification",canvas.transform,0,0,510,76);
        var frame=(RectTransform)view.panel.parent;frame.anchorMin=frame.anchorMax=Vector2.right;frame.pivot=Vector2.right;
        frame.anchoredPosition=new Vector2(-30,28);
        view.panel.GetComponent<Image>().raycastTarget=true;
        view.message=Text("Notification message",view.panel,20,16,260,42,"",21,new Color32(29,40,59,255),true,TMPro.TextAlignmentOptions.MidlineLeft);
        view.action=style.Control("View My Trips",view.panel,286,18,166,40,"View My Trips",new Color32(235,240,255,255),new Color32(47,80,247,255),18,2);
        view.action.onClick.AddListener(()=>{var callback=view.onAction;view.Hide();callback?.Invoke();});
        style.Control("Dismiss notification",view.panel,466,18,28,40,"×",Color.clear,new Color32(96,113,139,255),25,2).onClick.AddListener(view.Hide);
        view.Hide();return view;
    }
    public void Show(string text,Action selected=null,float seconds=4)
    {
        message.text=text;message.rectTransform.sizeDelta=new Vector2(selected==null?430:260,42);
        message.textWrappingMode=TMPro.TextWrappingModes.Normal;message.enableAutoSizing=true;message.fontSizeMin=16;message.fontSizeMax=21;
        onAction=selected;action.gameObject.SetActive(selected!=null);
        expires=Time.unscaledTime+seconds;panel.parent.SetAsLastSibling();panel.parent.gameObject.SetActive(true);
    }
    public void Hide() {if(panel!=null)panel.parent.gameObject.SetActive(false);onAction=null;}
    void Update() {if(IsVisible&&Time.unscaledTime>=expires)Hide();}
}

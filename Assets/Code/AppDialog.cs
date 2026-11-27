using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using static DateSelectionPage;

// One modal surface on the shared Canvas, with the existing keyboard focus scope.
public sealed class AppDialog : MonoBehaviour
{
    DateSelectionPage style;
    RectTransform overlay,body;
    TMP_Text title,description,error;
    TMP_InputField input;
    Transform actions;
    public string InputText => input.text;
    public bool IsOpen => overlay!=null&&overlay.gameObject.activeSelf;
    public static AppDialog Get(DateSelectionPage style,Transform owner)
    {
        var canvas=owner.GetComponentInParent<Canvas>();var view=canvas.GetComponent<AppDialog>();if(view!=null)return view;
        view=canvas.gameObject.AddComponent<AppDialog>();view.style=style;
        view.overlay=style.Shape("Application dialog",canvas.transform,0,0,1920,1080,new Color(.04f,.08f,.15f,.38f));
        view.overlay.anchorMin=Vector2.zero;view.overlay.anchorMax=Vector2.one;view.overlay.offsetMin=view.overlay.offsetMax=Vector2.zero;
        view.overlay.GetComponent<Image>().raycastTarget=true;view.overlay.gameObject.SetActive(false);
        view.overlay.gameObject.AddComponent<KeyboardFocusScope>();
        view.body=style.Panel("Dialog panel",view.overlay,0,0,800,370);
        var frame=(RectTransform)view.body.parent;frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
        view.body.GetComponent<Image>().raycastTarget=true;
        view.title=Text("Dialog title",view.body,30,25,738,42,"",28,new Color32(29,40,59,255),true,TextAlignmentOptions.MidlineLeft);
        view.description=Text("Dialog description",view.body,30,80,738,66,"",20,new Color32(96,113,139,255),false,TextAlignmentOptions.TopLeft);view.description.textWrappingMode=TextWrappingModes.Normal;view.description.richText=false;
        view.input=CreateInput(style,view.body);
        view.error=Text("Dialog error",view.body,30,224,738,38,"",17,new Color32(168,49,54,255),false,TextAlignmentOptions.MidlineLeft);
        view.actions=new GameObject("Dialog actions",typeof(RectTransform)).transform;view.actions.SetParent(view.body,false);
        var a=(RectTransform)view.actions;a.anchorMin=a.anchorMax=a.pivot=Vector2.up;a.anchoredPosition=new Vector2(30,-280);a.sizeDelta=new Vector2(738,58);
        return view;
    }
    public void Show(string heading,string text,string[] choices,Func<int,bool> choose,string initialInput=null)
    {
        foreach(Transform child in actions){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        title.text=heading;description.text=text;error.text="";
        input.gameObject.SetActive(initialInput!=null);input.SetTextWithoutNotify(initialInput??"");
        // Without a text field, close the gap so the choices sit right under the message.
        bool asking=initialInput!=null;float actionsY=asking?280:210;
        error.rectTransform.anchoredPosition=new Vector2(30,-(actionsY-56));
        ((RectTransform)actions).anchoredPosition=new Vector2(30,-actionsY);
        var frame=(RectTransform)body.parent;frame.sizeDelta=new Vector2(800,actionsY+90);body.sizeDelta=new Vector2(797,actionsY+87);
        for(int i=0;i<choices.Length;i++)
        {
            int index=i;float width=(738-12*(choices.Length-1))/choices.Length;
            var button=style.Control(choices[i],actions,i*(width+12),0,width,56,choices[i],new Color32(235,240,255,255),new Color32(47,80,247,255),18,2);
            var label=button.GetComponentInChildren<TMP_Text>();label.enableAutoSizing=true;label.fontSizeMin=15;label.fontSizeMax=18;
            button.onClick.AddListener(()=>{if(choose(index))Close();});
        }
        overlay.SetAsLastSibling();overlay.gameObject.SetActive(true);
    }
    void Update() {if(IsOpen && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
    public void Error(string message) => error.text=message;
    public void Close()=>overlay.gameObject.SetActive(false);
    static TMP_InputField CreateInput(DateSelectionPage style,Transform parent)
    {
        var rect=style.Shape("Trip name",parent,30,158,738,54,new Color32(241,244,249,255),2);rect.gameObject.SetActive(false);rect.gameObject.AddComponent<RectMask2D>();
        var text=Text("Text",rect,16,0,706,54,"",21,new Color32(29,40,59,255),false,TextAlignmentOptions.Left);
        var hint=Text("Placeholder",rect,16,0,706,54,"Trip name",21,new Color32(96,113,139,255),false,TextAlignmentOptions.Left);
        var field=rect.gameObject.AddComponent<TMP_InputField>();field.textViewport=rect;field.textComponent=text;field.placeholder=hint;field.targetGraphic=rect.GetComponent<Image>();field.targetGraphic.raycastTarget=true;field.characterLimit=60;
        rect.gameObject.SetActive(true);return field;
    }
}

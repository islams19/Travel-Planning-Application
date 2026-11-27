using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Presentation and focus only. Validation rules stay with the existing form/services.
public sealed class FormFieldFeedback : MonoBehaviour
{
    TMP_InputField input;
    FocusOutline border;
    Func<string> validate;
    public TMP_Text Message { get; private set; }
    public bool HasError => !string.IsNullOrEmpty(Message?.text);
    public static FormFieldFeedback Attach(TMP_InputField field,Func<string> validation=null,bool labelRow=false)
    {
        var view=field.GetComponent<FormFieldFeedback>()??field.gameObject.AddComponent<FormFieldFeedback>();
        if(view.Message!=null)return view;
        view.input=field;view.validate=validation;
        var edge=(RectTransform)new GameObject("Invalid field border",typeof(RectTransform),typeof(CanvasRenderer)).transform;
        edge.SetParent(field.transform,false);edge.anchorMin=Vector2.zero;edge.anchorMax=Vector2.one;
        edge.offsetMin=-Vector2.one;edge.offsetMax=Vector2.one;
        view.border=edge.gameObject.AddComponent<FocusOutline>();view.border.color=new Color32(168,49,54,255);view.border.raycastTarget=false;
        view.Message=DateSelectionPage.Text("Field error",field.transform,0,0,0,0,"",14,view.border.color,false,TextAlignmentOptions.MidlineLeft);
        view.Message.font=field.textComponent.font;
        var rect=view.Message.rectTransform;
        if(labelRow)
        {
            rect.anchorMin=new Vector2(.36f,1);rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(0,6);rect.offsetMax=new Vector2(0,38);
            view.Message.alignment=TextAlignmentOptions.MidlineRight;view.Message.fontSize=16;
            view.Message.enableAutoSizing=true;view.Message.fontSizeMin=13;view.Message.fontSizeMax=16;
        }
        else
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,0);
            rect.offsetMin=new Vector2(0,-28);rect.offsetMax=new Vector2(0,-4);
        }
        view.Message.textWrappingMode=TextWrappingModes.Normal;
        field.onValueChanged.AddListener(_=>{if(view.HasError&&view.validate!=null)view.Validate();});
        view.SetError(null);return view;
    }
    public bool Validate() {SetError(validate?.Invoke());return !HasError;}
    public void SetError(string message) {Message.text=message??"";SetBorder(HasError);}
    public void SetBorder(bool invalid) {if(border!=null)border.gameObject.SetActive(invalid);}
    public static void Focus(TMP_InputField field)
    {
        if(KeyboardFocusNavigation.Instance!=null)KeyboardFocusNavigation.Instance.Focus(field);
        else {EventSystem.current?.SetSelectedGameObject(field.gameObject);field.ActivateInputField();}
    }
}

using System;
using System.Linq;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using static DateSelectionPage;

// Account editing and defaults use AccountService and the existing save document.
public sealed class ProfilePage : MonoBehaviour
{
    static readonly Color Ink=new Color32(29,40,59,255),Muted=new Color32(96,113,139,255),Blue=new Color32(47,80,247,255),Pale=new Color32(235,240,255,255);
    DateSelectionPage style;
    AccountService accounts;
    TMP_InputField nameInput,emailInput;
    DestinationAutocomplete home;
    TravelPreferences editing;
    TMP_Text status;
    UnityEngine.UI.Button cabin,seat,distance;
    GameObject popup;
    DropdownChevron expanded;
    KeyboardFocusOrder order;
    Action saved;
    public void Build(DateSelectionPage theme,Action onSaved)
    {
        style=theme;saved=onSaved;accounts=TravelAppRuntime.Instance.Services.Accounts;
        var background=GetComponent<Image>()??gameObject.AddComponent<Image>();background.sprite=null;background.color=new Color32(245,247,250,255);
        Text("Profile introduction",transform,60,40,1400,40,"Your account. Your travel preferences.",28,Ink,true,TextAlignmentOptions.Left);
        Text("Profile guidance",transform,60,88,1400,28,"Set defaults for future trips. Your current itinerary stays as you planned it.",18,Muted,false,TextAlignmentOptions.Left);
        var account=style.Panel("Account information",transform,60,144,650,496);
        Text("Account heading",account,26,24,590,36,"Account information",26,Ink,true,TextAlignmentOptions.Left);
        nameInput=Field(account,"Profile name","Name",26,90,594);
        emailInput=Field(account,"Profile email","Email",26,212,594);
        FormFieldFeedback.Attach(nameInput,()=>string.IsNullOrWhiteSpace(nameInput.text)||nameInput.text.Trim().Length>80?"Enter a name of 1–80 characters.":null);
        FormFieldFeedback.Attach(emailInput,()=>EmailAddressValidator.IsValid(emailInput.text)?null:"Enter a valid email address.");
        Text("Username",account,26,354,594,32,"",18,Muted,false,TextAlignmentOptions.Left);
        var note=Text("Account note",account,26,400,594,68,"",17,Muted,false,TextAlignmentOptions.TopLeft);note.textWrappingMode=TextWrappingModes.Normal;
        var preferences=style.Panel("Travel preferences",transform,748,144,728,590);
        Text("Preferences heading",preferences,26,24,674,36,"Travel preferences",26,Ink,true,TextAlignmentOptions.Left);
        Text("Home airport label",preferences,26,88,674,28,"Preferred home airport",18,Ink,true,TextAlignmentOptions.Left);
        var shape=style.Shape("Preferred home airport",preferences,26,124,674,64,new Color32(241,244,249,255),2);shape.gameObject.SetActive(false);
        home=shape.gameObject.AddComponent<DestinationAutocomplete>();home.ConfigureOrigin(new[]{"New York, New York (JFK)"});shape.gameObject.SetActive(true);
        home.ConfigureCompact(preferences.parent,"Home — city or airport");home.SuggestionsOpened+=ClosePopup;
        cabin=Choice(preferences,"Preferred cabin class",26,242,326,TravelPreferences.Cabins,()=>editing.cabinClass,v=>editing.cabinClass=v);
        seat=Choice(preferences,"Preferred seat type",376,242,326,TravelPreferences.SeatTypes,()=>editing.seatType,v=>editing.seatType=v);
        distance=Choice(preferences,"Distance units",26,368,326,TravelPreferences.Distances,()=>editing.distanceUnits,v=>editing.distanceUnits=v);
        var tip=Text("Preferences note",preferences,26,488,674,72,"Home airport and cabin apply to new trips. Seat preferences highlight matching available seats. Distance units apply to Explore World.",17,Muted,false,TextAlignmentOptions.TopLeft);tip.textWrappingMode=TextWrappingModes.Normal;
        status=Text("Profile status",transform,60,768,1010,76,"",18,Muted,false,TextAlignmentOptions.TopLeft);status.textWrappingMode=TextWrappingModes.Normal;
        var save=style.Control("Save profile",transform,1180,774,296,58,"Save preferences",Blue,Color.white,22,2);save.onClick.AddListener(Save);
        order=gameObject.AddComponent<KeyboardFocusOrder>();order.Configure(nameInput,emailInput,home.GetComponentInChildren<TMP_InputField>(true),cabin,seat,distance,save);
        Reload();
    }
    TMP_InputField Field(Transform parent,string name,string caption,float x,float y,float width)
    {
        Text(name+" label",parent,x,y,width,28,caption,18,Ink,true,TextAlignmentOptions.Left);
        var field=style.Shape(name,parent,x,y+36,width,58,new Color32(241,244,249,255),2);field.gameObject.SetActive(false);
        var view=(RectTransform)new GameObject("Text viewport",typeof(RectTransform),typeof(RectMask2D)).transform;view.SetParent(field,false);view.anchorMin=Vector2.zero;view.anchorMax=Vector2.one;view.offsetMin=new Vector2(16,0);view.offsetMax=new Vector2(-16,0);
        var text=Text("Text",view,0,0,width-32,58,"",22,Ink,false,TextAlignmentOptions.Left);
        var hint=Text("Placeholder",view,0,0,width-32,58,caption,22,Muted,false,TextAlignmentOptions.Left);
        foreach(var label in new[]{text,hint}) {label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.pivot=new Vector2(.5f,.5f);label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;}
        var input=field.gameObject.AddComponent<TMP_InputField>();input.textViewport=view;input.textComponent=text;input.placeholder=hint;input.targetGraphic=field.GetComponent<Image>();input.targetGraphic.raycastTarget=true;input.characterLimit=name.Contains("email")?120:80;input.customCaretColor=true;input.caretColor=Blue;input.caretWidth=2;input.lineType=TMP_InputField.LineType.SingleLine;field.gameObject.SetActive(true);return input;
    }
    UnityEngine.UI.Button Choice(Transform parent,string caption,float x,float y,float width,string[] options,Func<string> current,Action<string> assign)
    {
        Text(caption+" label",parent,x,y,width,28,caption,18,Ink,true,TextAlignmentOptions.Left);
        // Same fill and left alignment as the text fields beside it, so the form reads as one set of inputs.
        var button=style.Control(caption,parent,x,y+36,width,58,"",new Color32(241,244,249,255),Ink,22,2);DropdownChevron.Attach(button,true);
        button.GetComponentInChildren<TMP_Text>().fontStyle=FontStyles.Normal;
        button.onClick.AddListener(()=>{
            bool closing=popup!=null && popup.name==caption+" choices";ClosePopup();home.HideSuggestions();if(closing)return;
            var panel=style.Panel(caption+" choices",parent,x,y+100,width,options.Length*48+12);popup=panel.parent.gameObject;popup.transform.SetAsLastSibling();
            for(int i=0;i<options.Length;i++) {string value=options[i];style.Control("Choose "+value,panel,6,6+i*48,width-12,44,value,value==current()?Blue:Color.white,value==current()?Color.white:Ink,18,2).onClick.AddListener(()=>{assign(value);RefreshChoices();ClosePopup();KeyboardFocusNavigation.Instance?.Focus(button);});}
            order.RegisterPopup(button,popup.transform);
            expanded=DropdownChevron.Attach(button);expanded.SetExpanded(true);
        });return button;
    }
    void RefreshChoices()
    {cabin.GetComponentInChildren<TMP_Text>().text=editing.cabinClass;seat.GetComponentInChildren<TMP_Text>().text=editing.seatType;distance.GetComponentInChildren<TMP_Text>().text=editing.distanceUnits;}
    void ClosePopup() {expanded?.SetExpanded(false);expanded=null;if(popup!=null){order?.ClosingPopup(popup.transform);popup.SetActive(false);Destroy(popup);popup=null;}}
    public void Reload()
    {
        if(accounts==null)return;ClosePopup();editing=accounts.Preferences;
        nameInput.SetTextWithoutNotify(accounts.DisplayName);emailInput.SetTextWithoutNotify(accounts.Email??"");
        nameInput.interactable=emailInput.interactable=accounts.IsLoggedIn;
        nameInput.GetComponent<FormFieldFeedback>().SetError(null);emailInput.GetComponent<FormFieldFeedback>().SetError(null);
        transform.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Username").text="Username · "+(accounts.Username??"Guest");
        transform.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Account note").text=accounts.IsLoggedIn?"Your username stays the same when you update your display name.":"Sign in to edit account details. Guest preferences are saved on this device.";
        home.TrySelectAirportDestination(editing.homeAirportCode);RefreshChoices();status.text="";
    }
    void Save()
    {
        ClosePopup();bool validName=!accounts.IsLoggedIn||nameInput.GetComponent<FormFieldFeedback>().Validate();bool validEmail=!accounts.IsLoggedIn||emailInput.GetComponent<FormFieldFeedback>().Validate();bool validHome=home.ValidateAirport();
        if(!validName||!validEmail||!validHome){if(!validName)FormFieldFeedback.Focus(nameInput);else if(!validEmail)FormFieldFeedback.Focus(emailInput);else home.FocusForCorrection();return;}
        editing.homeAirportCode=home.SelectedAirportCode;
        var result=accounts.SaveProfile(nameInput.text,emailInput.text,editing);status.color=result.success?Muted:new Color32(168,49,54,255);status.text=result.message;
        if(result.success){saved?.Invoke();AppNotification.Get(style,transform).Show("Preferences saved");}
    }
    void Update(){if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)ClosePopup();}
    void OnEnable(){if(accounts!=null)Reload();}
    void OnDisable(){ClosePopup();home?.HideSuggestions();}
}

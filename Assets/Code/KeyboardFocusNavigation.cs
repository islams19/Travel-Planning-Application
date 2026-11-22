using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// One input service over existing Selectables. It owns no form values or booking state.
[DefaultExecutionOrder(10000)]
public sealed class KeyboardFocusNavigation : MonoBehaviour
{
    public static KeyboardFocusNavigation Instance { get; private set; }
    FocusOutline ring;
    Selectable outlined;
    bool keyboardMode;
    readonly List<KeyboardFocusScope> modals=new List<KeyboardFocusScope>();
    bool enterPending;
    void Awake() {Instance=this;}
    void OnDestroy() {if(Instance==this)Instance=null;}
    void LateUpdate()
    {
        if(EventSystem.current==null)return;
        var keyboard=Keyboard.current;
        if(enterPending)
        {
            enterPending=false;var choices=Candidates();if(choices.Count>0)Focus(choices[0]);
        }
        if(keyboard!=null && keyboard.tabKey.wasPressedThisFrame)Advance(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed);
        var selected=EventSystem.current.currentSelectedGameObject;
        if(keyboard!=null && selected!=null && selected.GetComponent<UnityEngine.UI.Button>()!=null)
        {
            var module=EventSystem.current.currentInputModule as InputSystemUIInputModule;
            bool nativeSpace=module!=null && module.submit!=null && module.submit.action!=null && module.submit.action.controls.Contains(keyboard.spaceKey);
            bool fallbackEnter=(module==null||module.submit==null||module.submit.action==null||!module.submit.action.enabled) && (keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame);
            if(keyboard.spaceKey.wasPressedThisFrame&&!nativeSpace || fallbackEnter)
                ExecuteEvents.Execute(selected,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        }
        if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)keyboardMode=false;
        UpdateRing();
    }
    public void EnterModal(KeyboardFocusScope scope)
    {
        modals.RemoveAll(s=>s==null);if(!modals.Contains(scope))modals.Add(scope);enterPending=true;
    }
    public void LeaveModal(KeyboardFocusScope scope)
    {
        modals.Remove(scope);enterPending=false;if(scope.Previous!=null && scope.Previous.gameObject.activeInHierarchy)Focus(scope.Previous);
    }
    public IReadOnlyList<Selectable> Candidates()
    {
        modals.RemoveAll(s=>s==null||!s.isActiveAndEnabled);
        Transform scope=modals.Count>0?modals[modals.Count-1].transform:null;
        var values=Selectable.allSelectablesArray.Where(s=>s!=null && s.IsActive()&&s.IsInteractable() && !(s is Scrollbar) &&
            (scope==null||s.transform.IsChildOf(scope))).ToList();
        // Row-major ordering follows the actual visual layout, independent of creation/sibling order.
        var visual=values.OrderBy(s=>-Mathf.Round(ScreenPoint(s).y/20f)).ThenBy(s=>ScreenPoint(s).x).ThenBy(s=>s.transform.GetSiblingIndex()).ToArray();
        if(scope!=null)return visual;
        var current=EventSystem.current?.currentSelectedGameObject;
        var order=current!=null?current.GetComponentInParent<KeyboardFocusOrder>():null;
        if(order==null)order=Object.FindObjectsByType<KeyboardFocusOrder>(FindObjectsSortMode.None).FirstOrDefault(o=>o.isActiveAndEnabled);
        if(order==null)return visual;
        var form=order.Ordered().Where(s=>values.Contains(s)).Distinct().ToArray();
        return form.Concat(visual.Where(s=>!form.Contains(s))).ToArray();
    }
    static Vector2 ScreenPoint(Selectable s)
    {
        var r=(RectTransform)s.transform;var canvas=s.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null,r.TransformPoint(r.rect.center));
    }
    public void Advance(bool backwards=false)
    {
        if(EventSystem.current==null)return;var choices=Candidates();if(choices.Count==0)return;
        var current=EventSystem.current.currentSelectedGameObject;
        int index=-1;for(int i=0;i<choices.Count;i++)if(choices[i].gameObject==current){index=i;break;}
        int next=index<0?(backwards?choices.Count-1:0):(index+(backwards?-1:1)+choices.Count)%choices.Count;
        Focus(choices[next]);
    }
    public void Focus(Selectable control)
    {
        if(control==null||!control.IsActive()||!control.IsInteractable()||EventSystem.current==null)return;
        keyboardMode=true;EventSystem.current.SetSelectedGameObject(control.gameObject);
        if(control is TMP_InputField input)input.ActivateInputField();
        Reveal(control);UpdateRing();
    }
    static void Reveal(Selectable control)
    {
        var target=(RectTransform)control.transform;
        foreach(var scroll in control.GetComponentsInParent<ScrollRect>())
        {
            if(!scroll.vertical||scroll.content==null||scroll.viewport==null)continue;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);
            var rect=scroll.viewport.rect;
            float delta=bounds.max.y>rect.yMax?rect.yMax-bounds.max.y:bounds.min.y<rect.yMin?rect.yMin-bounds.min.y:0;
            if(Mathf.Abs(delta)>.5f){scroll.StopMovement();scroll.content.anchoredPosition+=new Vector2(0,delta);}
        }
    }
    void UpdateRing()
    {
        var current=EventSystem.current.currentSelectedGameObject;
        var control=current==null?null:current.GetComponent<Selectable>();
        if(outlined!=control || ring==null)
        {
            if(ring!=null)Destroy(ring.gameObject);outlined=control;
            if(control!=null)
            {
                var rect=(RectTransform)new GameObject("Keyboard focus",typeof(RectTransform),typeof(CanvasRenderer)).transform;
                rect.SetParent(control.transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*-3;rect.offsetMax=Vector2.one*3;
                ring=rect.gameObject.AddComponent<FocusOutline>();ring.color=new Color32(47,80,247,255);ring.raycastTarget=false;
            }
        }
        if(ring!=null)ring.gameObject.SetActive(keyboardMode&&control!=null&&control.IsActive()&&control.IsInteractable());
    }
}
public sealed class FocusOutline : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(10,r.height*.22f);
        for(int corner=0;corner<4;corner++)
        {
            var c=new Vector2(corner==0||corner==3?r.xMin+radius:r.xMax-radius,corner<2?r.yMax-radius:r.yMin+radius);
            float start=180-corner*90;
            for(int i=0;i<8;i++)
            {float a=(start-i*90/8f)*Mathf.Deg2Rad,b=(start-(i+1)*90/8f)*Mathf.Deg2Rad;UIStroke.Line(mesh,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,2);}
        }
        UIStroke.Line(mesh,new Vector2(r.xMin+radius,r.yMax),new Vector2(r.xMax-radius,r.yMax),color,2);
        UIStroke.Line(mesh,new Vector2(r.xMin+radius,r.yMin),new Vector2(r.xMax-radius,r.yMin),color,2);
        UIStroke.Line(mesh,new Vector2(r.xMin,r.yMin+radius),new Vector2(r.xMin,r.yMax-radius),color,2);
        UIStroke.Line(mesh,new Vector2(r.xMax,r.yMin+radius),new Vector2(r.xMax,r.yMax-radius),color,2);
    }
}

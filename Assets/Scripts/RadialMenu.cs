using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class RadialMenu : MonoBehaviour
{
    public UIDocument document;
    public VisualTreeAsset menuItemTemplate;
    public VisualTreeAsset radialScrollViewTemplate;
    public Transform fingerTip;
    public MenuItem menu;
    MenuItem currentMenu;
    VisualElement scrollContent;
    VisualElement scrollBar;
    VisualElement scrollHandle;
    VisualElement scrollView;
    VisualElement radialScrollView;
    PanelEventHandler panelEventHandler;
    PointerEventData lastPointerEventData;
    int segmentSize = 21;
    int viewportStartAngle = 40;
    int viewportSize = 100;
    float screenWidth = 1000;
    float segmentMinRadius = 125f;
    float segmentMaxRadius = 380f;
    float scrollMinRadius = 380f;
    float scrollMaxRadius = 430f;
    float scrollMinDistance = 50f;
    float clickMinDistance = 10f;
    float fingerRadius = 0.015f;
    float distanceToUi = -1;
    float maxScrollAngle = 0;
    float pageSize = 0;
    RaycastHit[] hits = new RaycastHit[ 1 ];
    bool isScrolling;
    bool isClicking;
    float startScrollOffset;
    float scrollBarAngle;
    Vector3 scrollMagnitude;
    Vector3 segmentMagnitude;
    List<VisualElement> currentMenuSegments = new List<VisualElement>();
    void Start()
    {
        SetupScrollView();
        GetPanelEventHandler();
        SetParents(menu);
        PopulateMenu(menu);
        fingerTip.localScale = Vector3.one * fingerRadius * 2;        
        
        document.rootVisualElement.RegisterCallback<PointerDownEvent>(e => {
            var posY = (e.position.y - screenWidth / 2)  * -1f;
            var clickAngle = Mathf.Atan2(e.position.x, posY) * Mathf.Rad2Deg;
            segmentMagnitude.x = e.position.x;
            segmentMagnitude.y = posY;
            Debug.Log(segmentMagnitude.magnitude);
            if(clickAngle >= viewportStartAngle && clickAngle <= viewportStartAngle + viewportSize &&
                segmentMagnitude.magnitude > segmentMinRadius && segmentMagnitude.magnitude < segmentMaxRadius) {
                var clickAngleWithScroll = clickAngle + scrollBar.style.rotate.value.angle.value - viewportStartAngle;
                var menuIdex = (int)Mathf.Floor(clickAngleWithScroll / segmentSize);
                var child = scrollContent.Children().ToList()[menuIdex];
                var touchpoint = child.Q<VisualElement>("touch-point");
                touchpoint.RemoveFromClassList("touch-no");
                touchpoint.AddToClassList("touch-grow");
                if(currentMenu.children[menuIdex].children.Length > 0) {
                    radialScrollView.AddToClassList("menu-transition-out");
                    
                    EventCallback<TransitionEndEvent> transitionOutCB = null;
                    transitionOutCB = e => {
                        radialScrollView.UnregisterCallback(transitionOutCB);
                        ClearScrollView();
                        SetupScrollView();
                        radialScrollView.AddToClassList("menu-transition-in");
                        _ = TransitionIn();
                        PopulateMenu(currentMenu.children[menuIdex]);
                    };

                    radialScrollView.RegisterCallback(transitionOutCB);
                }else{
                    currentMenu.children[menuIdex].click?.Invoke();
                }
                EventCallback<TransitionEndEvent> touchPointCB = null;
                touchPointCB = e => {
                    touchpoint.RemoveFromClassList("touch-grow");
                    touchpoint.AddToClassList("touch-no");
                    touchpoint.UnregisterCallback(touchPointCB);
                };
                touchpoint.RegisterCallback(touchPointCB);
            }
        });
        document.rootVisualElement.RegisterCallback<PointerMoveEvent>(e => {
            var pos = e.position;
            pos.z = distanceToUi * screenWidth;
            scrollMagnitude.x = pos.x;
            scrollMagnitude.y = (pos.y - screenWidth / 2) * -1f;
            float scrollAngle = Mathf.Atan2(scrollMagnitude.x, scrollMagnitude.y) * Mathf.Rad2Deg;
            if(scrollMagnitude.magnitude > scrollMinRadius && scrollMagnitude.magnitude < scrollMaxRadius && 
                pos.z < scrollMinDistance && scrollAngle >= viewportStartAngle && scrollAngle <= viewportStartAngle + viewportSize) {
                float currentScrollAngle = scrollBar.style.rotate.value.angle.value;
                if(!isScrolling) {
                    isScrolling = true;
                    startScrollOffset = scrollAngle - currentScrollAngle;
                }
                if(scrollAngle - viewportStartAngle > currentScrollAngle && scrollAngle - viewportStartAngle < viewportSize - maxScrollAngle + currentScrollAngle) {
                    // Touched inside scroll handle, move from touch point.
                    scrollBarAngle = Mathf.Clamp(scrollAngle - startScrollOffset, 0, maxScrollAngle);
                }else{
                    // Touched scroll area outside scroll handle, move scroll handle to place touch point in the middle of scroll bar, reset startscrollloffset.
                    scrollBarAngle = Mathf.Clamp(scrollAngle - viewportStartAngle - (viewportSize - maxScrollAngle) / 2, 0, maxScrollAngle);
                    startScrollOffset = scrollAngle - scrollBarAngle;
                }
                scrollBar.style.rotate = new Rotate(scrollBarAngle);
                scrollContent.style.rotate = new Rotate(scrollBarAngle / maxScrollAngle * (pageSize - viewportSize) * -1);
                ShowOnlyVisible();
            }else{
                isScrolling = false;
            }
            if(hits.Length > 0) {
                if(pos.z < clickMinDistance && !isClicking) {
                    isClicking = true;
                    PointerDown(hits[0].collider, hits[0].point, hits[0].normal);
                }else if(pos.z > clickMinDistance && isClicking) {
                    PointerUp(hits[0].collider, hits[0].point, hits[0].normal);
                    isClicking = false;
                }
            }else{
                isClicking = false;
            }
            foreach(var segment in currentMenuSegments) {
                var distance = Vector3.Distance(pos, segment.Q("item-mid-point").worldTransform.GetPosition());
                distance = Mathf.Clamp(distance, 0f, 100f) / 100f;
                var scale = ((1f - distance) * 0.57f) + 0.43f;
                segment.Q("inner-swipe").style.scale = new Scale(new Vector2(scale, scale));
            }
        });
    }

    IEnumerator NextFrame(Action callback) {
        yield return new WaitForEndOfFrame();
        callback();
    }

    public void Back() {
        radialScrollView.AddToClassList("menu-transition-in");
        EventCallback<TransitionEndEvent> transitionOutCB = null;
        transitionOutCB = e => {
            radialScrollView.UnregisterCallback(transitionOutCB);
            ClearScrollView();
            SetupScrollView();
            radialScrollView.AddToClassList("menu-transition-out");
            _ = TransitionOut();
            PopulateMenu(currentMenu.parent);
        };
        radialScrollView.RegisterCallback(transitionOutCB);
    }

    async Task TransitionOut() {
        await Task.Delay(20);
        radialScrollView.RemoveFromClassList("menu-transition-out");
    }

    async Task TransitionIn() {
        await Task.Delay(20);
        radialScrollView.RemoveFromClassList("menu-transition-in");
    }

    void SetupScrollView() {
        scrollView = radialScrollViewTemplate.CloneTree();
        scrollView.style.position = Position.Absolute;
        scrollView.style.width = 1000;
        scrollView.style.height = 1000;
        document.rootVisualElement.Add(scrollView);
        radialScrollView = scrollView.Q("radial-scroll-view");
        scrollContent = scrollView.Q("scroll-content");
        scrollBar = scrollView.Q("scroll-bar");
        scrollHandle = scrollView.Q("scroll-handle");
    }

    void ClearScrollView() {
        if(scrollView != null) {
            document.rootVisualElement.Remove(scrollView);
            scrollView = null;
        }
    }

    void FixedUpdate() {
        if(!fingerTip.hasChanged) {
            return;
        }
        Debug.DrawRay(fingerTip.position, transform.forward * distanceToUi, Color.yellow, 0.05f, false);
        var hitCount = Physics.RaycastNonAlloc(fingerTip.position, transform.forward, hits, 1f, 1 << 5);
        if (hitCount > 0) {
            distanceToUi = hits[0].distance - fingerRadius;
            PointerMove(hits[0].collider, hits[0].point, hits[0].normal);
        }else{
            distanceToUi = -1;
        }
        fingerTip.hasChanged = false;
    }

    void ShowOnlyVisible() {
        var i = 0;
        var currentContentScroll = scrollBarAngle / maxScrollAngle * (pageSize - viewportSize);
        foreach(var segment in currentMenuSegments) {  
            i++; 
            if(i * segmentSize < currentContentScroll || i * segmentSize > currentContentScroll + viewportSize + segmentSize) {
                segment.Q("menu-item").AddToClassList("hidden");
            }else{
                segment.Q("menu-item").RemoveFromClassList("hidden");
                segment.Q<Label>("label").style.left = 734;
                segment.Q<Label>("label").style.top = 533;
            }
        }
    }

    void PointerDown(Collider collider, Vector3 point, Vector3 normal) {
        var eventData = GetEventData(collider.transform, point, normal);
        panelEventHandler?.OnPointerDown(eventData);
    }
    void PointerUp(Collider collider, Vector3 point, Vector3 normal) {
        var eventData = GetEventData(collider.transform, point, normal);
        panelEventHandler?.OnPointerUp(eventData);
    }

    void PointerMove(Collider collider, Vector3 point, Vector3 normal) {
        lastPointerEventData = GetEventData(collider.transform, point, normal);
        panelEventHandler?.OnPointerMove(lastPointerEventData);
    }
    PointerEventData GetEventData(Transform t, Vector3 point, Vector3 normal) {
        var eventData = new PointerEventData(EventSystem.current);
        Vector3 position;
        Plane panelPlane = new Plane(normal, t.position);
        Ray ray = new Ray(point + normal, -normal );

        if (panelPlane.Raycast(ray, out float distance))
        {
            // get local pointer position within the panel
            position = ray.origin + distance * ray.direction.normalized;
            position = t.InverseTransformPoint(position);
            // compute a fake pointer screen position so it results in the proper panel position when projected from the camera by the PanelEventHandler
            position.x += 0.5f; position.y -= 0.5f;
            position = Vector3.Scale(position, new Vector3(document.panelSettings.targetTexture.descriptor.width, document.panelSettings.targetTexture.descriptor.height, 1.0f));
            position.y += Screen.height;
            // print(new Vector2(position.x, Screen.height - position.y)); // print actual computed position in panel UIToolkit coords

            // update the event data with the new calculated position
            eventData.position = position;
            RaycastResult raycastResult = eventData.pointerCurrentRaycast;
            raycastResult.screenPosition = position;
            eventData.pointerCurrentRaycast = raycastResult;
            raycastResult = eventData.pointerPressRaycast;
            raycastResult.screenPosition = position;
            eventData.pointerPressRaycast = raycastResult;
        }

        return eventData;
    }

    void GetPanelEventHandler() {
        PanelEventHandler[] handlers = FindObjectsOfType<PanelEventHandler>();
        foreach (PanelEventHandler handler in handlers){
            if (handler.panel == document.rootVisualElement.panel){
                panelEventHandler = handler;
                PanelRaycaster panelRaycaster = panelEventHandler.GetComponent<PanelRaycaster>();
                if (panelRaycaster != null) {
                    panelRaycaster.enabled = false;
                }
                break;
            }
        }
    }

    void SetParents(MenuItem menuItems){
        foreach(var item in menuItems.children){
            item.parent = menuItems;
            SetParents(item);
        }
    }

    void PopulateMenu(MenuItem menuItems) {
        var i = 0;
        currentMenuSegments.Clear();
        foreach(var item in menuItems.children){
            VisualElement ui = menuItemTemplate.CloneTree();
            scrollContent.Add(ui);
            currentMenuSegments.Add(ui);
            var touchPoint = ui.Q<VisualElement>("touch-point");
            var label = ui.Q<TextElement>("label");
            label.text = item.label;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            ui.Q<VisualElement>("icon").style.backgroundImage = new StyleBackground(Background.FromTexture2D(item.icon));
            ui.style.width = 1000;
            ui.style.height = 1000;
            ui.style.rotate = new Rotate(i * segmentSize);
            ui.style.position = Position.Absolute;
            i++;
        }
        currentMenu = menuItems;
        SetScrollBarSize(menuItems.children.Length);
        ShowOnlyVisible();
    }

    void SetScrollBarSize(int size) {
        pageSize = segmentSize * size - 1; // Minus one degree to trim the trailing spacing. 
        if(pageSize > viewportSize) {
           scrollBar.style.scale =  new Scale(Vector3.one);
           var scrollAngle = viewportSize / pageSize * 100f;
           maxScrollAngle = viewportSize - scrollAngle;
           scrollHandle.style.rotate = new Rotate(-maxScrollAngle);
        }else{
           scrollBar.style.scale =  new Scale(Vector3.zero);
        }
    }
}

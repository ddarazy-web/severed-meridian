using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace PopupUI.Editor
{
    [InitializeOnLoad]
    public static partial class PopupFrameworkVerification
    {
        private const string PlayKey = "PopupFramework.Input";
        static PopupFrameworkVerification()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.EraseBool(PlayKey); InputAsync().Forget(Debug.LogException); }
            };
        }
        public static void RunInputScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output);
            GameScreen.Editor.PuzzleUIRenderVerification.RememberSize();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(PlayKey, true); EditorApplication.EnterPlaymode();
        }
        public static void RunReviewNavigation() { SessionState.SetInt("PopupFramework.Review",0);RunInputScene(); }
        public static void RunReviewFocus() { SessionState.SetInt("PopupFramework.Review",1);RunInputScene(); }
        public static void RunReviewDynamic() { SessionState.SetInt("PopupFramework.Review",2);RunInputScene(); }
        public static void RunReviewRequests() { SessionState.SetInt("PopupFramework.Review",3);RunInputScene(); }
        private static async UniTask InputAsync()
        {
            results.Clear(); int exit = 0;
            GameObject canvasObject = null, prefab = null, eventObject = null;
            PopupCatalog catalog = null; PopupService service = null;
            Keyboard keyboard = null;
            Mouse mouse = null; Touchscreen touch = null;
            try
            {
                Application.runInBackground = true;
                eventObject = new GameObject("Popup-test-events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EventSystem events = eventObject.GetComponent<EventSystem>();
                canvasObject = new GameObject("Popup-test-canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(600, 800); scaler.matchWidthOrHeight = .5f;
                UnityEngine.UI.Button background = MakeButton(canvasObject.transform, "Game-screen", new Vector2(0, -250));
                int backgroundClicks = 0; background.onClick.AddListener(() => backgroundClicks++);
                GameObject hostObject = new GameObject("Popup-host", typeof(RectTransform), typeof(PopupHost));
                hostObject.transform.SetParent(canvasObject.transform, false); Stretch(hostObject.transform as RectTransform);
                PopupHost host = hostObject.GetComponent<PopupHost>();
                prefab = new GameObject("Popup-input-prefab", typeof(RectTransform), typeof(VerificationPopupView), typeof(UnityEngine.UI.Image));
                prefab.SetActive(false);
                UnityEngine.UI.Image shade = prefab.GetComponent<UnityEngine.UI.Image>(); shade.color = new Color(.12f,.17f,.25f,.5f);
                UnityEngine.UI.Button button = MakeButton(prefab.transform, "Action", Vector2.zero);
                prefab.GetComponent<VerificationPopupView>().SetDefaultSelection(button.gameObject);
                catalog = ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[] {
                    new PopupCatalog.Entry { Id="A", Prefab=prefab.GetComponent<VerificationPopupView>(), PauseGameplay=true },
                    new PopupCatalog.Entry { Id="B", Prefab=prefab.GetComponent<VerificationPopupView>(), PauseGameplay=true },
                    new PopupCatalog.Entry { Id="C", Prefab=prefab.GetComponent<VerificationPopupView>() },
                    new PopupCatalog.Entry { Id="locked", Prefab=prefab.GetComponent<VerificationPopupView>(), CloseOnCancel=false }
                });
                service = new PopupService(catalog); host.Attach(service, new PopupContext("input", "test", "1"));
                PopupHandle a = service.Open("A", new VerificationPopupState { Text="A" });
                PopupHandle b = service.Open("B", new VerificationPopupState { Text="B" });
                PopupHandle c = service.Open("C", new VerificationPopupState { Text="C" });
                UnityEngine.UI.Button ab = service.GetView(a).GetComponentInChildren<UnityEngine.UI.Button>();
                UnityEngine.UI.Button bb = service.GetView(b).GetComponentInChildren<UnityEngine.UI.Button>();
                UnityEngine.UI.Button cb = service.GetView(c).GetComponentInChildren<UnityEngine.UI.Button>();
                int ac=0, bc=0, cc=0;
                ab.onClick.AddListener(() => ac++); bb.onClick.AddListener(() => bc++); cb.onClick.AddListener(() => cc++);
                int review=SessionState.GetInt("PopupFramework.Review",-1);SessionState.EraseInt("PopupFramework.Review");
                if(review>=0){await ReviewChecks(review,service,host,events,background,hostObject);return;}
                await UniTask.Yield(); Canvas.ForceUpdateCanvases();
                Check(!ab.IsInteractable() && !bb.IsInteractable() && cb.IsInteractable(), "아래 팝업 입력 차단 최상위만 허용");
                ExecuteEvents.Execute(ab.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                ExecuteEvents.Execute(bb.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                ExecuteEvents.Execute(cb.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                Check(ac==0 && bc==0 && cc==1, "실제 Submit 아래0 최상위1");
                Check(events.currentSelectedGameObject == cb.gameObject, "최상위 기본 포커스");
                ExecuteEvents.Execute(cb.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
                Check(service.Top==b && service.Count==2 && events.currentSelectedGameObject==bb.gameObject, "Cancel 한 번 C만 닫기 B 포커스");
                ExecuteEvents.Execute(bb.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
                Check(service.Count==2, "같은 프레임 Cancel 재전달 차단");
                await UniTask.Yield();
                PopupHandle locked = service.Open("locked", null);
                UnityEngine.UI.Button lb = service.GetView(locked).GetComponentInChildren<UnityEngine.UI.Button>();
                ExecuteEvents.Execute(lb.gameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
                Check(service.Top==locked && service.Count==3, "닫기 금지 최상위 Cancel 소비");
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                await UniTask.Yield(); await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await UniTask.Yield();
                Check(service.Top==locked && service.Count==3, "실제 가상 Escape 닫기 금지 유지");
                service.Close(locked);
                await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                for (int frame=0; frame<3; frame++) await UniTask.Yield();
                Check(service.Top==a && service.Count==1,"가상 Escape 길게 눌러 B만 닫기");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await UniTask.Yield();
                b=service.Open("B",null); bb=service.GetView(b).GetComponentInChildren<UnityEngine.UI.Button>();
                int callbackCount=0;
                bb.onClick.AddListener(() => { callbackCount++; service.Close(b); service.Open("C",null); });
                ExecuteEvents.Execute(bb.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
                Check(callbackCount==1 && service.Count==2 && service.Top!=a,"Submit 콜백에서 닫기와 신규 열기");
                service.Close(service.Top.Value);
                b=service.Open("B",null); bb=service.GetView(b).GetComponentInChildren<UnityEngine.UI.Button>();
                foreach (Vector2Int size in new[] { new Vector2Int(1280,720),new Vector2Int(450,800),new Vector2Int(450,975),new Vector2Int(600,800) })
                {
                    GameScreen.Editor.PuzzleUIRenderVerification.SetSize(size.x,size.y);
                    for (int frame=0; frame<20; frame++) await UniTask.Yield();
                    Check(Screen.width==size.x && Screen.height==size.y, "실제 Game View 크기 "+size);
                    Rect safe=new Rect(24,36,size.x-48,size.y-72);
                    host.ApplySafeArea(safe,size);
                    Canvas.ForceUpdateCanvases();
                    Vector3[] popupCorners=new Vector3[4]; (service.GetView(b).transform as RectTransform).GetWorldCorners(popupCorners);
                    Check(popupCorners[0].x>=safe.xMin-1 && popupCorners[0].y>=safe.yMin-1 && popupCorners[2].x<=safe.xMax+1 && popupCorners[2].y<=safe.yMax+1,"비영점 안전 영역 "+size);
                    Vector2 center = RectTransformUtility.WorldToScreenPoint(null, bb.transform.position);
                    List<RaycastResult> hits = new List<RaycastResult>();
                    events.RaycastAll(new PointerEventData(events) { position=center },hits);
                    Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==bb, "최상위 버튼 raycast "+size);
                    hits.Clear(); events.RaycastAll(new PointerEventData(events) { position=RectTransformUtility.WorldToScreenPoint(null, background.transform.position) }, hits);
                    Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=background, "배경 입력 차단 "+size);
                    string path=Output+"input-"+size.x+"x"+size.y+".png";
                    if(File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    float deadline=Time.realtimeSinceStartup+10;
                    while ((!File.Exists(path) || new FileInfo(path).Length<1000) && Time.realtimeSinceStartup<deadline) await UniTask.Yield();
                    Texture2D image=new Texture2D(2,2);
                    try { Check(File.Exists(path) && image.LoadImage(File.ReadAllBytes(path)) && image.width==size.x && image.height==size.y,"실제 UI PNG "+size); }
                    finally { UnityEngine.Object.Destroy(image); }
                }
                Check(backgroundClicks==0,"배경 동작0");
                int topClicks=0; bb.onClick.AddListener(() => topClicks++);
                mouse=InputSystem.AddDevice<Mouse>();
                Vector2 buttonPoint=RectTransformUtility.WorldToScreenPoint(null,bb.transform.position);
                InputSystem.QueueStateEvent(mouse,new MouseState { position=buttonPoint,buttons=1 });
                for(int frame=0;frame<3;frame++) await UniTask.Yield();
                InputSystem.QueueStateEvent(mouse,new MouseState { position=buttonPoint });
                for(int frame=0;frame<3;frame++) await UniTask.Yield();
                Check(topClicks==1,"가상 마우스 실제 UI 클릭1");
                touch=InputSystem.AddDevice<Touchscreen>();
                InputSystem.RemoveDevice(mouse); mouse=null;
                await UniTask.Yield();
                InputSystem.QueueStateEvent(touch,new TouchState { touchId=71,phase=UnityEngine.InputSystem.TouchPhase.Began,position=buttonPoint });
                for(int frame=0;frame<3;frame++) await UniTask.Yield();
                results.Add("TRACE touch began press="+touch.primaryTouch.press.ReadValue()+" position="+touch.primaryTouch.position.ReadValue()+" point="+buttonPoint+" clicks="+topClicks+" focused="+Application.isFocused);
                InputSystem.QueueStateEvent(touch,new TouchState { touchId=71,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=buttonPoint });
                for(int frame=0;frame<3;frame++) await UniTask.Yield();
                results.Add("TRACE touch ended press="+touch.primaryTouch.press.ReadValue()+" clicks="+topClicks+" leftControls="+eventObject.GetComponent<InputSystemUIInputModule>().leftClick.action.controls.Count);
                Check(topClicks==2,"가상 터치 실제 UI 클릭1 actual="+topClicks);
                events.SetSelectedGameObject(background.gameObject);
                await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                for(int frame=0;frame<3;frame++) await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); await UniTask.Yield();
                Check(backgroundClicks==0 && topClicks==3,"선택 외부 오염 복구 실제 Enter 최상위1");
                service.Open("A",null);
                UnityEngine.UI.Button alternate=MakeButton(service.GetView(a).transform,"Alternate",new Vector2(0,100));
                events.SetSelectedGameObject(alternate.gameObject);
                PopupHandle overlay=service.Open("C",null); service.Close(overlay);
                Check(events.currentSelectedGameObject==alternate.gameObject,"이전 사용자 선택 포커스 복귀");
                service.CloseAll();
                PopupHandle pointerA=service.Open("A",null),pointerB=service.Open("B",null),pointerC=service.Open("C",null);
                UnityEngine.UI.Button pointerButton=service.GetView(pointerC).GetComponentInChildren<UnityEngine.UI.Button>();
                int pointerCallbacks=0;
                pointerButton.onClick.AddListener(()=>{pointerCallbacks++;service.Close(pointerA);service.Close(pointerC);service.Open("locked",null);});
                Vector2 callbackPoint=RectTransformUtility.WorldToScreenPoint(null,pointerButton.transform.position);
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=72,phase=UnityEngine.InputSystem.TouchPhase.Began,position=callbackPoint});
                for(int frame=0;frame<3;frame++)await UniTask.Yield();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=72,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=callbackPoint});
                for(int frame=0;frame<3;frame++)await UniTask.Yield();
                Check(pointerCallbacks==1 && service.Count==2 && service.GetView(pointerA)==null && service.GetView(pointerB)!=null && service.GetView(pointerC)==null,"실제 터치 콜백 자신과 다른 팝업 제거 신규 열기");
                bool popupBlock=true,popupPause=true,externalBlock=true,externalPause=true;
                int gesture=1,selection=1;
                Action<bool> blockHandler=value => { popupBlock=value; if(value){gesture=0;selection=0;} };
                Action<bool> pauseHandler=value => popupPause=value;
                host.InputBlockChanged += blockHandler;
                host.PauseRequestChanged += pauseHandler;
                service.CloseAll();
                Check(!popupBlock && !popupPause && (externalBlock||popupBlock) && (externalPause||popupPause),"팝업 종료 외부 차단 정지 보존");
                service.Open("A",null); PopupHandle middlePause=service.Open("B",null); PopupHandle nonpause=service.Open("C",null);
                Check(popupBlock && popupPause && gesture==0 && selection==0,"열기 차단 정지 제스처 선택 취소 연결");
                service.Close(middlePause);
                Check(popupPause && service.Top==nonpause && service.Count==2,"정지 중간 팝업 제거 비정지 최상위와 다른 정지 유지");
                service.Open("B",null);service.Open("C",null);
                service.Close(nonpause);
                Check(popupPause,"비정지 최상위 제거 정지 유지");
                PopupHandle lastPause=service.Top.Value; service.Close(lastPause);
                Check(popupPause,"정지 두 개 중 하나 제거 정지 유지");
                service.CloseAll(); Check(!popupPause,"마지막 정지 제거 요청 해제");
                float scale=Time.timeScale;
                service.Open("A",null); hostObject.SetActive(false); await UniTask.Yield();
                Check(service.Count==0 && hostObject.GetComponentsInChildren<PopupView>(true).Length==0,"host 비활성 뷰 정리");
                host.Detach(); host.Detach(); hostObject.SetActive(true); host.Attach(service,new PopupContext("input","test","2"));
                service.Open("A",null); host.Detach(); await UniTask.Yield();
                Check(service.Count==0 && hostObject.GetComponentsInChildren<PopupView>(true).Length==0,"Detach 반복 재연결 뷰 정리");
                host.Attach(service,new PopupContext("input","test","3")); service.Open("A",null);
                bool duringDetachRejected=false,attempted=false;
                Action changedHandler=() => {
                    if(service.Count!=0 || attempted) return; attempted=true;
                    try { service.Open("C",null); } catch(InvalidOperationException){duringDetachRejected=true;}
                };
                service.Changed += changedHandler;
                host.Detach(); await UniTask.Yield();
                Check(duringDetachRejected && service.Count==0,"Detach 알림 콜백 재열기 차단");
                service.Changed-=changedHandler;host.InputBlockChanged-=blockHandler;host.PauseRequestChanged-=pauseHandler;
                Check(typeof(PopupService).GetField("Changed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service)==null &&
                    typeof(PopupHost).GetField("InputBlockChanged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host)==null &&
                    typeof(PopupHost).GetField("PauseRequestChanged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host)==null &&
                    typeof(PopupService).GetField("host",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service)==null,"외부 구독 명시 해제 프레임워크 내부 구독0 호스트 참조0");
                host.Attach(service,new PopupContext("input","test","destroy")); service.Open("A",null);
                UnityEngine.Object.Destroy(hostObject); await UniTask.Yield();
                Check(service.Count==0 && host==null,"host 파괴 서비스와 뷰 정리");
                Check(Time.timeScale==scale,"Time.timeScale 불변");
                GameObject savedHost=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Popup/PopupHost.prefab");
                PopupView savedTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Popup/PopupTemplate.prefab")?.GetComponent<PopupView>();
                Check(savedHost!=null && savedTemplate!=null,"실제 저장 프리팹 로드");
                GameObject prefabHost=UnityEngine.Object.Instantiate(savedHost);
                PopupCatalog savedCatalog=ScriptableObject.CreateInstance<PopupCatalog>();
                try
                {
                    savedCatalog.Configure(new[]{new PopupCatalog.Entry { Id="template",Prefab=savedTemplate }});
                    PopupService savedService=new PopupService(savedCatalog);
                    prefabHost.GetComponent<PopupHost>().Attach(savedService,new PopupContext("prefab","test","1"));
                    PopupHandle savedHandle=savedService.Open("template",null);
                    UnityEngine.UI.Button savedClose=savedService.GetView(savedHandle).GetComponentInChildren<UnityEngine.UI.Button>();
                    ExecuteEvents.Execute(savedClose.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
                    Check(savedService.Count==0,"저장 프리팹 실제 Submit 닫기 연결");
                }
                finally { UnityEngine.Object.Destroy(prefabHost);UnityEngine.Object.Destroy(savedCatalog); }
            }
            catch (Exception error) { exit=1; results.Add("FAIL "+error); }
            finally
            {
                service?.CloseAll();
                if (keyboard!=null) InputSystem.RemoveDevice(keyboard);
                if (mouse!=null) InputSystem.RemoveDevice(mouse);
                if (touch!=null) InputSystem.RemoveDevice(touch);
                if (canvasObject!=null) UnityEngine.Object.Destroy(canvasObject);
                if (prefab!=null) UnityEngine.Object.Destroy(prefab);
                if (eventObject!=null) UnityEngine.Object.Destroy(eventObject);
                if (catalog!=null) UnityEngine.Object.Destroy(catalog);
                await UniTask.Yield();
                GameScreen.Editor.PuzzleUIRenderVerification.RestoreSize();
                results.Add("UTC "+DateTime.UtcNow.ToString("O"));
                File.WriteAllLines(Output+"input-results.txt",results);
                EditorApplication.Exit(exit);
            }
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; }
        private static UnityEngine.UI.Button MakeButton(Transform parent,string name,Vector2 position)
        {
            GameObject root=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            root.transform.SetParent(parent,false);
            RectTransform rect=root.transform as RectTransform; rect.sizeDelta=new Vector2(180,70); rect.anchoredPosition=position;
            root.GetComponent<UnityEngine.UI.Image>().color=new Color(.3f,.8f,.72f,1);
            return root.GetComponent<UnityEngine.UI.Button>();
        }
    }
}

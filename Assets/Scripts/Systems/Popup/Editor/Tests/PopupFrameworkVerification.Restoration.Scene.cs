using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        public static void RunRestorationScene()
        {
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output);GameScreen.Editor.PuzzleUIRenderVerification.RememberSize();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool("PopupFramework.Restoration",true);EditorApplication.EnterPlaymode();
        }
        private static int lateRestoreActions;
        private static async UniTask<bool> LateRestoreAttempt(PopupService owner,CancellationToken token)
        {
            bool canceled=await UniTask.Delay(1000,cancellationToken:token).SuppressCancellationThrow();
            if(!canceled){lateRestoreActions++;owner.Open("C",null);}return canceled;
        }
        private static async UniTask RestorationSceneAsync()
        {
            results.Clear();int exit=0;Scene bootstrap=SceneManager.GetActiveScene(),sceneA=default,sceneB=default,returnA=default;
            GameObject prefab=null;PopupCatalog catalog=null;PopupService service=null;Keyboard keyboard=null;
            try
            {
                Application.runInBackground=true;VerificationRestoreView.Reset();lateRestoreActions=0;
                prefab=CreateRestorationPrefab();SceneManager.MoveGameObjectToScene(prefab,bootstrap);
                catalog=ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[]{
                    new PopupCatalog.Entry{Id="A",Prefab=prefab.GetComponent<VerificationRestoreView>(),Restorable=true,PauseGameplay=true},
                    new PopupCatalog.Entry{Id="B",Prefab=prefab.GetComponent<VerificationRestoreView>()},
                    new PopupCatalog.Entry{Id="C",Prefab=prefab.GetComponent<VerificationRestoreView>(),Restorable=true}
                });service=new PopupService(catalog);
                sceneA=SceneManager.CreateScene("Popup-Restore-A");SceneManager.SetActiveScene(sceneA);
                PopupHost first=CreateRestorationHost(sceneA);PopupContext context=new PopupContext("Popup-Restore-A","mission","same-game");
                first.Attach(service,context);
                PopupHandle oldA=service.Open("A",new VerificationRestoreState{Text="A",Input="kept input",Tab=2,Selected=3,Scroll=.75f,Values=new[]{7,8}});
                service.Open("B",null);PopupHandle oldC=service.Open("C",new VerificationRestoreState{Text="C",Input="top input",Tab=1,Selected=2,Scroll=.5f});
                EventSystem.current.SetSelectedGameObject(service.GetView(oldC).transform.Find("Alternate").gameObject);
                float timeScale=Time.timeScale;
                PopupExitTicket failed=service.BeginSceneExit(true);bool failedDestination=false;
                try { await SceneManager.LoadSceneAsync("Popup-Stage02-Deliberately-Missing",LoadSceneMode.Additive).ToUniTask(); }
                catch(Exception error){failedDestination=true;results.Add("EXPECTED destination load failure "+error.GetType().Name);service.RollbackSceneExit(failed);}
                Check(failedDestination && service.Count==3 && service.Top==oldC && EventSystem.current.currentSelectedGameObject.name=="Alternate","실제 이동 실패 Rollback 표시 포커스 유지");
                UniTask<bool> late=LateRestoreAttempt(service,first.GetCancellationTokenOnDestroy());
                PopupExitTicket ticket=service.BeginSceneExit(true);
                sceneB=SceneManager.CreateScene("Popup-Restore-B");SceneManager.SetActiveScene(sceneB);service.CommitSceneExit(ticket);
                await SceneManager.UnloadSceneAsync(sceneA).ToUniTask();
                Check(first==null && service.Count==0 && await late && lateRestoreActions==0,"원래 씬 언로드 표시 참조 소멸 늦은 콜백 취소");
                PopupHost away=CreateRestorationHost(sceneB);PopupContext elsewhere=new PopupContext("Popup-Restore-B","mission","same-game");away.Attach(service,elsewhere);
                Check(service.Restore(away,elsewhere).Status==PopupRestoreStatus.None && service.Count==0,"다른 실제 씬 팝업 표시0");
                away.Detach();returnA=SceneManager.CreateScene("Popup-Restore-A");SceneManager.SetActiveScene(returnA);
                await SceneManager.UnloadSceneAsync(sceneB).ToUniTask();
                PopupHost current=CreateRestorationHost(returnA);current.Attach(service,context);
                bool popupBlock=false,popupPause=false,externalBlock=true,externalPause=true;
                Action<bool> block=value=>popupBlock=value,pause=value=>popupPause=value;
                current.InputBlockChanged+=block;current.PauseRequestChanged+=pause;
                VerificationRestoreView.Endpoint="return-session-adapter";
                VerificationRestoreEndpoint previousRecipient=VerificationRestoreView.Recipient,currentRecipient=new VerificationRestoreEndpoint();VerificationRestoreView.Recipient=currentRecipient;
                PopupRestoreResult restore=service.Restore(current,context);
                Check(restore.Status==PopupRestoreStatus.Restored && restore.Handles.Count==2 && !service.Close(oldA) && !service.Close(oldC),"실제 원래 씬 새 핸들 명시 복원");
                VerificationRestoreView a=(VerificationRestoreView)service.GetView(restore.Handles[0]),c=(VerificationRestoreView)service.GetView(restore.Handles[1]);
                Check(a.State.Text=="A" && c.State.Text=="C" && c.Connected=="return-session-adapter" && VerificationRestoreView.Actions==0,"새 참조 상대 순서 현재 연결 행동0");
                await UniTask.Yield();Canvas.ForceUpdateCanvases();
                Dropdown[] choices=a.GetComponentsInChildren<Dropdown>();ScrollRect scroll=a.GetComponentInChildren<ScrollRect>();
                Check(a.GetComponentInChildren<InputField>().text=="kept input" && choices[0].value==2 && choices[1].value==3 && Mathf.Abs(scroll.verticalNormalizedPosition-.75f)<.02f,"실제 입력 선택 탭 스크롤 UI 값 복원");
                Check(EventSystem.current.currentSelectedGameObject==c.transform.Find("Alternate").gameObject,"실제 왕복 포커스 복원");
                Check(popupBlock && popupPause && (externalBlock||popupBlock) && (externalPause||popupPause) && Time.timeScale==timeScale,"복원 요청 외부 소유권 TimeScale 보존");
                keyboard=InputSystem.AddDevice<Keyboard>();await UniTask.Yield();await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));for(int i=0;i<3;i++)await UniTask.Yield();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await UniTask.Yield();
                Check(VerificationRestoreView.Actions==1 && currentRecipient.Calls==1 && previousRecipient.Calls==0,"복원 최상위 실제 Enter 현재 수신1 이전0");
                ExecuteEvents.Execute(a.transform.Find("Action").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                Check(VerificationRestoreView.Actions==1,"복원 아래 Submit0");
                foreach(Vector2Int size in new[]{new Vector2Int(1280,720),new Vector2Int(450,800),new Vector2Int(450,975),new Vector2Int(600,800)})
                {
                    GameScreen.Editor.PuzzleUIRenderVerification.SetSize(size.x,size.y);for(int i=0;i<20;i++)await UniTask.Yield();
                    current.ApplySafeArea(new Rect(24,36,size.x-48,size.y-72),size);Canvas.ForceUpdateCanvases();
                    Vector3[] corners=new Vector3[4];(c.transform as RectTransform).GetWorldCorners(corners);
                    bool safe=corners[0].x>=23 && corners[0].y>=35 && corners[2].x<=size.x-23 && corners[2].y<=size.y-35;
                    Button button=c.transform.Find("Alternate").GetComponent<Button>();Vector2 point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
                    List<RaycastResult> hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                    Check(safe && Screen.width==size.x && Screen.height==size.y && hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button && EventSystem.current.currentSelectedGameObject==button.gameObject,"복원 크기 안전 영역 포커스 raycast "+size);
                    string capture=Output+"stage02-"+size.x+"x"+size.y+".png";if(File.Exists(capture))File.Delete(capture);ScreenCapture.CaptureScreenshot(capture);
                    float deadline=Time.realtimeSinceStartup+10;while((!File.Exists(capture)||new FileInfo(capture).Length<1000)&&Time.realtimeSinceStartup<deadline)await UniTask.Yield();
                    Texture2D image=new Texture2D(2,2);try{Check(File.Exists(capture)&&image.LoadImage(File.ReadAllBytes(capture))&&image.width==size.x&&image.height==size.y,"복원 새 PNG "+size);}finally{UnityEngine.Object.Destroy(image);}
                }
                service.CloseAll();await UniTask.Yield();
                Check(!popupBlock && !popupPause && (externalBlock||popupBlock) && (externalPause||popupPause) && VerificationRestoreView.Subscriptions==0,"복원 뷰 종료 구독0 외부 요청 유지");
                current.InputBlockChanged-=block;current.PauseRequestChanged-=pause;
                service.Open("A",new VerificationRestoreState{Text="A"});service.Open("C",new VerificationRestoreState{Text="C"});CaptureRestore(service,current,context);
                VerificationRestoreView.FailPrepare=true;PopupRestoreResult bad=service.Restore(current,context);await UniTask.Yield();
                Check(bad.Status==PopupRestoreStatus.Failed && service.Count==0 && current.GetComponentsInChildren<PopupView>(true).Length==0 && VerificationRestoreView.Subscriptions==0,"Play 候보 예외 부분 표시 구독0");
                VerificationRestoreView.FailPrepare=false;
                Check(service.Restore(current,context).Status==PopupRestoreStatus.Restored,"Play 명시 재시도 성공");
                CaptureRestore(service,current,context);
                int lifecycleErrors=0;
                Application.LogCallback lifecycleLog=(message,stack,type)=>{if(type==LogType.Exception && message.Contains("팝업 목록을 변경"))lifecycleErrors++;};
                Application.logMessageReceived+=lifecycleLog;
                try
                {
                    VerificationRestoreView.DisableDuringPrepare=current;
                    PopupRestoreResult interrupted=service.Restore(current,context);await UniTask.Yield();
                    results.Add("TRACE preparation host disable errors="+lifecycleErrors+" status="+interrupted.Status+" subscribers="+VerificationRestoreView.Subscriptions);
                    Check(interrupted.Status==PopupRestoreStatus.Failed && service.Count==0 && VerificationRestoreView.Subscriptions==0 && lifecycleErrors==0,"준비 중 Host 비활성 예외 로그0 후보 구독0");
                    VerificationRestoreView.DisableDuringPrepare=null;current.gameObject.SetActive(true);current.Attach(service,context);
                    Check(service.Restore(current,context).Status==PopupRestoreStatus.Restored,"Play Host 재연결 후 복원 재시도");
                    VerificationRestoreView.DisableDuringCapture=current;
                    bool rejected=RejectExit(()=>service.BeginSceneExit(true));await UniTask.Yield();
                    results.Add("TRACE capture host disable errors="+lifecycleErrors+" count="+service.Count+" subscribers="+VerificationRestoreView.Subscriptions);
                    Check(rejected && service.Count==0 && current.GetComponentsInChildren<PopupView>(true).Length==0 && VerificationRestoreView.Subscriptions==0 && lifecycleErrors==0,"Play 캡처 중 Host 비활성 예외0 뷰 구독0");
                }
                finally{VerificationRestoreView.DisableDuringPrepare=null;VerificationRestoreView.DisableDuringCapture=null;Application.logMessageReceived-=lifecycleLog;}
            }
            catch(Exception error){exit=1;results.Add("FAIL "+error);}
            finally
            {
                try
                {
                    VerificationRestoreView.FailPrepare=false;VerificationRestoreView.DisableDuringPrepare=null;VerificationRestoreView.DisableDuringCapture=null;service?.CloseAll();
                    if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
                    SceneManager.SetActiveScene(bootstrap);
                    foreach(Scene owned in new[]{sceneA,sceneB,returnA})if(owned.IsValid()&&owned.isLoaded)await SceneManager.UnloadSceneAsync(owned).ToUniTask();
                    if(prefab!=null)UnityEngine.Object.Destroy(prefab);if(catalog!=null)UnityEngine.Object.Destroy(catalog);await UniTask.NextFrame();
                    Check(VerificationRestoreView.Subscriptions==0,"시험 종료 소유 구독0");
                    results.Add("TRACE cleanup count="+service.Count+" prefab="+(prefab==null)+" catalog="+(catalog==null)+" sceneA="+SceneManager.GetSceneByName("Popup-Restore-A").isLoaded+" sceneB="+SceneManager.GetSceneByName("Popup-Restore-B").isLoaded);
                    Check(service.Count==0 && prefab==null && catalog==null && !SceneManager.GetSceneByName("Popup-Restore-A").isLoaded && !SceneManager.GetSceneByName("Popup-Restore-B").isLoaded,"시험 종료 소유 뷰 상태 에셋 메모리 씬0");
                }
                catch(Exception error){exit=1;results.Add("FAIL cleanup "+error);}
                finally
                {
                    GameScreen.Editor.PuzzleUIRenderVerification.RestoreSize();results.Add("UTC "+DateTime.UtcNow.ToString("O"));
                    File.WriteAllLines(Output+"stage02-scene-results.txt",results);EditorApplication.Exit(exit);
                }
            }
        }
        private static PopupHost CreateRestorationHost(Scene scene)
        {
            GameObject background=new GameObject("Restore-background",typeof(Camera));SceneManager.MoveGameObjectToScene(background,scene);
            Camera camera=background.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.08f,.11f);camera.cullingMask=0;
            GameObject events=new GameObject("Restore-events",typeof(EventSystem),typeof(InputSystemUIInputModule));SceneManager.MoveGameObjectToScene(events,scene);
            GameObject canvas=new GameObject("Restore-canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(canvas,scene);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(600,800);scaler.matchWidthOrHeight=.5f;
            GameObject holder=new GameObject("Restore-host",typeof(RectTransform),typeof(PopupHost));holder.transform.SetParent(canvas.transform,false);Stretch(holder.transform as RectTransform);return holder.GetComponent<PopupHost>();
        }
        private static GameObject CreateRestorationPrefab()
        {
            GameObject prefab=new GameObject("Restore-scene-view",typeof(RectTransform),typeof(VerificationRestoreView),typeof(Image));prefab.SetActive(false);prefab.GetComponent<Image>().color=new Color(.12f,.17f,.25f,.9f);
            Button action=MakeButton(prefab.transform,"Action",new Vector2(0,100)),alternate=MakeButton(prefab.transform,"Alternate",new Vector2(0,0));
            prefab.GetComponent<VerificationRestoreView>().SetDefaultSelection(action.gameObject);
            GameObject inputObject=new GameObject("Input",typeof(RectTransform),typeof(Image),typeof(InputField));inputObject.transform.SetParent(prefab.transform,false);RectTransform inputRect=inputObject.transform as RectTransform;inputRect.sizeDelta=new Vector2(180,40);inputRect.anchoredPosition=new Vector2(0,200);
            GameObject label=new GameObject("Text",typeof(RectTransform),typeof(Text));label.transform.SetParent(inputObject.transform,false);Stretch(label.transform as RectTransform);Text text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.color=Color.black;inputObject.GetComponent<InputField>().textComponent=text;
            for(int i=0;i<2;i++){GameObject choice=new GameObject(i==0?"Tab":"Selection",typeof(RectTransform),typeof(Dropdown));choice.transform.SetParent(prefab.transform,false);choice.GetComponent<Dropdown>().AddOptions(new List<string>{"0","1","2","3"});}
            GameObject scrollObject=new GameObject("Scroll",typeof(RectTransform),typeof(ScrollRect));scrollObject.transform.SetParent(prefab.transform,false);RectTransform viewport=scrollObject.transform as RectTransform;viewport.sizeDelta=new Vector2(120,60);
            GameObject content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(scrollObject.transform,false);RectTransform contentRect=content.transform as RectTransform;contentRect.sizeDelta=new Vector2(120,240);ScrollRect scroll=scrollObject.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=contentRect;scroll.horizontal=false;scroll.inertia=false;
            return prefab;
        }
    }
}

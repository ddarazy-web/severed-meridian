using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        public static void RunRestorationData()
        {
            results.Clear(); int exit=0;
            GameObject root=null,prefab=null,otherRoot=null;
            PopupCatalog catalog=null; PopupService owner=null,other=null;
            try
            {
                root=new GameObject("Restore-data-host",typeof(RectTransform),typeof(PopupHost));
                otherRoot=new GameObject("Restore-other-host",typeof(RectTransform),typeof(PopupHost));
                prefab=new GameObject("Restore-data-view",typeof(RectTransform),typeof(VerificationPopupView));
                prefab.SetActive(false);
                catalog=ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[]{
                    new PopupCatalog.Entry{Id="A",Prefab=prefab.GetComponent<VerificationPopupView>(),Restorable=true,PauseGameplay=true},
                    new PopupCatalog.Entry{Id="B",Prefab=prefab.GetComponent<VerificationPopupView>()},
                    new PopupCatalog.Entry{Id="C",Prefab=prefab.GetComponent<VerificationPopupView>(),Restorable=true},
                    new PopupCatalog.Entry{Id="multi",Prefab=prefab.GetComponent<VerificationPopupView>(),Restorable=true,AllowMultiple=true}
                });
                owner=new PopupService(catalog);other=new PopupService(catalog);
                PopupHost host=root.GetComponent<PopupHost>();PopupContext context=new PopupContext("scene-a","feature","game1");
                host.Attach(owner,context);otherRoot.GetComponent<PopupHost>().Attach(other,context);
                PopupHandle a=owner.Open("A",new VerificationPopupState{Text="A"});
                PopupHandle b=owner.Open("B",null),c=owner.Open("C",new VerificationPopupState{Text="C"});
                PopupView original=owner.GetView(a);
                PopupExitTicket ticket=owner.BeginSceneExit(true);
                Check(ticket.IsPending,"발급 ticket 이동 준비 상태 조회");
                Check(owner.Count==3 && owner.Top==c && owner.GetView(a)==original,"Begin 표시 핸들 순서 유지");
                Check(RejectExit(()=>owner.BeginSceneExit(true)),"중복 Begin 거부");
                Check(RejectExit(()=>owner.Open("B",null)) && RejectExit(()=>owner.Close(b)) && RejectExit(()=>owner.CloseAll()),"pending 목록 변경 거부");
                Check(RejectExit(()=>other.CommitSceneExit(ticket)) && other.Count==0,"다른 서비스 ticket 거부");
                owner.RollbackSceneExit(ticket);
                Check(!ticket.IsPending,"Rollback ticket 이동 준비 종료 조회");
                Check(owner.Count==3 && owner.Top==c && owner.GetView(a)==original,"Rollback 표시 핸들 순서 유지");
                Check(RejectExit(()=>owner.RollbackSceneExit(ticket)) && RejectExit(()=>owner.CommitSceneExit(ticket)),"소비 ticket 재사용 거부");
                ticket=owner.BeginSceneExit(true);owner.CommitSceneExit(ticket);
                Check(!ticket.IsPending,"Commit ticket 이동 준비 종료 조회");
                Check(owner.Count==0 && root.GetComponentsInChildren<PopupView>(true).Length==0,"Commit 표시 정리");
                bool ticketSceneReference=false;
                foreach(System.Reflection.FieldInfo field in typeof(PopupExitTicket).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic))
                    if(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.GetValue(ticket)!=null)ticketSceneReference=true;
                Check(!ticketSceneReference,"소비 ticket 이전 표시 영역 참조 해제");
                Check(RejectExit(()=>owner.Open("A",null)),"Commit 호스트 연결 해제");
                host.Attach(owner,context);owner.Open("A",null);
                ticket=owner.BeginSceneExit(true);host.Detach();
                Check(!ticket.IsPending,"Host 종료 ticket 이동 준비 종료 조회");
                Check(owner.Count==0 && RejectExit(()=>owner.CommitSceneExit(ticket)),"Host 종료 pending ticket 무효");
                host.Attach(owner,context);owner.Open("A",null);
                ticket=owner.BeginSceneExit(false);owner.RollbackSceneExit(ticket);
                Check(owner.Count==1,"preserve false Rollback 기존 표시 유지");
                owner.CloseAll();ticket=owner.BeginSceneExit(true);owner.CommitSceneExit(ticket);
                Check(owner.Count==0,"빈 캡처 Commit 가능");
                RestorationValueChecks();
            }
            catch(Exception error){exit=1;results.Add("FAIL "+error);}
            finally
            {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(otherRoot!=null)UnityEngine.Object.DestroyImmediate(otherRoot);
                owner?.CloseAll();other?.CloseAll();
                if(prefab!=null)UnityEngine.Object.DestroyImmediate(prefab);
                if(catalog!=null)UnityEngine.Object.DestroyImmediate(catalog);
                Directory.CreateDirectory(Output);results.Add("UTC "+DateTime.UtcNow.ToString("O"));
                File.WriteAllLines(Output+"stage02-data-results.txt",results);EditorApplication.Exit(exit);
            }
        }
        private static bool RejectExit(Action action)
        {try{action();return false;}catch(InvalidOperationException){return true;}}
    }
}


namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        private static void RestorationValueChecks()
        {
            GameObject root=new GameObject("Restoration-values-host",typeof(RectTransform),typeof(PopupHost));
            GameObject prefab=new GameObject("Restoration-values-view",typeof(RectTransform),typeof(VerificationRestoreView));
            GameObject events=new GameObject("Restoration-values-events",typeof(VerificationRestoreEventSystem));
            events.GetComponent<VerificationRestoreEventSystem>().Activate();
            prefab.SetActive(false);
            GameObject control=new GameObject("Focus",typeof(RectTransform),typeof(UnityEngine.UI.Button));control.transform.SetParent(prefab.transform,false);
            prefab.GetComponent<VerificationRestoreView>().SetDefaultSelection(control);
            GameObject alternate=new GameObject("Alternate",typeof(RectTransform),typeof(UnityEngine.UI.Button));alternate.transform.SetParent(prefab.transform,false);
            PopupCatalog catalog=ScriptableObject.CreateInstance<PopupCatalog>();PopupService service=null;
            VerificationRestoreView.Reset();
            try
            {
                catalog.Configure(new[]{
                    new PopupCatalog.Entry{Id="A",Prefab=prefab.GetComponent<VerificationRestoreView>(),Restorable=true,PauseGameplay=true},
                    new PopupCatalog.Entry{Id="B",Prefab=prefab.GetComponent<VerificationRestoreView>()},
                    new PopupCatalog.Entry{Id="C",Prefab=prefab.GetComponent<VerificationRestoreView>(),Restorable=true},
                    new PopupCatalog.Entry{Id="multi",Prefab=prefab.GetComponent<VerificationRestoreView>(),Restorable=true,AllowMultiple=true}
                });
                service=new PopupService(catalog);PopupHost host=root.GetComponent<PopupHost>();
                PopupContext context=new PopupContext("A","mission","session1");host.Attach(service,context);
                PopupHandle oldA=service.Open("A",new VerificationRestoreState{Text="A",Input="typed",Tab=2,Selected=3,Scroll=.75f,Values=new[]{1,2}});
                VerificationRestoreView original=(VerificationRestoreView)service.GetView(oldA);
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(original.transform.GetChild(1).gameObject);
                service.Open("B",null);PopupHandle oldC=service.Open("C",new VerificationRestoreState{Text="C"});
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(service.GetView(oldC).transform.GetChild(1).gameObject);
                PopupExitTicket saved=service.BeginSceneExit(true);original.State.Values[0]=99;service.CommitSceneExit(saved);
                host.Attach(service,context);
                PopupRestoreResult restored=service.Restore(host,context);
                Check(restored.Status==PopupRestoreStatus.Restored && restored.Handles.Count==2 && service.Count==2,"復원 제외 B 제거 A C 순서");
                VerificationRestoreView newA=(VerificationRestoreView)service.GetView(restored.Handles[0]);VerificationRestoreView newC=(VerificationRestoreView)service.GetView(restored.Handles[1]);
                Check(newA.State.Text=="A" && newC.State.Text=="C" && service.Top==restored.Handles[1],"복원 내용 상대 순서 최상위");
                Check(newA.State.Values[0]==1 && newA.State.Input=="typed" && newA.State.Tab==2 && newA.State.Selected==3 && newA.State.Scroll==.75f,"중첩 값 깊은 복사 입력 탭 선택 스크롤");
                Check(restored.Handles[0]!=oldA && restored.Handles[1]!=oldC && !service.Close(oldA) && service.Count==2,"새 핸들 옛 핸들 무효");
                Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==newC.transform.GetChild(1).gameObject,"최상위 포커스 값 복원");
                service.Close(restored.Handles[1]);
                Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==newA.transform.GetChild(1).gameObject,"하위 포커스 값 복원");
                service.CloseAll();Check(service.Restore(host,context).Status==PopupRestoreStatus.None,"성공 소비 재복원 None");
                service.Open("multi",new VerificationRestoreState{Text="one"});service.Open("multi",new VerificationRestoreState{Text="two"});CaptureRestore(service,host,context);
                restored=service.Restore(host,context);
                Check(restored.Handles.Count==2 && ((VerificationRestoreView)service.GetView(restored.Handles[0])).State.Text=="one" && ((VerificationRestoreView)service.GetView(restored.Handles[1])).State.Text=="two","복수 같은 종류 순서 별도 핸들");
                service.CloseAll();service.Open("A",new VerificationRestoreState{Text="stored"});CaptureRestore(service,host,context);
                host.Detach();PopupContext away=new PopupContext("B","mission","session1");host.Attach(service,away);
                Check(service.Restore(host,away).Status==PopupRestoreStatus.None,"다른 씬 표시 없음 원래 보관 유지");
                host.Detach();host.Attach(service,context);Check(service.Restore(host,context).Status==PopupRestoreStatus.Restored,"원래 씬 명시 복원");
                CaptureRestore(service,host,context);host.Detach();PopupContext newGame=new PopupContext("A","mission","session2");host.Attach(service,newGame);
                Check(service.Restore(host,newGame).Status==PopupRestoreStatus.ContextMismatch && service.Restore(host,newGame).Status==PopupRestoreStatus.None,"새 게임 문맥 폐기");
                host.Detach();host.Attach(service,context);service.Open("A",new VerificationRestoreState{Text="old"});CaptureRestore(service,host,context);
                service.Open("C",new VerificationRestoreState{Text="current"});
                PopupRestoreResult busy=service.Restore(host,context);Check(busy.Status==PopupRestoreStatus.Failed && busy.Handles.Count==0 && service.Count==1,"열린 호스트 실패 기존 목록 유지");
                saved=service.BeginSceneExit(false);service.RollbackSceneExit(saved);service.CloseAll();
                restored=service.Restore(host,context);Check(((VerificationRestoreView)service.GetView(restored.Handles[0])).State.Text=="old","preserve false Rollback 오래된 보관 유지");
                CaptureRestore(service,host,context);saved=service.BeginSceneExit(false);service.CommitSceneExit(saved);host.Attach(service,context);
                Check(service.Restore(host,context).Status==PopupRestoreStatus.None,"preserve false Commit 오래된 보관 폐기");
                service.Open("A",new VerificationRestoreState{Text="A"});service.Open("C",new VerificationRestoreState{Text="C"});CaptureRestore(service,host,context);
                VerificationRestoreView.FailApply=true;restored=service.Restore(host,context);
                results.Add("TRACE candidate failure status="+restored.Status+" count="+service.Count+" views="+root.GetComponentsInChildren<PopupView>(true).Length+" subscriptions="+VerificationRestoreView.Subscriptions);
                Check(restored.Status==PopupRestoreStatus.Failed && restored.Handles.Count==0 && !string.IsNullOrEmpty(restored.Error) && service.Count==0 && root.GetComponentsInChildren<PopupView>(true).Length==0 && VerificationRestoreView.Subscriptions==0,"두 번째 Apply 예외 후보 구독 정리");
                VerificationRestoreView.FailApply=false;VerificationRestoreView.FailPrepare=true;restored=service.Restore(host,context);
                Check(restored.Status==PopupRestoreStatus.Failed && service.Count==0 && root.GetComponentsInChildren<PopupView>(true).Length==0 && VerificationRestoreView.Subscriptions==0,"두 번째 현재 연결 예외 후보 정리");
                VerificationRestoreView.FailPrepare=false;VerificationRestoreView.Endpoint="new-connection";
                VerificationRestoreEndpoint previousRecipient=VerificationRestoreView.Recipient;
                VerificationRestoreEndpoint currentRecipient=new VerificationRestoreEndpoint();VerificationRestoreView.Recipient=currentRecipient;
                VerificationRestoreView.ProbeService=service;
                restored=service.Restore(host,context);
                Check(restored.Status==PopupRestoreStatus.Restored && VerificationRestoreView.Subscriptions==2 && ((VerificationRestoreView)service.GetView(restored.Handles[0])).Connected=="new-connection" && VerificationRestoreView.Actions==0,"명시 재시도 새 연결 행동 재실행0");
                Check(VerificationRestoreView.PrepareInputAttempts>=2 && VerificationRestoreView.PartialCount==0,"준비 중 실제 Submit0 부분 목록0");
                Check(previousRecipient.Calls==0 && currentRecipient.Calls==0,"후보 준비 이전 현재 명령 수신0");
                UnityEngine.EventSystems.ExecuteEvents.Execute(service.GetView(restored.Handles[1]).transform.GetChild(1).gameObject,new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                Check(currentRecipient.Calls==1 && previousRecipient.Calls==0,"복원 실제 Submit 현재 수신1 이전 수신0");
                VerificationRestoreView.SendPulse();
                Check(currentRecipient.Calls==3 && previousRecipient.Calls==0,"복원 현재 이벤트 두 수신 이전 수신0");
                VerificationRestoreView.ProbeService=null;
                CaptureRestore(service,host,context);service.Discard(newGame);Check(service.Restore(host,context).Status==PopupRestoreStatus.Restored,"다른 문맥 Discard 원본 유지");
                CaptureRestore(service,host,context);service.Discard(context);Check(service.Restore(host,context).Status==PopupRestoreStatus.None,"명시 Discard 보관 폐기");
                service.Open("A",new VerificationRestoreState{Text="capture"});VerificationRestoreView.FailCapture=true;
                Check(RejectExit(()=>service.BeginSceneExit(true)) && service.Count==1,"캡처 실패 표시 보존 pending 없음");
                VerificationRestoreView.FailCapture=false;saved=service.BeginSceneExit(true);service.RollbackSceneExit(saved);
                Check(service.Count==1,"캡처 실패 이후 재시도 가능");
                PopupView focus=service.GetView(service.Top.Value);string key=focus.CaptureFocusKey(focus.transform.GetChild(1).gameObject);
                Check(focus.ResolveFocusKey(key)==focus.transform.GetChild(1).gameObject && focus.ResolveFocusKey("999/3")==null,"기본 포커스 키 왕복 누락 키 null");
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focus.transform.GetChild(1).gameObject);
                CaptureRestore(service,host,context);VerificationRestoreView.DisableFocused=true;
                restored=service.Restore(host,context);
                Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==service.GetView(restored.Handles[0]).DefaultSelection,"복원 비활성 포커스 기본 선택 복귀");
                VerificationRestoreView.DisableFocused=false;CaptureRestore(service,host,context);VerificationRestoreView.MissingFocus=true;
                restored=service.Restore(host,context);
                Check(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==service.GetView(restored.Handles[0]).DefaultSelection,"복원 누락 포커스 기본 선택 복귀");
                VerificationRestoreView.MissingFocus=false;CaptureRestore(service,host,context);
                Check(service.Restore(null,context).Status==PopupRestoreStatus.Failed && service.Restore(host,newGame).Status==PopupRestoreStatus.Failed,"잘못된 Host 연결 문맥 실패");
                host.Detach();PopupContext otherFeature=new PopupContext("A","other-feature","session1");host.Attach(service,otherFeature);
                Check(service.Restore(host,otherFeature).Status==PopupRestoreStatus.ContextMismatch,"다른 기능 문맥 폐기");
                host.Detach();host.Attach(service,context);service.Open("A",new VerificationRestoreState{Text="prior"});CaptureRestore(service,host,context);
                saved=service.BeginSceneExit(true);service.CommitSceneExit(saved);host.Attach(service,context);
                Check(service.Restore(host,context).Status==PopupRestoreStatus.None,"빈 캡처 오래된 보관 폐기");
                service.Open("A",new VerificationRestoreState{Text="A"});CaptureRestore(service,host,context);
                VerificationRestoreView.DetachDuringPrepare=host;restored=service.Restore(host,context);
                Check(restored.Status==PopupRestoreStatus.Failed && service.Count==0 && VerificationRestoreView.Subscriptions==0,"준비 중 Host 해제 후보 연결 정리");
                VerificationRestoreView.DetachDuringPrepare=null;host.Attach(service,context);
                Check(service.Restore(host,context).Status==PopupRestoreStatus.Restored,"Host 종료 복원 실패 후 새 연결 재시도");
                bool captureBlock=true,capturePause=true;
                host.InputBlockChanged+=value=>captureBlock=value;host.PauseRequestChanged+=value=>capturePause=value;
                VerificationRestoreView.DetachDuringCapture=host;
                bool captureRejected=RejectExit(()=>service.BeginSceneExit(true));
                results.Add("TRACE capture detach rejected="+captureRejected+" count="+service.Count+" subscriptions="+VerificationRestoreView.Subscriptions+" block="+captureBlock+" pause="+capturePause);
                Check(captureRejected && service.Count==0 && root.GetComponentsInChildren<PopupView>(true).Length==0 && VerificationRestoreView.Subscriptions==0 && !captureBlock && !capturePause,"캡처 중 Host 종료 표시 구독 요청 정리");
                VerificationRestoreView.DetachDuringCapture=null;host.Attach(service,newGame);
                Check(service.Restore(host,newGame).Status==PopupRestoreStatus.None && service.Count==0,"캡처 중단 이전 문맥 표시 보관 누출0");
                service.Open("A",new VerificationRestoreState{Text="fresh"});saved=service.BeginSceneExit(true);service.RollbackSceneExit(saved);
                Check(service.Count==1,"캡처 중 Host 종료 후 새 문맥 이동 준비 가능");
                VerificationTransitionCaller caller=new VerificationTransitionCaller();
                VerificationRestoreView.FailCapture=true;
                Check(RejectExit(()=>caller.Run(service,()=>{},()=>{})) && !caller.Blocked && service.Count==1,"호출 예제 캡처 실패 이동 차단 해제");
                VerificationRestoreView.FailCapture=false;
                VerificationRestoreState.FailCopy=true;
                Check(RejectExit(()=>caller.Run(service,()=>{},()=>{})) && !caller.Blocked && service.Count==1,"호출 예제 값 복사 실패 이동 차단 해제");
                VerificationRestoreState.FailCopy=false;
                Check(RejectExit(()=>caller.Run(service,()=>throw new InvalidOperationException("시험 이동 준비 실패"),()=>{})) && !caller.Blocked && service.Count==1 && !caller.Ticket.IsPending,"호출 예제 이동 준비 실패 Rollback 차단 해제");
                Check(RejectExit(()=>caller.Run(service,()=>host.Detach(),()=>{})) && !caller.Blocked && service.Count==0 && !caller.Ticket.IsPending,"호출 예제 Host 종료 Commit 실패 차단 해제");
                host.Attach(service,newGame);service.Open("A",new VerificationRestoreState{Text="committed"});
                Check(RejectExit(()=>caller.Run(service,()=>{},()=>throw new InvalidOperationException("시험 언로드 실패"))) && !caller.Blocked && !caller.Ticket.IsPending,"호출 예제 Commit 뒤 실패 Rollback 없이 차단 해제");
                host.Attach(service,newGame);restored=service.Restore(host,newGame);
                Check(restored.Status==PopupRestoreStatus.Restored && ((VerificationRestoreView)service.GetView(restored.Handles[0])).State.Text=="committed","호출 예제 Commit 뒤 실패 확정 보관 유지");
                service.CloseAll();int delivered=currentRecipient.Calls;VerificationRestoreView.SendPulse();
                Check(currentRecipient.Calls==delivered && previousRecipient.Calls==0 && VerificationRestoreView.Subscriptions==0,"종료 후 현재 이전 이벤트 수신0");
            }
            finally
            {
                VerificationRestoreView.FailApply=false;VerificationRestoreView.FailPrepare=false;VerificationRestoreView.FailCapture=false;
                VerificationRestoreView.DetachDuringPrepare=null;VerificationRestoreView.ProbeService=null;VerificationRestoreView.DisableFocused=false;VerificationRestoreView.MissingFocus=false;
                VerificationRestoreView.DetachDuringCapture=null;
                VerificationRestoreState.FailCopy=false;
                UnityEngine.Object.DestroyImmediate(root);service?.CloseAll();UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(catalog);events.GetComponent<VerificationRestoreEventSystem>().Deactivate();UnityEngine.Object.DestroyImmediate(events);
            }
        }
        private static void CaptureRestore(PopupService service,PopupHost host,PopupContext context)
        {service.CommitSceneExit(service.BeginSceneExit(true));host.Attach(service,context);}
    }
    public sealed class VerificationRestoreState:PopupState
    {
        public string Text,Input;public int Tab,Selected;public float Scroll;public int[] Values;
        public static bool FailCopy;
        public override PopupState Copy()
        {if(FailCopy)throw new InvalidOperationException("시험 Copy 실패");return new VerificationRestoreState{Text=Text,Input=Input,Tab=Tab,Selected=Selected,Scroll=Scroll,Values=Values==null?null:(int[])Values.Clone()};}
    }
    public sealed class VerificationRestoreView:PopupView
    {
        public VerificationRestoreState State;
        public string Connected;
        public static bool FailApply,FailPrepare,FailCapture;
        public static string Endpoint;
        public static int Actions;
        public static VerificationRestoreEndpoint Recipient=new VerificationRestoreEndpoint();
        public static bool DisableFocused,MissingFocus;
        public static PopupHost DetachDuringPrepare;
        public static PopupHost DetachDuringCapture;
        public static PopupHost DisableDuringPrepare;
        public static PopupService ProbeService;
        public static int PrepareInputAttempts,PartialCount;
        public static event Action Pulse;
        public static int Subscriptions=>Pulse?.GetInvocationList().Length??0;
        private Action listener;
        public static void Reset(){FailApply=false;FailPrepare=false;FailCapture=false;DisableFocused=false;MissingFocus=false;DetachDuringPrepare=null;DetachDuringCapture=null;DisableDuringPrepare=null;DisableDuringCapture=null;ProbeService=null;PrepareInputAttempts=0;PartialCount=0;Endpoint="initial";Pulse=null;Actions=0;Recipient=new VerificationRestoreEndpoint();}
        public static PopupHost DisableDuringCapture;
        public static void SendPulse(){Pulse?.Invoke();}
        public override void ApplyState(PopupState value)
        {
            State=value==null?null:(VerificationRestoreState)value.Copy();
            UnityEngine.UI.InputField input=GetComponentInChildren<UnityEngine.UI.InputField>(true);
            if(input!=null)input.SetTextWithoutNotify(State?.Input??"");
            UnityEngine.UI.Dropdown[] choices=GetComponentsInChildren<UnityEngine.UI.Dropdown>(true);
            if(choices.Length==2){choices[0].SetValueWithoutNotify(State?.Tab??0);choices[1].SetValueWithoutNotify(State?.Selected??0);}
            UnityEngine.UI.ScrollRect scroll=GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            if(scroll!=null)scroll.verticalNormalizedPosition=State?.Scroll??0;
            if(FailApply && State?.Text=="C")throw new InvalidOperationException("시험 Apply 실패");
        }
        public override PopupState CaptureState()
        {if(DetachDuringCapture!=null)DetachDuringCapture.Detach();if(DisableDuringCapture!=null)DisableDuringCapture.gameObject.SetActive(false);if(FailCapture)throw new InvalidOperationException("시험 Capture 실패");return State;}
        public override void PrepareRestore(PopupContext context)
        {
            Connected=Endpoint;VerificationRestoreEndpoint recipient=Recipient;listener=()=>{Actions++;recipient.Invoke();};Pulse+=listener;
            foreach(UnityEngine.UI.Button button in GetComponentsInChildren<UnityEngine.UI.Button>(true))button.onClick.AddListener(OnAction);
            if(DisableFocused)transform.GetChild(1).GetComponent<UnityEngine.UI.Button>().interactable=false;
            if(ProbeService!=null)
            {
                PrepareInputAttempts++;PartialCount+=ProbeService.Count;
                UnityEngine.EventSystems.ExecuteEvents.Execute(GetComponentInChildren<UnityEngine.UI.Button>(true).gameObject,new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            }
            if(DetachDuringPrepare!=null)DetachDuringPrepare.Detach();
            if(DisableDuringPrepare!=null)DisableDuringPrepare.gameObject.SetActive(false);
            if(FailPrepare && State?.Text=="C")throw new InvalidOperationException("시험 현재 연결 실패");
        }
        private void OnAction(){listener?.Invoke();}
        public override GameObject ResolveFocusKey(string key)=>MissingFocus?null:base.ResolveFocusKey(key);
        public override void ReleaseRestore()
        {
            if(listener!=null){Pulse-=listener;listener=null;}
            foreach(UnityEngine.UI.Button button in GetComponentsInChildren<UnityEngine.UI.Button>(true))button.onClick.RemoveListener(OnAction);
        }
        private void OnDestroy(){ReleaseRestore();}
    }
    public sealed class VerificationRestoreEndpoint
    {
        public int Calls;
        public void Invoke(){Calls++;}
    }
    public sealed class VerificationTransitionCaller
    {
        public bool Blocked;
        public PopupExitTicket Ticket;
        public void Run(PopupService service,Action prepare,Action finish)
        {
            Ticket=null;Blocked=true;
            try{Ticket=service.BeginSceneExit(true);prepare();service.CommitSceneExit(Ticket);finish();}
            catch{if(Ticket!=null && Ticket.IsPending)service.RollbackSceneExit(Ticket);throw;}
            finally{Blocked=false;}
        }
    }
}

namespace PopupUI.Editor
{
    public sealed class VerificationRestoreEventSystem:UnityEngine.EventSystems.EventSystem
    {
        public void Activate(){if(UnityEngine.EventSystems.EventSystem.current!=this)base.OnEnable();UnityEngine.EventSystems.EventSystem.current=this;}
        public void Deactivate(){base.OnDisable();}
    }
}

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        private static async UniTask ReviewChecks(int review,PopupService service,PopupHost host,EventSystem events,UnityEngine.UI.Button background,GameObject hostObject)
        {
            Keyboard keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                // 새 가상 장치의 기본 UI action 바인딩이 준비된 뒤 입력을 보낸다.
                await UniTask.Yield();await UniTask.Yield();
                service.CloseAll();
                if(review==0)
                {
                    PopupHandle locked=service.Open("locked",null);
                    UnityEngine.UI.Button top=service.GetView(locked).GetComponentInChildren<UnityEngine.UI.Button>();
                    int backgroundSubmit=0;background.onClick.AddListener(()=>backgroundSubmit++);
                    VerificationCancelReceiver outside=background.gameObject.AddComponent<VerificationCancelReceiver>();
                    UnityEngine.UI.Navigation nav=top.navigation;nav.mode=UnityEngine.UI.Navigation.Mode.Explicit;nav.selectOnDown=background;top.navigation=nav;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.DownArrow,Key.Enter));
                    for(int frame=0;frame<3;frame++)await UniTask.Yield();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());await UniTask.Yield();
                    events.SetSelectedGameObject(top.gameObject);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.DownArrow,Key.Escape));
                    for(int frame=0;frame<3;frame++)await UniTask.Yield();
                    results.Add("TRACE same-update outside submit="+backgroundSubmit+" cancel="+outside.Count);
                    Check(backgroundSubmit==0 && outside.Count==0 && service.Top==locked,"review1 같은 업데이트 Move Submit Cancel 모달 경계");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());await UniTask.Yield();
                    UnityEngine.UI.Button inside=MakeButton(service.GetView(locked).transform,"InsideNavigation",new Vector2(0,120));
                    int insideClicks=0;inside.onClick.AddListener(()=>insideClicks++);
                    nav=top.navigation;nav.mode=UnityEngine.UI.Navigation.Mode.Automatic;top.navigation=nav;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow));
                    for(int frame=0;frame<3;frame++)await UniTask.Yield();
                    Check(events.currentSelectedGameObject==inside.gameObject,"review1 최상위 내부 Automatic 탐색");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());await UniTask.Yield();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                    for(int frame=0;frame<3;frame++)await UniTask.Yield();
                    Check(insideClicks==1 && backgroundSubmit==0,"review1 내부 탐색 후 Enter1");
                }
                else if(review==1)
                {
                    PopupHandle a=service.Open("A",null);
                    UnityEngine.UI.Button fallback=service.GetView(a).GetComponentInChildren<UnityEngine.UI.Button>();
                    UnityEngine.UI.Button alternate=MakeButton(service.GetView(a).transform,"DisabledSelection",new Vector2(0,100));
                    events.SetSelectedGameObject(alternate.gameObject);
                    PopupHandle b=service.Open("B",null);alternate.interactable=false;service.Close(b);
                    Check(events.currentSelectedGameObject==fallback.gameObject,"review2 비활성 기억 선택 기본 버튼 복귀");
                    alternate.interactable=true;alternate.enabled=false;events.SetSelectedGameObject(alternate.gameObject);
                    await UniTask.Yield();await UniTask.Yield();
                    Check(events.currentSelectedGameObject==fallback.gameObject,"review2 disabled 컴포넌트 현재 선택 복구");
                }
                else if(review==2)
                {
                    PopupHandle a=service.Open("A",null);PopupHandle b=service.Open("B",null);
                    UnityEngine.UI.Button dynamic=MakeButton(service.GetView(b).transform,"DynamicAfterOpen",Vector2.zero);
                    events.SetSelectedGameObject(dynamic.gameObject);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));
                    for(int frame=0;frame<3;frame++)await UniTask.Yield();
                    results.Add("TRACE dynamic view="+(dynamic==null?"closed":"alive")+" selected="+(events.currentSelectedGameObject==null?"null":events.currentSelectedGameObject.name)+" count="+service.Count+" key="+keyboard.escapeKey.isPressed);
                    Check(service.Top==a && service.Count==1,"review3 Refresh 없이 동적 버튼 Escape 닫기");
                }
                else
                {
                    bool all=false;
                    for(int mode=0;mode<3;mode++)
                    {
                        if(mode>0)host.Attach(service,new PopupContext("input","review","detach"));
                        bool block=false,pause=false,observedBlock=false;int stale=0,staleBlock=0;
                        Action<bool> pauseHandler=value=>{pause=value;if(value && service.Count==0)stale++;};
                        int selectedMode=mode;
                        Action<bool> blockHandler=value=>{block=value;if(value){if(selectedMode==0)service.CloseAll();else if(selectedMode==1)host.Detach();else service.Open("C",null);}};
                        Action<bool> observer=value=>{observedBlock=value;if(value && service.Count==0)staleBlock++;};
                        host.InputBlockChanged+=blockHandler;host.PauseRequestChanged+=pauseHandler;
                        host.InputBlockChanged+=observer;
                        try
                        {
                            service.Open("A",null);await UniTask.Yield();
                            results.Add("TRACE request reentry mode="+mode+" block="+block+" pause="+pause+" stale="+stale+" observedBlock="+observedBlock+" staleBlock="+staleBlock);
                            bool good=mode==2?service.Count==2 && block && observedBlock && pause && stale==0:service.Count==0 && !block && !observedBlock && !pause && stale==0 && staleBlock==0;
                            all=mode==0?good:all&&good;
                        }
                        finally{host.InputBlockChanged-=blockHandler;host.InputBlockChanged-=observer;host.PauseRequestChanged-=pauseHandler;host.Detach();}
                    }
                    Check(all,"review4 차단 콜백 CloseAll Detach 후 정지 잔류0");
                }
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
    }
    public sealed class VerificationCancelReceiver:MonoBehaviour,ICancelHandler
    {
        public int Count;
        public void OnCancel(BaseEventData data){Count++;}
    }
}

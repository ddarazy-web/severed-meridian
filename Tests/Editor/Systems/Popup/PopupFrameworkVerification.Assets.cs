using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        // 검사 전용 진입점이다. 사용자 씬을 열거나 저장하지 않는다.
        public static void CreatePrefabs()
        {
            results.Clear(); int exit=0;
            GameObject host=null,template=null;
            try
            {
                string folder="Assets/Prefabs/UI/Popup";
                if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs/UI","Popup");
                string hostPath=folder+"/PopupHost.prefab",templatePath=folder+"/PopupTemplate.prefab";
                Check(!File.Exists(hostPath) && !File.Exists(templatePath),"신규 프리팹 기존 파일 덮어쓰기 없음");
                host=new GameObject("PopupHost",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster),typeof(PopupHost));
                host.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                UnityEngine.UI.CanvasScaler scaler=host.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(600,800); scaler.matchWidthOrHeight=.5f;
                template=new GameObject("PopupTemplate",typeof(RectTransform),typeof(PopupView),typeof(UnityEngine.UI.Image));
                Stretch(template.transform as RectTransform);
                template.GetComponent<UnityEngine.UI.Image>().color=new Color(.1f,.15f,.23f,.55f);
                GameObject panel=new GameObject("Panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(template.transform,false);
                RectTransform panelRect=panel.transform as RectTransform;
                panelRect.anchorMin=new Vector2(.1f,.25f);panelRect.anchorMax=new Vector2(.9f,.75f);panelRect.offsetMin=Vector2.zero;panelRect.offsetMax=Vector2.zero;
                panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.16f,.21f,.3f,1);
                UnityEngine.UI.Button close=MakeButton(panel.transform,"Close",new Vector2(0,-45));
                template.GetComponent<PopupView>().SetDefaultSelection(close.gameObject);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick,template.GetComponent<PopupView>().Close);
                Font font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MoonRabbitUI-Regular.ttf");
                foreach(string label in new[]{"Heading","CloseLabel"})
                {
                    GameObject text=new GameObject(label,typeof(RectTransform),typeof(UnityEngine.UI.Text));
                    text.transform.SetParent(label=="Heading"?panel.transform:close.transform,false);
                    UnityEngine.UI.Text ui=text.GetComponent<UnityEngine.UI.Text>(); ui.font=font;ui.fontSize=24;
                    ui.text=label=="Heading"?"Popup":"닫기";ui.alignment=TextAnchor.MiddleCenter;ui.color=Color.white;ui.raycastTarget=false;
                    RectTransform rect=text.transform as RectTransform;
                    if(label=="Heading"){rect.sizeDelta=new Vector2(220,60);rect.anchoredPosition=new Vector2(0,50);}else Stretch(rect);
                }
                PrefabUtility.SaveAsPrefabAsset(host,hostPath); PrefabUtility.SaveAsPrefabAsset(template,templatePath);
                GameObject savedHost=AssetDatabase.LoadAssetAtPath<GameObject>(hostPath),savedTemplate=AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);
                Check(savedHost!=null && savedHost.GetComponent<PopupHost>()!=null && savedHost.GetComponent<UnityEngine.UI.GraphicRaycaster>()!=null,"호스트 프리팹 저장 연결");
                Check(savedTemplate!=null && savedTemplate.GetComponent<PopupView>().DefaultSelection!=null,"템플릿 기본 선택 직렬화");
                UnityEngine.UI.Button button=savedTemplate.GetComponentInChildren<UnityEngine.UI.Button>();
                Check(button.onClick.GetPersistentEventCount()==1 && button.onClick.GetPersistentTarget(0)==savedTemplate.GetComponent<PopupView>(),"템플릿 닫기 연결 직렬화");
            }
            catch(Exception error){exit=1;results.Add("FAIL "+error);}
            finally
            {
                if(host!=null)UnityEngine.Object.DestroyImmediate(host);
                if(template!=null)UnityEngine.Object.DestroyImmediate(template);
                Directory.CreateDirectory(Output);File.WriteAllLines(Output+"prefab-results.txt",results);
                EditorApplication.Exit(exit);
            }
        }
    }
}

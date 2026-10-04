using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        public static void RunTools()
        {
            results.Clear();int exit=0;
            GameObject root=null,other=null,prefab=null,wrong=null;PopupCatalog catalog=null;PopupService service=null;PopupManagementWindow window=null;
            const string brokenPath="Assets/Prefabs/UI/Popup/PopupStage03OwnedBroken.prefab";bool ownsBroken=false;
            try
            {
                prefab=new GameObject("Popup-tools-prefab",typeof(RectTransform),typeof(VerificationPopupView));prefab.SetActive(false);
                wrong=new GameObject("Popup-tools-wrong-root",typeof(VerificationPopupView));wrong.SetActive(false);
                catalog=ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[]{new PopupCatalog.Entry{Id="same",Prefab=prefab.GetComponent<PopupView>()},new PopupCatalog.Entry{Id="same",Prefab=prefab.GetComponent<PopupView>()}});
                Check(PopupCatalogValidation.Validate(catalog).Length>0,"도구 중복 ID 등록 오류");
                catalog.Configure(new[]{new PopupCatalog.Entry{Id=" ",Prefab=prefab.GetComponent<PopupView>()}});
                Check(PopupCatalogValidation.Validate(catalog).Length>0,"도구 빈 ID 등록 오류");
                catalog.Configure(new[]{new PopupCatalog.Entry{Id="missing"}});
                Check(PopupCatalogValidation.Validate(catalog).Length>0,"도구 누락 프리팹 등록 오류");
                catalog.Configure(new[]{new PopupCatalog.Entry{Id="wrong",Prefab=wrong.GetComponent<PopupView>()}});
                Check(PopupCatalogValidation.Validate(catalog).Length>0,"도구 RectTransform 누락 등록 오류");
                Check(!File.Exists(brokenPath) && !File.Exists(brokenPath+".meta"),"잘못된 직렬화 시험 프리팹 경로 미사용");
                string originalPrefab=File.ReadAllText("Assets/Prefabs/UI/Popup/PopupTemplate.prefab");
                System.Text.RegularExpressions.Match group=System.Text.RegularExpressions.Regex.Match(originalPrefab,@"--- !u!225 &(\d+)\r?\nCanvasGroup:[\s\S]*?(?=--- !u!|\z)");
                Check(group.Success,"CanvasGroup 누락 시험 원본 구조 확인");
                string brokenYaml=originalPrefab.Replace(group.Value,"");
                brokenYaml=System.Text.RegularExpressions.Regex.Replace(brokenYaml,@"  - component: \{fileID: "+group.Groups[1].Value+@"\}\r?\n","");
                using(FileStream file=new FileStream(brokenPath,FileMode.CreateNew,FileAccess.Write))
                { ownsBroken=true;using(StreamWriter writer=new StreamWriter(file))writer.Write(brokenYaml); }
                AssetDatabase.ImportAsset(brokenPath,ImportAssetOptions.ForceSynchronousImport);
                GameObject broken=AssetDatabase.LoadAssetAtPath<GameObject>(brokenPath);
                Check(broken!=null && broken.GetComponent<PopupView>()!=null,"직렬화 누락 프리팹 View 로드: object="+(broken!=null)+" view="+(broken!=null&&broken.GetComponent<PopupView>()!=null)+" repairedGroup="+(broken!=null&&broken.GetComponent<CanvasGroup>()!=null));
                catalog.Configure(new[]{new PopupCatalog.Entry{Id="broken",Prefab=broken.GetComponent<PopupView>()}});
                Check(Array.Exists(PopupCatalogValidation.Validate(catalog),error=>error.Contains("CanvasGroup")),"도구 CanvasGroup 누락 항목 진단");
                Check(File.ReadAllText("Assets/Prefabs/UI/Popup/PopupTemplate.prefab")==originalPrefab,"잘못된 프리팹 검사 원본 에셋 보존");
                catalog.Configure(new[]{new PopupCatalog.Entry{Id="A",Prefab=prefab.GetComponent<PopupView>(),Restorable=true,PauseGameplay=true},new PopupCatalog.Entry{Id="B",Prefab=prefab.GetComponent<PopupView>()},new PopupCatalog.Entry{Id="C",Prefab=prefab.GetComponent<PopupView>(),Restorable=true}});
                Check(PopupCatalogValidation.Validate(catalog).Length==0,"도구 유효 catalog 등록 허용");
                root=new GameObject("Popup-tools-host",typeof(RectTransform),typeof(PopupHost));other=new GameObject("Popup-tools-other",typeof(RectTransform),typeof(PopupHost));
                PopupHost host=root.GetComponent<PopupHost>();PopupContext context=new PopupContext("tools","feature","game1");
                service=new PopupService(catalog);host.Attach(service,context);
                PopupHandle a=service.Open("A",null),b=service.Open("B",null),c=service.Open("C",null);
                int changes=0;service.Changed+=()=>changes++;
                PopupInspection before=service.Inspect();
                Check(before.Count==3 && before.Items[0].Handle==a && before.Items[1].Handle==b && before.Top==c && before.Items[2].IsTop && before.Items[0].PauseGameplay && before.Items[0].Restorable && changes==0,"읽기 조회 순서 Top 정책 일치 변경 알림0");
                bool mutationRejected=false;try{((IList<PopupInspectionItem>)before.Items)[0]=before.Items[2];}catch(NotSupportedException){mutationRejected=true;}
                Check(mutationRejected && service.Top==c,"조회 컬렉션 변경 거부 원본 불변");
                service.Close(b);Check(before.Count==3 && service.Inspect().Count==2 && service.Inspect().Items[1].Id=="C","중간 제거 조회 갱신 이전 값 스냅샷 유지");
                window=ScriptableObject.CreateInstance<PopupManagementWindow>();window.ShowUtility();window.SelectHost(host);
                Check(window.rootVisualElement.Q<ObjectField>("popup-catalog")!=null && window.rootVisualElement.Q<ObjectField>("popup-host")!=null && window.rootVisualElement.Q<Button>("popup-save")!=null,"관리 창 catalog Host 저장 조작 제공");
                Check(window.ReadTarget().Count==2 && window.ReadTarget().Top==c,"관리 창 선택 Host 실제 목록 조회");
                PopupExitTicket ticket=service.BeginSceneExit(true);
                Check(service.Inspect().HasPendingExit && window.ReadTarget().HasPendingExit,"이동 준비 조회 일치");
                service.CommitSceneExit(ticket);
                Check(window.ReadTarget()==null && service.Inspect().Count==0 && !service.Inspect().HasPendingExit && service.Inspect().Stored.Count==1 && service.Inspect().Stored[0].Count==2 && service.Inspect().Stored[0].Context.Equals(context),"Host 해제와 보관 문맥 개수 조회");
                other.GetComponent<PopupHost>().Attach(service,context);window.SelectHost(other.GetComponent<PopupHost>());
                service.Restore(other.GetComponent<PopupHost>(),context);
                Check(window.ReadTarget().Count==2 && window.ReadTarget().Stored.Count==0 && window.ReadTarget().Top==service.Top,"관리 창 새 Host 복원 소비 조회");
                window.Close();window=null;service.CloseAll();
                Check(catalog.Entries.Length==3 && catalog.Entries[0].Id=="A","도구 실행 조회 원본 catalog 불변");
                Check(PopupTemplateGenerator.ValidateRequest("../Outside","Example","popup.example").Length>0,"템플릿 경로 이탈 생성 전 거부");
                Check(PopupTemplateGenerator.ValidateRequest("Feature","class","popup.example").Length>0,"템플릿 C# 예약어 생성 전 거부");
                Check(PopupTemplateGenerator.ValidateRequest("CON","Example","popup.example").Length>0,"템플릿 Windows 장치 경로 생성 전 거부");
                foreach(string deviceName in new[]{"CON","prn","AUX","nul","COM1","com9","LPT1","lpt9"})
                    Check(PopupTemplateGenerator.ValidateRequest("PopupOwnedTemplateTest",deviceName,"popup.example").Length>0,"템플릿 Windows 장치 프리팹 이름 생성 전 거부 "+deviceName);
                Check(PopupTemplateGenerator.ValidateRequest("Feature","Example"," ").Length>0,"템플릿 빈 ID 생성 전 거부");
                Check(PopupTemplateGenerator.ValidateRequest("PopupOwnedTemplateTest","Example","popup.example").Length==0,"템플릿 유효 요청 허용");
                PopupTemplateWindow templateWindow=ScriptableObject.CreateInstance<PopupTemplateWindow>();
                try
                {
                    templateWindow.ShowUtility();
                    Check(templateWindow.rootVisualElement.Q<TextField>("popup-template-feature")!=null && templateWindow.rootVisualElement.Q<Button>("popup-template-generate")!=null,"템플릿 생성 창 입력과 실제 생성 조작 제공");
                }
                finally { templateWindow.Close(); }
            }
            catch(Exception error){exit=1;results.Add("FAIL "+error);}
            finally
            {
                if(window!=null)window.Close();service?.CloseAll();
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(other!=null)UnityEngine.Object.DestroyImmediate(other);
                if(prefab!=null)UnityEngine.Object.DestroyImmediate(prefab);if(wrong!=null)UnityEngine.Object.DestroyImmediate(wrong);if(catalog!=null)UnityEngine.Object.DestroyImmediate(catalog);
                if(ownsBroken)AssetDatabase.DeleteAsset(brokenPath);
                Directory.CreateDirectory(Output);results.Add("UTC "+DateTime.UtcNow.ToString("O"));File.WriteAllLines(Output+"stage03-tools-results.txt",results);EditorApplication.Exit(exit);
            }
        }
    }
}

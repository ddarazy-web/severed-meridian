using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PopupUI.Editor
{
    public static partial class PopupFrameworkVerification
    {
        private const string Output = "Logs/PopupFramework/";
        private static readonly List<string> results = new List<string>();
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); results.Add("PASS " + name); }

        public static void Data()
        {
            results.Clear();
            int exit = 0;
            GameObject root = null, prefab = null;
            PopupCatalog catalog = null;
            PopupService service = null;
            try
            {
                root = new GameObject("Popup-data-host", typeof(RectTransform), typeof(PopupHost));
                prefab = new GameObject("Popup-data-prefab", typeof(RectTransform), typeof(VerificationPopupView));
                prefab.SetActive(false);
                VerificationPopupView view = prefab.GetComponent<VerificationPopupView>();
                catalog = ScriptableObject.CreateInstance<PopupCatalog>();
                catalog.Configure(new[] {
                    new PopupCatalog.Entry { Id = "A", Prefab = view },
                    new PopupCatalog.Entry { Id = "B", Prefab = view, PauseGameplay = true },
                    new PopupCatalog.Entry { Id = "C", Prefab = view },
                    new PopupCatalog.Entry { Id = "multi", Prefab = view, AllowMultiple = true }
                });
                service = new PopupService(catalog);
                root.GetComponent<PopupHost>().Attach(service, new PopupContext("scene", "feature", "session"));
                Check(service.Count == 0 && service.Top == null, "초기 빈 관리 상태");
                PopupHandle a = service.Open("A", new VerificationPopupState { Text = "first" });
                PopupHandle b = service.Open("B", new VerificationPopupState { Text = "middle" });
                PopupHandle c = service.Open("C", new VerificationPopupState { Text = "top" });
                Check(service.Count == 3 && service.Top == c, "A B C 순서와 최상위 C");
                Check(service.Close(b) && service.Count == 2 && service.Top == c, "중간 B만 제거");
                Check(!service.Close(b) && service.Count == 2 && service.Top == c, "닫힌 B 재닫기 불변");
                PopupHandle again = service.Open("A", new VerificationPopupState { Text = "updated" });
                Check(again == a && service.Count == 2 && service.Top == a, "단일 A 동일 핸들 맨 위");
                VerificationPopupView active = (VerificationPopupView)service.GetView(a);
                Check(((VerificationPopupState)active.CaptureState()).Text == "updated", "다시 연 A 내용 갱신");
                PopupHandle m1 = service.Open("multi", null), m2 = service.Open("multi", null);
                Check(m1 != m2 && service.Count == 4, "복수 종류 고유 핸들");
                bool rejected = false;
                try { service.Open("unknown", null); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && service.Count == 4 && service.Top == m2, "미지 ID 원래 상태 보존");
                bool failed = false;
                try { service.Open("A", new VerificationPopupState { Text = "broken", Fail = true }); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed && service.Top == m2 && service.Count == 4, "기존 내용 적용 실패 순서 보존");
                Check(((VerificationPopupState)active.CaptureState()).Text == "updated", "기존 내용 적용 실패 값 복구");
                service.Close(a);
                int children = root.GetComponentsInChildren<VerificationPopupView>(true).Length;
                failed = false;
                try { service.Open("A", new VerificationPopupState { Text = "new-broken", Fail = true }); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed && service.Count == 3 && root.GetComponentsInChildren<VerificationPopupView>(true).Length == children, "신규 내용 실패 후보 정리");
                PopupCatalog invalid = ScriptableObject.CreateInstance<PopupCatalog>();
                try
                {
                    invalid.Configure(new[] { new PopupCatalog.Entry { Id = "same", Prefab = view }, new PopupCatalog.Entry { Id = "same", Prefab = view } });
                    rejected = false;
                    try { new PopupService(invalid); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "중복 ID 거부");
                    invalid.Configure(new[] { new PopupCatalog.Entry { Id = "missing" } });
                    rejected = false;
                    try { new PopupService(invalid); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected, "누락 프리팹 거부");
                }
                finally { UnityEngine.Object.DestroyImmediate(invalid); }
                Check(new PopupContext("s", "f", "p").Equals(new PopupContext("s", "f", "p")) &&
                    !new PopupContext("s", "f", "p").Equals(new PopupContext("s", "f", "other")), "문맥 값 일치 계약");
                service.CloseAll();
                Check(service.Count == 0 && service.Top == null, "전체 닫기 빈 상태");
                Check(root.GetComponentsInChildren<VerificationPopupView>(true).Length == 0, "전체 닫기 뷰 정리");
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); }
            finally
            {
                service?.CloseAll();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                Directory.CreateDirectory(Output);
                results.Add("UTC " + DateTime.UtcNow.ToString("O"));
                File.WriteAllLines(Output + "data-results.txt", results);
                EditorApplication.Exit(exit);
            }
        }
    }

    public sealed class VerificationPopupState : PopupState
    {
        public string Text;
        public bool Fail;
        public override PopupState Copy() => new VerificationPopupState { Text = Text, Fail = Fail };
    }

    public sealed class VerificationPopupView : PopupView
    {
        private VerificationPopupState state;
        public override void ApplyState(PopupState value)
        {
            state = value == null ? null : (VerificationPopupState)value.Copy();
            if (state != null && state.Fail) throw new InvalidOperationException("시험용 ApplyState 실패");
        }
        public override PopupState CaptureState() => state?.Copy();
    }
}

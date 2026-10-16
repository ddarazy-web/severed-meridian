using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine.UIElements;

namespace Products.Editor
{
    internal static class BuildToolAndroidSigning
    {
        private static string loadedContext, loadedStore;

        internal static void Draw(VisualElement parent, BuildToolProfileInfo profile, Action changed)
        {
            var section = new VisualElement { name = "platform-settings" }; section.AddToClassList("section"); parent.Add(section);
            var title = new Label("Android 서명 설정"); title.AddToClassList("section-title"); section.Add(title);
            var help = new Label("비밀번호를 안전하게 저장하면 다음 실행부터 자동으로 채웁니다. 저장값은 현재 Windows 계정으로 암호화하며 프로필·Keystore·Alias별로 구분합니다.");
            help.AddToClassList("caption"); section.Add(help);
            // 조회 프로필을 바꿔도 실행 중인 요청의 전역 서명 비밀번호를 바꾸지 않는다.
            if (BuildToolController.Runner.IsLocked)
            {
                section.Add(new HelpBox("빌드 요청이 진행 중입니다. 요청이 끝나면 선택한 프로필의 서명 설정을 불러옵니다.", HelpBoxMessageType.Info));
                return;
            }
            if (profile.Profile == null) { section.Add(new HelpBox("프로필을 읽을 수 없습니다.", HelpBoxMessageType.Error)); return; }
            try
            {
                var data = new SerializedObject(ProductProfiles.ProfileSettings(profile.Profile, false));
                var store = data.FindProperty("AndroidKeystoreName");
                var alias = data.FindProperty("AndroidKeyaliasName");
                var custom = data.FindProperty("androidUseCustomKeystore");
                if (store == null || alias == null || custom == null) throw new InvalidOperationException();
                section.Add(new TextField("Keystore") { name = "android-keystore-path", value = store.stringValue, isReadOnly = true,
                    tooltip = "선택한 프로필에 지정된 keystore입니다. 파일 변경은 해당 Build Profile의 Player Settings → Publishing Settings에서 할 수 있습니다." });
                if (!custom.boolValue)
                {
                    section.Add(new HelpBox("현재 디버그 서명을 사용합니다. 사용자 keystore를 쓰려면 해당 프로필의 Publishing Settings에서 Custom Keystore를 켜세요.", HelpBoxMessageType.Info));
                    return;
                }
                string storeContext = Path.GetFullPath(".") + "\n" + profile.Guid + "\n" + store.stringValue;
                string context = storeContext + "\n" + alias.stringValue;
                var passwords = new BuildToolSigningPasswords(context);
                var savedStatus = new Label(); savedStatus.AddToClassList("caption");
                if (context != loadedContext)
                {
                    if (loadedContext != null)
                    {
                        if (loadedStore != storeContext) PlayerSettings.Android.keystorePass = "";
                        PlayerSettings.Android.keyaliasPass = "";
                    }
                    try
                    {
                        if (BuildToolSigningPasswords.Supported && passwords.TryRead(out string savedStore, out string savedKey))
                        {
                            PlayerSettings.Android.keystorePass = savedStore; PlayerSettings.Android.keyaliasPass = savedKey;
                            savedStatus.text = "저장된 비밀번호를 복원했습니다.";
                        }
                    }
                    catch (Exception) { savedStatus.text = "저장값을 복원하지 못했습니다. 비밀번호를 다시 입력하고 저장하세요."; }
                    loadedContext = context; loadedStore = storeContext;
                }
                var storePassword = new TextField("Keystore 비밀번호") { name = "android-keystore-password", isPasswordField = true,
                    value = PlayerSettings.Android.keystorePass, tooltip = "Keystore 파일을 열기 위한 비밀번호입니다. 저장 버튼을 누르면 Windows 계정으로 암호화해 보관합니다." };
                section.Add(storePassword);
                var aliases = new DropdownField("Alias", new List<string> { alias.stringValue }, 0) { name = "android-key-alias",
                    tooltip = "먼저 목록을 불러온 뒤 서명에 사용할 키를 선택하세요. 선택한 프로필에 즉시 저장됩니다." };
                aliases.SetEnabled(false);
                var status = new Label("Keystore 비밀번호 입력 후 목록을 불러오세요."); status.AddToClassList("caption");
                int revision = 0;
                storePassword.RegisterValueChangedCallback(change =>
                {
                    PlayerSettings.Android.keystorePass = change.newValue;
                    revision++; aliases.SetEnabled(false); status.text = "비밀번호가 변경됐습니다. Alias 목록을 다시 불러오세요.";
                });
                string storePath = store.stringValue;
                var load = new Button { name = "android-load-aliases", text = "Alias 목록 불러오기", tooltip = "Keystore의 서명 가능한 키 목록만 읽습니다. 빌드하거나 키를 생성하지 않습니다." };
                load.clicked += async () =>
                {
                    int requestedRevision = revision;
                    load.SetEnabled(false); aliases.SetEnabled(false); status.text = "Alias 목록을 읽는 중…";
                    try
                    {
                        string[] names = await BuildToolKeystoreAliases.Read(storePath, storePassword.value);
                        if (section.panel == null || revision != requestedRevision) return;
                        aliases.choices = names.ToList();
                        aliases.SetValueWithoutNotify(names.Contains(alias.stringValue) ? alias.stringValue : "");
                        aliases.SetEnabled(true); status.text = "Alias " + names.Length + "개를 찾았습니다. 사용할 키를 선택하세요.";
                    }
                    catch (Exception)
                    {
                        if (section.panel != null && revision == requestedRevision)
                            status.text = "Alias를 읽지 못했습니다. Keystore 경로·비밀번호·Unity JDK 설정을 확인하세요.";
                    }
                    finally { if (section.panel != null) load.SetEnabled(true); }
                };
                section.Add(load); section.Add(aliases); section.Add(status);
                aliases.RegisterValueChangedCallback(change =>
                {
                    if (BuildToolController.Runner.IsLocked || !aliases.choices.Contains(change.newValue)) return;
                    try { SaveAlias(profile.Profile, change.newValue); changed(); }
                    catch (Exception) { status.text = "Alias 저장 실패. 선택 프로필을 다시 불러와 확인하세요."; }
                });
                var keyPassword = new TextField("키 비밀번호") { name = "android-key-password", isPasswordField = true,
                    value = PlayerSettings.Android.keyaliasPass, tooltip = "선택한 Alias의 키 비밀번호입니다. Keystore 비밀번호와 다를 수 있습니다." };
                keyPassword.RegisterValueChangedCallback(change => PlayerSettings.Android.keyaliasPass = change.newValue);
                section.Add(keyPassword);
                var buttons = new VisualElement(); buttons.AddToClassList("result-actions"); section.Add(buttons);
                var save = new Button(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(storePassword.value) || string.IsNullOrEmpty(keyPassword.value) || string.IsNullOrEmpty(aliases.value))
                        { savedStatus.text = "Alias와 두 비밀번호를 입력하세요."; return; }
                        passwords.Save(storePassword.value, keyPassword.value);
                        savedStatus.text = "암호화 저장 완료. 다음 실행부터 자동 복원합니다.";
                    }
                    catch (Exception) { savedStatus.text = "암호화 저장 실패. 저장 경로와 Windows 계정을 확인하세요."; }
                }) { name = "android-save-passwords", text = "비밀번호 안전하게 저장", tooltip = "이 PC의 현재 Windows 계정에서 복원할 수 있도록 암호화합니다. Git과 빌드 산출물에는 포함하지 않습니다." };
                buttons.Add(save);
                var delete = new Button(() =>
                {
                    try
                    {
                        passwords.Delete(); storePassword.value = ""; keyPassword.value = "";
                        PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                        savedStatus.text = "현재 프로필·Alias의 저장된 비밀번호와 입력값을 삭제했습니다.";
                    }
                    catch (Exception) { savedStatus.text = "저장값 삭제 실패. 저장 경로의 접근 권한을 확인하세요."; }
                }) { name = "android-delete-passwords", text = "저장된 비밀번호 삭제", tooltip = "현재 선택한 프로필·Keystore·Alias에 저장된 비밀번호만 삭제합니다. Keystore 파일과 키는 삭제하지 않습니다." };
                buttons.Add(delete);
                save.SetEnabled(BuildToolSigningPasswords.Supported); delete.SetEnabled(BuildToolSigningPasswords.Supported);
                if (!BuildToolSigningPasswords.Supported) savedStatus.text = "암호화 저장은 Windows에서 지원합니다. 현재 환경에서는 비밀번호를 직접 입력하세요.";
                section.Add(savedStatus);
            }
            catch (Exception) { section.Add(new HelpBox("이 프로필의 Android 서명 설정을 읽지 못했습니다. Player Settings를 확인하세요.", HelpBoxMessageType.Error)); }
        }

        internal static void SaveAlias(BuildProfile profile, string value)
        {
            // Unity 6000.3의 기존 프로필별 PlayerSettings 저장 경로를 재사용한다.
            var serialize = typeof(BuildProfile).GetMethod("SerializePlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (serialize == null) throw new InvalidOperationException("프로필 설정 저장 API를 확인해야 합니다.");
            var data = new SerializedObject(ProductProfiles.ProfileSettings(profile, false));
            data.FindProperty("AndroidKeyaliasName").stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo(); serialize.Invoke(profile, null);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
}

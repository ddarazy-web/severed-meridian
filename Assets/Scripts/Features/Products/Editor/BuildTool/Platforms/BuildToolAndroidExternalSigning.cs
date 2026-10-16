using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Products.Editor
{
    public static class BuildToolAndroidExternalSigning
    {
        public static async Task<JObject> Execute(BuildToolProfileInfo profile, JObject input, string preferencesPath,
            Func<string, string, Task<string[]>> readAliases = null)
        {
            string id = (string)input["requestId"];
            string directory = BuildToolExternalOperations.RequestDirectory(id);
            string project = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var transfer = new BuildToolSigningPasswords("external-transfer\n" + project + "\n" + id, directory);
            string suppliedFile = (string)input["credentialsFile"];
            bool validTransfer = suppliedFile != null && string.Equals(Path.GetFullPath(suppliedFile), transfer.FilePath, StringComparison.OrdinalIgnoreCase);
            if (suppliedFile != null && !validTransfer) throw new ArgumentException("SigningError: 임시 암호화 파일 경로가 올바르지 않습니다.");
            if (validTransfer)
            {
                foreach (string path in new[] { Path.GetFullPath("Library"), Path.GetDirectoryName(directory), directory, transfer.FilePath })
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("SigningError: 링크 경로는 사용할 수 없습니다.");
                if (new FileInfo(transfer.FilePath).Length > 65536) throw new ArgumentException("SigningError: 잘못된 임시 파일입니다.");
            }
            try
            {
                string operation = (string)input["operation"];
                if (operation != "signing-aliases" && operation != "save-signing") throw new ArgumentException("지원하지 않는 서명 작업입니다.");
                if (input["customKeystore"]?.Type != JTokenType.Boolean || input["keystorePath"]?.Type != JTokenType.String || input["alias"]?.Type != JTokenType.String)
                    throw new ArgumentException("서명 설정 형식을 확인하세요.");
                bool custom = (bool)input["customKeystore"];
                string store = (string)input["keystorePath"], alias = (string)input["alias"];
                string storePassword = "", keyPassword = "";
                if (custom && (!validTransfer || !transfer.TryRead(out storePassword, out keyPassword) || string.IsNullOrEmpty(storePassword)))
                    throw new ArgumentException("Keystore 비밀번호를 입력하세요.");
                string[] aliases = custom ? await (readAliases ?? BuildToolKeystoreAliases.Read)(store, storePassword) : Array.Empty<string>();
                if (operation == "signing-aliases") return new JObject { ["aliases"] = new JArray(aliases) };
                if (custom && (!aliases.Contains(alias) || string.IsNullOrEmpty(keyPassword))) throw new ArgumentException("Alias와 키 비밀번호를 확인하세요.");
                // 비동기 Alias 조회 사이에 설정이 바뀌었으면 저장하지 않는다.
                if ((string)BuildToolExternalSettings.Read(profile, preferencesPath)["revision"] != (string)input["expectedRevision"])
                    throw new InvalidOperationException("RevisionConflict: 설정이 변경되었습니다. 다시 조회하세요.");
                var target = new BuildToolSigningPasswords(project + "\n" + profile.Guid + "\n" + store + "\n" + alias);
                byte[] asset = File.ReadAllBytes(profile.Path);
                byte[] oldCredentials = custom && File.Exists(target.FilePath) ? File.ReadAllBytes(target.FilePath) : null;
                using (File.Open(profile.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { }
                try
                {
                    using var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile.Profile, false));
                    settings.FindProperty("androidUseCustomKeystore").boolValue = custom;
                    settings.FindProperty("AndroidKeystoreName").stringValue = store;
                    settings.FindProperty("AndroidKeyaliasName").stringValue = alias;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    typeof(BuildProfile).GetMethod("SerializePlayerSettings", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(profile.Profile, null);
                    EditorUtility.SetDirty(profile.Profile); AssetDatabase.SaveAssetIfDirty(profile.Profile);
                    AssetDatabase.ImportAsset(profile.Path, ImportAssetOptions.ForceUpdate);
                    profile.Profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profile.Path);
                    JObject persisted = BuildToolAndroidSigning.ReadMetadata(profile);
                    if ((bool)persisted["customKeystore"] != custom || (string)persisted["keystorePath"] != store || (string)persisted["alias"] != alias)
                        throw new IOException("서명 설정 저장에 실패했습니다.");
                    if (custom) target.Save(storePassword, keyPassword);
                    return BuildToolExternalSettings.Read(profile, preferencesPath);
                }
                catch
                {
                    File.WriteAllBytes(profile.Path, asset); EditorUtility.ClearDirty(profile.Profile);
                    AssetDatabase.ImportAsset(profile.Path, ImportAssetOptions.ForceUpdate);
                    if (custom) { if (oldCredentials == null) target.Delete(); else File.WriteAllBytes(target.FilePath, oldCredentials); }
                    throw;
                }
            }
            finally { if (validTransfer) transfer.Delete(); }
        }
    }
}

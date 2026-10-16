using System;
using System.Reflection;
using Products.Editor;
using UnityEditor;
using UnityEngine.AddressableAssets;
public static class BuildToolAndroidBuildBoundaryVerification {
 public static object Main() {
 var plugin=(PluginImporter)AssetImporter.GetAtPath("Assets/Scripts/Features/LevelTool/Runtime/LevelToolDocumentPicker.java");
 var match=typeof(ProductProfiles).GetMethod("MatchesProductConstraints",BindingFlags.Static|BindingFlags.NonPublic);
 if((bool)match.Invoke(null,new object[]{plugin,new[]{"PRODUCT_ANDROID_GAME"}})) throw new Exception("Game must exclude tool plugin");
 if(!(bool)match.Invoke(null,new object[]{plugin,new[]{"PRODUCT_LEVEL_EDITOR"}})) throw new Exception("Tool must retain plugin");
 var backend=new UnityProfileBuildBackend(); var profile=backend.Read("3190b50e4249f5046adbed90befd227e");
 var request=new BuildToolRequest {profileGuid=profile.Guid,profilePath=profile.Path,symbol=profile.Product.Symbol,target=profile.Target,output=System.IO.Path.GetFullPath(profile.Product.Output)};
 request.selectionSnapshot=backend.CaptureSelection(request);
 var ready=typeof(ProductProfiles).GetProperty("ContentRoutingReady",BindingFlags.Static|BindingFlags.NonPublic);
 bool original=(bool)ready.GetValue(null); string path=Addressables.LibraryPath;
 try {
 ready.SetValue(null,false); Addressables.LibraryPath="Library/com.unity.addressables/";
 if(backend.IsReady(request,out _)) throw new Exception("Must wait for routing");
 ready.SetValue(null,true); bool rejected=false;
 try {backend.IsReady(request,out _);} catch(InvalidOperationException){rejected=true;}
 if(!rejected) throw new Exception("Must still reject external routing mutation");
 } finally {Addressables.LibraryPath=path;ready.SetValue(null,original);}
 if(!backend.IsReady(request,out var reason)) throw new Exception(reason);
 System.IO.File.WriteAllText("Logs/ApkFixChecks-result.txt","PASS product constraints: game excluded, tool retained\nPASS routing: waits before restore, rejects external changes, ready after restore\n"+ProductProfiles.AuditAll());
 return "PASS product constraints / routing restoration / product audit";
 }
}

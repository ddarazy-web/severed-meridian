using System;
using System.IO;
using Tutorial;
using UnityEditor;
using UnityEngine;

public static partial class TutorialGuidanceVerification
{
    private const int PersistenceLevel = 987650031;
    private const string PersistenceKey = "MoonRabbit.Tutorial.Completed.987650031";
    private const string PersistenceBackup = "Logs/Tutorial/Stage04/persistence-backup.txt";
    public static void WritePersistentRecord()
    {
        try
        {
            if (File.Exists(PersistenceBackup)) throw new Exception("미복원 검사 기록이 있습니다.");
            File.WriteAllText(PersistenceBackup, PlayerPrefs.HasKey(PersistenceKey) ? PlayerPrefs.GetInt(PersistenceKey).ToString() : "missing");
            PlayerPrefs.DeleteKey(PersistenceKey); PlayerPrefs.Save();
            TutorialExecutionContext context = TutorialExecutionContext.CreatePlayer();
            if (!context.ShouldRun(PersistenceLevel, true)) throw new Exception("미완료 영구 기록 검사 초기화 실패");
            context.Complete(PersistenceLevel);
            if (context.ShouldRun(PersistenceLevel, true)) throw new Exception("완료 저장 후 자동 생략 실패");
            File.WriteAllText("Logs/Tutorial/Stage04/persistence-results.txt", "PASS 실제 완료 저장 후 자동 생략\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void ReadPersistentRecord()
    {
        int exit = 0;
        try
        {
            if (!File.Exists(PersistenceBackup)) throw new Exception("복원할 검사 소유 기록이 없습니다.");
            TutorialExecutionContext context = TutorialExecutionContext.CreatePlayer();
            if (context.ShouldRun(PersistenceLevel, true)) throw new Exception("프로세스 재진입 후 완료 기록 소실");
            TutorialExecutionContext test = TutorialExecutionContext.CreateTest(2); test.Complete(PersistenceLevel);
            if (PlayerPrefs.GetInt(PersistenceKey) != 1) throw new Exception("시험 모드가 실제 완료 기록을 변경함");
            File.AppendAllText("Logs/Tutorial/Stage04/persistence-results.txt", "PASS 별도 Unity 프로세스 재진입에서 완료 유지\nPASS 시험 기록은 실제 완료 키를 변경하지 않음\n");
        }
        catch (Exception error) { Debug.LogException(error); exit = 1; }
        finally
        {
            if (File.Exists(PersistenceBackup))
            {
                string original = File.ReadAllText(PersistenceBackup);
                if (original == "missing") PlayerPrefs.DeleteKey(PersistenceKey); else PlayerPrefs.SetInt(PersistenceKey, int.Parse(original));
                PlayerPrefs.Save(); File.Delete(PersistenceBackup);
                File.AppendAllText("Logs/Tutorial/Stage04/persistence-results.txt", "PASS 검사 전용 키의 원래 값/존재 여부 복원\n");
            }
            EditorApplication.Exit(exit);
        }
    }
}

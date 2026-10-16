using System;
using System.Collections.Generic;
using UnityEditor;

namespace Products.Editor
{
    public enum BuildToolRunPhase { Preparing, Selecting, WaitingForEditor, Ready, Building, Finalizing, Succeeded, Failed, Canceled, NeedsAttention, Postprocessing }

    [Serializable]
    public sealed class BuildToolResult
    {
        public BuildToolRunPhase outcome;
        public string error;
        public ulong bytes;
    }

    [Serializable]
    public sealed class BuildToolRequest
    {
        public int version = 1;
        public string id, session, profileGuid, profilePath, product, symbol, output, fingerprint;
        public string startedUtc, finishedUtc, logPath, selectionSnapshot, error, cleanupError, status;
        public BuildTarget target;
        public BuildToolRunPhase phase, pendingOutcome;
        public bool isMock, userConfirmed, selectionStarted, selectionEstablished, callStarted, packsPrepared, modeTouched;
        public BuildToolResult result;
        public string effectiveFingerprint, postprocessError;
        public bool? debugMode;
        public bool sharedSymbolsPrepared;
        public List<BuildToolOptionState> options = new List<BuildToolOptionState>();
        public List<BuildToolOptionMutation> optionMutations = new List<BuildToolOptionMutation>();
        public bool IsTerminal => phase == BuildToolRunPhase.Succeeded || phase == BuildToolRunPhase.Failed || phase == BuildToolRunPhase.Canceled;
        public double ElapsedSeconds => (DateTime.Parse(finishedUtc ?? DateTime.UtcNow.ToString("O")).ToUniversalTime() - DateTime.Parse(startedUtc).ToUniversalTime()).TotalSeconds;
    }

    public interface IBuildToolBackend
    {
        bool IsMock { get; }
        bool IsBusy { get; }
        bool HasCompilationErrors { get; }
        BuildToolProfileInfo Read(string guid);
        void Validate(BuildToolProfileInfo profile);
        string Fingerprint(BuildToolProfileInfo profile);
        string CaptureSelection(BuildToolRequest request);
        void Select(BuildToolRequest request);
        bool IsReady(BuildToolRequest request, out string reason);
        BuildToolResult Build(BuildToolRequest request);
        void Cleanup(BuildToolRequest request);
    }
}

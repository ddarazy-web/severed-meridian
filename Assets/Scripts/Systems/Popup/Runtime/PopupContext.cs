using System;

namespace PopupUI
{
    public readonly struct PopupContext : IEquatable<PopupContext>
    {
        public string SceneKey { get; }
        public string FeatureKey { get; }
        public string SessionKey { get; }
        public PopupContext(string sceneKey, string featureKey, string sessionKey)
        { SceneKey = sceneKey; FeatureKey = featureKey; SessionKey = sessionKey; }
        public bool Equals(PopupContext other) => SceneKey == other.SceneKey && FeatureKey == other.FeatureKey && SessionKey == other.SessionKey;
        public override bool Equals(object obj) => obj is PopupContext other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SceneKey, FeatureKey, SessionKey);
    }
}

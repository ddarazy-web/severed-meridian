using System.Collections.Generic;
using UnityEngine;
namespace Tutorial
{
    public sealed class TutorialUserSampleDefinition : ScriptableObject
    {
        [TextArea] public string description = "";
        public List<TutorialStepDefinition> steps = new List<TutorialStepDefinition>();
    }
}

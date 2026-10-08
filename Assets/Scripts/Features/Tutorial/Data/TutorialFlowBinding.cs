using System;
using System.Collections.Generic;
using Board;
using Levels;

namespace Tutorial
{
    [Serializable]
    public sealed class TutorialFlowBinding
    {
        public string key = "";
        public TutorialFlowField field;
        public int number = 1;
        public string definitionId = "";
        public BoardCoordinate coordinate;
        public List<BoardCoordinate> cells = new List<BoardCoordinate>();
        public TutorialTargetDefinition target = new TutorialTargetDefinition();
        public RabbitColor color;
    }
}

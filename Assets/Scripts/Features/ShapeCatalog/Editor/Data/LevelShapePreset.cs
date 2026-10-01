using Board;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>
    /// 등록 시점의 모양과 장애물 사용 기록. 원본 참조 대신 값을 복사하여
    /// 원본 수정·이름 변경·삭제 후에도 기록을 유지한다. Editor 전용 에셋이다.
    /// </summary>
    public sealed class LevelShapePreset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private bool[] cells;
        [SerializeField] private string sourceName;
        [SerializeField] private int sourceLevelNumber;
        [SerializeField] private string[] obstacleHistory;
        public IReadOnlyList<bool> Cells => cells;
        public string SourceName => sourceName;
        public int SourceLevelNumber => sourceLevelNumber;
        public IReadOnlyList<string> ObstacleHistory => obstacleHistory;
        public bool IsValid => cells != null && cells.Length == BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns && cells.Any(active => active);

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (cells == null || cells.Length != 100) return;
            // 기존 10×10 기록은 마지막 행과 열만 잘라 좌표를 보존한다.
            bool[] cropped = new bool[BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns];
            for (int row = 0; row < BoardDefinition.DefaultRows; row++)
                for (int column = 0; column < BoardDefinition.DefaultColumns; column++)
                    cropped[row * BoardDefinition.DefaultColumns + column] = cells[row * 10 + column];
            cells = cropped;
        }

        /// <summary>레벨의 활성 칸과 장애물 기록을 독립된 배열로 복사한다.</summary>
        /// <param name="source">등록할 저장된 레벨.</param>
        /// <param name="history">집계한 장애물 종류별 기록.</param>
        internal void Capture(LevelDefinition source, IEnumerable<string> history)
        {
            cells = source.Board.Cells.Select(cell => cell.IsActive).ToArray();
            sourceName = source.name; sourceLevelNumber = source.LevelNumber;
            obstacleHistory = history.ToArray();
        }
    }
}

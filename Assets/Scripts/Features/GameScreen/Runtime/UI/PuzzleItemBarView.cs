using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleItemBarView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] buttons;
        public void Configure(UnityEngine.UI.Button[] targets) => buttons = targets;
        public void Bind(PuzzleBoardInput input)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                BoardItem item = (BoardItem)i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => input.SelectItem(item));
            }
        }
        public void Refresh(PuzzleGameSession session, PuzzleBoardInput input)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = session.CanUseItems;
                buttons[i].GetComponent<UnityEngine.UI.Image>().color = input.SelectedItem == (BoardItem)i
                    ? new Color32(245, 200, 90, 255) : new Color32(255, 244, 217, 255);
            }
        }
    }
}

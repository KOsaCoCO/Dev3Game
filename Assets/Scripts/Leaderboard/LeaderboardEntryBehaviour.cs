// One row of the leaderboard chart, holding the four columns of a single player.

using TMPro;
using UnityEngine;


namespace NTGD124
{
    public class LeaderboardEntryBehaviour : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "A single leaderboard row. Column 1 is the name, column 2 is an empty spacer that keeps the " +
            "chart readable, column 3 is the win points and column 4 is the loss points.";


        ///// Public Variables /////

        [Header("The Four Columns")]
        public TMP_Text NameField;
        public TMP_Text SpacerField;
        public TMP_Text WinPointsField;
        public TMP_Text LossPointsField;


        ///// Custom Methods - Action Methods /////

        // Action method: writes one player's numbers into this row.
        public void UpdateRow(string inputName, int inputWinPoints, int inputLossPoints)
        {
            if (NameField != null)
            {
                NameField.text = inputName;
            }

            if (SpacerField != null)
            {
                SpacerField.text = "";
            }

            if (WinPointsField != null)
            {
                WinPointsField.text = inputWinPoints.ToString();
            }

            if (LossPointsField != null)
            {
                LossPointsField.text = inputLossPoints.ToString();
            }
        }
    }
}


// Implementation steps:
// 1. This component sits on a row object built by PotionGameSceneBuilder, one per player.
// 2. The four text fields are filled in automatically when the row is built.
// 3. LeaderboardUIManager calls UpdateRow() for every downloaded player.

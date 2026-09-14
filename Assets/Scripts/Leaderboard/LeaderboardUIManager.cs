// Builds the four column leaderboard chart on screen from the rows the manager downloaded.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace NTGD124
{
    public class LeaderboardUIManager : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Draws the leaderboard chart inside the Menu panel. One row is created for every downloaded " +
            "player, with four columns: name, spacer, win points, loss points.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static LeaderboardUIManager Singleton;

        [Header("Where The Rows Are Placed")]
        public RectTransform RowsParent;

        [Header("Row Look")]
        public float RowHeight = 30f;
        public float RowFontSize = 18f;
        public Color RowTextColour = Color.white;
        public Color EvenRowColour = new Color(1f, 1f, 1f, 0.06f);
        public Color OddRowColour = new Color(1f, 1f, 1f, 0.12f);

        [Header("Column Widths (as a share of the row, must add up to 1)")]
        public float NameColumnShare = 0.45f;
        public float SpacerColumnShare = 0.15f;
        public float WinColumnShare = 0.2f;
        public float LossColumnShare = 0.2f;


        ///// Private Variables /////

        // Every row object on screen, kept so the chart can be wiped and redrawn.
        private List<GameObject> _spawnedRows = new List<GameObject>();


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by LeaderboardManager as soon as a fresh table has been downloaded.
        public void UpdateLeaderboardUI()
        {
            ClearRows();

            if (LeaderboardManager.Singleton == null || RowsParent == null)
            {
                Debug.LogWarning("[LeaderboardUIManager] Nothing to draw, the manager or the parent is missing.");
                return;
            }

            CreateHeaderRow();

            List<string> names = LeaderboardManager.Singleton.PlayerNames;
            List<int> winPoints = LeaderboardManager.Singleton.PlayerWinPoints;
            List<int> lossPoints = LeaderboardManager.Singleton.PlayerLossPoints;

            for (int i = 0; i < names.Count; i++)
            {
                int rowWinPoints = i < winPoints.Count ? winPoints[i] : 0;
                int rowLossPoints = i < lossPoints.Count ? lossPoints[i] : 0;

                LeaderboardEntryBehaviour newRow = CreateRow(i);
                newRow.UpdateRow(names[i], rowWinPoints, rowLossPoints);
            }

            Debug.Log("[LeaderboardUIManager] Chart redrawn with " + names.Count + " player row(s).");
        }


        ///// Custom Methods - Action Methods /////

        // Action method: builds the titles that sit above the chart.
        private void CreateHeaderRow()
        {
            LeaderboardEntryBehaviour headerRow = CreateRow(-1);

            headerRow.NameField.text = "NAME";
            headerRow.SpacerField.text = "";
            headerRow.WinPointsField.text = "WINS";
            headerRow.LossPointsField.text = "LOSSES";

            headerRow.NameField.fontStyle = FontStyles.Bold;
            headerRow.WinPointsField.fontStyle = FontStyles.Bold;
            headerRow.LossPointsField.fontStyle = FontStyles.Bold;
        }


        // Action method: builds one empty row with its four columns already in place.
        private LeaderboardEntryBehaviour CreateRow(int rowIndex)
        {
            GameObject rowObject = new GameObject("LeaderboardRow_" + rowIndex, typeof(RectTransform));
            rowObject.transform.SetParent(RowsParent, false);

            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, RowHeight);

            Image rowBackground = rowObject.AddComponent<Image>();
            rowBackground.color = (rowIndex % 2 == 0) ? EvenRowColour : OddRowColour;

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = RowHeight;
            rowLayout.preferredHeight = RowHeight;

            // The four columns are laid out side by side as shares of the full row width.
            float nameStart = 0f;
            float spacerStart = nameStart + NameColumnShare;
            float winStart = spacerStart + SpacerColumnShare;
            float lossStart = winStart + WinColumnShare;

            TMP_Text nameField = CreateColumn(rowRect, "NameColumn", nameStart, spacerStart, TextAlignmentOptions.Left);
            TMP_Text spacerField = CreateColumn(rowRect, "SpacerColumn", spacerStart, winStart, TextAlignmentOptions.Center);
            TMP_Text winField = CreateColumn(rowRect, "WinColumn", winStart, lossStart, TextAlignmentOptions.Right);
            TMP_Text lossField = CreateColumn(rowRect, "LossColumn", lossStart, 1f, TextAlignmentOptions.Right);

            LeaderboardEntryBehaviour rowBehaviour = rowObject.AddComponent<LeaderboardEntryBehaviour>();
            rowBehaviour.NameField = nameField;
            rowBehaviour.SpacerField = spacerField;
            rowBehaviour.WinPointsField = winField;
            rowBehaviour.LossPointsField = lossField;

            _spawnedRows.Add(rowObject);

            return rowBehaviour;
        }


        // Action method: builds one column of a row, stretched between two shares of the row width.
        private TMP_Text CreateColumn(RectTransform rowRect, string columnName, float startShare, float endShare, TextAlignmentOptions alignment)
        {
            GameObject columnObject = new GameObject(columnName, typeof(RectTransform));
            columnObject.transform.SetParent(rowRect, false);

            RectTransform columnRect = columnObject.GetComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(startShare, 0f);
            columnRect.anchorMax = new Vector2(endShare, 1f);
            columnRect.offsetMin = new Vector2(8f, 0f);
            columnRect.offsetMax = new Vector2(-8f, 0f);

            TextMeshProUGUI columnLabel = columnObject.AddComponent<TextMeshProUGUI>();
            columnLabel.text = "";
            columnLabel.fontSize = RowFontSize;
            columnLabel.color = RowTextColour;
            columnLabel.alignment = alignment;
            columnLabel.enableAutoSizing = true;
            columnLabel.fontSizeMin = 8f;
            columnLabel.fontSizeMax = RowFontSize;

            return columnLabel;
        }


        // Action method: removes every row currently on screen.
        private void ClearRows()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
            {
                if (_spawnedRows[i] != null)
                {
                    Destroy(_spawnedRows[i]);
                }
            }

            _spawnedRows.Clear();
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Link RowsParent to the content object inside the Menu panel, which needs a VerticalLayoutGroup.
// 3. LeaderboardManager calls UpdateLeaderboardUI() by itself after every download.

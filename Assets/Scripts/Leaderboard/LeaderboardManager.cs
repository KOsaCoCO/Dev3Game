// Sends scores to a Firebase Realtime Database and downloads the table back again. Firebase is used
// instead of a plain HTTP leaderboard service because it answers over HTTPS with proper CORS headers,
// which a browser build hosted on itch.io actually needs to be able to talk to it at all.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;


namespace NTGD124
{
    public class LeaderboardManager : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Talks to a Firebase Realtime Database over its plain REST API, no SDK needed. Every player " +
            "gets one row at /leaderboard/<safe key>, holding their display name and their running win and " +
            "loss point totals. Posting a score reads the player's existing totals first and adds the new " +
            "points on top, so scores accumulate across every round they ever play, the same way the old " +
            "dreamlo board worked.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static LeaderboardManager Singleton;

        [Header("Firebase Realtime Database")]
        // Paste your own project's Realtime Database URL here, it looks like:
        // https://your-project-id-default-rtdb.firebaseio.com
        // (or .<region>.firebasedatabase.app for a database created outside the US region).
        // See the Implementation steps at the bottom of this file for how to set one up.
        public string DatabaseUrl = "https://cookinggame-ecd62-default-rtdb.firebaseio.com";

        [Header("Downloaded Table (read only at runtime)")]
        public List<string> PlayerNames = new List<string>();
        public List<int> PlayerWinPoints = new List<int>();
        public List<int> PlayerLossPoints = new List<int>();

        [Header("Testing Values")]
        public string TestUserName = "TestCook";
        public int TestWinPoints = 100;
        public int TestLossPoints = 25;


        ///// Unity Methods /////

        private void OnEnable()
        {
            Singleton = this;
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by GameSequenceManager whenever a round is won or lost.
        public void PostScoreOnline(string playerUserName, int winPoints, int lossPoints)
        {
            StartCoroutine(PostScoreRoutine(playerUserName, winPoints, lossPoints));
        }


        // Called when the menu is opened, so the table on screen is fresh.
        public void DownloadLeaderboard()
        {
            StartCoroutine(DownloadLeaderboardRoutine());
        }


        // Testing trigger: posts the test values above without needing to play a whole round.
        [ContextMenu("Post Test Score")]
        public void PostTestScore()
        {
            PostScoreOnline(TestUserName, TestWinPoints, TestLossPoints);
        }


        ///// Coroutines /////

        // Reads whatever this player already has on the board, adds the new points onto it, then
        // writes the combined total back. Two requests, but it is what keeps scores cumulative.
        private IEnumerator PostScoreRoutine(string playerUserName, int winPoints, int lossPoints)
        {
            string recordUrl = GetDatabaseUrl() + "/leaderboard/" + BuildSafeFirebaseKey(playerUserName) + ".json";

            int existingWinPoints = 0;
            int existingLossPoints = 0;

            using (UnityWebRequest getRequest = UnityWebRequest.Get(recordUrl))
            {
                yield return getRequest.SendWebRequest();

                if (getRequest.result == UnityWebRequest.Result.Success)
                {
                    ReadExistingPoints(getRequest.downloadHandler.text, out existingWinPoints, out existingLossPoints);
                }
            }

            int totalWinPoints = existingWinPoints + winPoints;
            int totalLossPoints = existingLossPoints + lossPoints;

            string jsonBody = "{\"displayName\":\"" + EscapeJsonString(playerUserName) + "\"," +
                               "\"winPoints\":" + totalWinPoints + "," +
                               "\"lossPoints\":" + totalLossPoints + "}";

            using (UnityWebRequest putRequest = new UnityWebRequest(recordUrl, "PUT"))
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                putRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
                putRequest.downloadHandler = new DownloadHandlerBuffer();
                putRequest.SetRequestHeader("Content-Type", "application/json");

                yield return putRequest.SendWebRequest();

                if (putRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[LeaderboardManager] Score sent successfully. Running total: " +
                              totalWinPoints + " win points, " + totalLossPoints + " loss points.");
                }
                else
                {
                    Debug.LogError("[LeaderboardManager] Could not post the score: " + putRequest.error);
                }
            }
        }


        // Downloads the whole /leaderboard object and turns it into the three public lists.
        private IEnumerator DownloadLeaderboardRoutine()
        {
            string listUrl = GetDatabaseUrl() + "/leaderboard.json";

            using (UnityWebRequest getRequest = UnityWebRequest.Get(listUrl))
            {
                yield return getRequest.SendWebRequest();

                if (getRequest.result == UnityWebRequest.Result.Success)
                {
                    InterpretLeaderboard(getRequest.downloadHandler.text);
                }
                else
                {
                    Debug.LogError("[LeaderboardManager] Could not download the leaderboard: " + getRequest.error);
                }
            }
        }


        ///// Custom Methods - Action Methods /////

        // Action method: trims a trailing slash off the configured database URL so paths never
        // end up with a doubled "//" in them.
        private string GetDatabaseUrl()
        {
            return DatabaseUrl.TrimEnd('/');
        }


        // Action method: reads winPoints and lossPoints out of one player's existing record, or
        // leaves both at zero when they have never posted before (Firebase answers "null" then).
        private void ReadExistingPoints(string jsonContent, out int winPoints, out int lossPoints)
        {
            winPoints = 0;
            lossPoints = 0;

            if (string.IsNullOrEmpty(jsonContent) || jsonContent.Trim() == "null")
            {
                return;
            }

            winPoints = ExtractJsonInt(jsonContent, "winPoints");
            lossPoints = ExtractJsonInt(jsonContent, "lossPoints");
        }


        // Action method: reads the downloaded /leaderboard object and fills the three public lists,
        // sorted with the highest win points first.
        private void InterpretLeaderboard(string jsonContent)
        {
            PlayerNames.Clear();
            PlayerWinPoints.Clear();
            PlayerLossPoints.Clear();

            if (!string.IsNullOrEmpty(jsonContent) && jsonContent.Trim() != "null")
            {
                // Every row is one flat, non-nested object, so grabbing everything between the
                // matching braces for each key is enough, no full JSON parser is needed.
                MatchCollection rowMatches = Regex.Matches(jsonContent, "\"[^\"]+\":\\{([^{}]*)\\}");
                List<(string Name, int WinPoints, int LossPoints)> parsedRows = new List<(string, int, int)>();

                foreach (Match rowMatch in rowMatches)
                {
                    string rowBody = rowMatch.Groups[1].Value;
                    string displayName = ExtractJsonString(rowBody, "displayName");

                    parsedRows.Add((
                        string.IsNullOrEmpty(displayName) ? "Unknown" : displayName,
                        ExtractJsonInt(rowBody, "winPoints"),
                        ExtractJsonInt(rowBody, "lossPoints")
                    ));
                }

                parsedRows.Sort((rowA, rowB) => rowB.WinPoints.CompareTo(rowA.WinPoints));

                for (int i = 0; i < parsedRows.Count; i++)
                {
                    PlayerNames.Add(parsedRows[i].Name);
                    PlayerWinPoints.Add(parsedRows[i].WinPoints);
                    PlayerLossPoints.Add(parsedRows[i].LossPoints);
                }
            }

            Debug.Log("[LeaderboardManager] Downloaded " + PlayerNames.Count + " leaderboard row(s).");

            if (LeaderboardUIManager.Singleton != null)
            {
                LeaderboardUIManager.Singleton.UpdateLeaderboardUI();
            }
        }


        // Action method: turns a player's typed name into something safe to use both as a Firebase
        // key (which cannot contain . # $ [ ] /) and as a URL path segment.
        private string BuildSafeFirebaseKey(string playerName)
        {
            string sanitized = playerName;
            char[] forbiddenKeyChars = { '.', '#', '$', '[', ']', '/' };

            for (int i = 0; i < forbiddenKeyChars.Length; i++)
            {
                sanitized = sanitized.Replace(forbiddenKeyChars[i], '_');
            }

            return UnityWebRequest.EscapeURL(sanitized);
        }


        // Action method: escapes quotes and backslashes so a typed name can sit safely inside JSON.
        private string EscapeJsonString(string rawText)
        {
            return rawText.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }


        // Action method: reverses EscapeJsonString once a name comes back from the database.
        private string UnescapeJsonString(string escapedText)
        {
            return escapedText.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }


        // Action method: pulls one string field's value out of a flat JSON object fragment.
        private string ExtractJsonString(string jsonFragment, string fieldName)
        {
            Match match = Regex.Match(jsonFragment, "\"" + fieldName + "\":\"((?:[^\"\\\\]|\\\\.)*)\"");
            return match.Success ? UnescapeJsonString(match.Groups[1].Value) : "";
        }


        // Action method: pulls one integer field's value out of a flat JSON object fragment.
        private int ExtractJsonInt(string jsonFragment, string fieldName)
        {
            Match match = Regex.Match(jsonFragment, "\"" + fieldName + "\":(-?\\d+)");
            int parsedNumber = 0;

            if (match.Success)
            {
                int.TryParse(match.Groups[1].Value, out parsedNumber);
            }

            return parsedNumber;
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Create a free Firebase project at https://console.firebase.google.com (a Google account is
//    all that is needed, no payment details).
// 3. Inside the project, open Build -> Realtime Database -> Create Database. Pick any region and
//    start in test mode for now.
// 4. Copy the database's URL (shown at the top of the Realtime Database page, looks like
//    https://your-project-id-default-rtdb.firebaseio.com) into DatabaseUrl above.
// 5. Before shipping, open the Rules tab and set:
//    { "rules": { "leaderboard": { ".read": true, ".write": true } } }
//    Test mode rules expire after 30 days, this keeps the leaderboard open to everyone permanently
//    without needing player accounts, which is fine for a public score board like this one.
// 6. Use the "Post Test Score" context menu entry to check the connection without playing a round.
// 7. GameSequenceManager calls PostScoreOnline() by itself when a round ends.

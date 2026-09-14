// Decides what the agent does when an instruction is unclear, using a 50/50 roll and a 0 to 100 roll.

using UnityEngine;


namespace NTGD124
{
    // The result of the two rolls, handed back to whoever asked for a guess.
    [System.Serializable]
    public struct UncertaintyRoll
    {
        public bool IsPositive;   // true means the agent guessed in the player's favour
        public int Magnitude;     // 0 to 100, how strongly the guess went that way
        public string Description; // a readable line for the console and the feedback panel
    }


    public class UncertaintyRandomizer : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "Two randomisers in one. The first is a 50/50 that decides whether an unclear instruction is " +
            "read well or badly. The second rolls 0 to 100 to decide how well, or how badly. Every roll is " +
            "written to the console so the outcome can be tracked while testing.";


        ///// Public Variables /////

        [Header("Singleton Access")]
        public static UncertaintyRandomizer Singleton;

        [Header("First Randomiser - Chance Of A Good Guess")]
        [Range(0, 100)] public int PositiveChancePercent = 50;

        [Header("Second Randomiser - Strength Range")]
        [Range(0, 100)] public int MinimumMagnitude = 0;
        [Range(0, 100)] public int MaximumMagnitude = 100;

        [Header("Testing")]
        public bool LogEveryRoll = true;


        ///// Unity Methods /////

        private void Awake()
        {
            Singleton = this;
        }


        ///// Custom Methods - Trigger Methods /////

        // Called by AiAgentBehaviour every time a step is missing information.
        public UncertaintyRoll RollForUnclearStep(CommandStep unclearStep)
        {
            UncertaintyRoll roll = RollOutcome();
            roll.Description = BuildDescription(unclearStep, roll);

            if (LogEveryRoll)
            {
                Debug.Log("[UncertaintyRandomizer] " + roll.Description);
            }

            return roll;
        }


        ///// Custom Methods - Action Methods /////

        // Action method: performs the two rolls that decide the outcome of an unclear instruction.
        public UncertaintyRoll RollOutcome()
        {
            UncertaintyRoll roll = new UncertaintyRoll();

            // First randomiser: is the guess good or bad for the player.
            int firstRoll = Random.Range(0, 100);
            roll.IsPositive = firstRoll < PositiveChancePercent;

            // Second randomiser: how strong that good or bad guess is.
            roll.Magnitude = Random.Range(MinimumMagnitude, MaximumMagnitude + 1);
            roll.Description = "";

            return roll;
        }


        // Action method: turns a roll into a sentence that explains what the agent decided to do.
        private string BuildDescription(CommandStep unclearStep, UncertaintyRoll roll)
        {
            string stepText = unclearStep != null ? unclearStep.GetReadableText() : "an unclear instruction";
            string reasonText = unclearStep != null ? unclearStep.AmbiguityReason : "something was missing";

            string outcomeWord = roll.IsPositive ? "GUESSED WELL" : "GUESSED BADLY";
            string strengthWord = GetStrengthWord(roll);

            return "\"" + stepText + "\" was unclear because " + reasonText + ". " +
                   "Agent " + outcomeWord + " (" + roll.Magnitude + "/100, " + strengthWord + ").";
        }


        // Action method: turns the 0 to 100 number into a word a beginner can read at a glance.
        public string GetStrengthWord(UncertaintyRoll roll)
        {
            if (roll.IsPositive)
            {
                if (roll.Magnitude >= 80) return "almost exactly right";
                if (roll.Magnitude >= 50) return "close enough";
                if (roll.Magnitude >= 20) return "a little off but usable";
                return "barely acceptable";
            }

            if (roll.Magnitude >= 80) return "completely wrong";
            if (roll.Magnitude >= 50) return "badly wrong";
            if (roll.Magnitude >= 20) return "noticeably wrong";
            return "slightly wrong";
        }


        // Action method: turns a roll into the score this step is worth, from 0 to 1.
        public float GetStepQualityFromRoll(UncertaintyRoll roll)
        {
            if (roll.IsPositive)
            {
                // A strong positive roll means the agent got very close to what the player meant.
                return Mathf.Clamp01(roll.Magnitude / 100f);
            }

            // A strong negative roll means the agent got it very wrong, so the quality drops.
            return Mathf.Clamp01(1f - (roll.Magnitude / 100f)) * 0.5f;
        }
    }
}


// Implementation steps:
// 1. Place this component on the GameSystems object.
// 2. Lower PositiveChancePercent to make the agent misunderstand the player more often.
// 3. Keep LogEveryRoll on while testing so every guess shows up in the console.

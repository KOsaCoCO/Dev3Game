// Authored lookup of what a raw ingredient turns into once a tool and a length of time are applied to it.

using System.Collections.Generic;


namespace NTGD124
{
    // Which family of tool touched the ingredient. This groups the nine tools into the five
    // ways they actually change something: knife/scissors/board all cut, only the hammer crushes,
    // only the blender blends, spoon/ladel both stir, and the peeler peels. Cook/heat/pour/wait
    // never reach a tool at all, so they resolve to None and fall back to the plain formula.
    public enum ToolArchetype
    {
        None,
        Cutting,
        Crushing,
        Blending,
        Stirring,
        Peeling
    }


    // Whether the cauldron time landed in the ordinary window or ran on far longer than it needed to.
    public enum TimeBracket
    {
        Normal,
        Over
    }


    // One resolved outcome: the raw ingredient the player picked, and what it became.
    [System.Serializable]
    public class ProcessedIngredientResult
    {
        public string IngredientName = "";
        public ToolArchetype UsedArchetype = ToolArchetype.None;
        public TimeBracket UsedTimeBracket = TimeBracket.Normal;
        public string ResultStateName = "";
        public string ToolWordUsed = "";
        public string VerbWordUsed = "";
        public int MinutesUsed = -1;

        // Filled in by AiAgentBehaviour once the uncertainty roll for the step is known, so the
        // reasoning log line can explain why the cauldron might still be showing bad feedback even
        // when the result text on its own looks perfectly fine.
        public int QualityPercent = 100;
        public string QualityWord = "";
        public string CauldronTemperatureNote = "";

        // True when the player never named this ingredient at all, the agent picked it entirely on
        // its own. AiAgentBehaviour force accepts these instead of waiting on AcceptItemState.
        public bool WasAgentChosenIngredient = false;

        // Action method: writes the "ingredient -> result, NN% word (note)" line for the reasoning log.
        public string GetLogLine()
        {
            string lineText = IngredientName + " -> " + ResultStateName + ", " + QualityPercent + "%";

            if (!string.IsNullOrEmpty(QualityWord))
            {
                lineText += " " + QualityWord;
            }

            if (!string.IsNullOrEmpty(CauldronTemperatureNote))
            {
                lineText += " (" + CauldronTemperatureNote + ")";
            }

            if (WasAgentChosenIngredient)
            {
                lineText += " [agent picked the ingredient]";
            }

            return lineText;
        }
    }


    // Static authored data: for every ingredient, what it becomes under each tool archetype, at a
    // normal amount of time and at a far longer one. Combinations nobody wrote by hand still resolve,
    // through GetResultText's fallback formula, so no ingredient x tool pairing is ever left blank.
    public static class IngredientOutcomeTable
    {
        ///// Private Variables /////

        // wordText (lower case) -> the archetype that tool belongs to.
        private static readonly Dictionary<string, ToolArchetype> _toolArchetypes = new Dictionary<string, ToolArchetype>
        {
            { "knife", ToolArchetype.Cutting },
            { "scissors", ToolArchetype.Cutting },
            { "chopping board", ToolArchetype.Cutting },
            { "hammer", ToolArchetype.Crushing },
            { "blender", ToolArchetype.Blending },
            { "spoon", ToolArchetype.Stirring },
            { "ladel", ToolArchetype.Stirring },
            { "peeler", ToolArchetype.Peeling },
            { "cauldron", ToolArchetype.None },
        };

        // ingredientName -> archetype -> (normal result, over-processed result).
        private static readonly Dictionary<string, Dictionary<ToolArchetype, (string Normal, string Over)>> _authoredOutcomes =
            new Dictionary<string, Dictionary<ToolArchetype, (string, string)>>
        {
            {
                "apple", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("chopped apple", "apple mash") },
                    { ToolArchetype.Crushing, ("crushed apple", "apple pulp") },
                    { ToolArchetype.Blending, ("apple puree", "apple juice") },
                    { ToolArchetype.Peeling, ("peeled apple", "apple peel scraps") },
                }
            },
            {
                "carrot", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("chopped carrot", "carrot shreds") },
                    { ToolArchetype.Crushing, ("crushed carrot", "carrot mash") },
                    { ToolArchetype.Blending, ("carrot juice", "watery carrot juice") },
                    { ToolArchetype.Peeling, ("peeled carrot", "carrot peel scraps") },
                }
            },
            {
                "watermelon", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Crushing, ("normally crushed watermelon", "watermelon sludge") },
                    { ToolArchetype.Cutting, ("sliced watermelon", "watermelon chunks in a puddle") },
                    { ToolArchetype.Blending, ("watermelon juice", "watery watermelon juice") },
                }
            },
            {
                "tree leaves", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("shredded tree leaves", "leaf confetti") },
                    { ToolArchetype.Crushing, ("crushed tree leaves", "leaf dust") },
                    { ToolArchetype.Stirring, ("stirred tree leaves", "wilted tree leaves") },
                }
            },
            {
                "herbs", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("chopped herbs", "minced herbs") },
                    { ToolArchetype.Crushing, ("crushed herbs", "herb paste") },
                    { ToolArchetype.Stirring, ("stirred herbs", "herb slurry") },
                }
            },
            {
                "fruits", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("chopped fruits", "fruit mash") },
                    { ToolArchetype.Blending, ("fruit puree", "fruit juice") },
                    { ToolArchetype.Crushing, ("crushed fruits", "fruit pulp") },
                }
            },
            {
                "cow tongue", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("sliced cow tongue", "shredded cow tongue") },
                    { ToolArchetype.Crushing, ("tenderized cow tongue", "mashed cow tongue") },
                }
            },
            {
                "chicken leg", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("chopped chicken leg", "shredded chicken leg") },
                    { ToolArchetype.Crushing, ("tenderized chicken leg", "crushed chicken bones") },
                }
            },
            {
                "liver", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("sliced liver", "minced liver") },
                    { ToolArchetype.Crushing, ("mashed liver", "liver paste") },
                    { ToolArchetype.Blending, ("liver puree", "liver slurry") },
                }
            },
            {
                "milk", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Stirring, ("stirred milk", "curdled milk") },
                    { ToolArchetype.Blending, ("frothed milk", "whipped milk foam") },
                }
            },
            {
                "honey", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Stirring, ("stirred honey", "crystallized honey") },
                    { ToolArchetype.Blending, ("whipped honey", "runny honey syrup") },
                }
            },
            {
                "tree root", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Crushing, ("mushed tree root", "tree root pulp") },
                    { ToolArchetype.Cutting, ("chopped tree root", "tree root splinters") },
                    { ToolArchetype.Blending, ("tree root juice", "watery tree root juice") },
                }
            },
            {
                "acid flower", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Cutting, ("sliced acid flower", "shredded acid flower petals") },
                    { ToolArchetype.Crushing, ("crushed acid flower", "acid flower paste") },
                    { ToolArchetype.Blending, ("acid flower extract", "acid flower syrup") },
                }
            },
            {
                "crystal", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Crushing, ("crushed crystal", "crystal dust") },
                    { ToolArchetype.Cutting, ("cut crystal shards", "crystal fragments") },
                }
            },
            {
                "salt", new Dictionary<ToolArchetype, (string, string)>
                {
                    { ToolArchetype.Crushing, ("crushed salt", "fine salt powder") },
                    { ToolArchetype.Stirring, ("dissolved salt", "salt brine") },
                }
            },
        };


        ///// Custom Methods - Lookup /////

        // Action method: returns which archetype a tool word belongs to, or None for an unknown tool.
        public static ToolArchetype GetArchetypeForTool(string toolWord)
        {
            if (string.IsNullOrEmpty(toolWord))
            {
                return ToolArchetype.None;
            }

            string keyText = toolWord.ToLower().Trim();

            if (_toolArchetypes.ContainsKey(keyText))
            {
                return _toolArchetypes[keyText];
            }

            return ToolArchetype.None;
        }


        // Action method: returns the result text for one ingredient under one archetype and time bracket.
        // Falls back to a generic formula when that exact combination was never hand written.
        public static string GetResultText(string ingredientName, ToolArchetype archetype, TimeBracket bracket)
        {
            string keyText = ingredientName.ToLower().Trim();

            if (_authoredOutcomes.ContainsKey(keyText) && _authoredOutcomes[keyText].ContainsKey(archetype))
            {
                (string normalText, string overText) = _authoredOutcomes[keyText][archetype];
                return bracket == TimeBracket.Normal ? normalText : overText;
            }

            return BuildFallbackText(ingredientName, archetype, bracket);
        }


        // Action method: every authored (archetype, normal text, over text) triple for one ingredient,
        // used by RecipeGenerator to pick a state the order can actually ask for.
        public static List<(ToolArchetype Archetype, TimeBracket Bracket, string StateName)> GetPossibleStates(string ingredientName)
        {
            List<(ToolArchetype, TimeBracket, string)> possibleStates = new List<(ToolArchetype, TimeBracket, string)>();

            string keyText = ingredientName.ToLower().Trim();

            if (!_authoredOutcomes.ContainsKey(keyText))
            {
                return possibleStates;
            }

            foreach (KeyValuePair<ToolArchetype, (string Normal, string Over)> entry in _authoredOutcomes[keyText])
            {
                possibleStates.Add((entry.Key, TimeBracket.Normal, entry.Value.Normal));
                possibleStates.Add((entry.Key, TimeBracket.Over, entry.Value.Over));
            }

            return possibleStates;
        }


        // Action method: a plain, always-available result for a pairing nobody authored by hand.
        private static string BuildFallbackText(string ingredientName, ToolArchetype archetype, TimeBracket bracket)
        {
            switch (archetype)
            {
                case ToolArchetype.Cutting:
                    return bracket == TimeBracket.Normal ? "chopped " + ingredientName : ingredientName + " shreds";

                case ToolArchetype.Crushing:
                    return bracket == TimeBracket.Normal ? "crushed " + ingredientName : ingredientName + " sludge";

                case ToolArchetype.Blending:
                    return bracket == TimeBracket.Normal ? ingredientName + " puree" : ingredientName + " juice";

                case ToolArchetype.Stirring:
                    return bracket == TimeBracket.Normal ? "stirred " + ingredientName : ingredientName + " mush";

                case ToolArchetype.Peeling:
                    return bracket == TimeBracket.Normal ? "peeled " + ingredientName : ingredientName + " peel scraps";

                default:
                    return bracket == TimeBracket.Normal ? "prepared " + ingredientName : ingredientName + " sludge";
            }
        }
    }
}


// Implementation steps:
// 1. This file only holds data and static lookups, it is not attached to a GameObject.
// 2. IngredientProcessor calls GetResultText once it has resolved a step's tool and time.
// 3. RecipeGenerator calls GetPossibleStates to pick a state an order can require.
// 4. To add a new ingredient variation (the todo list from the design chat: chopped apples,
//    mushed root, carrot juice and friends), add or extend an entry in _authoredOutcomes. Nothing
//    else needs to change, unauthored pairings already resolve through the fallback formula.

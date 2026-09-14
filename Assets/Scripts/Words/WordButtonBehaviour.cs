// A single clickable one-word container that greys itself out once the player has used it.

using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace NTGD124
{
    public class WordButtonBehaviour : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)][SerializeField] private string _componentDescription =
            "One clickable word container. When clicked it reports its word to WordSelectionManager " +
            "and turns grey so the player can see it has already been used. Clicking it again while " +
            "grey deselects it. PageNumber says which of the three word pages it belongs to: " +
            "1 is actions, 2 is tools, 3 is ingredients.";


        ///// Public Variables /////

        [Header("UI References")]
        public Button WordButton;
        public Image BackgroundImage;
        public TMP_Text WordLabel;

        [Header("Colours")]
        public Color AvailableColour = new Color(0.85f, 0.85f, 0.85f, 1f);
        public Color UsedColour = new Color(0.35f, 0.35f, 0.35f, 1f);
        public Color LockedColour = new Color(0.5f, 0.3f, 0.3f, 1f);

        [Header("Word Data (filled by the spawner)")]
        public string WordText = "";
        public WordCategory Category = WordCategory.Verb;
        public int PageNumber = 1;


        ///// Private Variables /////

        // The manager that gets told about every click.
        private WordSelectionManager _ownerManager;

        // True once the player has already picked this word.
        private bool _isUsed = false;

        // True once the raw ingredient this word names has been accepted in a processed form, so
        // it can never be picked again until the next round resets every lock.
        private bool _isLocked = false;


        ///// Custom Methods - Setup /////

        // Action method: fills this container with a word and remembers who to report clicks to.
        // The click listener is attached here, not in Awake, because the spawner fills the
        // WordButton field straight after adding this component, so in Awake it is still empty.
        public void SetupWord(WordDefinition wordDefinition, WordSelectionManager ownerManager, int pageNumber)
        {
            WordText = wordDefinition.WordText;
            Category = wordDefinition.Category;
            PageNumber = pageNumber;
            _ownerManager = ownerManager;

            if (WordLabel != null)
            {
                WordLabel.text = WordText;
            }

            ListenForClicks();
            SetUsedState(false);
        }


        // Action method: input-based trigger setup, hooks this container up to its own button.
        private void ListenForClicks()
        {
            if (WordButton == null)
            {
                Debug.LogError("[WordButton] " + WordText + " has no Button, it can never be clicked.");
                return;
            }

            // Removing first means a container can be set up twice without doubling its clicks.
            WordButton.onClick.RemoveListener(OnWordButtonClicked);
            WordButton.onClick.AddListener(OnWordButtonClicked);
        }


        ///// Custom Methods - Trigger Methods /////

        // Input-based trigger: called by the Button component when the player clicks this container.
        // Clicking an unused word selects it; clicking a used word again deselects it (toggle).
        // A locked word never responds to clicks at all.
        public void OnWordButtonClicked()
        {
            if (_isLocked)
            {
                return;
            }

            if (_ownerManager != null)
            {
                _ownerManager.AddWordToOutput(this);
            }
        }


        ///// Custom Methods - Action Methods /////

        // Action method: greys the container out or brings it back to its available look. A locked
        // container ignores this and always keeps its locked look, so clearing the current sentence
        // can never make an accepted ingredient's button look pickable again.
        public void SetUsedState(bool isUsed)
        {
            _isUsed = isUsed;

            if (_isLocked)
            {
                return;
            }

            if (BackgroundImage != null)
            {
                BackgroundImage.color = isUsed ? UsedColour : AvailableColour;
            }

            if (WordLabel != null)
            {
                WordLabel.color = isUsed ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.black;
            }
        }


        // Action method: tells other scripts whether this word has been used already.
        public bool IsUsed()
        {
            return _isUsed;
        }


        // Action method: permanently locks or unlocks this container. Locking it also marks it used
        // so anything that already skips used containers (like SelectWordByText) skips it too.
        public void SetLocked(bool isLocked)
        {
            _isLocked = isLocked;

            if (isLocked)
            {
                _isUsed = true;

                if (BackgroundImage != null)
                {
                    BackgroundImage.color = LockedColour;
                }

                if (WordLabel != null)
                {
                    WordLabel.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
            }
            else
            {
                SetUsedState(_isUsed);
            }
        }


        // Action method: tells other scripts whether this word is permanently locked out.
        public bool IsLocked()
        {
            return _isLocked;
        }
    }
}


// Implementation steps:
// 1. This component is placed on a container built by PotionGameSceneBuilder.
// 2. The container needs a Button, an Image and a child TMP_Text, all linked in the fields above.
// 3. WordSelectionManager calls SetupWord to fill it, and SetUsedState(false) to reset it.
// 4. SetupWord is what connects the button click, so it must always be called after spawning.
// 5. WordSelectionManager.LockIngredientButton locks a container after its raw ingredient has been
//    accepted in a processed form, UnlockAllIngredientButtons clears every lock for a new round.

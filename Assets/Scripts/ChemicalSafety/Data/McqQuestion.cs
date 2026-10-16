using System;
using UnityEngine;

namespace ChemicalSafety.Data
{
    /// <summary>
    /// One multiple-choice question with options, correct index, and per-option rationales.
    /// Content should come from the GIZ / NOSHTRI scenario document.
    /// </summary>
    [Serializable]
    public class McqQuestion
    {
        [Tooltip("Question id from the curriculum, e.g. MCS 1.1")]
        [SerializeField] private string questionId = "MCS 1.1";

        [TextArea(2, 4)]
        [SerializeField] private string prompt;

        [SerializeField] private string[] options = Array.Empty<string>();

        [Tooltip("Zero-based index of the correct option.")]
        [SerializeField] private int correctOptionIndex;

        [Tooltip("Rationale per option, same length as options.")]
        [SerializeField] private string[] rationales = Array.Empty<string>();

        public string QuestionId => questionId;
        public string Prompt => prompt;
        public string[] Options => options;
        public int CorrectOptionIndex => correctOptionIndex;
        public string[] Rationales => rationales;

        public bool IsCorrect(int selectedIndex) =>
            selectedIndex == correctOptionIndex;

        public string GetRationale(int selectedIndex)
        {
            if (rationales == null || selectedIndex < 0 || selectedIndex >= rationales.Length)
                return string.Empty;

            return rationales[selectedIndex];
        }

        public static McqQuestion Create(
            string id,
            string promptText,
            string[] optionTexts,
            int correctIndex,
            string[] rationaleTexts)
        {
            return new McqQuestion
            {
                questionId = id,
                prompt = promptText,
                options = optionTexts,
                correctOptionIndex = correctIndex,
                rationales = rationaleTexts
            };
        }
    }
}

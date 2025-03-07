using System;
using System.Linq;

namespace Tuvi.Core.Impl.SecurityManagement
{
    internal class SeedQuiz : ISeedQuiz
    {
        public SeedQuiz(string[] seedPhrase)
        {
            seedNormalized = seedPhrase.Select(word => SeedNormalizer.NormalizeWord(word)).ToArray();
        }

        public int[] GenerateTask()
        {
            int wordsCount = seedNormalized.Length;
            int[] wordNumbers = new int[wordsCount / 2];

            System.Random random = new System.Random();
            for (int i = 0; i < wordNumbers.Length; i++)
            {
                int randomNumber;
                do
                {
#pragma warning disable CA5394 // Do not use unsafe random number generators.
                    // In this case, cryptographic security is not needed.
                    randomNumber = random.Next(0, wordsCount);
#pragma warning restore CA5394 // Do not use unsafe random number generators.
                }
                while (wordNumbers.Take(i).Contains<int>(randomNumber));
                wordNumbers[i] = randomNumber;
            }

            hiddenWords = wordNumbers;
            return hiddenWords;
        }

        public bool VerifySolution(string[] solution, out bool[] result)
        {
            result = null;

            try
            {
                if (solution == null)
                {
                    return false;
                }

                result = new bool[hiddenWords.Length];

                for (int i = 0; i < hiddenWords.Length; i++)
                {
                    string solutionWord = SeedNormalizer.NormalizeWord(solution[i]);
                    string correctWord = seedNormalized[hiddenWords[i]];

                    bool solutionIsNotEmpty = !string.IsNullOrEmpty(solutionWord);
                    bool solutionIsCorrect = string.Equals(solutionWord, correctWord, StringComparison.OrdinalIgnoreCase);
                    result[i] = solutionIsNotEmpty && solutionIsCorrect;
                }

                return !result.Any(e => e == false);
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private readonly string[] seedNormalized;
        private int[] hiddenWords;
    }
}

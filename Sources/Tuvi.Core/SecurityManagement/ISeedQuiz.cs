namespace Tuvi.Core
{
    /// <summary>
    /// Can be used to confirm user has properly stored his seed phrase.
    /// </summary>
    public interface ISeedQuiz
    {
        /// <summary>
        /// Generate task for quiz game.
        /// Task is the seed phrase words indexes user has to enter.
        /// </summary>
        /// <returns>Task as an array of seed phrase word's indexes.</returns>
        int[] GenerateTask();

        /// <summary>
        /// Verifies user entered quiz solution.
        /// Solution is the words user was asked to enter with call of <see cref="GenerateTask()"/>.
        /// </summary>
        /// <param name="solution">Solution as an array of requested seed phrase words in specified order.</param>
        /// <param name="result">Output parameter which shows if each entered word is correct separately.</param>
        /// <returns>If quiz passed or not</returns>
        bool VerifySolution(string[] solution, out bool[] result);

        // Example:
        // Seed = { "ozone",    "drill",    "grab",
        //          "fiber",    "curtain",  "grace",
        //          "pudding",  "thank",    "cruise",
        //          "elder",    "eight",    "picnic" }
        // Task = { 3, 9, 11, 0, 8, 4 }
        // Solution = { "fiber", "elder", "picnic", "ozone", "cruise", "curtain" }
    }
}

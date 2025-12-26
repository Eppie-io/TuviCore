// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

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

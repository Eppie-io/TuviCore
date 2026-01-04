// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core;
using Tuvi.Core.Impl.SecurityManagement;

namespace SecurityManagementTests
{
    public class SeedQuizTests
    {
        [Test]
        public void TaskNumbersRange()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            int[] task = quiz.GenerateTask();

            Assert.That(testSeed.Length, Is.GreaterThanOrEqualTo(task.Length));
            foreach (var number in task)
            {
                Assert.That(number, Is.GreaterThanOrEqualTo(0));
                Assert.That(number, Is.LessThan(testSeed.Length));
            }
        }

        [Test]
        public void TaskRandomizationRange()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);

            using var tokenSource = new CancellationTokenSource();
            var cancelationToken = tokenSource.Token;
            tokenSource.CancelAfter(5000);

            Action testDelegate = new Action(
                () =>
                {
                    int[] task;
                    for (int i = 0; i < testSeed.Length; i++)
                    {
                        do
                        {
                            task = quiz.GenerateTask();
                            cancelationToken.ThrowIfCancellationRequested();
                        }
                        while (task.Contains(i) == false);
                    }
                });

            Assert.DoesNotThrow(() => Task.Run(testDelegate, cancelationToken).Wait());
        }

        [Test]
        public void TaskNumbersUnique()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            int[] task = quiz.GenerateTask();

            Assert.That(task.Length, Is.EqualTo(task.Distinct().Count()));
        }

        [Test]
        public void SolutionIsRight()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            int[] task = quiz.GenerateTask();

            string[] solution = new string[task.Length];
            for (int i = 0; i < solution.Length; i++)
            {
                solution[i] = testSeed[task[i]];
            }

            Assert.That(quiz.VerifySolution(solution, out bool[] res), Is.True);
            Assert.That(res.Any(e => e == false), Is.False);
        }

        [Test]
        public void SolutionIsNull()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            var task = quiz.GenerateTask();

            Assert.That(quiz.VerifySolution(null, out bool[] res), Is.False);
        }

        [Test]
        public void SolutionIsPartiallyProvided()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            var task = quiz.GenerateTask();

            var partialSolution = new string[3]
            {
                testSeed[task[0]],
                testSeed[task[1]],
                testSeed[task[2]]
            };
            Assert.That(quiz.VerifySolution(partialSolution, out bool[] res), Is.False);
        }

        [Test]
        public void SolutionIsPartiallyTrue()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            var task = quiz.GenerateTask();

            var partialSolution = new string[6]
            {
                testSeed[task[0]],
                "abra",
                testSeed[task[2]],
                "kadabra",
                "435345",
                testSeed[task[5]]
            };
            Assert.That(quiz.VerifySolution(partialSolution, out bool[] res), Is.False);
            Assert.That(res.SequenceEqual(
                new bool[]
                {
                    true,
                    false,
                    true,
                    false,
                    false,
                    true
                }), Is.True);
        }

        [Test]
        public void SolutionWordsAreEmpty()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            var task = quiz.GenerateTask();

            var emptySolutionWords = new string[task.Length];
            Assert.That(quiz.VerifySolution(emptySolutionWords, out bool[] res), Is.False);
            Assert.That(res.Any(e => e == true), Is.False);
        }

        [Test]
        public void VerifyCalledBeforeGenerate()
        {
            var testSeed = TestData.GetTestSeed();
            ISeedQuiz quiz = SecurityManagerCreator.CreateSeedQuiz(testSeed);
            var testSolution = testSeed.Take(6).ToArray();

            Assert.That(quiz.VerifySolution(testSolution, out bool[] res), Is.False);
            Assert.That(res, Is.Null);
        }
    }
}

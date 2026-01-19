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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage.Tests
{
    public class DataStorageAIAgentsTests : TestWithStorageBase
    {
        [SetUp]
        public async Task SetupAsync()
        {
            DeleteStorage();
            TestData.Setup();

            await CreateDataStorageAsync().ConfigureAwait(true);
        }

        private static LocalAIAgent CreateAgent(string name,
                                                Account account = null,
                                                LocalAIAgent pre = null,
                                                LocalAIAgent post = null)
        {
            return new LocalAIAgent
            {
                Name = name,
                AgentSpecialty = LocalAIAgentSpecialty.EmailComposer,
                SystemPrompt = "You are a helpful assistant.",
                Account = account,
                PreProcessorAgent = pre,
                PostProcessorAgent = post,
                IsAllowedToSendingEmail = true,
                DoSample = true,
                TopK = 40,
                TopP = 0.9f,
                Temperature = 0.7f
            };
        }

        private static Account CreateTestAccount(string email)
        {
            var account = TestData.CreateAccountWithFolder();
            account.Email = new EmailAddress(email);
            return account;
        }

        [Test]
        public async Task AddAIAgentShouldInsertAndAssignId()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var agent = CreateAgent("agent-1");

                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                var agents = await db.GetAIAgentsAsync().ConfigureAwait(true);

                Assert.That(agents.Count, Is.EqualTo(1));
                Assert.That(agents[0].Id, Is.Positive);
                Assert.That(agents[0].Name, Is.EqualTo("agent-1"));
            }
        }

        [Test]
        public async Task AddAIAgentWithAccountAndProcessorsShouldPersistRelationships()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("agent-account@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);

                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);
                Assert.That(account.Id, Is.Positive);

                var pre = CreateAgent("pre");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("post");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("main", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                var saved = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(saved, Is.Not.Null);
                Assert.That(saved.AccountId, Is.EqualTo(account.Id));
                Assert.That(saved.Account, Is.Not.Null);
                Assert.That(saved.Account.Email.Address, Is.EqualTo(account.Email.Address));

                Assert.That(saved.PreProcessorAgentId, Is.EqualTo(pre.Id));
                Assert.That(saved.PreProcessorAgent, Is.Not.Null);
                Assert.That(saved.PreProcessorAgent.Name, Is.EqualTo("pre"));

                Assert.That(saved.PostProcessorAgentId, Is.EqualTo(post.Id));
                Assert.That(saved.PostProcessorAgent, Is.Not.Null);
                Assert.That(saved.PostProcessorAgent.Name, Is.EqualTo("post"));
            }
        }

        [Test]
        public async Task GetAIAgentsShouldReturnAllAgents()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                await db.AddAIAgentAsync(CreateAgent("a1")).ConfigureAwait(true);
                await db.AddAIAgentAsync(CreateAgent("a2")).ConfigureAwait(true);
                await db.AddAIAgentAsync(CreateAgent("a3")).ConfigureAwait(true);

                var agents = await db.GetAIAgentsAsync().ConfigureAwait(true);

                Assert.That(agents.Count, Is.EqualTo(3));
                Assert.That(agents.Select(x => x.Name).OrderBy(x => x).ToList(), Is.EqualTo(new List<string> { "a1", "a2", "a3" }));
            }
        }

        [Test]
        public async Task GetAIAgentShouldReturnNullForUnknownId()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var agent = await db.GetAIAgentAsync(999999).ConfigureAwait(true);

                Assert.That(agent, Is.Null);
            }
        }

        [Test]
        public async Task GetAIAgentShouldPopulateLinkedEntities()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("linked@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var pre = CreateAgent("pre-linked");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("post-linked");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("main-linked", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Account, Is.Not.Null);
                Assert.That(loaded.Account.Id, Is.EqualTo(account.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(pre.Id));
                Assert.That(loaded.PostProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PostProcessorAgent.Id, Is.EqualTo(post.Id));
            }
        }

        [Test]
        public async Task UpdateAIAgentShouldUpdateScalarFields()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var agent = CreateAgent("update-me");
                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                agent.Name = "updated";
                agent.SystemPrompt = "new prompt";
                agent.AgentSpecialty = LocalAIAgentSpecialty.Summarizer;
                agent.IsAllowedToSendingEmail = false;
                agent.DoSample = false;
                agent.TopK = 10;
                agent.TopP = 0.5f;
                agent.Temperature = 0.2f;

                await db.UpdateAIAgentAsync(agent).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);
                Assert.That(loaded, Is.Not.Null);

                Assert.That(loaded.Name, Is.EqualTo("updated"));
                Assert.That(loaded.SystemPrompt, Is.EqualTo("new prompt"));
                Assert.That(loaded.AgentSpecialty, Is.EqualTo(LocalAIAgentSpecialty.Summarizer));
                Assert.That(loaded.IsAllowedToSendingEmail, Is.False);
                Assert.That(loaded.DoSample, Is.False);
                Assert.That(loaded.TopK, Is.EqualTo(10));
                Assert.That(loaded.TopP, Is.EqualTo(0.5f));
                Assert.That(loaded.Temperature, Is.EqualTo(0.2f));
            }
        }

        [Test]
        public async Task UpdateAIAgentShouldUpdateRelationships()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account1 = CreateTestAccount("acc1@test");
                await db.AddAccountAsync(account1).ConfigureAwait(true);
                account1 = await db.GetAccountAsync(account1.Email).ConfigureAwait(true);

                var account2 = CreateTestAccount("acc2@test");
                await db.AddAccountAsync(account2).ConfigureAwait(true);
                account2 = await db.GetAccountAsync(account2.Email).ConfigureAwait(true);

                var pre1 = CreateAgent("pre1");
                await db.AddAIAgentAsync(pre1).ConfigureAwait(true);
                var post1 = CreateAgent("post1");
                await db.AddAIAgentAsync(post1).ConfigureAwait(true);

                var pre2 = CreateAgent("pre2");
                await db.AddAIAgentAsync(pre2).ConfigureAwait(true);
                var post2 = CreateAgent("post2");
                await db.AddAIAgentAsync(post2).ConfigureAwait(true);

                var main = CreateAgent("main", account: account1, pre: pre1, post: post1);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                main.Account = account2;
                main.PreProcessorAgent = pre2;
                main.PostProcessorAgent = post2;

                await db.UpdateAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.EqualTo(account2.Id));
                Assert.That(loaded.Account, Is.Not.Null);
                Assert.That(loaded.Account.Id, Is.EqualTo(account2.Id));

                Assert.That(loaded.PreProcessorAgentId, Is.EqualTo(pre2.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(pre2.Id));

                Assert.That(loaded.PostProcessorAgentId, Is.EqualTo(post2.Id));
                Assert.That(loaded.PostProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PostProcessorAgent.Id, Is.EqualTo(post2.Id));
            }
        }

        [Test]
        public async Task UpdateAIAgentShouldPreserveRelationshipsWhenNavigationPropertiesAreNull()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("acc-preserve@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var pre = CreateAgent("pre-preserve");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("post-preserve");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("main-preserve", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                main.Account = null;
                main.PreProcessorAgent = null;
                main.PostProcessorAgent = null;

                await db.UpdateAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.EqualTo(account.Id));
                Assert.That(loaded.Account, Is.Not.Null);
                Assert.That(loaded.Account.Id, Is.EqualTo(account.Id));
                Assert.That(loaded.PreProcessorAgentId, Is.EqualTo(pre.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(pre.Id));
                Assert.That(loaded.PostProcessorAgentId, Is.EqualTo(post.Id));
                Assert.That(loaded.PostProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PostProcessorAgent.Id, Is.EqualTo(post.Id));
            }
        }

        [Test]
        public async Task UpdateAIAgentShouldClearRelationshipsWhenExplicitlySetToZero()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("acc-clear@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var pre = CreateAgent("pre-clear");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("post-clear");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("main-clear", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                main.Account = null;
                main.PreProcessorAgent = null;
                main.PostProcessorAgent = null;
                main.AccountId = 0;
                main.PreProcessorAgentId = 0;
                main.PostProcessorAgentId = 0;

                await db.UpdateAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.Zero);
                Assert.That(loaded.Account, Is.Null);
                Assert.That(loaded.PreProcessorAgentId, Is.Zero);
                Assert.That(loaded.PreProcessorAgent, Is.Null);
                Assert.That(loaded.PostProcessorAgentId, Is.Zero);
                Assert.That(loaded.PostProcessorAgent, Is.Null);
            }
        }

        [Test]
        public async Task DeleteAIAgentShouldRemoveAgent()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var agent = CreateAgent("to-delete");
                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                await db.DeleteAIAgentAsync(agent.Id).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);
                Assert.That(loaded, Is.Null);

                var agents = await db.GetAIAgentsAsync().ConfigureAwait(true);
                Assert.That(agents.Count, Is.Zero);
            }
        }

        [Test]
        public async Task DeleteAIAgentShouldNotThrowWhenAgentDoesNotExist()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                Assert.DoesNotThrowAsync(async () => await db.DeleteAIAgentAsync(123456).ConfigureAwait(true));
            }
        }

        [Test]
        public async Task GetAIAgentsShouldHydrateRelationshipsForEachAgent()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("agents-hydration@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var pre = CreateAgent("pre-hyd");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("post-hyd");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var withLinks = CreateAgent("with-links", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(withLinks).ConfigureAwait(true);

                var noLinks = CreateAgent("no-links");
                await db.AddAIAgentAsync(noLinks).ConfigureAwait(true);

                var agents = await db.GetAIAgentsAsync().ConfigureAwait(true);

                var byName = agents.ToDictionary(x => x.Name, x => x);

                Assert.That(byName.ContainsKey("with-links"), Is.True);
                Assert.That(byName.ContainsKey("no-links"), Is.True);

                var a1 = byName["with-links"];
                Assert.That(a1.AccountId, Is.EqualTo(account.Id));
                Assert.That(a1.Account, Is.Not.Null);
                Assert.That(a1.Account.Id, Is.EqualTo(account.Id));
                Assert.That(a1.PreProcessorAgentId, Is.EqualTo(pre.Id));
                Assert.That(a1.PreProcessorAgent, Is.Not.Null);
                Assert.That(a1.PreProcessorAgent.Id, Is.EqualTo(pre.Id));
                Assert.That(a1.PostProcessorAgentId, Is.EqualTo(post.Id));
                Assert.That(a1.PostProcessorAgent, Is.Not.Null);
                Assert.That(a1.PostProcessorAgent.Id, Is.EqualTo(post.Id));

                var a2 = byName["no-links"];
                Assert.That(a2.AccountId, Is.Zero);
                Assert.That(a2.Account, Is.Null);
                Assert.That(a2.PreProcessorAgentId, Is.Zero);
                Assert.That(a2.PreProcessorAgent, Is.Null);
                Assert.That(a2.PostProcessorAgentId, Is.Zero);
                Assert.That(a2.PostProcessorAgent, Is.Null);
            }
        }

        [Test]
        public async Task UpdateAIAgentWithOnlyForeignKeyIdsShouldPreserveForeignKeys()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("fk-only@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var pre = CreateAgent("fk-pre");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("fk-post");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("fk-main", account: account, pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                main.Account = null;
                main.PreProcessorAgent = null;
                main.PostProcessorAgent = null;

                main.AccountId = account.Id;
                main.PreProcessorAgentId = pre.Id;
                main.PostProcessorAgentId = post.Id;

                await db.UpdateAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.EqualTo(account.Id));
                Assert.That(loaded.Account, Is.Not.Null);
                Assert.That(loaded.Account.Id, Is.EqualTo(account.Id));
                Assert.That(loaded.PreProcessorAgentId, Is.EqualTo(pre.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(pre.Id));
                Assert.That(loaded.PostProcessorAgentId, Is.EqualTo(post.Id));
                Assert.That(loaded.PostProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PostProcessorAgent.Id, Is.EqualTo(post.Id));
            }
        }

        [Test]
        public async Task GetAIAgentShouldReturnNullNavigationWhenLinkedAgentsAreDeleted()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var pre = CreateAgent("dangling-pre");
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var post = CreateAgent("dangling-post");
                await db.AddAIAgentAsync(post).ConfigureAwait(true);

                var main = CreateAgent("dangling-main", pre: pre, post: post);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                await db.DeleteAIAgentAsync(pre.Id).ConfigureAwait(true);
                await db.DeleteAIAgentAsync(post.Id).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgentId, Is.EqualTo(pre.Id));
                Assert.That(loaded.PostProcessorAgentId, Is.EqualTo(post.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Null);
                Assert.That(loaded.PostProcessorAgent, Is.Null);
            }
        }

        [Test]
        public async Task GetAIAgentShouldReturnNullNavigationWhenLinkedAccountIsDeleted()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var account = CreateTestAccount("dangling-account@test");
                await db.AddAccountAsync(account).ConfigureAwait(true);
                account = await db.GetAccountAsync(account.Email).ConfigureAwait(true);

                var agent = CreateAgent("agent-with-account", account: account);
                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                await db.DeleteAccountAsync(account).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.EqualTo(account.Id));
                Assert.That(loaded.Account, Is.Null);
            }
        }

        [Test]
        public async Task GetAIAgentShouldPerformShallowLoad()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var grandPre = CreateAgent("grand-pre");
                await db.AddAIAgentAsync(grandPre).ConfigureAwait(true);

                var pre = CreateAgent("pre", pre: grandPre);
                await db.AddAIAgentAsync(pre).ConfigureAwait(true);

                var main = CreateAgent("main", pre: pre);
                await db.AddAIAgentAsync(main).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(main.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(pre.Id));

                Assert.That(loaded.PreProcessorAgent.PreProcessorAgentId, Is.EqualTo(grandPre.Id));
                Assert.That(loaded.PreProcessorAgent.PreProcessorAgent, Is.Null, "Second-level navigation property should not be hydrated (shallow load)");
            }
        }

        [Test]
        public async Task UpdateAIAgentShouldSupportSelfReference()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var agent = CreateAgent("self-ref");
                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                agent.PreProcessorAgent = agent;
                agent.PostProcessorAgent = agent;

                await db.UpdateAIAgentAsync(agent).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);

                Assert.That(loaded.PreProcessorAgentId, Is.EqualTo(agent.Id));
                Assert.That(loaded.PreProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PreProcessorAgent.Id, Is.EqualTo(agent.Id));

                Assert.That(loaded.PostProcessorAgentId, Is.EqualTo(agent.Id));
                Assert.That(loaded.PostProcessorAgent, Is.Not.Null);
                Assert.That(loaded.PostProcessorAgent.Id, Is.EqualTo(agent.Id));
            }
        }

        [Test]
        public async Task AddAIAgentShouldAllowDuplicateNames()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var a1 = CreateAgent("duplicate-name");
                await db.AddAIAgentAsync(a1).ConfigureAwait(true);

                var a2 = CreateAgent("duplicate-name");
                Assert.DoesNotThrowAsync(async () => await db.AddAIAgentAsync(a2).ConfigureAwait(true));

                var agents = await db.GetAIAgentsAsync().ConfigureAwait(true);
                var dupes = agents.Where(x => x.Name == "duplicate-name").ToList();

                Assert.That(dupes.Count, Is.EqualTo(2));
                Assert.That(dupes[0].Id, Is.Not.EqualTo(dupes[1].Id));
            }
        }

        [Test]
        public async Task AddAIAgentWithUnsavedAccountShouldNotPersistLink()
        {
            using (var db = GetDataStorage())
            {
                await db.OpenAsync(Password).ConfigureAwait(true);

                var unsavedAccount = CreateTestAccount("unsaved@test");

                var agent = CreateAgent("agent-unsaved-acc", account: unsavedAccount);

                await db.AddAIAgentAsync(agent).ConfigureAwait(true);

                var loaded = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);

                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.AccountId, Is.Zero);
                Assert.That(loaded.Account, Is.Null);

                await db.AddAccountAsync(unsavedAccount).ConfigureAwait(true);

                var loadedAfter = await db.GetAIAgentAsync(agent.Id).ConfigureAwait(true);
                Assert.That(loadedAfter.Account, Is.Null);
            }
        }
    }
}

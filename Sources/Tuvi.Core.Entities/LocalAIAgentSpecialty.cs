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

namespace Tuvi.Core.Entities
{
    public enum LocalAIAgentSpecialty
    {
        // Content Creation & Editing
        Writer,             // Creates original content
        Rewriter,           // Rewrites and reformulates text
        Proofreader,        // Checks grammar and spelling
        Summarizer,         // Summarizes emails and documents
        EmailComposer,      // Generates email drafts

        // Language & Communication
        Translator,         // Translates emails between languages
        SentimentAnalyzer,  // Analyzes emotional tone of emails
        PersonalitySimulator, // Simulates writing style of a person

        // Information Processing & Research
        Researcher,         // Finds relevant information
        Analyst,            // Analyzes trends and extracts insights
        DataExtractor,      // Extracts key data from emails and attachments
        NewsAggregator,     // Gathers and summarizes news

        // Organization & Workflow
        Scheduler,          // Plans meetings and sets reminders
        Prioritizer,        // Identifies and ranks important emails
        Classifier,         // Sorts emails into categories
        Archivist,          // Saves and retrieves important documents

        // Decision Making & Compliance
        Jurist,             // Provides legal assistance
        ComplianceChecker,  // Ensures regulatory compliance
        Auditor,            // Verifies data consistency and detects anomalies
        CyberSecurity,      // Detects phishing attempts and ensures data security

        // Communication & Negotiation
        Mediator,           // Facilitates conflict resolution
        Negotiator,         // Assists in negotiations and argumentation

        // Customer & Business Support
        CustomerSupport,    // Handles automated responses and inquiries
        FinanceAdvisor,     // Analyzes financial transactions and expenses
        MarketingAdvisor,   // Provides marketing strategies and A/B testing insights

        // Code & Technical Review
        CodeReviewer,       // Analyzes and suggests improvements to code snippets

        // Email Filtering & Security
        SpamFilter,         // Detects and filters out spam emails
        WhitelistManager    // Manages a whitelist of contacts allowed to send emails
    }
}

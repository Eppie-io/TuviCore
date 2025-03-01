using System;
using System.Collections.Generic;
using System.Text;

namespace Tuvi.Core.Entities
{
    /// <summary>
    /// Represents a local AI agent with a system prompt and an associated email address.
    /// </summary>
    public class LocalAIAgent
    {
        /// <summary>
        /// Gets or sets the name of the local AI agent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the system prompt for the local AI agent.
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Gets or sets the email address associated with the local AI agent.
        /// </summary>
        public EmailAddress Email { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the local AI agent is allowed to send emails.
        /// </summary>
        public bool IsAllowedToSendingEmail { get; set; }
    }
}

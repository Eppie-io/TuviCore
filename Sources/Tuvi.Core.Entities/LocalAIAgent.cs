namespace Tuvi.Core.Entities
{
    /// <summary>
    /// Represents a local AI agent with a system prompt and an associated email address.
    /// </summary>
    public class LocalAIAgent
    {
        /// <summary>
        /// Gets or sets the ID of the local AI agent.
        /// </summary>
        [SQLite.PrimaryKey]
        [SQLite.AutoIncrement]
        public uint Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the local AI agent.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the specialty of the local AI agent.
        /// </summary>
        public LocalAIAgentSpecialty AgentSpecialty { get; set; }

        /// <summary>
        /// Gets or sets the system prompt for the local AI agent.
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Gets or sets the email address associated with the local AI agent.
        /// </summary>
        [SQLite.Indexed]
        public int AccountId { get; set; }

        [SQLite.Ignore]
        public Account Account { get; set; }

        [SQLite.Ignore]
        public EmailAddress Email => Account?.Email;

        /// <summary>
        /// Gets or sets a value indicating whether the local AI agent is allowed to send emails.
        /// </summary>
        public bool IsAllowedToSendingEmail { get; set; }

        /// <summary>
        /// Gets or sets the preprocessor agent for the local AI agent.
        /// </summary>        
        public uint PreProcessorAgentId { get; set; }

        [SQLite.Ignore]
        public LocalAIAgent PreProcessorAgent { get; set; }

        /// <summary>
        /// Gets or sets the postprocessor agent for the local AI agent.
        /// </summary>
        public uint PostProcessorAgentId { get; set; }

        [SQLite.Ignore]
        public LocalAIAgent PostProcessorAgent { get; set; }

        /// <summary>
        /// Gets or sets the DoSample parameter.
        /// If Sampling is disabled, a greedy approach will be used and the model will select the most likely token every time. 
        /// If enabled, tokens will be selected based on the token probability distribution. 
        /// The Top K, Top P, and Temperature parameters only apply if sampling is enabled.
        /// </summary>
        public bool DoSample { get; set; }

        /// <summary>
        /// Gets or sets the top K parameter.
        /// The top K parameter tells the model to only consider the K most probable tokens at each generation step.
        /// Lower this value for more predictable, focused generation and increase it for more random response.
        /// </summary>
        public int TopK { get; set; }

        /// <summary>
        /// Gets or sets the top P parameter.
        /// The top P parameter tells the model to only consider tokens up until a cumulative probability of P. 
        /// Lower this value for more predictable, focused generation and increase it for more random response.
        /// </summary>
        public float TopP { get; set; }

        /// <summary>
        /// Gets or sets the temperature parameter.
        /// The temperature parameter is a scaling factor for the probability distribution of tokens during generation. 
        /// Values lower than 1 will produce more deterministic response while values higher than 1 will increase randomness.
        /// </summary>
        public float Temperature { get; set; }
        

        /// <summary>
        /// Returns the name of the local AI agent.
        /// </summary>
        /// <returns>The name of the local AI agent.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}

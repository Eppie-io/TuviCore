using System;

namespace Tuvi.Core.Entities
{
    public class LocalAIAgentEventArgs : EventArgs
    {
        public LocalAIAgent AIAgent { get; }

        public LocalAIAgentEventArgs(LocalAIAgent agent)
        {
            AIAgent = agent;
        }
    }

}

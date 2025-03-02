using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Core.Entities;

namespace Tuvi.Core.DataStorage
{    
    public interface IAIAgentsStorage
    {
        Task AddAIAgentAsync(LocalAIAgent agent, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<LocalAIAgent>> GetAIAgentsAsync(CancellationToken cancellationToken = default);
        Task<LocalAIAgent> GetAIAgentAsync(uint id, CancellationToken cancellationToken = default);
        Task UpdateAIAgentAsync(LocalAIAgent agent, CancellationToken cancellationToken = default);
        Task DeleteAIAgentAsync(uint id, CancellationToken cancellationToken = default);
    }
}

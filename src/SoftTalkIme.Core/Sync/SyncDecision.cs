using SoftTalkIme.Core.Models;

namespace SoftTalkIme.Core.Sync;

public static class SyncDecision
{
    public static IReadOnlyList<string> FindChangedScopes(SyncHead head, KnowledgeSnapshot snapshot)
    {
        var changedScopes = new List<string>();
        foreach (var scope in SyncConstants.FormalScopes)
        {
            var remote = head.LatestByScope.TryGetValue(scope, out var remoteValue) ? remoteValue : 0L;
            var local = snapshot.ScopeSequences.TryGetValue(scope, out var localValue) ? localValue : 0L;
            if (remote != local)
            {
                changedScopes.Add(scope);
            }
        }

        return changedScopes;
    }
}

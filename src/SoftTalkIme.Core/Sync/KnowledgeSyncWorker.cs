using SoftTalkIme.Core.Models;
using SoftTalkIme.Core.Storage;

namespace SoftTalkIme.Core.Sync;

public sealed class KnowledgeSyncWorker
{
    private readonly KnowledgeSyncCoordinator _coordinator;
    private readonly KnowledgeSnapshotStore _snapshotStore;
    private readonly string _snapshotPath;

    public KnowledgeSyncWorker(
        KnowledgeSyncCoordinator coordinator,
        KnowledgeSnapshotStore snapshotStore,
        string snapshotPath)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _snapshotPath = string.IsNullOrWhiteSpace(snapshotPath)
            ? throw new ArgumentException("快照路径不能为空。", nameof(snapshotPath))
            : snapshotPath;
    }

    public async Task<SyncRunResult> PollAndSaveAsync(CancellationToken cancellationToken = default)
    {
        var current = _snapshotStore.LoadOrEmpty(_snapshotPath);
        var result = await _coordinator.PollOnceAsync(current, cancellationToken).ConfigureAwait(false);
        if (result.UpdatedScopes.Count > 0)
        {
            _snapshotStore.SaveAtomic(_snapshotPath, result.Snapshot);
        }
        return result;
    }

    public async Task RunAsync(
        Action<SyncRunResult>? onSuccess = null,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await PollAndSaveAsync(cancellationToken).ConfigureAwait(false);
                onSuccess?.Invoke(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception);
            }

            await Task.Delay(SyncConstants.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}

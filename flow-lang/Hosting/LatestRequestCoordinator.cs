namespace FlowLang.Hosting;

/// <summary>How a coordinated job ended.</summary>
public enum JobStatus
{
    /// <summary>The job finished successfully and was the latest request; its result is published.</summary>
    Succeeded,
    /// <summary>The job finished with errors; the last good result stays published.</summary>
    Failed,
    /// <summary>The job was cancelled by <see cref="LatestRequestCoordinator{T}.Dispose"/> or its own token.</summary>
    Cancelled,
    /// <summary>The job ran out of its time budget; the last good result stays published.</summary>
    TimedOut,
    /// <summary>A newer request arrived; the job was cancelled and any result it produced is discarded.</summary>
    Superseded,
}

/// <summary>What a job's work function reports.</summary>
public sealed record JobResult<T>(JobStatus Status, T? Value, string? Error = null) where T : class;

/// <summary>A job's final state, delivered once per submitted request.</summary>
public sealed record JobCompletion<T>(long Generation, JobStatus Status, T? Value, string? Error) where T : class;

/// <summary>
/// Latest-request-wins job coordinator for editor-style hosts (watch mode, a
/// language server, a DAW). At most one job runs at a time. Submitting a new request
/// cancels the running job, waits for it to terminate, then starts the new one.
/// Each request carries a generation number, and only the latest generation can
/// publish a result: a job that finishes after being superseded is rejected even if
/// its cancellation arrived too late to stop it. A failed or timed-out job leaves
/// the last good result in place, so a broken edit never replaces working output.
///
/// Cancellation is cooperative (see <see cref="Core.FlowEngine.Evaluate"/>). Work
/// that ignores its token is still counted in <see cref="ActiveJobs"/> after the
/// termination grace period expires; use a process worker when a hard stop is required.
/// </summary>
public sealed class LatestRequestCoordinator<T> : IDisposable where T : class
{
    private readonly object _lock = new();
    private long _generation;
    private CancellationTokenSource? _currentCts;
    private Task? _currentTask;
    private int _active;
    private bool _disposed;

    /// <summary>How long a new request waits for the superseded job to stop before starting anyway.</summary>
    public TimeSpan TerminationGrace { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>The most recent successful result of a latest-generation job.</summary>
    public T? LastGood { get; private set; }

    /// <summary>Generation that produced <see cref="LastGood"/>; 0 before any success.</summary>
    public long LastGoodGeneration { get; private set; }

    /// <summary>Generation of the most recently submitted request.</summary>
    public long LatestGeneration
    {
        get { lock (_lock) return _generation; }
    }

    /// <summary>Jobs whose work function is still executing.</summary>
    public int ActiveJobs => Volatile.Read(ref _active);

    /// <summary>Raised once per request when it ends (on the job's thread).</summary>
    public event Action<JobCompletion<T>>? Completed;

    /// <summary>
    /// Submits <paramref name="work"/> as the newest request. The returned task
    /// completes when this request has ended, with its status.
    /// </summary>
    public Task<JobCompletion<T>> Submit(Func<CancellationToken, JobResult<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return SubmitAsync(token => Task.FromResult(work(token)));
    }

    /// <summary>Async work follows the same latest-generation publication rule.</summary>
    public Task<JobCompletion<T>> SubmitAsync(Func<CancellationToken, Task<JobResult<T>>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        long generation;
        CancellationTokenSource cts;
        Task? previous;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            generation = ++_generation;
            _currentCts?.Cancel();
            previous = _currentTask;
            cts = new CancellationTokenSource();
            _currentCts = cts;
        }

        var task = Task.Run(async () =>
        {
            if (previous is not null)
                await WaitForTermination(previous).ConfigureAwait(false);

            JobResult<T> result;
            if (cts.IsCancellationRequested)
            {
                result = new JobResult<T>(JobStatus.Cancelled, null);
            }
            else
            {
                Interlocked.Increment(ref _active);
                try
                {
                    result = await work(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    result = new JobResult<T>(JobStatus.Cancelled, null);
                }
                catch (Exception ex)
                {
                    result = new JobResult<T>(JobStatus.Failed, null, ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _active);
                }
            }

            JobCompletion<T> completion;
            lock (_lock)
            {
                bool latest = generation == _generation && !_disposed;
                var status = latest ? result.Status : JobStatus.Superseded;
                if (latest && status == JobStatus.Succeeded)
                {
                    LastGood = result.Value;
                    LastGoodGeneration = generation;
                }
                completion = new JobCompletion<T>(generation, status, latest ? result.Value : null, result.Error);
            }
            // A superseded result is never published; release it if it owns resources.
            if (completion.Status == JobStatus.Superseded && result.Value is IDisposable stale)
                stale.Dispose();

            Completed?.Invoke(completion);
            return completion;
        });

        lock (_lock)
        {
            if (generation == _generation) _currentTask = task;
        }
        return task;
    }

    private async Task WaitForTermination(Task previous)
    {
        try
        {
            await previous.WaitAsync(TerminationGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The superseded work ignored cancellation; it stays counted in ActiveJobs.
        }
        catch
        {
            // The previous job's own failure is reported through its completion.
        }
    }

    /// <summary>Cancels the running job and waits (up to the grace period) for it to stop.</summary>
    public void Dispose()
    {
        Task? running;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _currentCts?.Cancel();
            running = _currentTask;
        }
        if (running is not null)
        {
            try { running.Wait(TerminationGrace); } catch { /* reported via completion */ }
        }
    }
}

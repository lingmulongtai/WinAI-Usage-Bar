namespace WinAiUsageBar.Infrastructure.Windows;

public sealed class SingleInstanceGuard(string mutexName) : IDisposable
{
    private static readonly object AcquiredNamesLock = new();
    private static readonly HashSet<string> AcquiredNames = new(StringComparer.Ordinal);

    private readonly Mutex mutex = new(initiallyOwned: false, mutexName);
    private bool acquired;
    private bool disposed;
    private bool registeredName;

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (acquired)
        {
            return true;
        }

        if (!TryRegisterName())
        {
            return false;
        }

        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero);
            if (!acquired)
            {
                UnregisterName();
            }

            return acquired;
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
            return true;
        }
        catch
        {
            UnregisterName();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (acquired)
        {
            mutex.ReleaseMutex();
            acquired = false;
        }

        UnregisterName();
        mutex.Dispose();
        disposed = true;
    }

    private bool TryRegisterName()
    {
        lock (AcquiredNamesLock)
        {
            if (AcquiredNames.Contains(mutexName))
            {
                return false;
            }

            AcquiredNames.Add(mutexName);
            registeredName = true;
            return true;
        }
    }

    private void UnregisterName()
    {
        if (!registeredName)
        {
            return;
        }

        lock (AcquiredNamesLock)
        {
            AcquiredNames.Remove(mutexName);
            registeredName = false;
        }
    }
}

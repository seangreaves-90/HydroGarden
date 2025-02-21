namespace HydroGarden.Foundation.Common.Locking
{
    public sealed class AsyncReaderWriterLock : IDisposable
    {
        private readonly SemaphoreSlim _readLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private volatile int _readerCount;
        private volatile bool _isDisposed;

        public async Task<IDisposable> ReaderLockAsync(CancellationToken ct = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AsyncReaderWriterLock));

            await _readLock.WaitAsync(ct);
            try
            {
                Interlocked.Increment(ref _readerCount);
                if (_readerCount == 1)
                {
                    await _writeLock.WaitAsync(ct);
                }
            }
            finally
            {
                _readLock.Release();
            }

            return new ReaderLockScope(this);
        }

        public async Task<IDisposable> WriterLockAsync(CancellationToken ct = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AsyncReaderWriterLock));

            await _writeLock.WaitAsync(ct);
            return new WriterLockScope(this);
        }

        private void ReleaseReaderLock()
        {
            if (_isDisposed) return;

            _readLock.Wait();
            try
            {
                var count = Interlocked.Decrement(ref _readerCount);
                if (count == 0)
                {
                    _writeLock.Release();
                }
            }
            finally
            {
                _readLock.Release();
            }
        }

        private void ReleaseWriterLock()
        {
            if (_isDisposed) return;
            _writeLock.Release();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _readLock.Dispose();
            _writeLock.Dispose();
        }

        private sealed class ReaderLockScope : IDisposable
        {
            private readonly AsyncReaderWriterLock _lock;
            private bool _isDisposed;

            public ReaderLockScope(AsyncReaderWriterLock @lock)
            {
                _lock = @lock;
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _lock.ReleaseReaderLock();
            }
        }

        private sealed class WriterLockScope : IDisposable
        {
            private readonly AsyncReaderWriterLock _lock;
            private bool _isDisposed;

            public WriterLockScope(AsyncReaderWriterLock @lock)
            {
                _lock = @lock;
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _lock.ReleaseWriterLock();
            }
        }
    }
}

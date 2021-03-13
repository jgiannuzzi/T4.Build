using System;
using System.Threading;

namespace T4.Build
{
    class Lock : IDisposable
    {
        Mutex mutex;

        public Lock(string lockPath, int timeout)
        {
            mutex = new Mutex(false, lockPath);
            if (!mutex.WaitOne(timeout * 1000))
                throw new Exception($"Another instance of T4.Build is still running after {timeout} sec");
        }

        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
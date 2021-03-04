using System;
using System.IO;
using System.Threading;

namespace T4.Build
{
    class Lock : IDisposable
    {
        FileStream lockFile;

        public Lock(string lockPath, int timeout)
        {
            var locked = SpinWait.SpinUntil(() =>
            {
                try
                {
                    lockFile = File.Open(lockPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }

            }, timeout * 1000);

            if (!locked)
                throw new Exception($"Another instance of T4.Build is still running after {timeout} sec");
        }

        public void Dispose()
        {
            lockFile.Close();

            try
            {
                File.Delete(lockFile.Name);
            }
            // If another process acquired the lock in the meantime, it becomes its responsibility to delete it
            catch (IOException) { }
        }
    }
}
using System.Threading;

namespace PageOfBob.Backup.Processes
{
    public abstract class AbstractProcessConfiguration
    {
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public string EncryptionKey { get; set; }
        public CancellationToken CancellationToken => cancellationTokenSource.Token;
        public void Cancel() => cancellationTokenSource.Cancel();
    }
}

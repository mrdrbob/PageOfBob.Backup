using System;
using System.IO;

namespace PageOfBob.Backup.FileSystem
{
    public class PartialStream : Stream
    {
        readonly Stream stream;
        readonly long start;
        readonly long end;

        public PartialStream(Stream stream, long start, long end)
        {
            this.stream = stream;
            this.start = start;
            this.end = end;

            this.stream.Seek(start, SeekOrigin.Begin);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => end - start;

        public override long Position {
            get => stream.Position - start;
            set => stream.Position = start + value;
        }

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = end - stream.Position;
            int adjustedCount = (int)Math.Min(remaining, count);
            if (adjustedCount == 0)
                return 0;

            return stream.Read(buffer, offset, adjustedCount);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: return stream.Seek(offset + start, SeekOrigin.Begin);
                case SeekOrigin.End: return stream.Seek(end - offset, SeekOrigin.End);
                case SeekOrigin.Current: return stream.Seek(offset, SeekOrigin.Current);
                default: throw new NotImplementedException();
            }
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);
    }
}

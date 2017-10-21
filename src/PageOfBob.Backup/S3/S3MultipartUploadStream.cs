using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace PageOfBob.Backup.S3
{
    /// <summary>
    /// SUPER hacky way of stremaing a file to S3 without having to know its length.
    /// Buffers writes into a 5MB memory stream buffer, then uploads chunks via S3's multipart upload
    /// 
    /// But... why?
    /// 
    /// IDestination's Write method is expected to be given a stream to WRITE to.
    /// S3 SDK's upload expects to be given a stream to READ from.
    /// So a intermediary is required.
    /// 
    /// * Could write to a memory stream, but this doubles that amount of memory required to run the backup process.  And with
    ///   PackedDestination the stream could be very large (hundreds of megs)
    /// * Could write to a temp file, but this is a lot of unnecessary I/O and might thrash the disk.  PackedDestination is already
    ///   being read from a file, so we'd basically duplicate the file for no reason before uploading.
    /// * Tried to use a pipe, but a pipe doesn't know it's length, and both PutObjectRequest and TransferUtility need to know
    ///   the length of the stream before it uploads.
    /// 
    /// Instead, with a multipart upload, we only need to know the size of the individual parts, not the full size of the object.
    /// So we basically buffer small parts into a memory stream until the stream is done being written to.  Each time the buffer
    /// is full, flush it as a part to S3.
    /// 
    /// This does add some overhead (more requests than streaming an entire file all at once). A smarter implementation would 
    /// probably detect if the entire file was written into a single buffer and do a normal PutObject request without the
    /// multipart stuff, but I'll consider that a TODO for now.
    /// </summary>
    public class S3MultipartUploadStream : Stream
    {
        const long BufferSize = 5 * 1024 * 1024;

        readonly AmazonS3Client client;
        readonly string bucketName;
        readonly string key;
        readonly string uploadId;
        Stream internalBuffer;
        readonly IList<UploadPartResponse> responses = new List<UploadPartResponse>();
        int partNumber = 1;

        S3MultipartUploadStream(AmazonS3Client client, string bucketName, string key, string uploadId)
        {
            this.client = client;
            this.bucketName = bucketName;
            this.key = key;
            this.uploadId = uploadId;

            internalBuffer = GlobalContext.MemoryStreamManager.GetStream();
        }

        public static async Task<S3MultipartUploadStream> CreateAsync(AmazonS3Client client, string bucketName, string key)
        {
            var uploadRequest = new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key
            };

            var response = await client.InitiateMultipartUploadAsync(uploadRequest);
            return new S3MultipartUploadStream(client, bucketName, key, response.UploadId);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            if (internalBuffer.Length == 0)
                return;

            internalBuffer.Seek(0, SeekOrigin.Begin);

            var uploadRequest = new UploadPartRequest
            {
                UploadId = uploadId,
                PartSize = internalBuffer.Length,
                InputStream = internalBuffer,
                BucketName = bucketName,
                Key = key,
                PartNumber = partNumber
            };

            var response = client.UploadPartAsync(uploadRequest).Result;
            responses.Add(response);

            internalBuffer.Dispose();
            internalBuffer = GlobalContext.MemoryStreamManager.GetStream();
            partNumber += 1;
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (internalBuffer.Length == 0)
                return;

            internalBuffer.Seek(0, SeekOrigin.Begin);

            var uploadRequest = new UploadPartRequest
            {
                UploadId = uploadId,
                PartSize = internalBuffer.Length,
                InputStream = internalBuffer,
                BucketName = bucketName,
                Key = key,
                PartNumber = partNumber
            };

            var response = await client.UploadPartAsync(uploadRequest);
            responses.Add(response);

            internalBuffer.Dispose();
            internalBuffer = GlobalContext.MemoryStreamManager.GetStream();
            partNumber += 1;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (internalBuffer.Length + count > BufferSize)
            {
                Flush();
            }

            internalBuffer.Write(buffer, offset, count);
        }
        public async Task CompleteUploadAsync()
        {
            await FlushAsync();

            var endRequest = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key,
                UploadId = uploadId
            };
            endRequest.AddPartETags(responses);

            await client.CompleteMultipartUploadAsync(endRequest);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                internalBuffer.Dispose();
            }
        }
    }
}

using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.S3;
using Amazon.S3.Model;

namespace PageOfBob.Backup.S3
{
    public class S3Destination : IDestinationWithPartialRead
    {
        readonly string bucket;
        readonly string prefix;
        readonly string accessKey;
        readonly string secretKey;
        readonly ISet<string> knownS3Keys = new HashSet<string>();

        public S3Destination(string bucket, string prefix, string accessKey, string secretKey)
        {
            this.bucket = bucket;
            this.prefix = prefix;
            this.accessKey = accessKey;
            this.secretKey = secretKey;
        }

        public async Task InitAsync()
        {
            // Build a list of known keys
            using (var client = new AmazonS3Client(accessKey, secretKey))
            {
                ListObjectsV2Response response;
                var request = new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix,
                    MaxKeys = 1000
                };

                do
                {
                    response = await client.ListObjectsV2Async(request);

                    foreach (var obj in response.S3Objects)
                    {
                        knownS3Keys.Add(obj.Key);
                    }

                    request.ContinuationToken = response.NextContinuationToken;
                } while (response.IsTruncated == true);
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            string fullKey = ObjectKey(key);
            if (!knownS3Keys.Contains(fullKey))
                return false;

            using (var client = new AmazonS3Client(accessKey, secretKey))
            {
                await client.DeleteObjectAsync(bucket, fullKey);
            }
            knownS3Keys.Remove(fullKey);

            return true;
        }

        public Task<bool> ExistsAsync(string key, ReadOptions readOptions)
            => Task.FromResult(knownS3Keys.Contains(ObjectKey(key)));

        public async Task<bool> ReadAsync(string key, long begin, long end, ReadOptions readOptions, ProcessStream readAction)
        {
            string fullKey = ObjectKey(key);
            if (!knownS3Keys.Contains(fullKey))
                return false;

            var request = new GetObjectRequest
            {
                ByteRange = new ByteRange(begin, end - 1),
                Key = fullKey,
                BucketName = bucket
            };

            var t = request.ByteRange.FormattedByteRange;

            using (var client = new AmazonS3Client(accessKey, secretKey))
            using (var response = await client.GetObjectAsync(request))
            {
                await readAction(response.ResponseStream);
            }

            return true;
        }

        public async Task<bool> ReadAsync(string key, ReadOptions readOptions, ProcessStream readAction)
        {
            string fullKey = ObjectKey(key);
            if (!knownS3Keys.Contains(fullKey))
                return false;

            using (var client = new AmazonS3Client(accessKey, secretKey))
            using (var response = await client.GetObjectAsync(bucket, fullKey))
            {
                await readAction(response.ResponseStream);
            }

            return true;
        }

        public async Task<bool> WriteAsync(string key, WriteOptions writeOptions, ProcessStream writeAction)
        {
            bool overwrite = (writeOptions & WriteOptions.Overwrite) != 0;
            string fullKey = ObjectKey(key);
            if (knownS3Keys.Contains(fullKey) && !overwrite)
                return false;

            using (var client = new AmazonS3Client(accessKey, secretKey))
            using (var s3UploadStream = await S3MultipartUploadStream.CreateAsync(client, bucket, fullKey))
            {
                await writeAction(s3UploadStream);
                await s3UploadStream.CompleteUploadAsync();
            }

            return true;
        }

        public Task FlushAsync() => Task.CompletedTask;

        string ObjectKey(string key) => $"{prefix}/{key}";
    }
}

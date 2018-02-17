using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace PageOfBob.Backup.App
{
    public class BackupSetConfiguration
    {
        public BackupSetConfiguration(ISource source, IDestination destination)
        {
            Source = source;
            Destination = destination;
        }

        public ISource Source { get; }
        public IDestination Destination { get; }
        public HashSet<string> SkipFilesContaining { get; set; }
        public HashSet<string> SkipCompressionContaining { get; set; }

        public static BackupSetConfiguration FromJson(string rawString)
        {
            dynamic config = JObject.Parse(rawString);
            var source = SourceFactory.Resolve(config.source);
            var destination = DestinationFactory.Resolve(config.destination);

            var set = new BackupSetConfiguration(source, destination);

            if (config.skipFilesContaining != null)
            {
                set.SkipFilesContaining = new HashSet<string>();
                foreach (string file in config.skipFilesContaining)
                    set.SkipFilesContaining.Add(file);
            }
            if (config.skipCompressionContaining != null)
            {
                set.SkipCompressionContaining = new HashSet<string>();
                foreach (string file in config.skipCompressionContaining)
                    set.SkipCompressionContaining.Add(file);
            }

            return set;
        }

        public static BackupSetConfiguration FromJsonFile(string filePath)
        {
            var fileContents = File.ReadAllText(filePath);
            return FromJson(fileContents);
        }
    }
}

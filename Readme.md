# PageOfBob.Backup

A rudimentary system for backing up files.

THIS BACKUP SYSTEM IS NOT WELL TESTED AND YOU SHOULD NOT USE IT.

## How it works

* An `ISource` is a source of files to be backed up. The only `ISource` currently implemented is the `FileSystemSource` which will read files from the file system, everything under a base path (unless filtered out).

* An `IDestination` is a store where largish chunks of data can be stored. There are two implemented: `FileSystemDestination` which dumps data into a tree on a normal file system, and `PackedDestination`, which wraps another `IDestination`, but packs data into large chunks. If you use `FileSystemDestniation` directly, you will have at least one file in the destination for every file in your source, with `PackedDestination`, files will be packed into large files and then flushed to the wrapped destination, making for significantly fewer files (but is slower and must write the packed file to a temporary file before flushing to the final destination).

## Neat Features

* The backup processor chunks files up and hashes them, eliminating duplicate files (and potentially) duplicate chunks of data. It also gzip compresses files that meet certain criteria, hopefully reducing the size of your backup archive.
* As an append-only archive, it's not possible to remove old files, so your collection of Scooby-Doo and Scrappy-Doo fan fiction will be safe forever despite your regrets.
* Supports client-side AES encryption.
* Can backup, restore, and verify files.  Can also run a report of all files and versions within the archive.
* In my testing, it seems reasonably fast and uses a reasonable amount of memory, though more extensive testing on larger backup archives is needed.

## Glaring omissions

* Makes no attempt at preventing you doing dumb things like backing up stuff with an encryption key and then losing the key.
* Does not use VSS or other techniques to read files that are in use. If a file is locked, it'll just have to skip it.
* Has no scheduling. Must be run from the command line.
* Has no GUI. Must be run from the command line.
* Will probably do terrible things if you run multiple instances at once.

The most likely use-case for this would be to run as a scheduled job in the middle of the night on files that are unlikely to be locked by the OS, in conjunction with another backup system that is proven to work.  Even then, NO WARRANTY, use at your own risk.

## TODO:

* Document command line arguments
* Add S3 as an IDestination choice
* Better heuristics for which files to compress
* More configurable filtering options
* Test, test, test

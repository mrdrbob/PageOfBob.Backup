# PageOfBob.Backup

A rudimentary system for backing up files.

THIS BACKUP SYSTEM IS NOT WELL TESTED AND YOU SHOULD NOT USE IT.

## How it works

* An `ISource` is a source of files to be backed up. The only `ISource` currently implemented is the `FileSystemSource` which will read files from the file system, everything under a base path (unless filtered out).

* An `IDestination` is a store where largish chunks of data can be stored. There are three implemented: 
  * `FileSystemDestination` dumps data into a tree on a normal file system.
  * `S3Destination` writes files to an S3 bucket with a prefix.
  * `PackedDestination` wraps another `IDestination`, but packs data into large chunks.

If you use `FileSystemDestniation` or `S3Destination` directly, you will have at least one file in the destination for every unique file in your source, with `PackedDestination`, files will be packed into large files and then flushed to the wrapped destination, making for significantly fewer files (but is slower and must write the packed file to a temporary file before flushing to the final destination).

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

## Usage

Backups are based on **Backup Sets**. A Backup Set is JSON file that declares a *Source* of files (or whatever) to backup, and a *Destination* to put the backups.  There are a couple Sources to chose from, but I'd recommend using the (poorly named) `GroupedSource`, for reasons described below.

### Sources

To build a custom source, implement `ISource` and update `SourceFactory`.  The following are built-in:

#### `FileSystemSource`

Recursively lists all files from a single starting directory.  Configuration consists of a single parameter, `basePath` which is the base path from which to recursively list files.

Example config:

```json
	"source": {
		"type": "FileSystemSource",
		"config": {
			"basePath": "C:\\ImportantData"
		}
	}
```

#### `GroupedSource`

A collection of named sources.  Generally prefer this over a single source, as you can add more sources without having to recreate your entire backup.  A grouped source takes a list of `NamedSource` objects.

Example config:

```json
	"source": {
		"type": "GroupedSource",
		"config": {
			"sources": [
				{
					"name": "Data",
					"source": {
						"type": "FileSystemSource",
						"config": {
							"basePath": "C:\\Data"
						}
					}
				},
				{
					"name": "Desktop",
					"source": {
						"type": "FileSystemSource",
						"config": {
							"basePath": "C:\\Users\\Me\\Desktop"
						}
					}
				}
			}
		}
	}
```

### Destinations

To build a custom destination, implement either `IDestinationWithPartialRead` or `IDestination`.  If your provider *can* implement `IDestinationWithPartialRead`, it *should*.  Destinations with partial read support are supported by `PackedDestination`, described below.

#### `FileSystemDestination`

Persists file chunks to the file system directly as files.  Simplest destination, but depending on the underlying file system, may cause a very large number of files and directories to be created.  Is configured with a base path.

Example config:

```json
	"destination": {
		"type": "FileSystemDestination",
		"config": {
			"basePath": "E:\\Backup"
		}
	}
```

#### `PackedDestination`

Wraps another destination, but packs the blobs into larger files before persisting to the wrapped destination.  This is useful when make a very large backup set and you don't wish to have too many files written to the destination.   It's configured with a destination to wrap.

Example config:

```json
	"destination": {
		"type": "PackedDestination",
		"config": {
			"destination": {
				"type": "FileSystemDestination",
				"config": {
					"basePath": "E:\\Backup"
				}
			}
		}
	}
```

#### `S3Destination`

Writes files to an S3 bucket, with prefix.  It takes bucket, prefix, access key, and secret key as configuration.  **This one is not well tested.**

Example config:

```json
	"destination": {
		"type": "S3Destination",
		"config": {
			"bucket": "my_backup_s3_bucket",
			"prefix": "backup",
			"accessKey": "XXX",
			"secretKey": "XXX"
		}
	}
```

### Other configuration options

There are two other options you can apply in the JSON config file:

* `skipFilesContaining` - Skip any file containing any of the strings in this array.  Example: `[ "node_modules", ".git", ".svn" ]`.  **These files will not be backed up.**
* `skipCompressionContaining` - Do not compress any file containing any of the strings in this array.  Example: `[ '.jpg', '.png', '.etc' ]`

### A completed example backup set JSON:

```json
	{
		"source": {
			"type": "GroupedSource",
			"config": {
				"sources": [
					{
						"name": "Data",
						"source": {
							"type": "FileSystemSource",
							"config": {
								"basePath": "C:\\Data"
							}
						}
					},
					{
						"name": "Desktop",
						"source": {
							"type": "FileSystemSource",
							"config": {
								"basePath": "C:\\Users\\Me\\Desktop"
							}
						}
					}
				]
			}
		},
		"destination": {
			"type": "PackedDestination",
			"config": {
				"destination": {
					"type": "FileSystemDestination",
					"config": {
						"basePath": "E:\\Backup"
					}
				}
			}
		},
		"skipFilesContaining": [ "node_modules", ".git", ".svn", "thumbs.db", ".etc" ],
		"skipCompressionContaining": [ ".mp2", ".mp3", ".mp4", ".etc" ]
	}
```

## Command Line Arguements

In general, you execute commands through the dontnet run time:

`dotnet PageOfBob.Backup.App.dll commandname --args`

The commands are as follows:

### backup

Backs up files from a source to a destination.  Requires a set JSON file.

* `-s|--set <set>` - Path to the set JSON file.
* `-p|--progress <progress>` - Save a "progress" file every `<progress>` files.  (Note: this will probably be moved to the set file).
* `-k|--key <key>` - Encryption key, if using encryption.

### restore

Restore files from a backup to a source.  Requires a set.

* `-s|--set <set>` - Path to the set JSON file.
* `-p|--prefix <prefix>` - Prefix to match for restoring files, or path of a file to restore.
* `-k|--key <key>` - Decryption key, if using encryption.
* `-e|--entry <entry>` - Backup Entry key.  Each time a backup is run it creates an entry; when restoring you can choose a specific run (entry) to use to get a file from a point in time.
* `-v|--verify` - Verify only.  Does not actually restore any files, but will do (slow) comparisons between the backup and source files and report any discrepancies.
* `-f|--force` - Force.  Restoring backed up files will not overwrite existing files by default.  This argument will overwrite files.

### report

Reports on the contents of the backup.  This is useful for searching for files and versions of files.  Requires a set.

* `-s|--set <set>` - Path to the set JSON file.
* `-p|--prefix <prefix>` - Will filter reported files to only those matching the prefix.
* `-k|--key <key>` - Decryption key if using encryption.
* `-e|--entry <entry>` - Only report from a particular backup entry.  Each time a backup is run, an entry is created.  If not provided, all entries are listed.
* `-o|--out <filename>` - Write report to a file.  Otherwise, report is dumped to `stdout`.
* `-s|--subhashes` - Files are frequently broken down into smaller chunks (called subhashes).  This option will list all of the subhashes associated to a file.
* `-i|--includeDupes` - Usually, files will be listed in the report the first time they appear; with this option, all instances of a file are reported.

### gen-key

Generates a key that can be used for encryption.  Takes no arguments and does not save the key, just reports it to `stdout`.

## TODO:

* Better heuristics for which files to compress
* More configurable filtering options
* Test, test, test

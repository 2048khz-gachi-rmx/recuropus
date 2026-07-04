CLI application to recursively convert audio files to Opus format using `ffmpeg` and `opusenc`.  
Slop-converted from a Luvit application for less hipster technology

## Features

- **Structure preserving**: the output's structure matches the original. (unless you disable it with `-f`)
- **Smart default output**: output directory defaults to the input folder's name (e.g., `-i ./music` outputs to `./music`); override with `-o`
- **Concurrent conversions**: configurable number of simultaneous jobs
- **Regex filtering**: filter files, directories, or full paths using regex
- **Non-audio file copying**: copies non-audio files to the output directory
- **Dry run mode**: preview actions without executing
- **Native AOT**: single executable. Still depends on ffmpeg and opusenc though

## Usage

```bash
# Basic usage: convert all audio files in current directory
recuropus.exe

# Specify input and output directories
recuropus.exe -i "C:\Music\Source" -o "C:\Music\Converted"

# Convert at 320kbps with 10 concurrent jobs
recuropus.exe -b 320 -j 10

# Dry run to preview what would happen
recuropus.exe -d

# Filter files by regex pattern
recuropus.exe -fr ".*episode.*"

# Flatten output (all files in single directory)
recuropus.exe -f

# Case-insensitive regex matching
recuropus.exe -fr ".*EPISODE.*" -ip
```

## CLI Options

| Option                             | Description                                       | Default            |
| ---------------------------------- | ------------------------------------------------- | ------------------ |
| `--bitrate`, `-b`                  | Target bitrate in kbps                            | `160`              |
| `--maxjobs`, `-j`                  | Max simultaneous conversion jobs                  | core count         |
| `--outdir`, `-o`, `--output`       | Output directory                                  | input folder name  |
| `--indir`, `-i`                    | Input directory to scan                           | current directory  |
| `--fullregex`, `-p`                | Full path regex filter (overrides file/dir regex) | _(none)_           |
| `--fileregex`, `-fr`               | Filename regex filter                             | _(none)_           |
| `--dirregex`, `-dr`                | Directory name regex filter                       | _(none)_           |
| `--extensions`, `-e`               | Comma-separated audio extensions to convert       | `wav,mp3,flac,m4a` |
| `--dryrun`, `-d`                   | Preview without executing                         | `false`            |
| `--flatten`, `-f`                  | Output all files to root                          | `false`            |
| `--nocopy`, `-nc`, `--convertonly` | Skip copying non-audio files                      | `false`            |
| `--verbose`, `-v`                  | Enable verbose output                             | `false`            |
| `--quiet`, `-q`                    | Suppress non-error output                         | `false`            |
| `--case-insensitive`, `-ip`        | Case-insensitive regex matching                   | `false`            |

## Required Dependencies

- **ffmpeg**: used from PATH or placed alongside the executable
- **opusenc**: [get from opus-codec](https://opus-codec.org/downloads/)

i'll bundle them somehow eventually, i'm just very lazy :^(
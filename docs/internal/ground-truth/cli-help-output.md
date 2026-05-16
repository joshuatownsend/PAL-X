# CLI `--help` ground truth

Source-of-truth capture of every `pal` command and subcommand.

- Captured: 2026-05-15
- Engine: PAL 2026.2.0 (.NET 8.0)
- Method: `dotnet run --project dotnet/src/Pal.Cli -c Release --no-build -- <cmd> --help`

Re-run via `scripts/capture-cli-help.sh` *(not yet committed)*. Update this file whenever a new command lands or a flag changes; the user-facing CLI reference under `docs/reference/cli/` is written against this file, not against memory.

## `pal --help`

```
USAGE:
    pal [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help       Prints help information   
    -v, --version    Prints version information

COMMANDS:
    analyze            Analyze one input dataset and generate report artifacts
    validate-pack      Validate one pack or a directory of packs              
    inspect-dataset    Import and inspect a dataset without running rules     
    list-packs         List all packs available on the search path            
    packs              Commands for working with PAL packs                    
    remote             Commands for interacting with a running PAL API server 
```

## `pal analyze --help`

```
DESCRIPTION:
Analyze one input dataset and generate report artifacts

USAGE:
    pal analyze [OPTIONS]

OPTIONS:
    -h, --help                   Prints help information                        
        --input <PATH>           Path to input dataset (CSV or BLG)             
        --output <DIR>           Output directory for report artifacts          
        --format <FORMAT>        Input format: auto, blg, csv (default: auto)   
        --pack <PACK-ID>         Pack ID to load (repeatable)                   
        --pack-dir <PATH>        Additional search path for packs (repeatable)  
        --auto-resolve-packs     Auto-load applicable packs based on dataset    
                                 content                                        
        --html                   Emit HTML report (default: true)               
        --json                   Emit JSON report (default: true)               
        --markdown               Emit Markdown report                           
        --html-only              Emit HTML only (mutually exclusive with        
                                 --json-only)                                   
        --json-only              Emit JSON only (mutually exclusive with        
                                 --html-only)                                   
        --fail-on-warning        Exit code 1 if any warning finding is produced 
        --machine-name <NAME>    Override machine name from source metadata     
        --time-zone <TZ>         Override or assign source time zone            
        --report-name <NAME>     Base name for generated artifact files         
        --include-charts         Emit chart SVG artifacts                       
        --chart-limit <N>        Maximum charts to generate (default: 20)       
        --host-memory-mb <MB>    Total physical memory in MB (for RAM-relative  
                                 thresholds)                                    
        --host-cpu-count <N>     Logical processor count (for CPU-relative      
                                 thresholds)                                    
        --now <ISO>              Override generation timestamp (for             
                                 deterministic test output)                     
        --verbose                Verbose output                                 
```

## `pal validate-pack --help`

```
DESCRIPTION:
Validate one pack or a directory of packs

USAGE:
    pal validate-pack [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information                         
        --path <PATH>           Path to pack directory or pack.yaml file        
        --strict                Treat warnings as errors                        
        --require-signature     Fail if pack.yaml.sig is missing or signature   
                                verification fails                              
        --trust-key <PATH>      Path to an additional trusted RSA public key PEM
                                file for signature verification                 
        --json-output <PATH>    Write validation results as JSON to this path   
```

## `pal inspect-dataset --help`

```
DESCRIPTION:
Import and inspect a dataset without running rules

USAGE:
    pal inspect-dataset [OPTIONS]

OPTIONS:
    -h, --help                   Prints help information                    
        --input <PATH>           Path to dataset                            
        --format <FORMAT>        Input format: auto, blg, csv               
        --output <PATH>          Write JSON inspection metadata to this path
        --machine-name <NAME>                                               
        --time-zone <TZ>                                                    
```

## `pal list-packs --help`

```
DESCRIPTION:
List all packs available on the search path

USAGE:
    pal list-packs [OPTIONS]

OPTIONS:
    -h, --help                  Prints help information             
        --pack-dir <PATH>       Additional search path (repeatable) 
        --json-output <PATH>    Write pack list as JSON to this path
```

## `pal packs --help`

```
DESCRIPTION:
Commands for working with PAL packs

USAGE:
    pal packs [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    sign    Sign a pack directory, producing pack.yaml.sig
```

## `pal packs sign --help`

```
DESCRIPTION:
Sign a pack directory, producing pack.yaml.sig

USAGE:
    pal packs sign [OPTIONS]

OPTIONS:
    -h, --help           Prints help information                                
        --pack <PATH>    Path to the pack directory containing pack.yaml        
        --key <PATH>     Path to the RSA private key PEM file (PKCS#8 or        
                         traditional format)                                    
```

## `pal remote --help`

```
DESCRIPTION:
Commands for interacting with a running PAL API server

USAGE:
    pal remote [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    submit                               Upload a file and queue an analysis job
                                         on the server                          
    status <job-id>                      Poll the status of an analysis job     
    results <job-id>                     Show findings from a completed analysis
                                         job                                    
    report <job-id>                      Download the HTML or JSON report for a 
                                         completed job                          
    packs                                List packs registered on the server    
    validate-pack <pack-id> <version>    Validate a stored pack version on the  
                                         server                                 
    dataset <job-id>                     Download the normalized dataset        
                                         artifact for a completed job           
    compare                              Compare two completed analysis jobs and
                                         show a finding diff                    
    diagnostics <job-id>                 Show guided diagnostics insights for a 
                                         completed job                          
    baselines                            Commands for managing baseline         
                                         designations                           
    trends                               Show finding trends across the last N  
                                         completed analysis jobs                
    correlations                         Show co-occurring finding pairs across 
                                         the last N completed analysis jobs     
    alerts                               Commands for managing Phase 4 alerts   
    schedules                            Commands for managing Phase 4 ingestion
                                         schedules                              
```

## `pal remote submit --help`

```
DESCRIPTION:
Upload a file and queue an analysis job on the server

USAGE:
    pal remote submit [OPTIONS]

OPTIONS:
                             DEFAULT                                            
    -h, --help                                        Prints help information   
        --api                http://localhost:8080    Base URL of the PAL API   
                                                      server                    
        --api-key                                     Personal access token for 
                                                      authentication (pal_...)  
    -f, --file                                        Path to the CSV or BLG    
                                                      file to analyze           
    -p, --pack                                        Pack ID(s) to run         
                                                      (repeatable)              
        --include-dataset                             Persist normalized dataset
                                                      artifact for later        
                                                      download                  
        --baseline                                    Baseline job ID (GUID) —  
                                                      auto-compares against this
                                                      baseline on completion    
```

## `pal remote status --help`

```
DESCRIPTION:
Poll the status of an analysis job

USAGE:
    pal remote status <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Analysis job ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote results --help`

```
DESCRIPTION:
Show findings from a completed analysis job

USAGE:
    pal remote results <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Analysis job ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
        --json                                Print raw findings JSON           
        --verbose                             Show recommendations for each     
                                              finding                           
```

## `pal remote report --help`

```
DESCRIPTION:
Download the HTML or JSON report for a completed job

USAGE:
    pal remote report <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Analysis job ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
        --format     html                     Report format: html (default),    
                                              json, or markdown                 
    -o, --output                              Save report to this file path     
                                              (default: print to stdout)        
```

## `pal remote dataset --help`

```
DESCRIPTION:
Download the normalized dataset artifact for a completed job

USAGE:
    pal remote dataset <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Analysis job ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
    -o, --output                              File path to save the dataset     
                                              (e.g. dataset.json.gz)            
```

## `pal remote diagnostics --help`

```
DESCRIPTION:
Show guided diagnostics insights for a completed job

USAGE:
    pal remote diagnostics <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Analysis job ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote compare --help`

```
DESCRIPTION:
Compare two completed analysis jobs and show a finding diff

USAGE:
    pal remote compare [OPTIONS]

OPTIONS:
                       DEFAULT                                                  
    -h, --help                                  Prints help information         
        --api          http://localhost:8080    Base URL of the PAL API server  
        --api-key                               Personal access token for       
                                                authentication (pal_...)        
        --baseline                              Job ID of the baseline run      
        --candidate                             Job ID of the candidate run to  
                                                compare against baseline        
```

## `pal remote trends --help`

```
DESCRIPTION:
Show finding trends across the last N completed analysis jobs

USAGE:
    pal remote trends [OPTIONS]

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
        --last       10                       Number of most-recent completed   
                                              jobs to include in the trend      
                                              window                            
        --json                                Print raw trends JSON             
```

## `pal remote correlations --help`

```
DESCRIPTION:
Show co-occurring finding pairs across the last N completed analysis jobs

USAGE:
    pal remote correlations [OPTIONS]

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
        --last       10                       Number of most-recent completed   
                                              jobs to include in the correlation
                                              window                            
        --json                                Print raw correlations JSON       
```

## `pal remote packs --help`

```
DESCRIPTION:
List packs registered on the server

USAGE:
    pal remote packs [OPTIONS]

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote validate-pack --help`

```
DESCRIPTION:
Validate a stored pack version on the server

USAGE:
    pal remote validate-pack <pack-id> <version> [OPTIONS]

ARGUMENTS:
    <pack-id>    Pack ID registered on the server     
    <version>    Pack version to validate (e.g. 1.0.0)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote baselines --help`

```
DESCRIPTION:
Commands for managing baseline designations

USAGE:
    pal remote baselines [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    list            List designated baselines, optionally filtered by type
    set <job-id>    Designate or clear a baseline for a completed job     
```

## `pal remote baselines list --help`

```
DESCRIPTION:
List designated baselines, optionally filtered by type

USAGE:
    pal remote baselines list [OPTIONS]

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
    -t, --type                                Filter by baseline type: machine, 
                                              role, workload, release           
```

## `pal remote baselines set --help`

```
DESCRIPTION:
Designate or clear a baseline for a completed job

USAGE:
    pal remote baselines set <job-id> [OPTIONS]

ARGUMENTS:
    <job-id>    Job ID (GUID) to designate as a baseline

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
    -l, --label                               Human-readable baseline label     
                                              (e.g. WEB-01)                     
    -t, --type                                Baseline type: machine, role,     
                                              workload, release                 
    -c, --context                             Context JSON (e.g.                
                                              '{"machine":"WEB-01"}')           
        --clear                               Remove the baseline designation   
                                              from this job                     
```

## `pal remote alerts --help`

```
DESCRIPTION:
Commands for managing Phase 4 alerts

USAGE:
    pal remote alerts [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    list                List alerts, optionally filtered by status or severity  
    acknowledge <id>    Mark an alert as acknowledged (open → acknowledged)     
    resolve <id>        Resolve an alert with an optional resolution note       
    snooze <id>         Suppress notifications for an alert until a specified   
                        time                                                    
    unsnooze <id>       Clear an active snooze on an alert                      
```

## `pal remote alerts list --help`

```
DESCRIPTION:
List alerts, optionally filtered by status or severity

USAGE:
    pal remote alerts list [OPTIONS]

OPTIONS:
                      DEFAULT                                                   
    -h, --help                                 Prints help information          
        --api         http://localhost:8080    Base URL of the PAL API server   
        --api-key                              Personal access token for        
                                               authentication (pal_...)         
    -s, --status                               Filter by status: open,          
                                               acknowledged, resolved           
        --severity                             Filter by severity: critical,    
                                               warning, informational           
```

## `pal remote alerts acknowledge --help`

```
DESCRIPTION:
Mark an alert as acknowledged (open → acknowledged)

USAGE:
    pal remote alerts acknowledge <id> [OPTIONS]

ARGUMENTS:
    <id>    Alert ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote alerts resolve --help`

```
DESCRIPTION:
Resolve an alert with an optional resolution note

USAGE:
    pal remote alerts resolve <id> [OPTIONS]

ARGUMENTS:
    <id>    Alert ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
    -n, --note                                Resolution note (optional, free   
                                              text)                             
```

## `pal remote alerts snooze --help`

```
DESCRIPTION:
Suppress notifications for an alert until a specified time

USAGE:
    pal remote alerts snooze <id> [OPTIONS]

ARGUMENTS:
    <id>    Alert ID (GUID)

OPTIONS:
                      DEFAULT                                                   
    -h, --help                                 Prints help information          
        --api         http://localhost:8080    Base URL of the PAL API server   
        --api-key                              Personal access token for        
                                               authentication (pal_...)         
    -d, --duration                             Snooze duration: 30m, 2h, 1d     
                                               (mutually exclusive with --until)
        --until                                Absolute ISO 8601 timestamp      
                                               (mutually exclusive with         
                                               --duration)                      
```

## `pal remote alerts unsnooze --help`

```
DESCRIPTION:
Clear an active snooze on an alert

USAGE:
    pal remote alerts unsnooze <id> [OPTIONS]

ARGUMENTS:
    <id>    Alert ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote schedules --help`

```
DESCRIPTION:
Commands for managing Phase 4 ingestion schedules

USAGE:
    pal remote schedules [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    list            List ingestion schedules in the current workspace
    create          Create a new directory-poll ingestion schedule   
    enable <id>     Enable a schedule                                
    disable <id>    Disable a schedule (worker stops polling it)     
    delete <id>     Delete a schedule permanently                    
```

## `pal remote schedules list --help`

```
DESCRIPTION:
List ingestion schedules in the current workspace

USAGE:
    pal remote schedules list [OPTIONS]

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote schedules create --help`

```
DESCRIPTION:
Create a new directory-poll ingestion schedule

USAGE:
    pal remote schedules create [OPTIONS]

OPTIONS:
                      DEFAULT                                                   
    -h, --help                                 Prints help information          
        --api         http://localhost:8080    Base URL of the PAL API server   
        --api-key                              Personal access token for        
                                               authentication (pal_...)         
    -n, --name                                 Human-readable schedule name     
                                               (unique within the workspace)    
    -i, --interval                             Polling interval in minutes      
                                               (5–1440)                         
        --path                                 Absolute directory path to scan  
                                               for new perfmon files            
        --glob        *.csv                    File glob pattern, e.g. *.csv or 
                                               *.blg                            
    -p, --pack                                 Pack ID(s) to run on each        
                                               ingested file (repeatable)       
        --disabled                             Create the schedule in disabled  
                                               state                            
```

## `pal remote schedules enable --help`

```
DESCRIPTION:
Enable a schedule

USAGE:
    pal remote schedules enable <id> [OPTIONS]

ARGUMENTS:
    <id>    Schedule ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```

## `pal remote schedules delete --help`

```
DESCRIPTION:
Delete a schedule permanently

USAGE:
    pal remote schedules delete <id> [OPTIONS]

ARGUMENTS:
    <id>    Schedule ID (GUID)

OPTIONS:
                     DEFAULT                                                    
    -h, --help                                Prints help information           
        --api        http://localhost:8080    Base URL of the PAL API server    
        --api-key                             Personal access token for         
                                              authentication (pal_...)          
```


﻿module Fake.Dotnet

open Fake
open FSharp.Data
open System
open System.IO

/// Dotnet cli installer script
let dotnetCliInstaller = "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1"

/// Dotnet cli install directory
let dotnetCliInstallDir = environVar "LocalAppData" @@ "Microsoft" @@ "dotnet"

// Dotnet cli executable path
let dotnetCliPath = dotnetCliInstallDir @@ "cli" @@ "bin" @@ "dotnet.exe"

// Temporary path of installer script
let private tempInstallerScript = Path.GetTempPath() @@ "dotnet_install.ps1"

let private downloadInstaller fileName =  
    let installScript = Http.RequestStream dotnetCliInstaller
    use outFile = File.OpenWrite(fileName)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet installer to %s" fileName)
    fileName

let dotnetInstall (forceDownload: bool) =
    let installScript = 
        match forceDownload || not(File.Exists(tempInstallerScript)) with
            | true -> downloadInstaller tempInstallerScript
            | false -> tempInstallerScript

    let args = sprintf "-NoProfile -NoLogo -Command \"%s; exit $LastExitCode;\"" installScript
    let exitCode = 
        ExecProcess (fun info ->
            info.FileName <- "powershell"
            info.WorkingDirectory <- Path.GetTempPath()
            info.Arguments <- args
        ) TimeSpan.MaxValue

    if exitCode <> 0 then failwithf "dotnet install failed with code %i" exitCode

type DotNetOptions =
    {
        /// Path to dotnet.exe
        ToolPath: string;
        /// Command working directory
        WorkingDirectory: string;
    }

    static member Default = {
        ToolPath = dotnetCliPath
        WorkingDirectory = currentDirectory
    }

let dotnet (options: DotNetOptions) args = 
    let errors = new System.Collections.Generic.List<string>()
    let messages = new System.Collections.Generic.List<string>()
    let timeout = TimeSpan.MaxValue

    let errorF msg =
        traceError msg
        errors.Add msg 

    let messageF msg =
        traceImportant msg
        messages.Add msg

    let result = 
        ExecProcessWithLambdas (fun info ->
            info.FileName <- options.ToolPath
            info.WorkingDirectory <- options.WorkingDirectory
            info.Arguments <- args
        ) timeout true errorF messageF

    ProcessResult.New result messages errors


/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "

type DotNetRestoreOptions =
    {   
        /// Common tool options
        Common: DotNetOptions;
        /// Nuget feeds to search updates in. Use default if empty.
        Sources: string list;
        /// Path to the nuget.exe.
        ConfigFile: string option;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotNetOptions.Default
        Sources = []
        ConfigFile = None
    }

/// [omit]
let private buildRestoreArgs (param: DotNetRestoreOptions) =
    [   param.Sources |> argList2 "source"
        param.ConfigFile |> Option.toList |> argList2 "configFile"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "

let dotnetRestore setParams project =    
    traceStartTask "dotnet:restore" project
    let param = DotNetRestoreOptions.Default |> setParams    
    let args = sprintf "restore %s %s" project (buildRestoreArgs param)
    let result = dotnet param.Common args    
    if not result.OK then failwithf "dotnet restore failed with code %i" result.ExitCode
    traceEndTask "dotnet:restore" project


type PackConfiguration =
    | Debug
    | Release
    | Custom of string

type DotNetPackOptions =
    {   
        /// Common tool options
        Common: DotNetOptions;
        /// Pack configuration (--configuration)
        Configuration: PackConfiguration;
        /// Version suffix to use
        VersionSuffix: string option;
        /// Build base path (--build-base-path)
        BuildBasePath: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// No build flag (--no-build)
        NoBuild: bool;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotNetOptions.Default
        Configuration = Release
        VersionSuffix = None
        BuildBasePath = None
        OutputPath = None
        NoBuild = false
    }

/// [omit]
let private buildPackArgs (param: DotNetPackOptions) =
    [  
        sprintf "--configuration %s" 
            (match param.Configuration with
            | Debug -> "Debug"
            | Release -> "Release"
            | Custom config -> config)
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        (if param.NoBuild then "--no-build" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "

let dotnetPack setParams project =    
    traceStartTask "dotnet:pack" project
    let param = DotNetPackOptions.Default |> setParams    
    let args = sprintf "pack %s %s" project (buildPackArgs param)
    let result = dotnet param.Common args    
    if not result.OK then failwithf "dotnet pack failed with code %i" result.ExitCode
    traceEndTask "dotnet:pack" project
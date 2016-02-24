﻿#I "packages/FAKE.Core/tools/"
#r "FakeLib.dll"

// change to nuget source, or build Fake.Dotnet debug library

// Debug build from visual studio output
#I "../../../src/Fake.Dotnet/bin/Debug/"

// Release build from build script output
// #I "../../../artifacts/build/"

#r "Fake.Dotnet.dll"

open Fake
open Fake.Dnx

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "test/*/bin"
        |> DeleteDirs
)

Target "UpgradeDnx" (fun _ ->
    // upgrade to latest
    DnvmUpgrade id    
)

Target "BuildProjects" (fun _ ->
    let sdkVersion = GlobalJsonSdk "global.json"

    //set sdk version from global.json
    let runtimeOptions = { DnvmRuntimeOptions.Default with VersionOrAlias = sdkVersion };

    !! "src/*/project.json" 
        |> Seq.iter(fun proj ->  
            DnuRestore (fun o -> { o with Runtime = runtimeOptions }) proj
        )
)


Target "Default" <| DoNothing

"Clean"
    ==> "UpgradeDnx"
    ==> "BuildProjects"
    ==> "Default"

// start build
RunTargetOrDefault "Default"
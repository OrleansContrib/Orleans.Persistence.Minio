#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
#nowarn "3180"

module Environment =
    let [<Literal>] APPVEYOR = "APPVEYOR"
    let [<Literal>] APPVEYOR_BUILD_NUMBER = "APPVEYOR_BUILD_NUMBER"
    let [<Literal>] APPVEYOR_PULL_REQUEST_NUMBER = "APPVEYOR_PULL_REQUEST_NUMBER"
    let [<Literal>] APPVEYOR_REPO_BRANCH = "APPVEYOR_REPO_BRANCH"
    let [<Literal>] APPVEYOR_REPO_COMMIT = "APPVEYOR_REPO_COMMIT"
    let [<Literal>] APPVEYOR_REPO_TAG_NAME = "APPVEYOR_REPO_TAG_NAME"
    let [<Literal>] BUILD_CONFIGURATION = "BuildConfiguration"
    let [<Literal>] REPOSITORY = "https://github.com/OrleansContrib/Orleans.Persistence.Minio.git"

module GitVersion =
    let showVariable =
        let execProcess f =
            Process.execWithResult f (System.TimeSpan.FromMinutes 2.)
            |> fun r -> r.Messages
            |> List.head

        let commit =
            match Environment.environVarOrNone Environment.APPVEYOR_REPO_COMMIT with
            | Some c -> c
            | None -> execProcess (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" })

        printfn "Executing gitversion from commit '%s'." commit

        fun variable ->
            match Environment.environVarOrNone Environment.APPVEYOR_REPO_BRANCH, Environment.environVarOrNone Environment.APPVEYOR_PULL_REQUEST_NUMBER with
            | Some branch, None ->
                execProcess (fun info ->
                    { info with
                        FileName = "gitversion"
                        Arguments = sprintf "/showvariable %s /url %s /b b-%s /dynamicRepoLocation .\gitversion /c %s" variable Environment.REPOSITORY branch commit })
            | _ ->
                execProcess (fun info -> { info with FileName = "gitversion"; Arguments = sprintf "/showvariable %s" variable })

    let get =
        let mutable value: Option<string * string * string> = None

        fun () ->
            match value with
            | None ->
                value <-
                    match Environment.environVarOrNone Environment.APPVEYOR_REPO_TAG_NAME with
                    | Some v -> Some (v, showVariable "AssemblySemVer", v)
                    | None -> Some (showVariable "FullSemVer", showVariable "AssemblySemVer", showVariable "NuGetVersionV2")

                Target.activateFinal "ClearGitVersionRepositoryLocation"
                Option.get value
            | Some v -> v

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    ++ "**/artifacts"
    ++ "gitversion"
    |> Shell.deleteDirs
)

Target.create "PrintVersion" (fun _ ->
    let (fullSemVer, assemblyVer, nugetVer) = GitVersion.get()
    printfn "Full version: '%s'" fullSemVer
    printfn "Assembly version: '%s'" assemblyVer
    printfn "NuGet version: '%s'" nugetVer
)

Target.create "UpdateBuildVersion" (fun _ ->
    let (fullSemVer, _, _) = GitVersion.get()

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s (%s)\"" fullSemVer (Environment.environVar Environment.APPVEYOR_BUILD_NUMBER))
    |> ignore
)

Target.create "Build" (fun _ ->
    let (fullSemVer, assemblyVer, _) = GitVersion.get()

    let setParams (buildOptions: DotNet.BuildOptions) =
        { buildOptions with
            Common = { buildOptions.Common with DotNet.CustomParams = Some (sprintf "/p:Version=%s /p:FileVersion=%s" fullSemVer assemblyVer) }
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }

    !! "**/*.*proj"
    -- "**/Orleans.Persistence.Minio.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.build setParams)
)

Target.create "Pack" (fun _ ->
    let (_, _, nuGetVer) = GitVersion.get()

    let setParams (packOptions: DotNet.PackOptions) =
        { packOptions with
            Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug
            OutputPath = Some "../artifacts"
            NoBuild = true
            Common = { packOptions.Common with CustomParams = Some (sprintf "/p:PackageVersion=%s" nuGetVer) } }

    !! "**/*.*proj"
    -- "**/Orleans.Persistence.Minio.*Test.*proj"
    -- "**/gitversion/**/*.*proj"
    |> Seq.iter (DotNet.pack setParams)
)

Target.createFinal "ClearGitVersionRepositoryLocation" (fun _ ->
    Shell.deleteDir "gitversion"
)

Target.create "All" ignore

"Clean"
  ==> "PrintVersion"
  =?> ("UpdateBuildVersion", Environment.environVarAsBool Environment.APPVEYOR)
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"

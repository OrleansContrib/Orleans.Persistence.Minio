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
    module Process =
        let exec f =
            Process.execWithResult f (System.TimeSpan.FromMinutes 2.)

    let private exec commit args =
        match Environment.environVarOrNone Environment.APPVEYOR_REPO_BRANCH, Environment.environVarOrNone Environment.APPVEYOR_PULL_REQUEST_NUMBER with
        | Some branch, None ->
            Process.exec (fun info ->
                { info with
                    FileName = "gitversion"
                    Arguments = sprintf "/url %s /b b-%s /dynamicRepoLocation .\gitversion /c %s %s" Environment.REPOSITORY branch commit args })
        | _ ->
            Process.exec (fun info -> { info with FileName = "gitversion"; Arguments = args })

    let private getResult (result: ProcessResult) =
        result.Messages |> List.head

    let get =
        let mutable value: Option<(unit -> ProcessResult) * string * string> = None

        fun () ->
            match value with
            | None ->
                let commit =
                    match Environment.environVarOrNone Environment.APPVEYOR_REPO_COMMIT with
                    | Some c -> c
                    | None -> Process.exec (fun info -> { info with FileName = "git"; Arguments = "rev-parse HEAD" }) |> getResult

                printfn "Executing gitversion from commit '%s'." commit

                match Environment.environVarOrNone Environment.APPVEYOR_REPO_TAG_NAME with
                | Some v ->
                    printfn "Full sementic versioning: '%s', NuGet sementic versioning: '%s'" v v
                    value <- Some ((fun () -> exec commit "/updateassemblyinfo"), v, v)
                | None ->
                    let fullSemVer = exec commit "/showvariable FullSemVer" |> getResult
                    let nuGetVer = exec commit "/showvariable NuGetVersionV2" |> getResult
                    printfn "Full sementic versioning: '%s', NuGet sementic versioning: '%s'" fullSemVer nuGetVer
                    value <- Some ((fun () -> exec commit "/updateassemblyinfo"), fullSemVer, nuGetVer)

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

Target.create "PatchAssemblyInfo" (fun _ ->
    let (updateAssemblyInfo, _, _) = GitVersion.get()

    updateAssemblyInfo()
    |> fun res -> res.Messages
    |> List.iter (printfn "%s")
)

Target.create "UpdateBuildVersion" (fun _ ->
    let (_, fullSemVer, _) = GitVersion.get()

    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s (%s)\"" fullSemVer (Environment.environVar Environment.APPVEYOR_BUILD_NUMBER))
    |> ignore
)

Target.create "Build" (fun _ ->
    let setParams (buildOptions: DotNet.BuildOptions) =
        { buildOptions with Configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault Environment.BUILD_CONFIGURATION DotNet.BuildConfiguration.Debug }

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
  =?> ("PatchAssemblyInfo", Environment.environVarAsBool Environment.APPVEYOR)
  =?> ("UpdateBuildVersion", Environment.environVarAsBool Environment.APPVEYOR)
  ==> "Build"
  ==> "Pack"
  ==> "All"

Target.runOrDefault "Build"

namespace AINoteSummarizer

open Avalonia
open System

module Program =

    [<EntryPoint>]
    let main args =

        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args)

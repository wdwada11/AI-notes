namespace AINoteSummarizer

open System
open Avalonia

module Program =

    [<EntryPoint; STAThread>]
    let main args =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args)

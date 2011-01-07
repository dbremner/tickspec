﻿module internal TickSpec.FeatureParser

open System.Text.RegularExpressions
open TickSpec.LineParser
open TickSpec.BlockParser

/// Computes combinations of table values
let computeCombinations (tables:Table []) =
    let values = 
        tables 
        |> Seq.map (fun table ->
            table.Rows |> Array.map (fun row ->
                row
                |> Array.mapi (fun i col ->
                    table.Header.[i],col
                )
            )
        )
        |> Seq.toList
    values |> List.combinations
/// Replace line with specified named values
let replaceLine (xs:seq<string * string>) (scenario,n,tags,line,step) =
    let replace s =
        let lookup (m:Match) =
            let x = m.Value.TrimStart([|'<'|]).TrimEnd([|'>'|])
            xs |> Seq.tryFind (fun (k,_) -> k = x)
            |> (function Some(_,v) -> v | None -> m.Value)
        let pattern = "<([^<]*)>"
        Regex.Replace(s, pattern, lookup)
    let step = 
        match step with
        | Given s -> replace s |> Given
        | When s -> replace s |> When
        | Then s  -> replace s |> Then
    let table =
        line.Table 
        |> Option.map (fun table ->
            Table(table.Header,
                table.Rows |> Array.map (fun row ->
                    row |> Array.map (fun col -> replace col)
                )
            )
        )
    let bullets =
        line.Bullets
        |> Option.map (fun bullets -> bullets |> Array.map replace)                                  
    (scenario,n,tags,{line with Table=table;Bullets=bullets},step)

/// Appends shared examples to scenarios as examples
let appendSharedExamples (sharedExamples:Table[]) scenarios  =
    if Seq.length sharedExamples = 0 then
        scenarios
    else
        scenarios |> Seq.map (function 
            | scenarioName,tags,steps,None ->
                scenarioName,tags,steps,Some(sharedExamples)
            | scenarioName,tags,steps,Some(exampleTables) ->
                scenarioName,tags,steps,Some(Array.append exampleTables sharedExamples)
        )
/// Parses lines of feature
let parseFeature (lines:string[]) =
    let featureName,background,scenarios,sharedExamples = parseBlocks lines     
    featureName,
        scenarios 
        |> appendSharedExamples sharedExamples
        |> Seq.collect (function
            | name,tags,steps,None ->
                let steps = Seq.append background steps
                Seq.singleton
                    (name, tags, steps, [||])
            | name,tags,steps,Some(exampleTables) ->            
                /// All combinations of tables
                let combinations = computeCombinations exampleTables
                // Execute each combination
                combinations |> Seq.mapi (fun i combination ->
                    let name = sprintf "%s(%d)" name i
                    let combination = Seq.concat combination |> Seq.toArray
                    let steps =
                        Seq.append background steps
                        |> Seq.map (replaceLine combination)                                          
                    name, tags, steps, combination
                )
        )
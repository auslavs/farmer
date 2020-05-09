[<AutoOpen>]
module Farmer.ArmBuilder

module Helpers =
    /// Creates a unique IArmResource from an arbitrary object.
    let toArmResource armObject =
        { new IArmResource with
             member _.ResourceName = ResourceName (System.Guid.NewGuid().ToString())
             member _.JsonValue = armObject }

    /// Creates a Builder that can be added to arm { } expressions.
    let asBuilder builder : Builder =
        fun location _ -> builder location

/// Represents all configuration information to generate an ARM template.
type ArmConfig =
    { Parameters : string Set
      Outputs : Map<string, string>
      Location : Location
      Resources : IArmResource list }

type ArmBuilder() =
    let mergeResources resources (newResource:IArmResource) =
        let resourceType = newResource.GetType()
        let existing =
            resources
            |> List.filter(fun (r:IArmResource) ->
                r.ResourceName = newResource.ResourceName &&
                r.GetType() = resourceType)

        match existing with
        | _ :: _ -> printfn "'%s/%s' has been replaced or updated." resourceType.Name newResource.ResourceName.Value
        | [] -> ()
        (resources |> List.except existing) @ [ newResource ]

    member __.Yield _ =
        { Parameters = Set.empty
          Outputs = Map.empty
          Resources = List.empty
          Location = WestEurope }

    member __.Run (state:ArmConfig) =
        let template =
            { Parameters = [
                for resource in state.Resources do
                    match resource with
                    | :? IParameters as p -> yield! p.SecureParameters
                    | _ -> ()
              ] |> List.distinct
              Outputs = state.Outputs |> Map.toList
              Resources = state.Resources }

        let postDeployTasks = [
            for resource in state.Resources do
                match resource with
                | :? IPostDeploy as pd -> pd
                | _ -> ()
            ]

        { Location = state.Location
          Template = template
          PostDeployTasks = postDeployTasks }

    /// Creates an output value that will be returned by the ARM template.
    [<CustomOperation "output">]
    member __.Output (state, outputName, outputValue) : ArmConfig = { state with Outputs = state.Outputs.Add(outputName, outputValue) }
    member this.Output (state:ArmConfig, outputName:string, (ResourceName outputValue)) = this.Output(state, outputName, outputValue)
    member this.Output (state:ArmConfig, outputName:string, outputValue:ArmExpression) = this.Output(state, outputName, outputValue.Eval())
    member this.Output (state:ArmConfig, outputName:string, outputValue:string option) =
        match outputValue with
        | Some outputValue -> this.Output(state, outputName, outputValue)
        | None -> state
    member this.Output (state:ArmConfig, outputName:string, outputValue:ArmExpression option) =
        match outputValue with
        | Some outputValue -> this.Output(state, outputName, outputValue)
        | None -> state

    /// Sets the default location of all resources.
    [<CustomOperation "location">]
    member __.Location (state, location) : ArmConfig = { state with Location = location }

    /// Adds a builder's ARM resources to the ARM template.
    [<CustomOperation "add_resource">]
    member this.AddResource(state:ArmConfig, input:IBuilder) =
        { state with
            Resources =
                input.BuildResources state.Location state.Resources
                |> List.fold mergeResources state.Resources }
    member _.AddResource (state:ArmConfig, input:Builder) =
        { state with
            Resources =
                input state.Location state.Resources
                |> List.fold mergeResources state.Resources }
    member _.AddResource (state:ArmConfig, input:IArmResource) =
        let updatedResources = mergeResources state.Resources input
        { state with Resources = updatedResources }

    [<CustomOperation "add_resources">]
    member this.AddResources(state:ArmConfig, input:IBuilder list) =
        input
        |> Seq.fold(fun state builder -> this.AddResource(state, builder)) state

let arm = ArmBuilder()
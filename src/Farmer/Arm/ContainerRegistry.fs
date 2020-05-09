[<AutoOpen>]
module Farmer.Arm.ContainerRegistry

open Farmer

type Registries =
    { Name : ResourceName
      Location : Location
      Sku : string
      AdminUserEnabled : bool }
    member this.LoginServer =
        (sprintf "reference(resourceId('Microsoft.ContainerRegistry/registries', '%s'),'2019-05-01').loginServer" this.Name.Value)
        |> ArmExpression
    interface IArmResource with
        member this.ResourceName = this.Name
        member this.JsonValue =
            {| name = this.Name.Value
               ``type`` = "Microsoft.ContainerRegistry/registries"
               apiVersion = "2019-05-01"
               sku = {| name = this.Sku.ToString().Replace("_", ".") |}
               location = this.Location.ArmValue
               tags = {||}
               properties = {| adminUserEnabled = this.AdminUserEnabled |}
            |} :> _
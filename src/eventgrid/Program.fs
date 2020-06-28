open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Azure.EventGrid
open Microsoft.Azure.EventGrid.Models
open Saturn
open System

module Option =
    let ofNullOrEmptyString s = if System.String.IsNullOrEmpty s then None else Some s

type StorageEvent =
    | BlobCreated of DateTime * StorageBlobCreatedEventData
    | BlobDeleted of DateTime * StorageBlobDeletedEventData

let inMemoryDatabase = ResizeArray()

let handleEvent (body:EventGridEvent) : HttpHandler option =
    match body.Data with
    | :? SubscriptionValidationEventData as e ->
        Some (Successful.OK (SubscriptionValidationResponse(ValidationResponse = e.ValidationCode)))

    | :? StorageBlobCreatedEventData as event ->
        inMemoryDatabase.Add (BlobCreated (DateTime.UtcNow, event))
        Some (Successful.OK "Handled Blob Created")

    | :? StorageBlobDeletedEventData as event ->
        inMemoryDatabase.Add (BlobDeleted (DateTime.UtcNow, event))
        Some (Successful.OK "Handled Blob Deleted")

    | _ ->
        None

let handleEventGridMessage next (ctx:HttpContext) = task {
    let subscriber = EventGridSubscriber()
    let! body = ctx.ReadBodyFromRequestAsync()

    return!
        body
        |> Option.ofNullOrEmptyString
        |> Option.map (subscriber.DeserializeEventGridEvents >> Array.choose handleEvent)
        |> Option.defaultValue Array.empty
        |> Array.tryHead
        |> function
        | Some firstResponse -> firstResponse next ctx
        | None -> RequestErrors.BAD_REQUEST "Unknown event grid message type" next ctx
}

let getAuditLog next ctx =
    json
        [ for event in inMemoryDatabase do
            match event with
            | BlobCreated (date, e) -> {| Date = date; Route = e.Url; Api = e.Api; BlobType = e.BlobType; Event = "Blob Created" |}
            | BlobDeleted (date, e) -> {| Date = date; Route = e.Url; Api = e.Api; BlobType = e.BlobType; Event = "Blob Deleted" |}
        ]
        next
        ctx

let routes = router {
    post "/api/event" handleEventGridMessage
    get "/api/database" getAuditLog
}

let app = application {
    disable_diagnostics
    use_router routes
}

run app
open FSharp.Control.Tasks
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Azure.EventGrid
open Microsoft.Azure.EventGrid.Models
open Saturn
open System
open Giraffe.GiraffeViewEngine

module Option =
    let ofNullOrEmptyString s = if System.String.IsNullOrEmpty s then None else Some s

type StorageEvent =
    | BlobCreated of DateTime * StorageBlobCreatedEventData
    | BlobDeleted of DateTime * StorageBlobDeletedEventData

let inMemoryDatabase = ResizeArray()

/// Handles Azure Event Grid messages
module EventGridHandling =
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

module RouteHandlers =
    let handleEventGridMessage next (ctx:HttpContext) = task {
        let subscriber = EventGridSubscriber()
        let! body = ctx.ReadBodyFromRequestAsync()

        return!
            body
            |> Option.ofNullOrEmptyString
            |> Option.map (subscriber.DeserializeEventGridEvents >> Array.choose EventGridHandling.handleEvent)
            |> Option.defaultValue Array.empty
            |> Array.tryHead
            |> function
            | Some firstResponse -> firstResponse next ctx
            | None -> RequestErrors.BAD_REQUEST "Unknown event grid message type" next ctx
    }

    let getAuditTable next ctx =
        let view =
            html [ ] [
                link [ _rel "stylesheet"; _href "https://cdn.jsdelivr.net/npm/bulma@0.9.0/css/bulma.min.css" ]
                body [] [
                    section [ _class "section" ] [
                        div [ _class "container" ] [
                            match inMemoryDatabase.Count with
                            | 0 ->
                                h1 [ _class "title" ] [ str "No data!" ]
                                h2 [ _class "subtitle" ] [ str "Upload or delete some blobs from the storage account!" ]
                            | events ->
                                h1 [ _class "title" ] [ str "Event Grid events!" ]
                                h2 [ _class "subtitle" ] [ str (sprintf "%d events so far" events) ]
                                table [ _class "table" ] [
                                    thead [] [
                                        tr [] [
                                            for heading in [ "Date"; "Url"; "Api"; "Blob Type"; "Event" ] do
                                                th [] [ str heading ]
                                        ]
                                    ]
                                    for event in inMemoryDatabase do
                                        let row =
                                            match event with
                                            | BlobCreated (date, e) -> [ string date; string e.Url; string e.Api; string e.BlobType; "Blob Created" ]
                                            | BlobDeleted (date, e) -> [ string date; string e.Url; string e.Api; string e.BlobType; "Blob Deleted" ]
                                        tr [] [
                                            for col in row do
                                                td [] [ str col ]
                                        ]
                                ]
                        ]
                    ]
                ]
            ]
        htmlView view next ctx

    let getAuditLog next ctx =
        let data =[
            for event in inMemoryDatabase do
                match event with
                | BlobCreated (date, e) -> {| Date = date; Route = e.Url; Api = e.Api; BlobType = e.BlobType; Event = "Blob Created" |}
                | BlobDeleted (date, e) -> {| Date = date; Route = e.Url; Api = e.Api; BlobType = e.BlobType; Event = "Blob Deleted" |}
        ]
        json data next ctx

let routes = router {
    post "/api/event" RouteHandlers.handleEventGridMessage
    get "/api/database" RouteHandlers.getAuditLog
    get "/" RouteHandlers.getAuditTable
}

let app = application {
    disable_diagnostics
    use_router routes
}

run app
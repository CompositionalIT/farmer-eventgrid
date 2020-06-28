open Farmer
open Farmer.Builders
open System.IO

let yourName = "isaac"

let topicName = yourName + "ssupertopic"
let subscriptionName = yourName + "supersub"
let storageName = yourName + "gestorage"
let webName = yourName + "geweb"

let toAzure = Deploy.execute "my-resource-group" [] >> ignore

let eventStorage = storageAccount {
    name storageName
    add_private_container "data"
}

let eventWeb = webApp {
    name webName
    run_from_package
    zip_deploy (Path.GetFullPath (__SOURCE_DIRECTORY__ + @"\..\..\deploy"))
}

// First deploy the web app - we need to do this so that the web app is up and running before
// the event grid tries to validate it.
arm { add_resources [ eventWeb ] } |> toAzure

// Now we've deployed the web app, deploy the whole lot
arm {
    add_resources [ eventStorage; eventWeb ]
    add_resource (EventGrid.eventGridTopic topicName eventStorage.Name EventGrid.Topics.StorageAccounts)
    add_resource
        (EventGrid.eventGridSubscription
            subscriptionName
            topicName
            eventWeb.Name
            (sprintf "https://%s/api/event" eventWeb.Endpoint)
            [ EventGrid.Events.BlobCreated; EventGrid.Events.BlobDeleted ])
}
|> toAzure
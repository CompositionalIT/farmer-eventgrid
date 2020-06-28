module EventGrid

open Farmer
open Farmer.Arm
open Farmer.CoreTypes
open Newtonsoft.Json

type EventType =
    | EventType of string member this.Value = match this with EventType s -> s
module Events =
    let BlobCreated = EventType "Microsoft.Storage.BlobCreated"
    let BlobDeleted = EventType "Microsoft.Storage.BlobDeleted"
type TopicType =
    | TopicType of string member this.Value = match this with TopicType s -> s
module Topics =
    let StorageAccounts = TopicType "Microsoft.Storage.StorageAccounts"

let eventGridTopic name (source:ResourceName) (TopicType topicType) (location:Location) = [
    sprintf """{
			"name": "%s",
			"type": "Microsoft.EventGrid/systemTopics",
			"apiVersion": "2020-04-01-preview",
			"location": "%s",
			"dependsOn": [ "%s" ],
			"properties": {
				"source": "%s",
				"topicType": "%s"
			}
		}""" name location.ArmValue source.Value (ArmExpression.resourceId(Storage.storageAccounts, source).Eval()) topicType
        |> Json.toIArmResource
    ]

let eventGridSubscription subscriptionName topicName (webApp:ResourceName) endpointUrl (eventTypes:EventType list) =
    sprintf """{
            "type": "Microsoft.EventGrid/systemTopics/eventSubscriptions",
            "apiVersion": "2020-04-01-preview",
            "name": "%s/%s",
            "dependsOn": [ "%s", "%s" ],
            "properties": {
                "destination": {
                    "properties": { "endpointUrl": "%s" },
                    "endpointType": "WebHook"
                },
                "filter": { "includedEventTypes": %s }
            }
        }""" topicName subscriptionName topicName webApp.Value endpointUrl (eventTypes |> List.map(fun r -> r.Value) |> JsonConvert.SerializeObject)
        |> Json.toIArmResource
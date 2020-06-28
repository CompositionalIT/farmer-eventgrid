This repository shows how to create an Event Grid topic listener and subscription for Storage Account events.

## Resources
This repository also contains all code required to create the resources using [Farmer](https://compositionalit.github.io/farmer/):

* Storage Account - the storage account monitored for Event Grid events
* ASP .NET Web Application - a web application that will consume the Event Grid events via a web hook
* Event Grid topic - the configured topic for listening to the storage account
* Event Grid subscription - the subscription that connects the topic to the web app

> Note: Farmer does not yet have build-in support Event Grid, so this repository utilises Farmer's
> plugin API to illustrate how we can quickly and easily create custom ARM resources using basic
> F# functions. These wrapper functions are in `EventGrid.fs`.

## Web App
The web application is an ASP .NET Core app with Saturn and Giraffe running on top for a better F#
experience. It exposes two endpoints:
    * The webhook endpoint that Event Grid will POST messages to (`api/event`)
    * A get endpoint that provides a list of all received messages (`api/database`)

## Testing
You can test the web app locally using the `sample.rest` file, which contains sample messages for
the webhook (both for the validation message and events).

## Deploying and Running
To deploy the infrastucture, run the infrastructure project which will provision all the resources.
You should rename `yourName` to some aribtrary name to uniquely identify your resources globally. The
resources are deployed in two steps: first, the web app is deployed, and then the remaining resources.
This is because EventGrid needs the web app to be running *with the deployed webhook*; otherwise, ARM
fails to deploy the template.

Once done, you can go to the storage account and add / remove several files; browse the web application
(`api/database`) and you will see that the events are displayed as JSON.
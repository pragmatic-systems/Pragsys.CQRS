# SimpleCQRS

A Simple CQRS implementation that mimics the MediatR interfaces.

## Features
* Request/Response Handlers
* Request/Void Handlers
* Pipeline Support

## Pending
* May look at adding notification/broadcast fan out.

## Building Locally
You can use the cake file to build, test and publish to a local folder:

Run: `dotnet cake --Target=NugetPackAndPush --Source="{local-folder}" --ApiKey="Key"`
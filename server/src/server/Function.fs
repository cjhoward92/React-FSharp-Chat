namespace server

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open Newtonsoft.Json

open System.Net

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Functions() =
    /// <summary>
    /// A Lambda function to respond to WS $connect route from API Gateway
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.OnConnect (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        let msg = sprintf "Hello from AWS F#! ConnectionId: %s" request.RequestContext.ConnectionId
        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = JsonConvert.SerializeObject({| data = msg |})
        )

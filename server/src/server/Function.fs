namespace server

open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open Newtonsoft.Json

open System.Net

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Functions() =
    static member val monitor: obj = System.Object() with get
    member val connections: list<string> = [] with get, set
    member __.OnConnect (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        lock Functions.monitor (fun () -> __.connections <- request.RequestContext.ConnectionId :: __.connections)

        let msg = sprintf "Hello from AWS F#! ConnectionId: %s" request.RequestContext.ConnectionId
        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = JsonConvert.SerializeObject({| data = msg |})
        )

    member __.OnDisconnect (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        lock Functions.monitor (fun () -> __.connections <- List.filter (fun c -> c = request.RequestContext.ConnectionId) __.connections)

        let msg = sprintf "Disconnecting... ConnectionId: %s" request.RequestContext.ConnectionId
        APIGatewayProxyResponse(
            StatusCode = int HttpStatusCode.OK,
            Body = JsonConvert.SerializeObject({| data = msg |})
        )

    member __.SendMessage (request: APIGatewayProxyRequest) (context: ILambdaContext) =
        sprintf "Request: %s" request.Path
        |> context.Logger.LogLine

        sprintf "Parsing" |> context.Logger.LogLine
        let body = JsonConvert.DeserializeObject<{| data: obj; |}>(request.Body)
        let dataText = JsonConvert.SerializeObject(body.data)

        sprintf "Serialized and ready to load" |> context.Logger.LogLine
        use memStream = new System.IO.MemoryStream()
        use streamWriter = new System.IO.StreamWriter(memStream, System.Text.Encoding.UTF8, dataText.Length, true)
        use management = new Amazon.ApiGatewayManagementApi.AmazonApiGatewayManagementApiClient(Amazon.RegionEndpoint.USWest2)
        let postReq = Amazon.ApiGatewayManagementApi.Model.PostToConnectionRequest()
        postReq.ConnectionId <- request.RequestContext.ConnectionId
        
        sprintf "About to start async" |> context.Logger.LogLine
        let result =
            async {
                do sprintf "Writing to mem stream" |> context.Logger.LogLine
                do! streamWriter.WriteAsync(dataText) |> Async.AwaitTask
                do! streamWriter.FlushAsync() |> Async.AwaitTask
                do memStream.Position <- 0L
                do postReq.Data <- memStream
                do sprintf "Sending...." |> context.Logger.LogLine
                return! management.PostToConnectionAsync postReq |> Async.AwaitTask
            } |> Async.RunSynchronously

        sprintf "Operation complete" |> context.Logger.LogLine
        match result.HttpStatusCode with
        | HttpStatusCode.OK ->
            let msg = sprintf "Hello from AWS F#! ConnectionId: %s" request.RequestContext.ConnectionId
            APIGatewayProxyResponse(
                StatusCode = int HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject({| data = msg |})
            )
        | _ ->
            let msg = sprintf "FAILURE! ConnectionId: %s" request.RequestContext.ConnectionId
            APIGatewayProxyResponse(
                StatusCode = int HttpStatusCode.BadRequest,
                Body = JsonConvert.SerializeObject({| data = msg |})
            )

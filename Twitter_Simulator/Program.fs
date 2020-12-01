open System
open System.Security.Cryptography
open System.Globalization
open System.Text
open Akka.Actor
open Akka.FSharp
open System.Diagnostics


(* For Json Libraries *)
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json


(* Different API request JSON message structures *)

type ReplyInfo = {
    ReqType : string
    Type : string
    Status : string
    Desc : string option
}



type RegJson = {
    ReqType : string
    UserID : int
    UserName : string
    PublicKey : string option
}

type TweetInfo = {
    ReqType : string
    UserID : int
    TweetID : string
    Time : DateTime
    Content : string
    Tag : string
    Mention : int
}

type TweetReply = {
    ReqType : string
    Type : string
    Status : int
    TweetInfo : TweetInfo
}

type SubInfo = {
    ReqType : string
    UserID : int 
    PublisherID : int
}

type SubReply = {
    ReqType : string
    Type : string
    Subscriber : int[]
    Publisher : int[]
}

type ConnectInfo = {
    ReqType : string
    UserID : int
}

type QueryInfo = {
    ReqType : string
    UserID : int
    Tag : string
}

 (* Actor System Configuration Settings (Locaol Side) *)
let config =
    Configuration.parse
        @"akka {
            actor.provider = remote
            remote.helios.tcp {
                hostname = localhost
                port = 0
            }
        }"

let globalTimer = Stopwatch()
let system = System.create "Simulator" config
let serverNode = system.ActorSelection("akka.tcp://TwitterEngine@localhost:9001/user/TWServer")


let clientActorNode (clientMailbox:Actor<string>) =
    let nodeName = "User" + clientMailbox.Self.Path.Name
    let nodeID = (int) clientMailbox.Self.Path.Name
    let nodeSelfRef = clientMailbox.Self
    
    (* User have to connect (online) to server first before using twitter API, register API has no this kind of limit *)
    let mutable isOnline = false
    let mutable isDebug = false // developer use, break this limit
    let mutable isOffline = true

    (* Need a query lock to make sure there is other query request until the last query has done*)
    (* If a new query request comes, set it to true, until the server replies query seccess in reply message *)
    let mutable isQuerying = false


    let rec loop() = actor {
        let! (message: string) = clientMailbox.Receive()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()
        isOffline <- (not isOnline) && (not isDebug)
        match reqType with
            | "Register" ->

                (* Example JSON message for register API *)
                let regMsg:RegJson = { 
                    ReqType = reqType ; 
                    UserID = nodeID ; 
                    UserName = nodeName ; 
                    PublicKey = Some (nodeName+"Key") ;
                }
                serverNode <! (Json.serialize regMsg)
                return! loop()
            | "SendTweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                    return! loop()

                (* tmp Tweet generated *)
                let rnd = System.Random()
                let (tmpTweet:TweetInfo) = {
                    ReqType = reqType ;
                    UserID  = nodeID ;
                    TweetID = (globalTimer.Elapsed.Ticks.ToString()) ;
                    Time = (DateTime.Now) ;
                    Content = "I am User" + (nodeID.ToString()) + ", I sends a Tweet!" ;
                    Tag = "#Intro" ;
                    Mention = rnd.Next(3) ;
                }
                serverNode <! (Json.serialize tmpTweet)
                return! loop()
            | "Subscribe" ->
                if isOffline then
                    printfn "[%s] Subscribe failed, please connect to Twitter server first" nodeName
                    return! loop()
                let rnd = System.Random()           
                let (subMsg:SubInfo) = {
                    ReqType = reqType ;
                    UserID = nodeID ;
                    PublisherID = rnd.Next(1,3);
                }
                serverNode <! (Json.serialize subMsg)
                return! loop()
            | "Connect" ->
                if isOnline then
                    let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                    nodeSelfRef <! triggerQueryHistory
                    return! loop()

                let (connectMsg:ConnectInfo) = {
                    ReqType = reqType ;
                    UserID = nodeID ;
                }
                serverNode <! (Json.serialize connectMsg)
                return! loop()
            | "Disconnect" ->
                isOnline <- false
                let (connectMsg:ConnectInfo) = {
                    ReqType = reqType ;
                    UserID = nodeID ;
                }
                serverNode <! (Json.serialize connectMsg)
                return! loop()
            | "QueryHistory" | "QuerySubscribe" | "QueryMention" | "QueryTag" ->
                if isOffline then
                    printfn "[%s] Query failed, please connect to Twitter server first" nodeName
                    return! loop()
                if isQuerying then
                    printfn "[%s] Query failed, please wait until the last query is done" nodeName
                    return! loop()

                (* Set querying lock avoiding concurrent queries *)
                isQuerying <- true

                if reqType = "QueryTag" then
                    let (queryMsg:QueryInfo) = {
                        ReqType = reqType ;
                        UserID = nodeID ;
                        Tag = "#Intro" ;
                    }
                    serverNode <! (Json.serialize queryMsg)
                else
                    let (queryMsg:QueryInfo) = {
                        ReqType = reqType ;
                        UserID = nodeID ;
                        Tag = "" ;
                    }
                    serverNode <! (Json.serialize queryMsg)
                return! loop()
            (* Deal with all reply messages  *)
            | "Reply" ->
                let replyType = jsonMsg?Type.AsString()
                match replyType with
                    | "Register" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Register done!" nodeName
                            (* If the user successfully registered, connect to server automatically *)
                            let triggerConnect = """{"ReqType":"Connect"}"""
                            nodeSelfRef <! triggerConnect
                        else
                            printfn "[%s] Register failed! (you might already registered before)" nodeName
                        ()
                    | "Subscribe" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Subscirbe done!" nodeName
                        else
                            printfn "[%s] Subscribe failed!" nodeName
                        ()
                    | "SendTweet" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Send Tweet done!" nodeName
                        else
                            printfn "[%s] Send Tweet failed! Desc: %s" nodeName (jsonMsg?Desc.AsString())
                        ()
                    | "Connect" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isOnline <- true
                            printfn "[%s] Connect to server done! Desc: %s" nodeName (jsonMsg?Desc.AsString())
                            (* Automatically show the history tweets *)
                            let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                            nodeSelfRef <! triggerQueryHistory
                        else
                            printfn "[%s] Failed to connect to server, Desc: %s" nodeName (jsonMsg?Desc.AsString())
                        ()
                    | "Disconnect" ->
                        printfn "[%s] Disconnected to the server, Desc: %s" nodeName (jsonMsg?Desc.AsString())
                    | "QueryHistory" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isQuerying <- false
                            printfn "\n[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else if status = "NoTweet" then
                            isQuerying <- false
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] Something Wrong with Querying History" nodeName
                        ()
                    | "ShowTweet" ->
                        let tweetReplyInfo = (Json.deserialize<TweetReply> message)
                        let tweetInfo = tweetReplyInfo.TweetInfo
                        printfn "\n------------------------------------"
                        printfn "Index: %i      Time: %s" (tweetReplyInfo.Status) (tweetInfo.Time.ToString())
                        printfn "Author: User%i" (tweetInfo.UserID)
                        printfn "Content: {%s}\n%s @User%i" (tweetInfo.Content) (tweetInfo.Tag) (tweetInfo.Mention)
                        printfn "TID: %s" (tweetInfo.TweetID)
                        ()
                    | "ShowSub" ->
                        let subReplyInfo = (Json.deserialize<SubReply> message)
                        printfn "\n------------------------------------"
                        printfn "Name: %s" nodeName
                        printf "Subscribe To: "
                        for id in subReplyInfo.Subscriber do
                            printf "User%i " id
                        printf "\nPublish To: "
                        for id in subReplyInfo.Publisher do
                            printf "User%i " id
                        printfn "\n"
                        printfn "[%s] Query Subscribe done" nodeName
                        isQuerying <- false
                        ()
                    | _ ->
                        printfn "[%s] Unhandled Reply Message" nodeName
                        ()

                return! loop()


            | _ ->
                printfn "client \"%s\" received unknown message \"%s\"" nodeName reqType
                Environment.Exit 1
                return! loop()
         
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =
    try
        globalTimer.Start()
        printfn "%A@%A" globalTimer.Elapsed globalTimer.GetHashCode
        printfn "%A" globalTimer.Elapsed
        let tmpTweet:TweetInfo = {
            ReqType = "SendTweet" ;
            UserID  = 1 ;
            //TweetID = DateTime.Now.ToString() + "9527" ;
            TweetID = globalTimer.Elapsed.Ticks.ToString() ;
            Time = DateTime.Now ;
            Content = "hehehehet" ;
            Tag = "#tagg" ;
            Mention = 2 ;
        }
        let json = Json.serialize tmpTweet
        printfn "oewrj:\n %s\n" json
        let aaa:TweetInfo = Json.deserialize<TweetInfo> json
        printfn "%A" aaa
        printfn "%u" globalTimer.Elapsed.Ticks

        let clientActor = spawn system "1" clientActorNode
        let clientActor2 = spawn system "2" clientActorNode
        let triggerReg = """{"ReqType":"Register"}"""
        let triggerSend = """{"ReqType":"SendTweet"}"""
        let triggerSub = """{"ReqType":"Subscribe"}"""
        let triggerCon = """{"ReqType":"Connect"}"""
        let triggerDiscon = """{"ReqType":"Disconnect"}"""
        let triggerQhis = """{"ReqType":"QueryHistory"}"""
        let triggerQMen = """{"ReqType":"QueryMention"}"""
        let triggerQtag = """{"ReqType":"QueryTag"}"""
        let triggerQsub = """{"ReqType":"QuerySubscribe"}"""

        clientActor <! triggerReg
        clientActor2 <! triggerReg
        clientActor <! triggerCon
        clientActor2 <! triggerCon
        Console.ReadLine() |> ignore
        //clientActor2 <! trigger
        for i in 1 .. 3 do
            clientActor <! triggerSub
            clientActor2 <! triggerSub
        clientActor2 <! triggerSend
        Console.ReadLine() |> ignore
        for i in 1 .. 20 do
            clientActor <! triggerSend
        Console.ReadLine() |> ignore
        clientActor <! triggerSend
        Console.ReadLine() |> ignore
        clientActor2 <! triggerQMen
        Console.ReadLine() |> ignore
        clientActor2 <! triggerQhis
        Console.ReadLine() |> ignore
        clientActor2 <! triggerQtag
        Console.ReadLine() |> ignore
        clientActor <! triggerQsub
        Console.ReadLine() |> ignore
        clientActor2 <! triggerQsub

        //clientActor2 <! triggerReg
        //clientActor <! triggerSend
        //clientActor <! triggerSub
        //clientActor <! triggerCon
        
        Console.ReadLine() |> ignore
    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code

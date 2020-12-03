open System
open System.Security.Cryptography
open System.Globalization
open System.Text
open System.Collections.Generic
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
    RetweetTimes : int
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

type RetweetInfo = {
    ReqType: string
    UserID : int
    TargetUserID : int
    RetweetID : string
}

 (* Actor System Configuration Settings (Locaol Side) *)
let config =
    Configuration.parse
        @"akka {
            log-dead-letters = off
            log-dead-letters-during-shutdown = off
            log-config-on-start = off
            actor.provider = remote
            remote.helios.tcp {
                hostname = localhost
                port = 0
            }
        }"

(* Some globalal variables *)
let system = System.create "Simulator" config
let serverNode = system.ActorSelection("akka.tcp://TwitterEngine@localhost:9001/user/TWServer")
let globalTimer = Stopwatch()
let mutable isSimulation = false


(* Client Node Actor*)
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
                if isSimulation then
                    (* Example JSON message for register API *)
                    let regMsg:RegJson = { 
                        ReqType = reqType ; 
                        UserID = nodeID ; 
                        UserName = nodeName ; 
                        PublicKey = Some (nodeName+"Key") ;
                    }
                    serverNode <! (Json.serialize regMsg)
                else
                    //TODO: modify this to customized JSON request
                    let regMsg:RegJson = { 
                        ReqType = reqType ; 
                        UserID = nodeID ; 
                        UserName = nodeName ; 
                        PublicKey = Some (nodeName+"Key") ;
                    }
                    serverNode <! (Json.serialize regMsg)
                
                
            | "SendTweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else   

                    if isSimulation then
                        serverNode <! message
                    else
                        //TODO: modify this to customized JSON request
                        (*
                        let rnd = System.Random()
                        let (tmpTweet:TweetInfo) = {
                            ReqType = reqType ;
                            UserID  = nodeID ;
                            TweetID = (globalTimer.Elapsed.Ticks.ToString()) ;
                            Time = (DateTime.Now) ;
                            Content = "I am User" + (nodeID.ToString()) + ", I sends a Tweet!" ;
                            Tag = "#Intro" ;
                            Mention = rnd.Next(3) ;
                            RetweetTimes = 0 ;
                        }
                        
                        serverNode <! (Json.serialize tmpTweet)
                        *)
                        serverNode <! message

            | "Retweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        //TODO: modify this to customized JSON request
                        (* user could enter a tweetID or empty string (let server choose) as retweet ID *)
                        let rnd = Random()
                        let (tmpreweet:RetweetInfo) = {
                            ReqType = reqType ;
                            UserID  = nodeID ;
                            TargetUserID =  1;//rnd.Next(3);
                            RetweetID = "" ;
                        }
                        serverNode <! (Json.serialize tmpreweet)
                
            | "Subscribe" ->
                if isOffline then
                    printfn "[%s] Subscribe failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        //TODO: modify this to customized JSON request
                        let rnd = System.Random()           
                        let (subMsg:SubInfo) = {
                            ReqType = reqType ;
                            UserID = nodeID ;
                            PublisherID = rnd.Next(1,3);
                        }
                        serverNode <! (Json.serialize subMsg)

            | "Connect" ->
                if isOnline then
                    if isSimulation then
                        let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                        nodeSelfRef <! triggerQueryHistory
                    else
                        //TODO: modify this to customized JSON request
                        let (connectMsg:ConnectInfo) = {
                            ReqType = reqType ;
                            UserID = nodeID ;
                        }
                        serverNode <! (Json.serialize connectMsg)
                else
                    if isSimulation then
                        let (connectMsg:ConnectInfo) = {
                            ReqType = reqType ;
                            UserID = nodeID ;
                        }
                        serverNode <! (Json.serialize connectMsg)
                    else
                        //TODO: modify this to customized JSON request
                        let (connectMsg:ConnectInfo) = {
                            ReqType = reqType ;
                            UserID = nodeID ;
                        }
                        serverNode <! (Json.serialize connectMsg)

            | "Disconnect" ->
                isOnline <- false

                if isSimulation then
                    let (disconnectMsg:ConnectInfo) = {
                        ReqType = reqType ;
                        UserID = nodeID ;
                    }
                    serverNode <! (Json.serialize disconnectMsg)
                else
                    //TODO: modify this to customized JSON request
                    let (disconnectMsg:ConnectInfo) = {
                        ReqType = reqType ;
                        UserID = nodeID ;
                    }
                    serverNode <! (Json.serialize disconnectMsg)

            | "QueryHistory" | "QuerySubscribe" | "QueryMention" | "QueryTag" ->
                if isOffline then
                    printfn "[%s] Query failed, please connect to Twitter server first" nodeName
                else
                    if isQuerying then
                        printfn "[%s] Query failed, please wait until the last query is done" nodeName
                    else
                        (* Set querying lock avoiding concurrent queries *)
                        isQuerying <- true

                        if reqType = "QueryTag" then
                            if isSimulation then
                                serverNode <! message
                            else
                                //TODO: modify this to customized JSON request
                                let (queryMsg:QueryInfo) = {
                                    ReqType = reqType ;
                                    UserID = nodeID ;
                                    Tag = "#Intro" ;
                                }
                                serverNode <! (Json.serialize queryMsg)
                        else
                            if isSimulation then
                                serverNode <! message
                            else
                                //TODO: modify this to customized JSON request
                                let (queryMsg:QueryInfo) = {
                                    ReqType = reqType ;
                                    UserID = nodeID ;
                                    Tag = "" ;
                                }
                                serverNode <! (Json.serialize queryMsg)
            (* Deal with all reply messages  *)
            | "Reply" ->
                let replyType = jsonMsg?Type.AsString()
                match replyType with
                    | "Register" ->
                        let status = jsonMsg?Status.AsString()
                        let registerUserID = jsonMsg?Desc.AsString() |> int
                        if status = "Success" then
                            printfn "[%s] Successfully registered" nodeName
                            (* If the user successfully registered, connect to the server automatically *)
                            let (connectMsg:ConnectInfo) = {
                                ReqType = "Connect" ;
                                UserID = registerUserID ;
                            }
                            serverNode <! (Json.serialize connectMsg)
                            //let triggerConnect = """{"ReqType":"Connect"}"""
                            //nodeSelfRef <! triggerConnect
                        else
                            printfn "[%s] Register failed!\n\t(this userID might have already registered before)" nodeName
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
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        ()
                    | "Connect" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isOnline <- true
                            printfn "[%s] User%s successfully connected to server" nodeName (jsonMsg?Desc.AsString())
                            (* Automatically query the history tweets of the connected user *)
                            let (queryMsg:QueryInfo) = {
                                ReqType = "QueryHistory" ;
                                UserID = (jsonMsg?Desc.AsString()|> int) ;
                                Tag = "" ;
                            }
                            serverNode <! (Json.serialize queryMsg)
                            //let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                            //nodeSelfRef <! triggerQueryHistory
                        else
                            printfn "[%s] Connection failed, %s" nodeName (jsonMsg?Desc.AsString())
                        ()
                    | "Disconnect" ->
                        printfn "[%s] User%s disconnected from the server" nodeName (jsonMsg?Desc.AsString())
                        ()
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
                        (* Don't print out any tweet on console if it is in simulation mode *)
                        if not isSimulation then
                            let tweetReplyInfo = (Json.deserialize<TweetReply> message)
                            let tweetInfo = tweetReplyInfo.TweetInfo
                            printfn "\n------------------------------------"
                            printfn "Index: %i      Time: %s" (tweetReplyInfo.Status) (tweetInfo.Time.ToString())
                            printfn "Author: User%i" (tweetInfo.UserID)
                            printfn "Content: {%s}\n%s  @User%i  Retweet times: %i" (tweetInfo.Content) (tweetInfo.Tag) (tweetInfo.Mention) (tweetInfo.RetweetTimes)
                            printfn "TID: %s" (tweetInfo.TweetID)
                        ()
                    | "ShowSub" ->
                        if not isSimulation then
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


            | _ ->
                printfn "Client node \"%s\" received unknown message \"%s\"" nodeName reqType
                Environment.Exit 1
         
        return! loop()
    }
    loop()

// ----------------------------------------------------------
// Clients Manipulation
// ----------------------------------------------------------
let register (client: IActorRef) = 
    client <! """{"ReqType":"Register"}"""
let sendTweet (client: IActorRef) (hashtag: string) (mention: int)= 
    let (request: TweetInfo) = {
        ReqType = "SendTweet";
        UserID = (int) client.Path.Name;
        TweetID = "";
        Time = DateTime.Now;
        Content = "Tweeeeeet";
        Tag = hashtag;
        Mention = mention;
        RetweetTimes = 0 ;
    }
    client <! (Json.serialize request)
let subscribe (client: IActorRef) (publisher: IActorRef) = 
    let (request: SubInfo) = {
        ReqType = "Subscribe";
        UserID = (int) client.Path.Name;
        PublisherID = (int) publisher.Path.Name;
    }
    client <! (Json.serialize request)
let connect (client: IActorRef) = 
    client <! """{"ReqType":"Connect"}"""
let disconnect (client: IActorRef) = 
    client <! """{"ReqType":"Disconnect"}"""
let queryHistory (client: IActorRef) = 
    client <! """{"ReqType":"QueryHistory"}"""
let queryByMention (client: IActorRef) = 
    client <! """{"ReqType":"QueryMention"}"""
let queryByTag (client: IActorRef) = 
    client <! """{"ReqType":"QueryTag"}"""
let queryBySubscribtion (client: IActorRef) = 
    client <! """{"ReqType":"QuerySubscribe"}"""

// ----------------------------------------------------------
// Simulator Functions
// | spawnClients
// | clientSampler
// | shuffleList
// | getNumOfSub : Assign random popularity (Zipf) to each acotr
// | tagSampler
// ----------------------------------------------------------

let spawnClients (clientNum: int) = 
    [1 .. clientNum]
    |> List.map (fun id -> spawn system ((string) id) clientActorNode)
    |> List.toArray

let clientSampler (allClients: IActorRef []) (num: int) = 
    let random = Random()
    let rand () = random.Next(1, (Array.length allClients))
    [for i in 1 .. num -> allClients.[rand()]] 

let shuffleList (rand: Random) (l) = 
    l |> List.sortBy (fun _ -> rand.Next()) 

let getNumOfSub (numClients: int)= 
    let constant = List.fold (fun acc i -> acc + (1.0/i)) 0.0 [1.0 .. (float) numClients]
    let res =
        [1.0 .. (float) numClients] 
        |> List.map (fun x -> (float) numClients/(x*constant) |> Math.Round |> int)
    shuffleList (Random()) res             

let tagSampler (hashtags: string []) = 
    let random = Random()
    let rand () = random.Next(hashtags.Length-1)
    hashtags.[rand()]

[<EntryPoint>]
let main argv =
    try
        globalTimer.Start()
        (* simulate / user / debug*)
        let programMode = argv.[0]
        
        (* Simulator parameter variables *)
        let mutable numClients = 0
        let mutable percentActive = 0
        let mutable totalRequest = 2147483647

        if programMode = "user" then
            printfn "Now you are Admin user, you could login as any other existing client or register new users own"
            (* Prompt User for Simulator Usage *)
            printfn "Please enter the commands listed below:"
            printfn "1. exit: terminate this program"
            while programMode = "user" do
                let inputStr = Console.ReadLine()
                match inputStr with
                    | "1"| "Connect"->
                        () 
                    | "exit" ->
                        Environment.Exit 1
                    | _ ->
                        ()
        else if programMode = "simulate" then
            (* Set to simulation mode *)
            printfn "\n\n[Simulator Mode]\n"
            printfn "Please enter some simulation parameters below:"
            printf "How many USERs you would like to simulate? "
            numClients <- Console.ReadLine() |> int
            printf "How many percent of USERs are active users? "
            percentActive <- Console.ReadLine() |> int
            printf "What is the total API  request to stop the simulator? "
            totalRequest <- Console.ReadLine() |> int
            printfn "This is you simulation settings..."
            printfn "Number of Users: %i" numClients
            printfn "Number of total requests: %i" totalRequest
            printfn "Percentage of active users: %i%%" percentActive
            printfn "(active users has three times more chances to send requests than inactive users)"
            printfn "\n\n[Press any key to start the simulation]\n\n"
            Console.ReadLine() |> ignore
            isSimulation <- true
        else if programMode = "debug" then
            printfn "\n\n[Debug Mode]\n"
            //isSimulation <- true
        else
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n\t1. dotnet run simulate\n\t2. dotnet run user\n\t3. dotnet run debug\n"
            Environment.Exit 1

        // ----------------------------------------------------------
        // Simulator Scenerio
        // 1. spawn clients
        // 2. register all clients
        // 3. randomly subscribe each other (follow the Zipf)
        // 4. assign random number tweets (1~5) for each client to send
        // 5. 
        // ----------------------------------------------------------
        
        (* Setup *)
        numClients <- 1000
        let hashtags = [|"#abc";"#123"; "#DOSP"; "#Twitter"; "#Akka"; "#Fsharp"|]
        (* 1. spawn clients *)
        let myClients = spawnClients numClients
        //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))
        //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))
        
        (* 2. register all clients *)
        myClients 
        |> Array.map(fun client ->
            async{
                register client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        

        
        Console.ReadLine() |> ignore
        
        (* 3. randomly subscribe each other (follow the Zipf) *)
        let numOfSub = getNumOfSub numClients
        printfn "[debug] numofSub:\n%A" numOfSub
        myClients
        |> Array.mapi(fun i client ->
            async {
                let sub = numOfSub.[i]
                let mutable s = sub
                let rand = Random()
                while s > 0 do
                    let subscriber = rand.Next(numClients)
                    if subscriber <> i then
                        s <- s - 1
                        subscribe (myClients.[subscriber]) client
                //printfn "user%s has enough subscriber(%d)" client.Path.Name sub
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        Console.ReadLine() |> ignore
        
        (* 4. assign random number tweets (1~5) for each client to send *)
        myClients
        |> Array.map (fun client ->
            async{
                let rand = Random()
                let numTweets = rand.Next(1,5)
                for i in 1 .. numTweets do
                    sendTweet client (tagSampler hashtags) 1
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        (*
        let mutable lastTStamp = globalTimer.ElapsedMilliseconds
        while true do
            if (globalTimer.ElapsedMilliseconds) - lastTStamp  >= (1000 |> int64) then 
                lastTStamp <- globalTimer.ElapsedMilliseconds
                myClients
                |> Array.map (fun client ->
                    async{
                        let rand = Random()
                        let numTweets = rand.Next(1,5)
                        for i in 1 .. numTweets do
                            sendTweet client (tagSampler hashtags) 1
                    })
                |> Async.Parallel
                |> Async.RunSynchronously
                |> ignore
        *)
        (*
        globalTimer.Start()
        printfn "%A@%A" globalTimer.Elapsed globalTimer.GetHashCode
        printfn "%A" globalTimer.Elapsed
        let asd = new Dictionary<string, TweetInfo>()
        let tmpTweet:TweetInfo = {
            ReqType = "SendTweet" ;
            UserID  = 1 ;
            //TweetID = DateTime.Now.ToString() + "9527" ;
            TweetID = globalTimer.Elapsed.Ticks.ToString() ;
            Time = DateTime.Now ;
            Content = "hehehehet" ;
            Tag = "#tagg" ;
            Mention = 2 ;
            RetweetTimes = 0;
        }
        asd.Add("a", tmpTweet)
        let newTweet:TweetInfo = {
            ReqType = "SendTweet" ;
            UserID  = 1 ;
            //TweetID = DateTime.Now.ToString() + "9527" ;
            TweetID = globalTimer.Elapsed.Ticks.ToString() ;
            Time = DateTime.Now ;
            Content = "hehehehet" ;
            Tag = "#tagg" ;
            Mention = 2 ;
            RetweetTimes = 0+1;
        }
        //asd.["a"] <- newTweet
        let json = Json.serialize asd.["a"]
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
        let triggerRetweet = """{"ReqType":"Retweet"}"""

        clientActor <! triggerReg
        clientActor2 <! triggerReg
        clientActor <! triggerCon
        clientActor2 <! triggerCon
        Console.ReadLine() |> ignore
        //clientActor2 <! trigger
        for i in 1 .. 3 do
            clientActor <! triggerSub
            clientActor2 <! triggerSub
        clientActor <! triggerSend
        Console.ReadLine() |> ignore
        clientActor <! triggerRetweet  //retweet user1
        for i in 1 .. 20 do
            clientActor <! triggerSend
            clientActor2 <! triggerRetweet
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
        
        *)
        Console.ReadLine() |> ignore

    with | :? IndexOutOfRangeException ->
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n1. dotnet run simulate\n2. dotnet run user\n\n"

         | :? FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 

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
    TargetUserID : int
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

(* User Mode Connect/Register check *)
type UserModeStatusCheck =
| Success
| Fail
| Waiting
| Timeout
let mutable (isUserModeLoginSuccess:UserModeStatusCheck) = Waiting

(* Client Node Actor*)
let clientActorNode (isSingleUser: bool) (clientMailbox:Actor<string>) =
    let mutable nodeName = "User" + clientMailbox.Self.Path.Name
    let mutable nodeID = 
        match (Int32.TryParse(clientMailbox.Self.Path.Name)) with
        | (true, value) -> value
        | (false, _) -> 0
    
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
                    serverNode <! message
                
                if isSingleUser then 
                    globalTimer.Restart()

            | "SendTweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else   

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message

                if isSingleUser then 
                    globalTimer.Restart()

            | "Retweet" ->
                if isOffline then
                    printfn "[%s] Send tweet failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message

                if isSingleUser then 
                    globalTimer.Restart()

            | "Subscribe" ->
                if isOffline then
                    printfn "[%s] Subscribe failed, please connect to Twitter server first" nodeName
                else

                    if isSimulation then
                        serverNode <! message
                    else
                        serverNode <! message
                
                if isSingleUser then 
                    globalTimer.Restart()

            | "Connect" ->
                if isOnline then
                    if isSimulation then
                        //let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                        nodeSelfRef <! """{"ReqType":"QueryHistory", "UserID":"""+"\""+ nodeID.ToString() + "\"}"
                    else
                        
                        serverNode <! message
                else
                    if isSimulation then
                        serverNode <! message
                        // let (connectMsg:ConnectInfo) = {
                        //    ReqType = reqType ;
                        //    UserID = nodeID ;
                        // }
                        // serverNode <! (Json.serialize connectMsg)
                    else
                        serverNode <! message
                
                if isSingleUser then 
                    globalTimer.Restart()

            | "Disconnect" ->
                isOnline <- false

                if isSimulation then
                    // let (disconnectMsg:ConnectInfo) = {
                    //     ReqType = reqType ;
                    //     UserID = nodeID ;
                    // }
                    // serverNode <! (Json.serialize disconnectMsg)
                    serverNode <! message
                else

                    serverNode <! message
                
                if isSingleUser then 
                    globalTimer.Restart()

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
                                serverNode <! message
                        else
                            if isSimulation then
                                // let (queryMsg:QueryInfo) = {
                                //     ReqType = reqType ;
                                //     UserID = nodeID ;
                                //     Tag = "" ;
                                // }
                                // serverNode <! (Json.serialize queryMsg)
                                serverNode <! message
                            else
                                serverNode <! message
                    
                    if isSingleUser then 
                        globalTimer.Restart()
                
            (* Deal with all reply messages  *)
            | "Reply" ->
                let replyType = jsonMsg?Type.AsString()
                match replyType with
                    | "Register" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for register: {0}", globalTimer.Elapsed)
                        let status = jsonMsg?Status.AsString()
                        let registerUserID = jsonMsg?Desc.AsString() |> int
                        if status = "Success" then
                            if isSimulation then 
                                printfn "[%s] Successfully registered" nodeName
                            else
                                isUserModeLoginSuccess <- Success
                            (* If the user successfully registered, connect to the server automatically *)
                            let (connectMsg:ConnectInfo) = {
                                ReqType = "Connect" ;
                                UserID = registerUserID ;
                            }
                            serverNode <! (Json.serialize connectMsg)
                            globalTimer.Restart()
                            //let triggerConnect = """{"ReqType":"Connect"}"""
                            //nodeSelfRef <! triggerConnect
                        else
                            if isSimulation then 
                                printfn "[%s] Register failed!\n\t(this userID might have already registered before)" nodeName
                            else
                                isUserModeLoginSuccess <- Fail
                        

                    | "Subscribe" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for subscribe: {0}", globalTimer.Elapsed)
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Subscirbe done!" nodeName
                        else
                            printfn "[%s] Subscribe failed!" nodeName

                    | "SendTweet" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for sendTweet: {0}", globalTimer.Elapsed)
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())

                    | "Connect" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for sendTweet: {0}", globalTimer.Elapsed)
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isOnline <- true
                            if isSimulation then 
                                printfn "[%s] User%s successfully connected to server" nodeName (jsonMsg?Desc.AsString())
                            else
                                isUserModeLoginSuccess <- Success
                            (* Automatically query the history tweets of the connected user *)
                            let (queryMsg:QueryInfo) = {
                                ReqType = "QueryHistory" ;
                                UserID = (jsonMsg?Desc.AsString()|> int) ;
                                Tag = "" ;
                            }
                            serverNode <! (Json.serialize queryMsg)
                            globalTimer.Restart()
                            //let triggerQueryHistory = """{"ReqType":"QueryHistory"}"""
                            //nodeSelfRef <! triggerQueryHistory
                        else
                            if isSimulation then 
                                printfn "[%s] Connection failed, %s" nodeName (jsonMsg?Desc.AsString())
                            else
                                isUserModeLoginSuccess <- Fail


                    | "Disconnect" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for Disconnect: {0}", globalTimer.Elapsed)
                        if isSimulation then 
                            printfn "[%s] User%s disconnected from the server" nodeName (jsonMsg?Desc.AsString())
                        else
                            isUserModeLoginSuccess <- Success
                    | "QueryHistory" ->
                        if isSingleUser then 
                            System.Console.WriteLine("Reponse time for QueryHistory: {0}", globalTimer.Elapsed)
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            isQuerying <- false
                            printfn "\n[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else if status = "NoTweet" then
                            isQuerying <- false
                            printfn "[%s] %s" nodeName (jsonMsg?Desc.AsString())
                        else
                            printfn "[%s] Something Wrong with Querying History" nodeName

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

                    | "ShowSub" ->
                        isQuerying <- false
                        if not isSimulation then
                            let subReplyInfo = (Json.deserialize<SubReply> message)
                            
                            printfn "\n------------------------------------"
                            printfn "Name: %s" ("User" + subReplyInfo.TargetUserID.ToString())
                            printf "Subscribe To: "
                            for id in subReplyInfo.Subscriber do
                                printf "User%i " id
                            printf "\nPublish To: "
                            for id in subReplyInfo.Publisher do
                                printf "User%i " id
                            printfn "\n"
                            printfn "[%s] Query Subscribe done" nodeName
                            

                    | _ ->
                        printfn "[%s] Unhandled Reply Message" nodeName

            | "UserModeOn" ->
                //printfn "\n\n\n\n%A\n\n\n\n" message
                //let curName = jsonMsg?CurUserName.AsString()
                let curUserID = jsonMsg?CurUserID.AsInteger()
                nodeID <- curUserID
                nodeName <- "User" + curUserID.ToString()

            | _ ->
                printfn "Client node \"%s\" received unknown message \"%s\"" nodeName reqType
                Environment.Exit 1
         
        return! loop()
    }
    loop()


// ----------------------------------------------------------
// User Mode for JSON request 
// ----------------------------------------------------------

let getUserInput (option:string) = 
    let mutable keepPrompt = true
    let mutable userInputStr = ""
    match option with
    | "int" ->
        while keepPrompt do
            printf "Enter a number: "
            userInputStr <- Console.ReadLine()
            match (Int32.TryParse(userInputStr)) with
            | (true, _) -> (keepPrompt <- false)
            | (false, _) ->  printfn "[Error] Invalid number"
        userInputStr
    | "string" ->
        while keepPrompt do
            printf "Enter a string: "
            userInputStr <- Console.ReadLine()
            match userInputStr with
            | "" | "\n" | "\r" | "\r\n" | "\0" -> printfn "[Error] Invalid string"
            | _ -> (keepPrompt <- false)
        userInputStr
    | "YesNo" ->
        while keepPrompt do
            printf "Enter yes/no: "
            userInputStr <- Console.ReadLine()
            match userInputStr.ToLower() with
            | "yes" | "y" -> 
                (keepPrompt <- false) 
                userInputStr<-"yes"
            | "no" | "n" ->
                (keepPrompt <- false) 
                userInputStr<-"no"
            | _ -> printfn "[Error] Invalid input"
        userInputStr
    | _ ->
        userInputStr                                
    

let genRegisterJSON (publicKey:string) =
    
    printfn "Pleae enter an unique number for \"UserID\": "
    let userid = (int) (getUserInput "int")
    printfn "Pleae enter a \"Name\": "
    let username = (getUserInput "string")
    let regJSON:RegJson = { 
        ReqType = "Register" ; 
        UserID =  userid ;
        UserName = username ; 
        PublicKey = Some (publicKey) ;
    }
    Json.serialize regJSON

let genConnectDisconnectJSON (option:string, curUserID:int) = 
    if option = "Connect" then
        printfn "Please enter a number for \"UserID\": "
        let userid = (int) (getUserInput "int")
        let connectJSON:ConnectInfo = {
            ReqType = "Connect" ;
            UserID = userid ;
        }
        Json.serialize connectJSON
    else
        let connectJSON:ConnectInfo = {
            ReqType = "Disconnect" ;
            UserID = curUserID ;
        }
        Json.serialize connectJSON

let genTweetJSON curUserID = 
    let mutable tag = ""
    let mutable mention = -1
    printfn "Please enter the \"Content\" of your Tweet: "
    let content = (getUserInput "string")
    printfn "Would you like to add a \"Tag\"?"
    if (getUserInput "YesNo") = "yes" then
        printfn "Please enter a \"Tag\" (with #): "
        tag <- (getUserInput "string")
    printfn "Would you like to add a \"Mention\"?"
    if (getUserInput "YesNo") = "yes" then
        printfn "Please enter a \"UserID\" to mention (w/o @): "
        mention <- (int) (getUserInput "int")

    let (tweetJSON:TweetInfo) = {
        ReqType = "SendTweet" ;
        UserID  = curUserID ;
        TweetID = "" ;
        Time = (DateTime.Now) ;
        Content = content ;
        Tag = tag ;
        Mention = mention ;
        RetweetTimes = 0 ;
    }
    Json.serialize tweetJSON

//subscribe
let genSubscribeJSON curUserID = 
    printfn "Please enter a \"UserID\" you would like to subscribe to: "
    let subToUserID = (int) (getUserInput "int")
    let (subJSON:SubInfo) = {
        ReqType = "Subscribe" ;
        UserID = curUserID ;
        PublisherID = subToUserID;
    }
    Json.serialize subJSON
//retweet
let genRetweetJSON curUserID = 
    printfn "Please enter a \"TweetID\" you would like to \"Retweet\": "
    let retweetID = (getUserInput "string")
    let (retweetJSON:RetweetInfo) = {
        ReqType = "Retweet" ;
        UserID  = curUserID ;
        TargetUserID =  -1 ;
        RetweetID = retweetID ;
    }
    Json.serialize retweetJSON
//queryhistory
//querytag
//querymention
//querysubscirbe
let genQueryJSON (option:string) =
    match option with
    | "QueryTag" ->
        printfn "Please enter the \"Tag\" you would like to query (with #): "
        let tag = getUserInput "string"
        let (queryTagJSON:QueryInfo) = {
            ReqType = "QueryTag" ;
            UserID = -1 ;
            Tag = tag ;
        }
        Json.serialize queryTagJSON
    | "QueryHistory" | "QueryMention" | "QuerySubscribe" ->
        printfn "Please enter a \"UserID\" you would like to \"%s\":" option
        let userid = (int) (getUserInput "int")
        let (queryJSON:QueryInfo) = {
            ReqType = option ;
            UserID = userid ;
            Tag = "" ;
        }
        Json.serialize queryJSON
    | _ -> 
        printfn "[Error] genQueryJSON function wrong input"
        Environment.Exit 1
        ""

let getUserID (jsonStr:string) = 
    let jsonMsg = JsonValue.Parse(jsonStr)
    (jsonMsg?UserID.AsInteger())



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
let retweet (client: IActorRef) (targetUserID: int)=
    let (request: RetweetInfo) = {
        ReqType = "Retweet";
        RetweetID = "";
        TargetUserID = targetUserID;
        UserID = (int) client.Path.Name;
    }
    client <! (Json.serialize request)
let connect (client: IActorRef) = 
    let (request: ConnectInfo) = {
        ReqType = "Connect";
        UserID = client.Path.Name |> int;
    }
    client <! (Json.serialize request)
let disconnect (client: IActorRef) = 
    let (request: ConnectInfo) = {
        ReqType = "Disconnect";
        UserID = client.Path.Name |> int;
    }
    client <! (Json.serialize request)

let queryHistory (client: IActorRef) = 
    let (request: QueryInfo) = {
        ReqType = "QueryHistory";
        UserID = client.Path.Name |> int;
        Tag = "";
    }
    client <! (Json.serialize request)
let queryByMention (client: IActorRef) (mentionedUserID: int) = 
    let (request: QueryInfo) = {
        ReqType = "QueryHistory";
        Tag = "";
        UserID = mentionedUserID;
    }
    client <! (Json.serialize request)
let queryByTag (client: IActorRef) (tag: string)= 
    let (request: QueryInfo) = {
        ReqType = "QueryTag";
        Tag = tag;
        UserID = 0;
    }
    client <! (Json.serialize request)
let queryBySubscribtion (client: IActorRef) (id: int) = 
    let (request: QueryInfo) = {
            ReqType = "QuerySubscribe";
            Tag = "";
            UserID = id;
        }
    client <! (Json.serialize request)
// ----------------------------------------------------------
// Simulator Functions
// | spawnClients
// | arraySampler
// | shuffleList
// | getNumOfSub : Assign random popularity (Zipf) to each acotr
// | tagSampler
// | getConnectedID
// | getDisconnectedID
// ----------------------------------------------------------

let spawnClients (clientNum: int) = 
    [1 .. clientNum]
    |> List.map (fun id -> spawn system ((string) id) (clientActorNode false))
    |> List.toArray

let arraySampler (arr: 'T []) (num: int) = 
    if arr.Length = 0 then 
        List.empty
    else
        let rnd = System.Random()    
        Seq.initInfinite (fun _ -> rnd.Next (arr.Length)) 
        |> Seq.distinct
        |> Seq.take(num)
        |> Seq.map (fun i -> arr.[i]) 
        |> Seq.toList

let shuffle (rand: Random) (l) = 
    l |> Array.sortBy (fun _ -> rand.Next()) 

let getNumOfSub (numClients: int)= 
    let constant = List.fold (fun acc i -> acc + (1.0/i)) 0.0 [1.0 .. (float) numClients]
    let res =
        [1.0 .. (float) numClients] 
        |> List.map (fun x -> (float) numClients/(x*constant) |> Math.Round |> int)
        |> List.toArray
    //printfn "res\n%A" res
    //Environment.Exit 1
    shuffle (Random()) res             

let tagSampler (hashtags: string []) = 
    let random = Random()
    let rand () = random.Next(hashtags.Length-1)
    hashtags.[rand()]

let getConnectedID (connections: bool []) =
    [1 .. connections.Length-1]
    |> List.filter (fun i -> connections.[i])
    |> List.toArray

let getDisconnectedID (connections: bool []) =
    [1 .. connections.Length-1]
    |> List.filter (fun i -> not connections.[i])
    |> List.toArray



(* User Mode Prompt  *)
let printBanner (printStr:string) =
    printfn "\n----------------------------------"
    printfn "%s" printStr
    printfn "----------------------------------\n"

let showPrompt option = 
    match option with
    | "loginFirst" ->
        printfn "Now you are in \"USER\" mode, you could login as any other existing client or register a new User\n"
        printfn "Please choose one of the commands listed below:"
        printfn "1. register\t register a Twitter account"
        printfn "2. connect\t login as a User"
        printfn "3. exit\t\t terminate this program"
    | "afterLogin" ->
        printfn "\nYou already logged in a Client Termianl\n"
        printfn "Please choose one of the commands listed below:"
        printfn "1. sendtweet\t Post a Tweet for current log in User"
        printfn "2. retweet\t Retweet a Tweet"
        printfn "3. subscribe\t Subscribe to a User"
        printfn "4. disconnect\t Disconnect/log out the current User"
        printfn "5. history\t Query a User's History Tweets"
        printfn "6. tag\t\t Query Tweets with a #Tag"
        printfn "7. mention\t Query Tweets for a mentioned User"
        printfn "8. Qsubscribe\t Query subscribe status for a User"
        printfn "9. exit\t\t terminate this program"
    | _ ->
        ()

let setTimeout _ =
    isUserModeLoginSuccess <- Timeout


let waitForServerResponse (timeout:float) =
    (* timeout: seconds *)
    let timer = new Timers.Timer(timeout*1000.0)
    isUserModeLoginSuccess <- Waiting
    timer.Elapsed.Add(setTimeout)
    timer.Start()
    printBanner "Waiting for server reply..."
    while isUserModeLoginSuccess = Waiting do ()
    timer.Close()



[<EntryPoint>]
let main argv =
    try
        globalTimer.Start()
        (* simulate / user / debug*)
        let programMode = argv.[0]
        
        (* Simulator parameter variables *)
        let mutable numClients = 1000
        let mutable maxCycle = 1000
        let mutable totalRequest = 2147483647
        let hashtags = [|"#abc";"#123"; "#DOSP"; "#Twitter"; "#Akka"; "#Fsharp"|]

        let mutable percentActive = 60
        let mutable percentSendTweet = 50
        let mutable percentRetweet = 20
        let mutable percentQueryHistory = 20
        let mutable percentQueryByMention = 10
        let mutable percentQueryByTag = 10

        let mutable numActive = numClients * percentActive / 100
        let mutable numSendTweet = numActive * percentSendTweet / 100
        let mutable numRetweet = numActive * percentRetweet / 100
        let mutable numQueryHistory = numActive * percentQueryHistory / 100
        let mutable numQueryByMention = numActive * percentQueryByMention / 100
        let mutable numQueryByTag = numActive * percentQueryByTag / 100
    
        
        if programMode = "user" then
            (* Create a terminal actor node for user mode *)
            
            let termianlRef = spawn system "TerminalNode" (clientActorNode true)
            let mutable curUserID = -1
            let mutable curState= 0
            (* Prompt User for Simulator Usage *)
            
            (showPrompt "loginFirst")
            while true do
                (* First State, User have to register or connect(login) first *)
                (* If successfully registered, *)
                while curState = 0 do
                    let inputStr = Console.ReadLine()
                    match inputStr with
                        | "1" | "register" ->
                            let requestJSON = genRegisterJSON "key"
                            let tmpuserID = getUserID requestJSON
                            termianlRef <! requestJSON
                            printfn "Send register JSON to server...\n%A" requestJSON
                            waitForServerResponse (5.0)
                            if isUserModeLoginSuccess = Success then
                                printBanner ("Successfully registered and login as User"+ tmpuserID.ToString())
                                termianlRef <! """{"ReqType":"UserModeOn", "CurUserID":"""+"\""+ tmpuserID.ToString() + "\"}"
                                curUserID <- tmpuserID
                                curState <- 1
                                (showPrompt "afterLogin")
                            else if isUserModeLoginSuccess = Fail then
                                printBanner ("Faild to register for UserID: " + tmpuserID.ToString())
                                (showPrompt "loginFirst")
                            else
                                printBanner ("Faild to register for UserID: " + tmpuserID.ToString() + "\n(Server no response, timeout occurs)")
                                (showPrompt "loginFirst")

                        | "2" | "connect" ->
                            let requestJSON = genConnectDisconnectJSON ("Connect", -1)
                            let tmpuserID = getUserID requestJSON
                            termianlRef <! requestJSON
                            printfn "Send Connect JSON to server...\n%A" requestJSON
                            waitForServerResponse (5.0)
                            if isUserModeLoginSuccess = Success then
                                printBanner ("Successfully connected and login as User"+ tmpuserID.ToString())
                                termianlRef <! """{"ReqType":"UserModeOn", "CurUserID":"""+"\""+ tmpuserID.ToString() + "\"}"
                                curUserID <- tmpuserID
                                curState <- 1
                                (showPrompt "afterLogin")
                            else if isUserModeLoginSuccess = Fail then
                                printBanner ("Faild to connect and login for UserID: " + tmpuserID.ToString())
                                (showPrompt "loginFirst")
                            else
                                printBanner ("Faild to connect and login for UserID: " + tmpuserID.ToString() + "\n(Server no response, timeout occurs)")
                                (showPrompt "loginFirst")

                        | "3" | "exit" | "ex" ->
                            printfn "Exit the program, Bye!"
                            Environment.Exit 1
                        | _ ->
                            (showPrompt "loginFirst")

                while curState = 1 do
                    let inputStr = Console.ReadLine()
                    match inputStr with
                        | "1"| "sendtweet" ->
                            termianlRef <! genTweetJSON curUserID
                            (showPrompt "afterLogin")
                        | "2"| "retweet" -> 
                            termianlRef <! genRetweetJSON curUserID
                            (showPrompt "afterLogin")
                        | "3"| "subscribe" | "sub" -> 
                            termianlRef <! genSubscribeJSON curUserID
                            (showPrompt "afterLogin")
                        | "4" | "disconnect" ->
                            termianlRef <! genConnectDisconnectJSON ("Disconnect", curUserID)
                            waitForServerResponse (5.0)
                            if isUserModeLoginSuccess = Success then
                                printBanner ("Successfully diconnected and logout User"+ curUserID.ToString())
                                curUserID <- -1
                                curState <- 0
                                (showPrompt "loginFirst")
                            else
                                printBanner ("Faild to disconnect and logout for UserID: " + curUserID.ToString() + "\n(Server no response, timeout occurs)")
                                (showPrompt "afterLogin")
                        | "5"| "history" -> 
                            termianlRef <! genQueryJSON "QueryHistory"
                            (showPrompt "afterLogin")
                        | "6"| "tag" -> 
                            termianlRef <! genQueryJSON "QueryTag"
                            (showPrompt "afterLogin")
                        | "7"| "mention" | "men" -> 
                            termianlRef <! genQueryJSON "QueryMention"
                            (showPrompt "afterLogin")
                        | "8"| "Qsubscribe" | "Qsub" -> 
                            termianlRef <! genQueryJSON "QuerySubscribe"
                            (showPrompt "afterLogin")
                        | "9" | "exit" | "ex" ->
                            printfn "Exit the program, Bye!"
                            Environment.Exit 1
                        | _ ->
                            (showPrompt "afterLogin")

        else if programMode = "simulate" then
            (* Set to simulation mode *)
            printfn "\n\n[Simulator Mode]\n"
            printfn "Please enter some simulation parameters below:"

            printf "How many USERs you would like to simulate?\n"
            numClients <- getUserInput "int" |> int

            printf "How many percent(%%) of USERs are active in each repeated cycle?\n "
            percentActive <- getUserInput "int" |> int
            numActive <- numClients * percentActive / 100

            printf "How many percent(%%) of active USERs send tweet in each repeated cycle?\n " 
            percentSendTweet <- getUserInput "int" |> int
            numSendTweet <- numActive * percentSendTweet / 100

            printf "How many percent(%%) of active USERs retweet in each repeated cycle?\n " 
            percentRetweet <- getUserInput "int" |> int
            numRetweet <- numActive * percentRetweet / 100

            let mutable remaining = 100    
            printf "How many percent(%%) of active USERs query history in each repeated cycle? (max: %d)\n " remaining
            percentQueryHistory <- getUserInput "int" |> int
            numQueryHistory <- numActive * percentQueryHistory / 100
            remaining <- remaining - percentQueryHistory

            printf "How many percent(%%) of active USERs query by metnion in each repeated cycle? (max: %d)\n " remaining
            percentQueryByMention<- getUserInput "int" |> int
            numQueryByMention <- numActive * percentQueryByMention / 100
            remaining <- remaining - percentQueryByMention

            printf "How many percent(%%) of active USERs query by tag in each repeated cycle? (max: %d)\n " remaining
            percentQueryByTag<- getUserInput "int" |> int
            numQueryByTag <- numActive * percentQueryByTag / 100
            remaining <- remaining - percentQueryByTag

            printf "What is the total API request to stop the simulator?\n "
            totalRequest <- getUserInput "int" |> int

            printf "What is the maximum number of cycle of repeated simulations?\n "
            maxCycle <- getUserInput "int" |> int
            
            isSimulation <- true

        else if programMode = "debug" then
            isSimulation <- true
            printfn "\n\n[Debug Mode]\n"
            //isSimulation <- true
        else
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n\t1. dotnet run simulate\n\t2. dotnet run user\n\t3. dotnet run debug\n"
            Environment.Exit 1

        // ----------------------------------------------------------
        // Simulator Scenerio
        // * BASIC SETUP
        //   1. spawn clients
        //   2. register all clients
        //   3. randomly subscribe each other (follow the Zipf)
        //   4. assign random number n = (1 .. 5) * (# of subscriptions) 
        //      tweets with randomly selected tag and mentioned 
        //      for each client to send
        //   5. make all clients disconnected
        // * In every 2 second
        //   1. randomly select some clients connected
        //   2. randomly select some clients disconnected
        //   3. randomly select some connected clients send tweets
        //   4. randomly select some connected clients retweet
        //   5.
        // ----------------------------------------------------------
        (* Setup *)

        printfn "\n\n\n\n\n
        -----------------------------Simulation Setup--------------------------------\n
        This is you simulation settings...\n
        Number of Users: %d\n
        Number of total requests: %d\n
        Number of maximum of repeated cycles: %d\n
        Number of active users: %d (%d%%)\n
        Number of active users who send a tweet: %d (%d%%)\n
        Number of active users who send a retweet: %d (%d%%)\n
        Number of active users who query history: %d (%d%%)\n
        Number of active users who query by mention: %d (%d%%)\n
        Number of active users who send by tag: %d (%d%%)\n
        -----------------------------------------------------------------------------"
            numClients totalRequest maxCycle numActive percentActive numSendTweet 
            percentSendTweet numRetweet percentRetweet numQueryHistory percentQueryHistory
            numQueryByMention percentQueryByMention numQueryByTag percentQueryByTag

        printfn "\n\n[Press any key to start the simulation]\n"
        printfn "[Once it starts, the client registration will need few seconds...]\n"
        Console.ReadLine() |> ignore

        (* 1. spawn clients *)
        let myClients = spawnClients numClients
        //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))
        //clientSampler myClients 5 |> List.iter(fun client -> printfn "%s" (client.Path.ToString()))

        (* 2. register all clients *)
        let numOfSub = getNumOfSub numClients
        
        myClients 
        |> Array.map(fun client -> 
            async{
                register client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        
        System.Threading.Thread.Sleep (max numClients 3000)
        //Console.ReadLine() |> ignore

        (* 3. randomly subscribe each other (follow the Zipf) *)
        myClients
        |> Array.mapi(fun i client ->
            async {
                let sub = numOfSub.[i]
                let mutable s = Set.empty
                let rand = Random()
                while s.Count < sub do
                    let subscriber = rand.Next(numClients-1)
                    if myClients.[subscriber].Path.Name <> client.Path.Name && not (s.Contains(subscriber)) then
                        s <- s.Add(subscriber)
                        subscribe (myClients.[subscriber]) client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        
        (* 4. assign random number n = (1 .. 5) * (# of subscriptions) tweets  for each client to send *)
        myClients
        |> Array.mapi (fun i client ->
            async{
                let rand1 = Random()
                let rand2 = Random(rand1.Next())
                let numTweets = max (rand1.Next(1,5) * numOfSub.[i]) 1
                
                for i in 1 .. numTweets do
                    sendTweet client (tagSampler hashtags) (rand2.Next(numClients))
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        (* 5. make all clients disconnected *)
        myClients
        |> Array.map (fun client -> 
            async{
                disconnect client
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        System.Threading.Thread.Sleep (max numClients 2000)
        printfn "\n\n[The simulation setup has done!]"
        printfn "\n[Press any key to start repeated simulation flow]\n\n"
        Console.ReadLine() |> ignore

        printfn "----------------------------- Repeated Simulation ----------------------------"
        printfn " maxCycle: %d" maxCycle
        printfn "------------------------------------------------------------------------------"
        
        globalTimer.Start()
        let timer = new Timers.Timer(1000.)
        let event = Async.AwaitEvent (timer.Elapsed) |> Async.Ignore
        let connections = Array.create (numClients+1) false
        let mutable cycle = 0
        timer.Start()
        while cycle < maxCycle do
            printfn "----------------------------- CYCLE %d ------------------------------------" cycle
            cycle <- cycle + 1
            Async.RunSynchronously event

            (* randomly select some clients connected *)
            let connecting = (getConnectedID connections)
            let numToDisonnect = ((float) connecting.Length) * 0.3 |> int

            //printfn "toDisconnect %A" numToDisonnect

            arraySampler connecting numToDisonnect        
            |> List.map (fun clientID -> 
                async{
                    disconnect myClients.[clientID-1]
                    connections.[clientID] <- false
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
                     
            
            (* randomly select some clients connected *)
            let disconnecting = (getDisconnectedID connections)
            let numToConnect = ((float) disconnecting.Length) * 0.31 |> int
            
            arraySampler disconnecting numToConnect
            |> List.map (fun clientID -> 
                async{
                    connect myClients.[clientID-1]
                    connections.[clientID] <- true
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore

            // Console.ReadLine() |> ignore
            // printfn "numActive %d" numActive
            // printfn "length: %d toConnect: %A" toConnect.Length toConnect
            printfn "length:%d Connecting: %A" (getConnectedID connections).Length (getConnectedID connections)
            // Console.ReadLine() |> ignore

            (* randomly select some clients to send a tweet *)
            let rand = Random()
            arraySampler (getConnectedID connections) numSendTweet
            |> List.map (fun clientID -> 
                    async{
                        sendTweet myClients.[clientID-1] (tagSampler hashtags) (rand.Next(1, numClients))
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore 
             
            printfn "START RETWEEEEEET------------------------------------"
            (* randomly select some clients to retweet *)
            let rand = Random()
            arraySampler (getConnectedID connections) numRetweet
            |> List.map (fun clientID -> 
                    async{
                        retweet myClients.[clientID-1] (rand.Next(1,numClients))
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore 

            printfn "START HISTORYYYYYY------------------------------------"
            (* randomly select some clients to query history*)
            let rand = Random()
            let shuffledConnects = getConnectedID connections |> shuffle rand

            // Console.ReadLine() |> ignore
            // printfn "connected %d" shuffledConnects.Length
            // printfn "num:%d   %A" numQueryHistory shuffledConnects.[0 .. numQueryHistory-1]
            // printfn "%A" shuffledConnects.[numQueryHistory .. (numQueryHistory + numQueryByMention - 1)]
            // printfn "%A" shuffledConnects.[(numQueryHistory + numQueryByMention) .. (numQueryHistory + numQueryByMention + numQueryByTag - 1)]
            // Console.ReadLine() |> ignore

            shuffledConnects.[0 .. numQueryHistory-1]
            |> Array.map (fun clientID -> 
                    async{
                        queryHistory myClients.[clientID-1]
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore 
             
            (* randomly select some clients to query by mention*)
            let rand = Random()
            shuffledConnects.[numQueryHistory .. (numQueryHistory + numQueryByMention - 1)]
            |> Array.map (fun clientID -> 
                    async{
                        queryByMention myClients.[clientID-1] (rand.Next(1,numClients))
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore 

            (* randomly select some clients to query by tag*)
            shuffledConnects.[(numQueryHistory + numQueryByMention) .. (numQueryHistory + numQueryByMention + numQueryByTag - 1)]
            |> Array.map (fun clientID -> 
                    async{
                        queryByTag myClients.[clientID-1] (tagSampler hashtags)
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore 
          


        globalTimer.Stop()
        System.Threading.Thread.Sleep (max numClients 2000)
        printfn "\n\n[%i cycles of simulation has done!]" maxCycle
        printfn "Total time %A" globalTimer.Elapsed
        Console.ReadLine() |> ignore
        printfn "Total time %A" globalTimer.Elapsed
        

          
    with | :? IndexOutOfRangeException ->
            printfn "\n\n[Error] Wrong argument!!\n Plese use: \n1. dotnet run simulate\n2. dotnet run user\n\n"

         | :? FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 
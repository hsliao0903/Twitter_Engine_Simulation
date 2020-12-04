open System
open System.Diagnostics
open System.Security.Cryptography
open System.Globalization
open System.Collections.Generic
open System.Text
open Akka.Actor
open Akka.FSharp
open Message

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

type RegInfo = {
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

(* Data Collections to Store Client informations *)
(* userID, info of user registration *)
let regMap = new Dictionary<int, RegInfo>()
(* tweetID, info of the tweet *)
let tweetMap = new Dictionary<string, TweetInfo>()
(* userID, list of tweetID*)
let historyMap = new Dictionary<int, List<string>>()
(* tag, list of tweetID *)
let tagMap = new Dictionary<string, List<string>>()
(* userID, list of subsriber's userID *)
let pubMap = new Dictionary<int, List<int>>()
(* userID, list of publisher's userID *)
let subMap = new Dictionary<int, List<int>>()
(* userID, list of tweetID that mentions the user *)
let mentionMap = new Dictionary<int, List<string>>()


(* Actor System Configuration Settings (Server) Side) *)
let config =
    Configuration.parse
        @"akka {
            log-config-on-start = off
            log-dead-letters = off
            log-dead-letters-during-shutdown = off
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""

            }


            remote {
                log-received-messages = off
                log-sent-messages = off
                helios.tcp {
                    hostname = localhost
                    port = 9001
                }
            }
        }"

let system = System.create "TwitterEngine" config
let mutable debugMode = false
let mutable showStatusMode = 0
let globalTimer = Stopwatch()

(* Spawn Actors Helpter Function *)
let actorOfSink f = actorOf f

(* Helper Functions for access storage data structure *)

let isValidUser userID = 
    (regMap.ContainsKey(userID)) 

 

let regMapAdder (userID, info:RegInfo) =
    regMap.Add(userID, info)
let regMapAdderActorRef =
    actorOfSink regMapAdder |> spawn system "regMap-Adder"

let updateRegDB (newInfo:RegInfo) =
    let userID = newInfo.UserID
    if not (regMap.ContainsKey(userID)) then
        regMapAdderActorRef <! (userID, newInfo)
        //regMap.Add(userID, newInfo)
        "Success"
    else
        "Fail"

let updateHistoryDB userID tweetID =
    (* Check if it is the very first Tweet for a user, 
        if not, initialize it, if yes, add it to the list *)
    if userID >= 0 && (isValidUser userID) then        
        if not (historyMap.ContainsKey(userID)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            historyMap.Add(userID, newList)
        else
            (* No duplicate tweetID in one's history *)
            if not (historyMap.[userID].Contains(tweetID)) then
                (historyMap.[userID]).Add(tweetID)
    
let updateTagDB tag tweetID = 
    (* Update Tag database *)
    if tag <> "" && tag.[0] = '#' then
        if not (tagMap.ContainsKey(tag)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            tagMap.Add(tag, newList)
        else
            (tagMap.[tag]).Add(tweetID)

let updatePubSubDB publisherID subscriberID = 
    let mutable isFail = false
    (* Don't allow users to subscribe themselves *)
    if publisherID <> subscriberID && (isValidUser publisherID) && (isValidUser subscriberID) then
        (* pubMap:  Publisher : list of subscribers  *)
        if not (pubMap.ContainsKey(publisherID)) then
            let newList = new List<int>()
            newList.Add(subscriberID)
            pubMap.Add(publisherID, newList)
        else
            if not ((pubMap.[publisherID]).Contains(subscriberID)) then
                (pubMap.[publisherID]).Add(subscriberID)
            else
                isFail <- true

        (* pubMap:  Subscriber : list of Publishers *)
        if not (subMap.ContainsKey(subscriberID)) then
            let newList = new List<int>()
            newList.Add(publisherID)
            subMap.Add(subscriberID, newList)
        else
            if not ((subMap.[subscriberID]).Contains(publisherID)) then
                (subMap.[subscriberID]).Add(publisherID)
            else
                isFail <- true
        if isFail then
            "Fail"
        else
            "Success"
    else
        "Fail"

let updateMentionDB userID tweetID =
    (* Make suer the mentino exist some valid userID *)
    if userID >= 0 && (isValidUser userID) then
       if not (mentionMap.ContainsKey(userID)) then
            let newList = new List<string>()
            newList.Add(tweetID)
            mentionMap.Add(userID, newList)
        else
            (mentionMap.[userID]).Add(tweetID)

let updateTweetDB (newInfo:TweetInfo) =
    let tweetID = newInfo.TweetID
    let userID = newInfo.UserID
    let tag = newInfo.Tag
    let mention = newInfo.Mention
    
    (* Add the new Tweet info Tweet DB *)
    (* Assume that the tweetID is unique *)
    tweetMap.Add(tweetID, newInfo)
    (* Update the history DB for the user when send this Tweet *)
    updateHistoryDB userID tweetID
    (* Update the tag DB if this tweet has a tag *)
    updateTagDB tag tweetID
    
    (* If the user has mentioned any user, update his history*) 
    updateMentionDB mention tweetID
    updateHistoryDB mention tweetID

    (* If the user has subscribers update their historyDB *)
    if (pubMap.ContainsKey(userID)) then
        for subscriberID in (pubMap.[userID]) do
            (* If the tweet mentions it's author's subscriber, skip it to avoid duplicate tweetID in history *)
            //if mention <> subscriberID then
            updateHistoryDB subscriberID tweetID

(* userID: the user who would like to retweet *)
let updateRetweet userID (orgTweetInfo:TweetInfo) =
    let newTweetInfo:TweetInfo = {
        ReqType = orgTweetInfo.ReqType ;
        UserID  = orgTweetInfo.UserID ;
        TweetID = orgTweetInfo.TweetID ;
        Time = orgTweetInfo.Time ;
        Content = orgTweetInfo.Content ;
        Tag = orgTweetInfo.Tag ;
        Mention = orgTweetInfo.Mention ;
        RetweetTimes = (orgTweetInfo.RetweetTimes+1) ;
    }
    (* Increase the retweet times by one *)
    tweetMap.[orgTweetInfo.TweetID] <- newTweetInfo

    (* Add to the history *)
    updateHistoryDB userID (orgTweetInfo.TweetID)
   
    (* If the user has subscribers update their historyDB *)
    if (pubMap.ContainsKey(userID)) then
        for subscriberID in (pubMap.[userID]) do
            updateHistoryDB subscriberID (orgTweetInfo.TweetID)         

let assignTweetID (orgTweetInfo:TweetInfo) =
    let newTweetInfo:TweetInfo = {
        ReqType = orgTweetInfo.ReqType ;
        UserID  = orgTweetInfo.UserID ;
        //TweetID = totalTweets.ToString() ; // assign new tweetID according to total tweet counts
        TweetID = (tweetMap.Count + 1).ToString() ;
        Time = orgTweetInfo.Time ;
        Content = orgTweetInfo.Content ;
        Tag = orgTweetInfo.Tag ;
        Mention = orgTweetInfo.Mention ;
        RetweetTimes = orgTweetInfo.RetweetTimes ;
    }
    newTweetInfo

(* Actor Nodes *)
let serverActorNode (serverMailbox:Actor<string>) =
    let nodeName = serverMailbox.Self.Path.Name
    
    // if user successfully connected (login), add the user to a set
    let mutable msgProcessed = 0
    let mutable onlineUserSet = Set.empty
    let updateOnlineUserDB userID option = 
        let isConnected = onlineUserSet.Contains(userID)
        if option = "connect" && not isConnected then
            if isValidUser userID then
                onlineUserSet <- onlineUserSet.Add(userID)
                0
            else
                -1
        else if option = "disconnect" && isConnected then
            onlineUserSet <- onlineUserSet.Remove(userID)
            0
        else
            0
    (* Server status variables to count requests and tweets *)
    let mutable totalRequests = 0
    let mutable maxThrougput = 0
    
    let showServerStatus _ =
        totalRequests <- totalRequests + msgProcessed
        maxThrougput <- Math.Max(maxThrougput, msgProcessed)
        if showStatusMode = 0 then    
            printfn "\n---------- Server Status ---------"
            printfn "Total Processed Requests: %i" totalRequests
            printfn "Request Porcessed Last Second: %i" msgProcessed
            printfn "Max Server Throughput: %i" maxThrougput   //message processed per second
            printfn "Total Tweets in DB: %i" (tweetMap.Keys.Count)
            printfn "Total Registered Users: %i" (regMap.Keys.Count)
            printfn "Online Users: %i" (onlineUserSet.Count)
            printfn "----------------------------------\n"
        msgProcessed <- 0

    let timer = new Timers.Timer(1000.0)
    timer.Elapsed.Add(showServerStatus)
    timer.Start()


    (* Server Actor Function *)
    let rec loop() = actor {
        let! (message: string) = serverMailbox.Receive()
        let  sender = serverMailbox.Sender()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()
        let  userID = jsonMsg?UserID.AsInteger()

        match reqType with
            | "Register" ->
                (* Save the register information into data strucute *)
                (* Check if the userID has already registered before *)
                let regMsg = (Json.deserialize<RegInfo> message)
                if debugMode then
                    printfn "[%s] Received Register Request from User%s" nodeName (sender.Path.Name)
                
                let status = updateRegDB regMsg
                let reply:ReplyInfo = { 
                    ReqType = "Reply" ;
                    Type = reqType ;
                    Status =  status ;
                    Desc =  Some (regMsg.UserID.ToString()) ;
                }
                
                (* Reply for the register satus *)
                sender <!  (Json.serialize reply)

            | "SendTweet" ->
                //totalTweets <- totalTweets + 1
                let orgtweetInfo = (Json.deserialize<TweetInfo> message)
                let tweetInfo = assignTweetID orgtweetInfo
                if debugMode then
                    printfn "[%s] Received a Tweet reqeust from User%s" nodeName (sender.Path.Name)
                    printfn "%A" tweetInfo
                (* Store the informations for this tweet *)
                (* Check if the userID has already registered? if not, don't accept this Tweet *)
                if (isValidUser tweetInfo.UserID) then
                    updateTweetDB tweetInfo

                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Success" ;
                        Desc =  Some "Successfully send a Tweet to Server" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Failed" ;
                        Desc =  Some "The user should be registered before sending a Tweet" ;
                    }
                    sender <! (Json.serialize reply)

            | "Retweet" ->
                let retweetID = jsonMsg?RetweetID.AsString()
                let tUserID = jsonMsg?TargetUserID.AsInteger()
                let mutable isFail = false
                
                (* user might assign a specific retweetID or empty string *)
                if retweetID = "" then
                    (* make sure the target user has at least one tweet in his history *)
                    if (isValidUser tUserID) && historyMap.ContainsKey(tUserID) && historyMap.[tUserID].Count > 0 then
                        (* random pick one tweet from the target user's history *)
                        let rnd = Random()
                        let numTweet = historyMap.[tUserID].Count
                        let rndIdx = rnd.Next(numTweet)
                        let targetReTweetID = historyMap.[tUserID].[rndIdx]
                        let retweetInfo = tweetMap.[targetReTweetID]
                        //let keyArray = Array.create (totalNum) ""
                        //tweetMap.Keys.CopyTo(keyArray, 0)

                        (* check if the author is the one who send retweet request *)
                        if (retweetInfo.UserID <> userID) then
                            updateRetweet userID retweetInfo
                        else
                            isFail <- true
                    else
                        isFail <- true
                else
                    (* Check if it is a valid retweet ID in tweetDB *)
                    if tweetMap.ContainsKey(retweetID) then
                        (* check if the author is the one who send retweet request *)
                        if (tweetMap.[retweetID].UserID) <> userID then
                            updateRetweet userID (tweetMap.[retweetID])
                        else
                            isFail <- true
                    else
                        isFail <- true

                (* Deal with reply message *)
                if isFail then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "SendTweet" ;
                        Status =  "Failed" ;
                        Desc =  Some "The random choose of retweet fails (same author situation)" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "SendTweet" ;
                        Status =  "Success" ;
                        Desc =  Some "Successfully retweet the Tweet!" ;
                    }
                    sender <! (Json.serialize reply)
                //return! loop()
            | "Subscribe" ->
                let status = updatePubSubDB (jsonMsg?PublisherID.AsInteger()) (jsonMsg?UserID.AsInteger())
                let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  status ;
                        Desc =  None ;
                }
                sender <! (Json.serialize reply)

            | "Connect" ->
                let userID = jsonMsg?UserID.AsInteger()
                (* Only allow user to query after successfully connected (login) and registered *)
                let ret = (updateOnlineUserDB userID "connect")
                if ret < 0 then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Fail" ;
                        Desc =  Some "Please register first" ;
                    }
                    sender <! (Json.serialize reply)
                else 
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Success" ;
                        Desc =  Some (userID.ToString()) ;
                    }
                    sender <! (Json.serialize reply)
                
            | "Disconnect" ->
                
                (* if disconnected, user cannot query or send tweet *)
                (updateOnlineUserDB userID "disconnect") |> ignore
                let (reply:ReplyInfo) = { 
                    ReqType = "Reply" ;
                    Type = reqType ;
                    Status =  "Success" ;
                    Desc =   Some (userID.ToString()) ;
                }
                sender <! (Json.serialize reply)

            | "QueryHistory" ->
                    
                (* No any Tweet in history *)
                if not (historyMap.ContainsKey(userID)) then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "NoTweet" ;
                        Desc =  Some "Query done, there is no any Tweet to show yet" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    (* send back all the tweets *)
                    let mutable tweetCount = 0
                    for tweetID in (historyMap.[userID]) do
                        tweetCount <- tweetCount + 1
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        sender <! (Json.serialize tweetReply)

                    (* After sending ball all the history tweet, reply to sender *)
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = reqType ;
                        Status =  "Success" ;
                        Desc =  Some "Query history Tweets done" ;
                    }
                    sender <! (Json.serialize reply)       

            | "QueryMention" ->
                (* No any Tweet that mentioned User *)
                if not (mentionMap.ContainsKey(userID)) then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "QueryHistory" ;
                        Status =  "NoTweet" ;
                        Desc =  Some "Query done, no any mentioned Tweet to show yet" ;
                    }
                    sender <! (Json.serialize reply)
                else
                    (* send back all mentioned tweets *)
                    let mutable tweetCount = 0
                    for tweetID in (mentionMap.[userID]) do
                        tweetCount <- tweetCount + 1
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        sender <! (Json.serialize tweetReply)

                    (* After sending ball all the history tweet, reply to sender *)
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "QueryHistory" ;
                        Status =  "Success" ;
                        Desc =  Some "Query mentioned Tweets done" ;
                    }
                    sender <! (Json.serialize reply)       

            | "QueryTag" ->
                let tag = jsonMsg?Tag.AsString()
                (* No any Tweet that mentioned User *)
                if not (tagMap.ContainsKey(tag)) then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "QueryHistory" ;
                        Status =  "NoTweet" ;
                        Desc =  Some ("Query done, no any Tweet belongs to " + tag)  ;
                    }
                    sender <! (Json.serialize reply)
                else
                    (* send back all mentioned tweets *)
                    let mutable tweetCount = 0
                    for tweetID in (tagMap.[tag]) do
                        tweetCount <- tweetCount + 1
                        let tweetReply:TweetReply = {
                            ReqType = "Reply" ;
                            Type = "ShowTweet" ;
                            Status = tweetCount ;
                            TweetInfo = tweetMap.[tweetID] ;
                        }
                        sender <! (Json.serialize tweetReply)

                    (* After sending back all the history tweet, reply to sender *)
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "QueryHistory" ;
                        Status =  "Success" ;
                        Desc =  Some ("Query Tweets with "+tag+ " done") ;
                    }
                    sender <! (Json.serialize reply)       

            | "QuerySubscribe" ->
                
                (* the user doesn't have any publisher subscripver information *)
                if not (subMap.ContainsKey(userID)) && not (pubMap.ContainsKey(userID))then
                    let (reply:ReplyInfo) = { 
                        ReqType = "Reply" ;
                        Type = "QueryHistory" ;
                        Status =  "NoTweet" ;
                        Desc =  Some ("Query done, the user has no any subscribers or subscribes to others ")  ;
                    }
                    sender <! (Json.serialize reply)
                else if (subMap.ContainsKey(userID)) && not (pubMap.ContainsKey(userID))then
                    let subReply:SubReply = {
                        ReqType = "Reply" ;
                        Type = "ShowSub" ;
                        TargetUserID = userID ;
                        Subscriber = subMap.[userID].ToArray() ;
                        Publisher = [||] ;
                    }
                    sender <! (Json.serialize subReply)    
                else if not (subMap.ContainsKey(userID)) && (pubMap.ContainsKey(userID))then
                    let subReply:SubReply = {
                        ReqType = "Reply" ;
                        Type = "ShowSub" ;
                        TargetUserID = userID ;
                        Subscriber = [||] ;
                        Publisher = pubMap.[userID].ToArray() ;
                    }
                    sender <! (Json.serialize subReply)
                else 
                    let subReply:SubReply = {
                        ReqType = "Reply" ;
                        Type = "ShowSub" ;
                        TargetUserID = userID ;
                        Subscriber = subMap.[userID].ToArray() ;
                        Publisher = pubMap.[userID].ToArray() ;
                    }
                    sender <! (Json.serialize subReply)                    

            | _ ->
                printfn "client \"%s\" received unknown message \"%s\"" nodeName reqType
                Environment.Exit 1

        totalRequests <- totalRequests + 1
        msgProcessed <- msgProcessed + 1
        return! loop()
    }
    loop()

let mutable a = 1
[<EntryPoint>]
let main argv =
    try
        (* Check if the parameter is "debug" or not, if yes, set the Server to debug mode, to get more ouptputs of requests *)
        if argv.Length <> 0 then
            debugMode <- 
                match (argv.[0]) with
                | "debug" -> true
                | _ -> false
       
        
        let serverActor = spawn system "TWServer" serverActorNode
        globalTimer.Start()
        // --------------------------- DB collections ---------------------------
        // regMap tweetMap historyMap tagMap mentionMap subMap pubMap
        //-----------------------------------------------------------------------
        let getTopID (subpubMap:Dictionary<int, List<int>>) = 
            let mutable maxCount = 0
            let mutable topID = -1 
            for entry in subpubMap do
                if entry.Value.Count > maxCount then
                    maxCount <- (entry.Value.Count)
                    topID <- entry.Key
            topID   
        let getTopTag (tagDB:Dictionary<string, List<string>>) =
            let mutable maxCount = 0
            let mutable topTag = ""
            for entry in tagDB do
                if entry.Value.Count > maxCount then
                    maxCount <- (entry.Value.Count)
                    topTag <- entry.Key
            topTag       
        let getTopMention (mentionDB:Dictionary<int, List<string>>) =
            let mutable maxCount = 0
            let mutable topMen = -1
            for entry in mentionMap do
                if entry.Value.Count > maxCount then
                    maxCount <- (entry.Value.Count)
                    topMen <- entry.Key
            topMen
        let getTopRetweet (tweetDB:Dictionary<string, TweetInfo>) =
            let mutable maxCount = 0
            let mutable topRetweet = ""
            for entry in tweetMap do
                if entry.Value.RetweetTimes > maxCount then
                    maxCount <- (entry.Value.RetweetTimes)
                    topRetweet <- entry.Key
            topRetweet

        let showDBStatus _ =
            if showStatusMode = 1 then
                let topPublisher = getTopID pubMap
                let topSubscriber = getTopID subMap
                let topTag = getTopTag tagMap
                let topMention = getTopMention mentionMap
                let topRetweet = getTopRetweet tweetMap
                        
                printfn "\n---------- DB Status ---------------------"
                printfn "Total Registered Users: %i" (regMap.Keys.Count)
                printfn "Total Tweets in DB: %i" (tweetMap.Keys.Count)
                if topRetweet <> "" then
                    printfn "Top retweeted Tweet: %s (%i times)" topRetweet (tweetMap.[topRetweet].RetweetTimes)
                if topTag <> "" then
                    printfn "Total different kinds of Tags: %i" (tagMap.Keys.Count)
                    printfn "Top used Tag: %s (%i times)" topTag (tagMap.[topTag].Count)
                if topMention >= 0 then
                    printfn "Top mentioned User: User%i (%i times)" topMention (mentionMap.[topMention].Count)
                if topPublisher >= 0 then
                    printfn "Top Publisher: %i (%i subscribers)" topPublisher (pubMap.[topPublisher].Count)
                if topSubscriber >= 0 then
                    printfn "Top Subscriber: %i (%i subscribes)" topSubscriber (subMap.[topSubscriber].Count)
                printfn "------------------------------------------\n"
        
        let timer2 = new Timers.Timer(1000.0)
        timer2.Elapsed.Add(showDBStatus)
        timer2.Start()
        
        (* Let user press any key to change the mode for showing status *)
        (* showDB -> showServer -> showDB  *)
        printfn "\n\n-------------------------------------------"
        printfn "Press any key to switch the ouput status..."
        printfn "Enter \"exit\" to terminate this program..."
        printfn "-------------------------------------------\n"
        while true do
            let userInputStr = Console.ReadLine()
            match userInputStr with
            | "exit" | "ex" | "quit" ->
                globalTimer.Stop()
                printfn "\n\nTwitter Engine Terminated...\nTotal running time: %A\n" (globalTimer.Elapsed)
                Environment.Exit 1
            | _ ->
                if showStatusMode = 0 then
                    showStatusMode <- 1
                else
                    showStatusMode <- 0

    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code

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
    Mention : string
}

(* Data Structure to Store Client informations *)

(* userID, info of user registration *)
let regMap = new Dictionary<int, RegInfo>()
(* tweetID, info of the tweet *)
let tweetMap = new Dictionary<string, TweetInfo>()
(* userID, list of tweetID*)
let historyMap = new Dictionary<int, List<string>>()
(* tag, list of tweetID *)
let tagMap = new Dictionary<string, List<string>>()
(* userID, list of subsriber's userID *)
let subMap = new Dictionary<int, List<int>>()



(* Actor System Configuration Settings (Server) Side) *)
let config =
    Configuration.parse
        @"akka {
            actor.provider = remote
            remote.helios.tcp {
                hostname = localhost
                port = 9001
            }
        }"

let system = System.create "TwitterEngine" config


(* Helper Functions for access storage data structure *)
let updateRegDB (newInfo:RegInfo) =
    let userID = newInfo.UserID
    if not (regMap.ContainsKey(userID)) then
        regMap.Add(userID, newInfo)
        "Success"
    else
        "Fail"

let updateHistoryDB userID tweetID =
    (* Check if it is the very first Tweet for a user, 
        if not, initialize it, if yes, add it to the list *)
    if not (historyMap.ContainsKey(userID)) then
        let newList = new List<string>()
        newList.Add(tweetID)
        historyMap.Add(userID, newList)
    else
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

let updateSubDB userID subscriberID = 
    (* Don't allow users to subscribe themselves *)
    if userID <> subscriberID then
        if not (subMap.ContainsKey(userID)) then
            let newList = new List<int>()
            newList.Add(subscriberID)
            subMap.Add(userID, newList)
        else
            (subMap.[userID]).Add(subscriberID)

let updateTweetDB (newInfo:TweetInfo) =
    let tweetID = newInfo.TweetID
    let userID = newInfo.UserID
    let tag = newInfo.Tag
    
    (* Add the new Tweet info Tweet DB *)
    (* Assume that the tweetID is unique *)
    tweetMap.Add(tweetID, newInfo)
    (* Update the history DB for the user when send this Tweet *)
    updateHistoryDB userID tweetID
    (* Update the tag DB if this tweet has a tag *)
    updateTagDB tag tweetID
    
    (* If the user has subscribers update their historyDB *)
    if (subMap.ContainsKey(userID)) then
        for subscriberID in (subMap.[userID]) do
            updateHistoryDB subscriberID tweetID
    
    (* If the user has mentioned any user, update his history, but if it is already a subscriber, skip it *)
        
        

(* Actor Nodes *)
let serverActorNode (serverMailbox:Actor<string>) =
    let nodeName = serverMailbox.Self.Path.Name
    let mutable nodeCount = 0
    let mutable networkNodeSet = Set.empty
    
    let rec loop() = actor {
        let! (message: string) = serverMailbox.Receive()
        let  sender = serverMailbox.Sender()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()
        printfn "\n[%s] Receive message %A\n" nodeName message
        match reqType with
            | "Register" ->
                (* Save the register information into data strucute *)
                let regMsg = (Json.deserialize<RegInfo> message)
                printfn "[%s] Received Register Request: \n%A" nodeName message
                
                let status = updateRegDB regMsg
                let reply:ReplyInfo = { 
                    ReqType = "Reply" ;
                    Type = reqType ;
                    Status =  status ;
                    Desc =  None }
                
                (* Reply for the register satus *)
                sender <!  (Json.serialize reply)

                printfn "[%s] register map: \n%A\n" nodeName regMap
                for entry in regMap do
                    printfn "Test %s" (entry.Value.PublicKey |> Option.defaultValue "")
                    //printfn "Test %s" entry.Key



                //let regReply:RegInfo = { ReqType = (regMsg.ReqType+"Reply") ; UserID = regMsg.UserID ; UserName = regMsg.UserName ; PublicKey = regMsg.PublicKey }
                
                
                return! loop()
            | "SendTweet" ->
                let tweetInfo = (Json.deserialize<TweetInfo> message)
                printfn "[%s] Received a Tweet from User%s" nodeName (sender.Path.Name)
                

                return! loop()
            | "QueryHistoryTweet" ->
                return! loop()
            | "Connect" ->
                return! loop()
            | "Disconnect" ->
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
        printfn "%A" subMap
        updateSubDB 1 2
        printfn "%i %A" (1) (subMap.[1])
        updateSubDB 1 31
        updateSubDB 1 1
        updateSubDB 1 3
        updateSubDB 2 1
        updateSubDB 2 31
        printfn "%i %A" (1) (subMap.[1])
        subMap.[1].Remove(4) |> ignore
        printfn "%i %A" (1) (subMap.[1])

        updateTagDB "a" "adf"
        updateTagDB "#a" "adfad"
        updateTagDB "#ab" "adf"
        //printfn "%s %A" ("#e") (tagMap.["#e"])
        printfn "test ---------"

        let serverActor = spawn system "TWServer" serverActorNode
        Console.ReadLine() |> ignore
    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code

module ActorOFDB

open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Json
open System
open System.Text
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open System.Diagnostics
open TwitterServerCollections


type QueryActorMsg =
   | QueryHistory of string * IActorRef * string[]
   | QueryTag of string * IActorRef * string[]
   | QueryMention of string * IActorRef * string[]

let queryActorNode (mailbox:Actor<QueryActorMsg>) =
    let nodeName = "QueryActor " + mailbox.Self.Path.Name
    let rec loop() = actor {
        let! (message: QueryActorMsg) = mailbox.Receive()
       
        match message with
            | QueryHistory (json,sender, tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let  userID = jsonMsg?UserID.AsInteger()
                
                (* send back all the tweets *)
                let mutable tweetCount = 0
                for tweetID in (tweetIDarray) do
                    if tweetMap.ContainsKey(tweetID) then
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
                    Desc =  Some "Query history Tweets done" ;
                }
                sender <! (Json.serialize reply)  

            | QueryTag (json, sender, tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let tag = jsonMsg?Tag.AsString()
                (* send back all mentioned tweets *)
                let mutable tweetCount = 0
                for tweetID in tweetIDarray do
                    if tweetMap.ContainsKey(tweetID) then
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

            | QueryMention (json, sender,tweetIDarray) ->
                let  jsonMsg = JsonValue.Parse(json)
                //printfn "[%s] %A" nodeName json
                let  userID = jsonMsg?UserID.AsInteger()
                let  reqType = jsonMsg?ReqType.AsString()
                (* send back all mentioned tweets *)
                let mutable tweetCount = 0
                for tweetID in (tweetIDarray) do
                    if tweetMap.ContainsKey(tweetID) then
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
        return! loop()
    }
    loop()
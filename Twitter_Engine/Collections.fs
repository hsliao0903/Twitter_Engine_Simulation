module TwitterServerCollections

open System
open System.Collections.Generic

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

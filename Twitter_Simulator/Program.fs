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
    Mention : string
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
    //printfn "[%s] %s" nodeName (nodeMailbox.Self.Path.ToString())
    //printfn "[%s]\n %A\n" nodeName proxMetric
    let mutable nodeCount = 0
    let mutable networkNodeSet = Set.empty
    
    let rec loop() = actor {
        let! (message: string) = clientMailbox.Receive()
        let  jsonMsg = JsonValue.Parse(message)
        let  reqType = jsonMsg?ReqType.AsString()

        match reqType with
            | "Register" ->

                (* Example JSON message for register API *)
                let regMsg:RegJson = { 
                    ReqType = "Register"; 
                    UserID = nodeID ; 
                    UserName = nodeName ; 
                    PublicKey = Some (nodeName+"Key") ;
                }
                serverNode <! (Json.serialize regMsg)
                return! loop()
            | "SendTweet" ->
                (* tmp Tweet generated *)
                let tmpTweet:TweetInfo = {
                    ReqType = "SendTweet" ;
                    UserID  = nodeID ;
                    TweetID = (globalTimer.Elapsed.ToString()) ;
                    Time = (DateTime.Now) ;
                    Content = "I am User" + (nodeID.ToString()) + ", I sends a Tweet!" ;
                    Tag = "#Intro" ;
                    Mention = "User2" ;
                }
                serverNode <! (Json.serialize tmpTweet)
                return! loop()
            | "Reply" ->
                let replyType = jsonMsg?Type.AsString()
                match replyType with
                    | "Register" ->
                        let status = jsonMsg?Status.AsString()
                        if status = "Success" then
                            printfn "[%s] Register done!" nodeName
                        else
                            printfn "[%s] Register failed!" nodeName
                        ()
                    | _ ->
                        printfn "[%s] Unhandled Reply Message" nodeName
                        ()

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
        globalTimer.Start()
        printfn "%A@%A" globalTimer.Elapsed globalTimer.GetHashCode
        printfn "%A" globalTimer.Elapsed
        let tmpTweet:TweetInfo = {
            ReqType = "SendTweet" ;
            UserID  = 9527 ;
            //TweetID = DateTime.Now.ToString() + "9527" ;
            TweetID = globalTimer.Elapsed.ToString() ;
            Time = DateTime.Now ;
            Content = "hehehehet" ;
            Tag = "#tagg" ;
            Mention = "User2" ;
        }
        let json = Json.serialize tmpTweet
        printfn "oewrj:\n %s\n" json
        let aaa:TweetInfo = Json.deserialize<TweetInfo> json
        printfn "%A" aaa

        let clientActor = spawn system "1" clientActorNode
        let clientActor2 = spawn system "2" clientActorNode
        let triggerReg = """{"ReqType":"Register"}"""
        let triggerSend = """{"ReqType":"SendTweet"}"""
        clientActor <! triggerReg
        clientActor2 <! triggerReg
        clientActor2 <! triggerReg
        clientActor <! triggerReg
        clientActor2 <! triggerReg
        clientActor <! triggerSend
        clientActor2 <! triggerSend
        Console.ReadLine() |> ignore
    with | :? IndexOutOfRangeException ->
            printfn "\n[Main] Incorrect Inputs or IndexOutOfRangeException!\n"

         | :?  FormatException ->
            printfn "\n[Main] FormatException!\n"


    0 // return an integer exit code

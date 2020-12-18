-----------------------------------------------------------------
Gaol of this project
-----------------------------------------------------------------
Twitter like engine to support some of the Twitter APIs, such as "Send a Tweet", "Retweet", "Subscribe" or "Querying Tweets" etc.
Simualator prgram that simulates lots of users simultaneously accessing the Twitter like engine.


-----------------------------------------------------------------
Libraries/Programming labguage
-----------------------------------------------------------------
.NET 5.0
F#
Akka.FSharp
AKKa.Remote
FSharp.JSON


-----------------------------------------------------------------
Brief usage
-----------------------------------------------------------------
1.
First, run the program in "Twitter_Engine" directory as a server:
cd Twitter_Engine
dotnet run

2. 
Run the program in "Twitter_Simulator" directory to start the simulation with default parameters:
cd Twetter_Simulator
dotnet run debug

After doing this, the simulator will start to send different kind of API requests to the server on port 9001
The simulator has 1000 clients, and it will keep simulating infinitely 
From the server side, there will be server status to show, if pressing any key, it will switch to show the DB status 

3.
Run the simulator with different parameters:
cd Twetter_Simulator
dotnet run simulate

It will prompt the user to input different kind of parameters
We have tested successfully using 10000+ clients for simulation

4.
Run the simulator in USER mode, which is a terminal for you to send API requests to server manually:
(It supports multiple simulator programs running in the same time)
cd Twetter_Simulator
dotnet run user


In the user mode, we have to connect to the server with a user account first, or register a new account. 
After successfully connected to the server, we could use different kind of commands to send requests.
We could disconnect to the server, and reconnect the server with different accounts

-----------------------------------------------------------------
Author:
Please let me know if there is any question, thanks!
Hsiang-Yuan Alex Liao    hs.liao@ufl.edu
-----------------------------------------------------------------

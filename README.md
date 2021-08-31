# Rovio Code Challenge
For Server Developer Position

## Introduction
Thank you for taking the time to review my submission. 

## Get the project up and running

### Prerequisites
- [.NET 5.0 SDK](https://dotnet.microsoft.com/download/visual-studio-sdks)
- Optional: [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension for VS Code.

### Build

Open the `Rovio.MatchMaking` directory in a terminal and run:

```powershell
dotnet build
```

This will restore the necessary NuGet packages and build the project.

### Running the project
Run the application in development mode to allow access to [Swagger UI](https://localhost:5001/swagger).

```powershell
dotnet run environment=develop
```

Use the `--urls` argument to bind the application to different ports.

```powershell
dotnet run environment=develop --urls="http://localhost:80;https://localhost:443"
```

Omitting `environment=develop` or changing it to `environment=production` will run the API in production mode without Swagger.

## Matchmaking Logic
### Terms

**Ticket:** Users waiting to join a game are represented by a ticket. The ticket contains the information need to perform matchmaking (e.g. latency, wait time and player rank) and information needed to connect to the user (e.g. IP address). For this implementation we only track the latency, time in queue and the ticket's identifier.

**Lobby:** A lobby represents a grouping of tickets for matchmaking. The most common boundry for a lobby will be the game being played, but additional boundaries such as region, client version, latency band and language may also apply. For this implementation the matchmaking service does not concern itself with determining which lobby a given ticket should join. Games are free to define any number of lobby definitions, provided they assign a fixed unique ID to each lobby definition.

**Session:** A session represents a single match made up of one or more tickets.

### Algorithm
The matchmaking service will queue tickets based on their latency and the time the ticket spent in the queue. In an ideal world, players will spend more time in-game than waiting in the queue. Therefore matchmaking will based on latency to the server will result in the best player experience. However, a ticket with poor latency in a large enough lobby would potentially sit in the queue forever as better candidates cycle in and out of the lobby. To account for wait time we can adjust a ticket's latency by its time spent in the lobby to make it a more desirable candidate as time passes.

It is assumed that network latency for a lobby will follow a right skewed normal distribution.

![Latency Distribution](/images/latency-distribution.png)

Latency to the server is affected the quality of the connection and the distance to the server.

We can mitigate distance to the server by deploying lobbies in regions closer to large concentrations of players.

The quality of connection between players can vary based on a number of factors, but will likely be determined by the ubiquity and affordability of a given connection type (fibre/ADSL/LTE/etc).

Therefore, for a given playber base with appropriately located servers, we will see a latency band formed by a combination of average distance to the server and the most common connection quality in the region that contains the majority of the player base. In the event that we have 2 distinct high densitity latency bands for a given server location, we can create seperate lobbies for each latency band to even out matchmaking.

This high population density band of tickets will form the foundation of our matchmaking. When creating a session the service will first calculate the mean latency for all the tickets in the lobby. It will then filter out tickets that fall outside 1 standard deviation from the mean. This will ensure an acceptable differnce in latency between players and take some load off the server when ordering the results. Finally we order tickets according to the absolute value of its deviation from mean, ascending, and select the top N tickets according to our session parameters.

To account for for wait time in the queue we will adjust the deviation of the ticket down towards mean to move ticket up in the matchmaking queue for a session. The service uses the cosine function to produce a factor (clamped at 0 for values exceeding the maximum wait time) that will be used to adjust the ticket's deviation down during filtering and ordering. The cosine function has a desirable characteristic in that is initially slow in its descent to zero, picking up speed as time goes on, but never surpassing a linear descent. This gives us a longer window period to match based on the a value closer to the ticket's true latency, while still ensuring that we will hit 0 by the end of the maximum wait time.

![Cosine](/images/cosine.png)

The maximum wait time will not guarrantee a place in the session, but a ticket will be at the top of the list for subsequent sessions once that time has passed.

## Architecture

### API
A full listing of all the endpoints can be found at the [Swagger endpoint](https://localhost:5001/swagger) when the service is run in a development environment (see [Running the project](#running-the-project)).

---
**POST:** /Lobbies/**{lobbyId}**/tickets

Queue a ticket in the specified lobby.

---

**PUT:** /Lobbies/**{lobbyId}**/tickets/**{ticketId}**

Updates a ticket's information.

---

**DELETE:** /Lobbies/**{lobbyId}**/tickets/**{ticketId}**

Removes a ticket from the lobby.

---

**POST:** /Lobbies/**{lobbyId}**/sessions

Creates a new session from the tickets available in the lobby.

---

**GET:** /Lobbies/**{lobbyId}**/sessions/**{sessionId}**

Fetch additional tickets that have been assigned to the session since its creation.

---

**DELETE:** /Lobbies/**{lobbyId}**/sessions/**{sessionId}**

Close the open session, returning any pendind tickets to the lobby.

---

### Actors
The service uses [actors](https://en.wikipedia.org/wiki/Actor_model) to model the lobby and the open sessions.

The lobby actor is fairly simple. An independent copy of the lobby actor is created based on the lobbyId we receive as part of the request to queue a ticket. The lobby actor is responsible for storing, updating and tracking individual tickets and matchmaking a new session.

The session actor represents an incomplete session where users can join the in-progress session. The lobby actor will evaluate incoming tickets against open sessions and queue the ticket on the open session instead if there is a match. Once the session is filled or game server decides to close the session via the service endpoint, the session will shut itself down and inform the lobby actor to no longer route tickets to it.

### Scaling
Currently the service requires that a given lobby ID must be routed the same instance of the service. In order to scale horizontally the service will require a load balancer that can route traffic based on the lobby ID in the route. We can improve on this behaviour by setting up an [Akka.NET cluster](https://getakka.net/articles/clustering/cluster-overview.html). This will allow any instance of the matchmaking service to act as an entry point for messages into the actor system as well as providing automated load balancing of the actors between available nodes. Unfortunately, the implementation of a cluster would have taken more time than I had available for this excercise.

## How to deploy the service to a production environment

Unfortunately I have no cloud hosting experience to speak of so I can't talk to specifics. From experience, the ideal would be to encapsulate the service in an API gateway, seperating the application logic from security concerns.

Instead of entrusting developers to perfectly secure their services and keep them up to date with the latest changes to security best practice, we can leverage off the continues improvements and hardening that the cloud based gateway offers.

From what I could find online, it looks like it is possible to deploy the service using a combination of AWS Lambda and AWS API Gateway, but I did not have the time test and investigate that option.

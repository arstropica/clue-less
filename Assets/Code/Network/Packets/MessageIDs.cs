public enum MessageIDs
{
    /*  splitting up inbound and outbound packet IDs as error-proofing
        if we specify the packet is going to server/client with the ID,
        it shouldn't break the game if a packet goes to the wrong endpoint */
    #region Client to Server

    Connect_ToServer,                   // sent to server when player wants to connect
    Disconnect_ToServer,                // sent to server when player is leaving
    Ready_ToServer,                     // sent to server when player is ready to start
    Chat_ToServer,                      // sent to server when player sends a chat message
    GameStart_ToServer,                 // sent to server when player is ready to start the game
    CharUpdate_ToServer,                // sent to server when player changes character/color
    MoveToRoom_ToServer,                // sent to server when player moves to a new room
    Guess_ToServer,                     // sent to server when player makes a guess
    Reveal_ToServer,                    // sent to server when player responds to guess with a clue
    TurnDone_ToServer,                  // sent to server when player is done with their turn

    #endregion
    

    
    #region Server to Client

    Connect_ToClient,                   // sent to 1 client when adding them to server
    ConnectForward_ToClient,            // sent to all other clients when a new player joins
    Disconnect_ToClient,                // sent to 1 client if forcibly removed from server
    ClientDrop_ToClient,                // sent to all clients when a client leaves the server
    Ready_ToClient,                     // sent to all other clients when a player is ready to start 
    Chat_ToClient,                      // sent to all clients when a player sends a chat message
    GameStart_ToClient,                 // sent to all clients when host starts game 
    Turn_ToClient,                      // sent to all clients when an action occurs that changes the turn or action
    CharUpdate_ToClient,                // sent to all clients when a player changes character/color
    MoveToRoom_ToClient,                // sent to all clients when a player moves to a new room
    Guess_ToClient,                     // sent to all clients when player makes a guess
    Reveal_ToClient,                    // sent to 1 client when a player responds to guess with a clue
    WinLose_ToClient,                   // sent to all clients when a player wins or loses the game

    #endregion
}
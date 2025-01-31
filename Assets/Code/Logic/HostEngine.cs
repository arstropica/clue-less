using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostEngine : BaseEngine
{
    public ServerNetworkInterface netInterface;

    private Dictionary<int, PlayerState> playersNeedToReveal = new Dictionary<int, PlayerState>();

    public override bool StartGame()
    {
        base.StartGame();
        foreach (ClueCard card in deck.GetCorrectCards())
        {
            Log("Correct card: " + card.cardName);
        }
        // send cards to players
        int cardsPerPlayer = Mathf.CeilToInt((float)(deck.TotalCards - 3) / (float)state.numPlayers);
        int turn = UnityEngine.Random.Range(0, state.numPlayers);
        foreach (PlayerState player in players.Values)
        {
            player.cards = deck.GetCards(cardsPerPlayer);

            RoomType initialRoom = board.GetStartingRoom(player.character);
            MoveToRoomPacket roomPkt = new MoveToRoomPacket(false, player.playerID, initialRoom, false);
            netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, roomPkt);
        }
        foreach (PlayerState player in players.Values)
        {
            if (CardDeck.GetCluesFromCards(player.cards, out List<CharacterType> characters, out List<WeaponType> weapons, out List<RoomType> rooms))
            {
                GameStartPacket gamePkt = new GameStartPacket(false, player.playerID, turn, characters, weapons, rooms);
                netInterface.SendMessage(player.playerID, gamePkt);
            }
            else
            {
                Log(String.Format("Error parsing {0} clues!!!", player.playerName));
            }
        }
        SetTurn(turn, TurnAction.MoveRoom);
        return true;        
    }

    public override void SetPlayerReady(int playerID, bool isReady)
    {
        if (players.TryGetValue(playerID, out PlayerState selectedPlayer))
        {
            selectedPlayer.isReady = isReady;
            ReadyPacket pkt = new ReadyPacket(false, playerID, isReady);
            netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, pkt);
            Log(String.Format("Player{0} is now {1}", playerID, isReady ? "ready" : "not ready"));
        }

        // check if all players are ready- if so, start the game
        foreach (PlayerState player in players.Values)
        {
            if (!player.isReady)
            {
                return;
            }
        }
        StartGame();
    }

    public override void SetTurn(int turn, TurnAction action)
    {
        base.SetTurn(turn, action);
        TurnPacket pkt = new TurnPacket(turn, action);
        netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, pkt);
    }

    public bool AddPlayer(int playerID, string name, out CharacterType assignedCharacter)
    {
        if (isGameStarted)
        {
            Log(string.Format("Can't add player {0}, game already started", playerID));
            assignedCharacter = CharacterType.Mustard;
            return false;
        }
        // check if name already taken
        foreach (PlayerState player in players.Values)
        {
            if (player.playerName == name)
            {
                assignedCharacter = CharacterType.Mustard;
                return false;
            }
        }

        // find available character to assign
        Dictionary<CharacterType, bool> availableChars = new Dictionary<CharacterType, bool>();
        foreach (CharacterType character in Enum.GetValues(typeof(CharacterType)))
        {
            availableChars.Add(character, true);
        }
        foreach (PlayerState player in players.Values)
        {
            availableChars[player.character] = false;
        }
        foreach (CharacterType character in availableChars.Keys)
        {
            if (availableChars[character])
            {
                assignedCharacter = character;
                PlayerState newPlayer = new PlayerState(playerID, name, assignedCharacter);
                if (players.TryAdd(playerID, newPlayer))
                {
                    newPlayer.currentRoom = board.GetStartingRoom(assignedCharacter);
                    state = new GameState(players.Keys.Count);
                    Log(string.Format("Adding player {0} to session", playerID));
                    return true;
                }
                break;
            }
        }
        
        // error adding player
        Log(string.Format("Error adding player {0}", playerID));
        assignedCharacter = CharacterType.Mustard;  // value does not matter at all if we fail. I just like col. Mustard the best
        return false;
    }

    public override bool RemovePlayer(int playerID)
    {
        
        if (players.TryGetValue(playerID, out PlayerState player))
        {
            if (!isGameStarted)
            {
                players.Remove(playerID);
                
                DisconnectPacket pkt = new DisconnectPacket(playerID);
                netInterface.Broadcast(playerID, pkt);
                
                Log(String.Format("Removed {0} (player {1})", player.playerName, playerID));
                return true;
            }
            else
            {
                GameOverPacket pkt = new GameOverPacket(playerID, GameOverType.Error);
                netInterface.Broadcast(playerID, pkt);
                Log("Cannot remove player, game has already started");
                return true;
            }
        }
        else
        {
            Log(String.Format("Failed to remove player {0}. They may have joined improperly", playerID));
            return false;
        }
    }

    public List<Tuple<int, string, CharacterType>> GetAllPlayerInfo()
    {
        List<Tuple<int, string, CharacterType>> allPlayerList = new List<Tuple<int, string, CharacterType>>();
        foreach (PlayerState ps in players.Values)
        {
            if (ps != null)
            {
                allPlayerList.Add(new Tuple<int, string, CharacterType>(ps.playerID, ps.playerName, ps.character));
            }
        }
        return allPlayerList;
    }

    public bool UpdateCharacter(int playerID, CharacterType newCharacter)
    {
        foreach (PlayerState player in players.Values)
        {
            if (player != null && player.character == newCharacter)
            {
                Log(String.Format("Cannot change {0} to {1}", playerID, newCharacter));
                return false;
            }
        }
        players[playerID].character = newCharacter;
        players[playerID].currentRoom = board.GetStartingRoom(newCharacter);
        Log(String.Format("Changed {0} to {1}", playerID, newCharacter));
        return true;
    }

    public bool MovePlayer(int playerID, RoomType destRoom, bool isForcedMove)
    {
        if (players.TryGetValue(playerID, out PlayerState playerState))
        {
            if (isForcedMove)
            {
                // do move
                playerState.currentRoom = destRoom;
                MoveToRoomPacket outPkt = new MoveToRoomPacket(false, playerID, destRoom, true);
                netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, outPkt);
            }
            else if (state.turn == playerID && state.action == TurnAction.MoveRoom)
            {
                if (board.IsValidMove(playerState.currentRoom, destRoom))
                {
                    // do move
                    playerState.currentRoom = destRoom;
                    MoveToRoomPacket outPkt = new MoveToRoomPacket(false, playerID, destRoom, false);
                    netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, outPkt);
                    if (board.IsGuessRoom(playerState.currentRoom))
                    {
                        SetTurn(state.turn, TurnAction.MakeGuess);
                    }
                    else
                    {
                        SetTurn(state.turn, TurnAction.Idle);
                    }
                    Log(String.Format("Moved Client{0} to {1}", playerID, destRoom.ToString()));
                    return true;
                }
            }
            else
            {
                Log(String.Format("{0} cannot move now", playerState.playerName));
            }
        }
        Log("Illegal move");
        return false;
    }

    public override bool Guess(int playerID, bool isFinal, CharacterType character, WeaponType weapon, RoomType room)
    {
        if (players.TryGetValue(playerID, out PlayerState playerState))
        {
            if (!isFinal)
            {
                if (state.turn == playerID && state.action == TurnAction.MakeGuess)
                {
                    GuessPacket outPkt = new GuessPacket(false, playerID, isFinal, character, weapon, room);
                    netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, outPkt);
                    SetTurn(state.turn, TurnAction.RevealCards);
                    // sort out players that need to reveal
                    playersNeedToReveal.Clear();
                    foreach (PlayerState player in players.Values)
                    {
                        if (player.playerID != playerID && player.isActive)
                        {
                            foreach (ClueCard card in player.cards)
                            {
                                if ((card.TryGetCharacterType(out CharacterType cardCharacter) && cardCharacter == character) ||
                                    (card.TryGetWeaponType(out WeaponType cardWeapon) && cardWeapon == weapon) ||
                                    (card.TryGetRoomType(out RoomType cardRoom) && cardRoom == room))
                                {
                                    playersNeedToReveal.Add(player.playerID, player);
                                    break;
                                }
                            }
                        }
                    }
                    if (playersNeedToReveal.Count == 0)
                    {
                        SetTurn(state.turn, TurnAction.Idle);
                    }

                    // also must move a player if their character was guessed!!!
                    foreach (PlayerState player in players.Values)
                    {
                        if (player != null && player.playerID != playerID && player.character == character)
                        {
                            MovePlayer(player.playerID, room, true);
                        }

                    }
                    
                    return true;
                }
                else
                {
                    Log(String.Format("{0} cannot guess now", playerState.playerName));
                }
            }
            else
            {
                if (state.turn == playerID)
                {
                    if (deck.IsCorrectGuess(character, weapon, room))
                    {
                        // win
                        GameOverPacket pkt = new GameOverPacket(playerID, GameOverType.Win);
                        netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, pkt);
                        return true;
                    }
                    else
                    {
                        // lose
                        playerState.isActive = false;
                        GameOverPacket pkt = new GameOverPacket(playerID, GameOverType.Lose);
                        netInterface.Broadcast(NetworkConstants.BROADCAST_ALL_CLIENTS, pkt);
                        EndTurn(playerID);
                        return true;
                    }
                }
                else
                {
                    Log(String.Format("{0} cannot accuse now", playerState.playerName));
                }
            }
        }
        Log("Illegal guess");
        return false;
    }

    public override bool Reveal(int sendID, int recvID, ClueType clueType, CharacterType character, WeaponType weapon, RoomType room)
    {
        bool status = false;
        if (state.turn == recvID && state.action == TurnAction.RevealCards)
        {
            if (playersNeedToReveal.TryGetValue(sendID, out PlayerState player))
            {
                switch (clueType)
                {
                    case ClueType.Character:
                    {
                        status = player.cards.Any(x => x.TryGetCharacterType(out CharacterType c) && c == character);
                        break;
                    }
                    case ClueType.Weapon:
                    {
                        status = player.cards.Any(x => x.TryGetWeaponType(out WeaponType w) && w == weapon);
                        break;
                    }
                    case ClueType.Room:
                    {
                        status = player.cards.Any(x => x.TryGetRoomType(out RoomType r) && r == room);
                        break;
                    }
                    default:
                    {
                        Log("Unrecognized clue type");
                        break;
                    }
                }
                if (status)
                {
                    playersNeedToReveal.Remove(sendID);
                    if (playersNeedToReveal.Count == 0)
                    {
                        SetTurn(state.turn, TurnAction.Idle);
                    }
                }
            }
            else
            {
                Log(String.Format("Player{0} does not need to reveal anything now", sendID));
            }
        }
        else
        {
            Log(String.Format("Player{0} does not need cards revealed to them now", recvID));
        }
        return status;
    }

    public bool EndTurn(int userID)
    {
        
        
        // if ((state.turn == userID && state.action == TurnAction.Idle) ||
        //     (players.TryGetValue(userID, out PlayerState prevPlayerState) && !prevPlayerState.isActive))
        // {
            int nextTurn = (state.turn + 1) % state.numPlayers;
            if (players.TryGetValue(nextTurn, out PlayerState nextPlayerState) && !nextPlayerState.isActive)
            {
                nextTurn = (nextTurn + 1) % state.numPlayers;
            }
            SetTurn(nextTurn, TurnAction.MoveRoom);
            return true;
        // }
        // else
        // {
        //     Log(String.Format("Player{0} cannot end their turn right now", userID));
        // }
        // return false;
    }

    public override void ReturnToMenu()
    {
        base.ReturnToMenu();
        netInterface.ShutDown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GuestEngine : BaseEngine
{
    public ClientNetworkInterface netInterface;
    public PlayerState player;
    public MasterUI masterUI;

    private Tuple<CharacterType, WeaponType, RoomType> lastGuess;

    private bool isReadyForNextReveal = true;
    private Queue<Tuple<int, int, ClueType, CharacterType, WeaponType, RoomType>> revealQueue = new Queue<Tuple<int, int, ClueType, CharacterType, WeaponType, RoomType>>();

    public int ID { get => player.playerID; }

    private void Update()
    {
        if (isReadyForNextReveal && revealQueue.TryDequeue(out Tuple<int, int, ClueType, CharacterType, WeaponType, RoomType> revealData))
        {
            Reveal(revealData.Item1, revealData.Item2, revealData.Item3, revealData.Item4, revealData.Item5, revealData.Item6);
        }
    }

    public override bool StartGame()
    {
        base.StartGame();
        List<string> names = new List<string> { player.playerName };
        foreach (PlayerState otherPlayer in players.Values)
        {
            if (otherPlayer != null)
            {
                names.Add(otherPlayer.playerName);
            }
        }
        masterUI.StartGame(names);
        return true;
    }

    public override void SetPlayerReady(int playerID, bool isReady)
    {
        masterUI.UpdatePlayerStatus(playerID, isReady ? PlayerStatus.Ready : PlayerStatus.NotReady);
    }

    public void ProcessChatMessage(int playerID, string message)
    {
        if (logger != null)
        {
            string formattedMessage = String.Format("[{0}] {1}", GetPlayerName(playerID), message);
            logger.LogChat(formattedMessage);
        }
    }

    public override void SetTurn(int turn, TurnAction action)
    {
        base.SetTurn(turn, action);
        if (player.playerID == turn)
        {
            masterUI.SetTurn("You", action, true);
            Log(String.Format("It is your turn"));
            // if (action == TurnAction.MakeGuess)
            // {
            //     masterUI.PromptGuess(false, player.currentRoom);
            // }
        }
        else if (players.TryGetValue(turn, out PlayerState player))
        {
            masterUI.SetTurn(player.playerName, action, false);
        }

        // update player status
        PlayerStatus activeStatus, waitingStatus;
        switch (action)
        {
            case TurnAction.MoveRoom :
            {
                activeStatus = PlayerStatus.Moving;
                waitingStatus = PlayerStatus.Waiting;
                break;
            }
            case TurnAction.MakeGuess :
            {
                activeStatus = PlayerStatus.Guessing;
                waitingStatus = PlayerStatus.Waiting;
                break;
            }
            case TurnAction.RevealCards :
            {
                activeStatus = PlayerStatus.Waiting;
                waitingStatus = PlayerStatus.Revealing;
                break;
            }
            case TurnAction.Idle :
            {
                activeStatus = PlayerStatus.EndingTurn;
                waitingStatus = PlayerStatus.Waiting;
                break;
            }
            default :
            {
                // should never get here but compiler complains otherwise
                activeStatus = PlayerStatus.NotReady;
                waitingStatus = PlayerStatus.NotReady;
                break;
            }
        }

        if (turn == ID)
        {
            masterUI.UpdatePlayerStatus(ID, waitingStatus);
        }
        else
        {
            masterUI.UpdatePlayerStatus(ID, activeStatus);
        }

        foreach (PlayerState otherPlayer in players.Values)
        {
            if (otherPlayer.isActive)
            {
                if (turn == otherPlayer.playerID)
                {
                    masterUI.UpdatePlayerStatus(otherPlayer.playerID, waitingStatus);
                }
                else
                {
                    masterUI.UpdatePlayerStatus(otherPlayer.playerID, activeStatus);
                }
            }
        }
    }

    public void AssignFromServer(int playerID, string name, CharacterType assignedCharacter)
    {
        player = new PlayerState(playerID, name, assignedCharacter);
        masterUI.SetCharacter(assignedCharacter);
        masterUI.AddPlayer(playerID, assignedCharacter, name);
        Log(String.Format("Joined server as {0} (Assigned ID is {1})", assignedCharacter, playerID));
    }

    public bool AssignClueCards(List<CharacterType> characterClues, List<WeaponType> weaponClues, List<RoomType> roomClues)
    {
        player.cards = deck.GetCardsFromClues(characterClues, weaponClues, roomClues);
        masterUI.SetCards(player.cards);
        Log(String.Format("{0}'s cards are {1}", player.playerName, String.Join(", ", player.cards.Select(x => x.cardName))));
        return true;
    }

    public override bool AddPlayer(int playerID, string name, CharacterType assignedCharacter)
    {
        if (players.TryAdd(playerID, new PlayerState(playerID, name, assignedCharacter)))
        {
            masterUI.AddPlayer(playerID, assignedCharacter, name);

            Log(String.Format("{0} Joined server as {1} (Assigned ID is {2})", name, assignedCharacter, playerID));
            state = new GameState(players.Keys.Count + 1);
            return true;
        }
        
        // error adding player
        Log(string.Format("Error adding player {0}", playerID));
        return false;
    }

    public override bool RemovePlayer(int playerID)
    {
        if (!isGameStarted)
        {
            if (players.TryGetValue(playerID, out PlayerState player))
            {
                players.Remove(playerID);
                
                masterUI.RemovePlayer(playerID, player.character);
                state = new GameState(players.Keys.Count + 1);

                SetPlayerReady(ID, false);

                Log(String.Format("Removed {0} (player {1})", player.playerName, playerID));
                return true;
            }
            else
            {
                Log(String.Format("Failed to remove player {0}", playerID));
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public bool UpdateCharacter(int playerID, CharacterType newCharacter)
    {
        CharacterType oldCharacter;
        if (playerID == ID)
        {
            oldCharacter = player.character;
            player.character = newCharacter;
            masterUI.SetCharacter(newCharacter);
            masterUI.UpdatePlayerCharacter(playerID, newCharacter, oldCharacter);
            Log(String.Format("Changed character to {0}", newCharacter));
            return true;
        }
        else if (players.TryGetValue(playerID, out PlayerState otherPlayer))
        {
            oldCharacter = otherPlayer.character;
            otherPlayer.character = newCharacter;
            masterUI.UpdatePlayerCharacter(playerID, newCharacter, oldCharacter);
            Log(String.Format("{0} changed character to {1}", otherPlayer.playerName, newCharacter));
            return true;
        }
        Log(String.Format("Error changing player {0} character to {1}", playerID, newCharacter));
        return false;
    }

    public bool MovePlayer(int playerID, RoomType newRoom, bool isForcedMove)
    {
        bool status = false;
        if (playerID == ID)
        {
            masterUI.MoveCharacterToRoom(player.character, player.currentRoom, newRoom);
            player.currentRoom = newRoom;
            masterUI.DisableRoomSelection();
            Log(String.Format("Moved to {0}", newRoom));

            if (isForcedMove)
            {
                masterUI.ShowGeneralNotification("Notice", String.Format("You were forcibly moved to {0}", newRoom), true);
            }

            status = true;
        }
        else if (players.TryGetValue(playerID, out PlayerState otherPlayer))
        {
            masterUI.MoveCharacterToRoom(otherPlayer.character, otherPlayer.currentRoom, newRoom);
            otherPlayer.currentRoom = newRoom;
            Log(String.Format("{0} moved to {1}", otherPlayer.playerName, newRoom));
            status = true;
        }
        else
        {
            Log(String.Format("Error moving player {0} to {1}", playerID, newRoom));
        }
        return status;
    }

    public override bool Guess(int playerID, bool isFinal, CharacterType character, WeaponType weapon, RoomType room)
    {
        bool status = false;
        if (playerID == ID)
        {
            Log(String.Format("Guessed {0} used the {1} in the {2}", character, weapon, room));
            status = true;
        }
        else if (players.TryGetValue(playerID, out PlayerState otherPlayer))
        {
            Log(String.Format("{0} guessed {1} used the {2} in the {3}", GetPlayerName(playerID), character, weapon, room));
            // store last guess so we can refer to it if we need to reveal any cards
            lastGuess = new Tuple<CharacterType, WeaponType, RoomType>(character, weapon, room);
            status = true;
        }
        else
        {
            Log("Error with guess");
        }
        return status;
    }

    public List<ClueCard> GetCardsToReveal()
    {
        List<ClueCard> cardsToReveal = new List<ClueCard>();
        foreach (ClueCard card in player.cards)
        {
            if ((card.TryGetCharacterType(out CharacterType cardCharacter) && cardCharacter == lastGuess.Item1) ||
                (card.TryGetWeaponType(out WeaponType cardWeapon) && cardWeapon == lastGuess.Item2) ||
                (card.TryGetRoomType(out RoomType cardRoom) && cardRoom == lastGuess.Item3))
            {
                cardsToReveal.Add(card);
            }
        }
        return cardsToReveal;
    }

    public void SetReadyForReveal(bool value)
    {
        isReadyForNextReveal = value;
    }
    
    public void EnqueueReveal(int sendID, int recvID, ClueType clueType, CharacterType character, WeaponType weapon, RoomType room)
    {
        revealQueue.Enqueue(Tuple.Create(sendID, recvID, clueType, character, weapon, room));
    }

    public override bool Reveal(int sendID, int recvID, ClueType clueType, CharacterType character, WeaponType weapon, RoomType room)
    {
        if (recvID == ID)
        {
            if (players.TryGetValue(sendID, out PlayerState otherPlayer))
            {
                ClueCard receivedCard;
                
                switch(clueType)
                {
                    case ClueType.Character:
                    {
                        if (!deck.TryGetCard(character, out receivedCard))
                        {
                            // error
                            Log(String.Format("Error parsing ClueCard from {0}", character.ToString()));
                            return false;
                        }
                        break;
                    }
                    case ClueType.Weapon:
                    {
                        if (!deck.TryGetCard(weapon, out receivedCard))
                        {
                            // error
                            Log(String.Format("Error parsing ClueCard from {0}", weapon.ToString()));
                            return false;
                        }
                        break;
                    }
                    case ClueType.Room:
                    {
                        if (!deck.TryGetCard(room, out receivedCard))
                        {
                            // error
                            Log(String.Format("Error parsing ClueCard from {0}", room.ToString()));
                            return false;
                        }
                        break;
                    }
                    default:
                    {
                        Log(String.Format("Unrecognized ClueType {0}", clueType.ToString()));
                        return false;
                    }
                }

                isReadyForNextReveal = false;
                masterUI.NotifyReveal(receivedCard, otherPlayer.playerName);
                Log(String.Format("{0} revealed card {1}", otherPlayer.playerName, clueType == ClueType.Character ? character : clueType == ClueType.Weapon ? weapon : room));
            }
        }
        return true;
    }

    public void Win(int playerID)
    {
        if (playerID == ID)
        {
            masterUI.NotifyGameOver("You", GameOverType.Win);
        }
        else if (players.TryGetValue(playerID, out PlayerState otherPlayer))
        {
            masterUI.NotifyGameOver(otherPlayer.playerName, GameOverType.Win);
        }
        masterUI.UpdatePlayerStatus(playerID, PlayerStatus.Won);
    }

    public void Lose(int playerID)
    {
        player.isActive = false;
        if (playerID == ID)
        {
            masterUI.NotifyGameOver("You", GameOverType.Lose);
        }
        else if (players.TryGetValue(playerID, out PlayerState otherPlayer))
        {
            masterUI.NotifyGameOver(otherPlayer.playerName, GameOverType.Lose);
        }
        masterUI.UpdatePlayerStatus(playerID, PlayerStatus.Lost);
    }

    public void ErrorOut(string text)
    {
       masterUI.ShowGeneralNotification("Error", text, false);
    }

    public override void ReturnToMenu()
    {
        base.ReturnToMenu();
        netInterface.ShutDown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// takes text in and publishes it as a chat message from a client
// really just a debug feature
public class ClientConsole : MonoBehaviour
{
    public InputField inputField;
    public BaseNetworkInterface netInterface;
    
    public GuestEngine engine;
    public ConsoleLogger logger;


    public void SubmitCommand()
    {
        string rawCmd = inputField.text;
        string[] tokens = rawCmd.Split(" ");
        string cmdType = tokens[0];
        if (netInterface is ClientNetworkInterface)
        {
            ClientNetworkInterface clientInterface = (ClientNetworkInterface)netInterface;
            switch (cmdType.ToLower())
            {
                // case ("connect"):
                // {
                //     string nameStr = tokens[1];
                //     netInterface.processName = nameStr;
                //     netInterface.Initialize(engine, logger);
                //     break;
                // }
                case ("\\start"):
                {
                    GameStartPacket pkt = new GameStartPacket(true, engine.ID);
                    clientInterface.SendMessage(pkt);
                    break;
                }
                case ("\\char"):
                {
                    string charStr = tokens[1];
                    if (Enum.TryParse<CharacterType>(charStr, true, out CharacterType charEnum))
                    {
                        CharUpdatePacket pkt = new CharUpdatePacket(true, engine.ID, charEnum);
                        clientInterface.SendMessage(pkt);
                    }
                    break;
                }
                case ("\\move"):
                {
                    string roomStr = tokens[1];
                    if (Enum.TryParse<RoomType>(roomStr, true, out RoomType roomEnum))
                    {
                        MoveToRoomPacket pkt = new MoveToRoomPacket(true, engine.ID, roomEnum, false);
                        clientInterface.SendMessage(pkt);
                    }
                    break;
                }
                case ("\\guess"):
                {
                    string charStr = tokens[1];
                    string weaponStr = tokens[2];
                    string roomStr = tokens[3];
                    if (Enum.TryParse<CharacterType>(charStr, true, out CharacterType charEnum) &&
                        Enum.TryParse<WeaponType>(weaponStr, true, out WeaponType weaponEnum) &&
                        Enum.TryParse<RoomType>(roomStr, true, out RoomType roomEnum))
                    {
                        GuessPacket pkt = new GuessPacket(true, engine.ID, false, charEnum, weaponEnum, roomEnum);
                        clientInterface.SendMessage(pkt);
                    }
                    break;
                }
                case ("\\accusation"):
                {
                    string charStr = tokens[1];
                    string weaponStr = tokens[2];
                    string roomStr = tokens[3];
                    if (Enum.TryParse<CharacterType>(charStr, true, out CharacterType charEnum) &&
                        Enum.TryParse<WeaponType>(weaponStr, true, out WeaponType weaponEnum) &&
                        Enum.TryParse<RoomType>(roomStr, true, out RoomType roomEnum))
                    {
                        GuessPacket pkt = new GuessPacket(true, engine.ID, true, charEnum, weaponEnum, roomEnum);
                        clientInterface.SendMessage(pkt);
                    }
                    break;
                }
                case ("\\reveal"):
                {
                    string cardStr = tokens[1];
                    RevealPacket pkt;
                    if (Enum.TryParse<CharacterType>(cardStr, true, out CharacterType charEnum))
                    {
                        pkt = new RevealPacket(true, engine.ID, engine.state.turn, ClueType.Character, charEnum, 0, 0);
                    }   
                    else if (Enum.TryParse<WeaponType>(cardStr, true, out WeaponType weaponEnum))
                    {
                        pkt = new RevealPacket(true, engine.ID, engine.state.turn, ClueType.Weapon, 0, weaponEnum, 0);
                    }
                    else if (Enum.TryParse<RoomType>(cardStr, true, out RoomType roomEnum))
                    {
                        pkt = new RevealPacket(true, engine.ID, engine.state.turn, ClueType.Room, 0, 0, roomEnum);
                    }
                    else
                    {
                        // error parsing
                        Log(String.Format("Error parsing clue type from {0}", cardStr));
                        return;
                    }
                    clientInterface.SendMessage(pkt);
                    break;
                }
                case ("\\done"):
                {
                    TurnDonePacket pkt = new TurnDonePacket(engine.ID);
                    clientInterface.SendMessage(pkt);
                    break;
                }
                case ("\\chat"):
                {
                    string text = string.Join(" ", tokens.Skip(1).ToList<string>());
                    logger.Log(text, SubsystemType.UI);
                    ChatPacket pkt = new ChatPacket(true, engine.ID, text);
                    clientInterface.SendMessage(pkt);
                    break;
                }
                default:
                {
                    string text = string.Join(" ", tokens.ToList<string>());
                    logger.LogChat(String.Format("[{0}] {1}", engine.player.playerName, text));
                    ChatPacket pkt = new ChatPacket(true, engine.ID, text);
                    clientInterface.SendMessage(pkt);
                    break;
                }
            }
        }
        inputField.text = "";
    }

    public void Log(string message)
    {
        if (logger != null)
        {
            logger.Log(message, SubsystemType.UI);
        }
    }
}
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class ClientNetworkInterface : BaseNetworkInterface
{
    private TcpClient tcpClient;
    private NetworkEndpoint netPlayer;

    public ClientNetworkInterface(IPAddress ipAddress, int portNum) : base (ipAddress, portNum)
    {
        tcpClient = new TcpClient();
        isConnected = true;
    }

    public override void Initialize()
    {
        // create a new thread to handle network client
        Thread connectThread = new Thread(ConnectToServer);
        connectThread.Start();
    }

    public override void ShutDown()
    {
        base.ShutDown();
        tcpClient.Dispose();
    }

    public void ConnectToServer()
    {
        Log("Starting connect thread");
        try
        {
            // attempt to connect to server
            Log(String.Format("attempting to connect to {0} : {1}", ipAddress.ToString(), portNum));
            tcpClient.Connect(ipAddress, portNum);
            Log("Connection successful");
            netPlayer = new NetworkEndpoint(tcpClient, this);
            networkStream = tcpClient.GetStream();
        }
        catch (SocketException e)
        {
            Log(e.Message);
        }
        catch (Exception e)
        {
            Log(String.Format("Unexpected error: {2}\n{2}\n{3}", e.Message, e.InnerException, e.StackTrace));
        }

        // send initial connect message so server knows our name
        ConnectRequestPacket pkt = new ConnectRequestPacket("TestUserName");
        netPlayer.SendMessage(pkt);

        // begin processing messages from server 
        if (tcpClient.Connected && isConnected)
        {
            netPlayer.GetMessage();
        }
    }

    public void SendMessage(INetworkPacket pkt)
    {
        netPlayer.SendMessage(pkt);
    }

    public override void ProcessMessage(int clientID, MessageIDs messageID, byte[] buffer)
    {
        Log(String.Format("Processing message {0}", messageID.ToString()));
        switch(messageID)
        {
            case MessageIDs.Connect_ToClient :
            {
                ConnectResponsePacket pkt = new ConnectResponsePacket(buffer);
                if (pkt.isAccepted)
                {
                    Log(String.Format("Joined server as {0} (Assigned ID is {1})", pkt.assignedCharacter, pkt.assignedCharacter));
                }
                else
                {
                    Log(String.Format("Rejected by server"));
                }
                break;
            }
            case MessageIDs.Disconnect_ToClient :

                break;
            case MessageIDs.Chat_ToClient :
            {
                ChatPacket pkt = new ChatPacket(buffer);
                Log(String.Format("Client{0} sent chat \"{1}\"", clientID, pkt.message));
                break;
            }
            case MessageIDs.GameStart_ToClient :

                break;
            case MessageIDs.CharUpdate_ToClient :
            {
                CharUpdatePacket pkt = new CharUpdatePacket(buffer);
                Log(String.Format("Client{0} requested to change character to {1}", clientID, pkt.character.ToString()));
                break;
            }
            case MessageIDs.MoveToRoom_ToClient :
            {
                MoveToRoomPacket pkt = new MoveToRoomPacket(buffer);
                Log(String.Format("Client{0} requested to move to {1}", clientID, pkt.room.ToString()));
                break;
            }
            case MessageIDs.Guess_ToClient :
            {
                GuessPacket pkt = new GuessPacket(buffer);
                Log(String.Format("Client{0} guessed {1} used the {2} in the {3}", clientID, pkt.character.ToString(), pkt.weapon.ToString(), pkt.room.ToString()));
                break;
            }
            case MessageIDs.Reveal_ToClient :

                break;
            case MessageIDs.Win_ToClient :

                break;
            case MessageIDs.Lost_ToClient :

                break;
            default :

                break;

        }
    }

}
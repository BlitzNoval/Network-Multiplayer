using Mirror;

public struct AuthRequestMessage : NetworkMessage
{
    public string roomName;
    public string playerName;
}

public struct AuthResponseMessage : NetworkMessage
{
    public bool success;
}

using Mirror;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Структура сообщения, которое клиент отправляет серверу при коннекте.
    /// </summary>
    public struct AuthRequestMessage : NetworkMessage
    {
        public string ClientToken;
    }
}

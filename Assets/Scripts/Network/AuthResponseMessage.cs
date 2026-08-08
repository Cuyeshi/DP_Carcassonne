using Mirror;
using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Network
{
    /// <summary>
    /// Структура ответа от сервера клиенту (Успех/Отказ)
    /// </summary>
    public struct AuthResponseMessage : NetworkMessage
    {
        public byte Code;
        public string Message;
    }
}

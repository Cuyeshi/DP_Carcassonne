using Assets.Scripts.Main;
using Mirror;
using System;
using UnityEngine;

namespace Assets.Scripts.Network
{
    public class TokenAuthenticator : NetworkAuthenticator
    {
        [Header("Настройки")]
        public string localPlayerTokenKey = "CarcaPlayerToken";

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnClientAuthenticate()
        {
            string myToken = Assets.Scripts.View.MainMenuUI.PlayerName;
            Debug.Log($"[АУТЕНТИФИКАЦИЯ] Мой токен (Имя): {myToken}. Отправляю серверу...");

            AuthRequestMessage authMessage = new AuthRequestMessage { ClientToken = myToken };
            NetworkClient.Send(authMessage);
        }

        private void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.Code == 100)
            {
                Debug.Log($"[АУТЕНТИФИКАЦИЯ] Сервер принял наш токен. Входим в игру!");
                ClientAccept();
            }
            else
            {
                Debug.LogError($"[АУТЕНТИФИКАЦИЯ] Ошибка: {msg.Message}");

                // === ИСПРАВЛЕНИЕ: СОХРАНЯЕМ ОШИБКУ В ПАМЯТЬ ===
                // Поскольку Mirror сейчас перезагрузит MenuScene, мы должны передать ошибку будущей сцене!
                PlayerPrefs.SetString("NetworkErrorReason", msg.Message);
                PlayerPrefs.Save();

                NetworkClient.connection.Disconnect();
            }
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        private void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            Debug.Log($"[АУТЕНТИФИКАЦИЯ] Сервер получил токен от клиента: {msg.ClientToken}");

            CarcaNetworkManager netManager = NetworkManager.singleton as CarcaNetworkManager;
            CarcaGameManager gm = FindFirstObjectByType<CarcaGameManager>();

            if (netManager != null)
            {
                bool isGameRunning = (gm != null && gm.isGameStarted);

                // 1. ЕСЛИ ИГРА УЖЕ ИДЕТ
                if (isGameRunning)
                {
                    if (netManager.PlayerSessions.TryGetValue(msg.ClientToken, out PlayerSessionData session))
                    {
                        // Если время вышло - закрываем доступ здесь
                        if (session.IsDead)
                        {
                            RejectConnection(conn, "Время на возвращение в игру истекло!");
                            return;
                        }

                        if (session.IsDisconnected)
                        {
                            Debug.Log($"[АУТЕНТИФИКАЦИЯ] Игрок {msg.ClientToken} возвращается.");
                        }
                        else
                        {
                            RejectConnection(conn, "Игрок с таким именем уже в игре!");
                            return;
                        }
                    }
                    else
                    {
                        RejectConnection(conn, "Игра уже идет! Вход заблокирован.");
                        return;
                    }
                }
                // 2. ЕСЛИ В ЛОББИ
                else
                {
                    if (netManager.PlayerSessions.ContainsKey(msg.ClientToken))
                    {
                        RejectConnection(conn, "Имя уже занято в лобби!");
                        return;
                    }
                }
            }

            conn.authenticationData = msg.ClientToken;

            AuthResponseMessage authResponse = new AuthResponseMessage { Code = 100, Message = "Success" };
            conn.Send(authResponse);

            ServerAccept(conn);
        }

        private void RejectConnection(NetworkConnectionToClient conn, string reason)
        {
            Debug.LogWarning($"[АУТЕНТИФИКАЦИЯ] Отказ: {reason}");
            AuthResponseMessage errorResponse = new AuthResponseMessage { Code = 200, Message = reason };
            conn.Send(errorResponse);
            StartCoroutine(DisconnectAfterDelay(conn));
        }

        private System.Collections.IEnumerator DisconnectAfterDelay(NetworkConnectionToClient conn)
        {
            yield return new WaitForSeconds(0.2f);
            conn.Disconnect();
        }
    }
}
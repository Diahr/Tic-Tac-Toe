using System.Net;

namespace Tic_Tac_Toe_Server.Classes
{
    /// <summary>
    /// Структура с информаией об игроке
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Имя игрока
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// IP адрес игрока
        /// </summary>
        public readonly IPEndPoint Ip;

        public Player(string name, IPEndPoint ip)
        {
            Name = name;
            Ip = ip;
        }
    }
}


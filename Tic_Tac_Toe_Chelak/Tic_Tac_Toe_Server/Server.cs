using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Tic_Tac_Toe_Server.Classes;
using System.Threading;
using System.Data.SqlClient;
using System.IO;

namespace Tic_Tac_Toe_Server
{
    class Server
    {
        /// <summary>
        /// A maximum number of simultaneous users.
        /// </summary>
        private static readonly int maxUsers = 4;
        /// <summary>
        /// A number of currently connected users.
        /// </summary>
        private static int currentUsers = 0;

        /// <summary>
        /// Process a connection request.
        /// </summary>
        /// <returns>Structure with info about an accepted player.</returns>
        private static Player AcceptPlayer()
        {
        start:
            using (UdpClient client = new UdpClient(8800))
            {
                IPEndPoint playerIp = null;
                string playerName = Encoding.ASCII.GetString(client.Receive(ref playerIp));
                byte[] answer = new byte[4];

                GetStats(ref answer, playerName);

                client.Connect(playerIp);
                if (currentUsers >= maxUsers)
                {
                    answer[0] = 0;
                    client.Send(answer, answer.Length);
                    goto start;
                }

                answer[0] = 1;

                client.Send(answer, answer.Length);

                Interlocked.Increment(ref currentUsers);

                Console.WriteLine("Accepted player: {0} with IP: {1}", playerName, playerIp);
                return new Player(playerName, playerIp);
            }
        }

        /// <summary>
        /// Get stats of a player from the database.
        /// </summary>
        /// <param name="array">An array in which you will store stats.</param>
        /// <param name="playerName">Name of the designated player.</param>
        private static void GetStats(ref byte[] array, string playerName)
        {
            string dbFileName = "AttachDbFilename=\""
                    + Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\"))
                    + "tttDB.mdf\";";

            string connect_string = "Data Source=(LocalDB)\\MSSQLLocalDB;" +
                 dbFileName +
                "Integrated Security=True";

            SqlConnection conn = new SqlConnection(connect_string);

            conn.Open();

            string[] stat = new string[] { "win", "lose", "draw" };
            string sql;
            SqlCommand command;
            SqlDataReader dataReader;

            // Get win/lose stats.
            //
            for (int idx = 0; idx < stat.Length; idx++)
            {
                sql = string.Format("select count(*) cnt from stats where playerName=\'{0}\' and matchResult=\'{1}\';", playerName, stat[idx]);
                command = new SqlCommand(sql, conn);
                dataReader = command.ExecuteReader();
                while (dataReader.Read())
                {
                    array[idx + 1] = Convert.ToByte(dataReader.GetValue(0));
                }
                dataReader.Close();
            }

            conn.Close();
        }

        /// <summary>
        /// Update stats info in the database.
        /// </summary>
        /// <param name="player1">First player.</param>
        /// <param name="player2">Second player.</param>
        /// <param name="res">Match result.</param>
        private static void UpdateStats(Player player1, Player player2, Result res)
        {
            // Update info in the database.
            //
            string dbFileName = "AttachDbFilename=\""
                + Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"..\..\"))
                + "tttDB.mdf\";";

            string connect_string = "Data Source=(LocalDB)\\MSSQLLocalDB;" +
                 dbFileName +
                "Integrated Security=True";

            SqlConnection conn = new SqlConnection(connect_string);

            conn.Open();

            string[] stat = new string[] { "win", "lose", "draw" };
            string sql;
            SqlCommand command;

            // Send info about the winner.
            //
            if (res == Result.Draw)
            {
                sql = string.Format("insert into stats (playerName, matchResult) values ('{0}', 'draw');" +
                    "insert into stats (playerName, matchResult) values ('{1}', 'draw');", player1.Name, player2.Name);
                command = new SqlCommand(sql, conn);
                command.ExecuteNonQuery();
            }
            else if (res == Result.Crosses)
            {
                sql = string.Format("insert into stats (playerName, matchResult) values ('{0}', 'win');" +
                    "insert into stats (playerName, matchResult) values ('{1}', 'lose');", player1.Name, player2.Name);
                command = new SqlCommand(sql, conn);
                command.ExecuteNonQuery();
            }
            else if (res == Result.Circles)
            {
                sql = string.Format("insert into stats (playerName, matchResult) values ('{0}', 'lose');" +
                    "insert into stats (playerName, matchResult) values ('{1}', 'win');", player1.Name, player2.Name);
                command = new SqlCommand(sql, conn);
                command.ExecuteNonQuery();
            }

            conn.Close();
        }

        /// <summary>
        /// Play the game between two players.
        /// </summary>
        /// <param name="player1">First player.</param>
        /// <param name="player2">Second player.</param>
        private static async Task PlayTheGame(Player player1, Player player2)
        {
            Console.WriteLine("Started the game between {0} and {1}", player1.Ip, player2.Ip);

            // Create a field to play.
            //
            Figure[] playField = new Figure[9];
            for (int i = 0; i < playField.Length; i++) playField[i] = Figure.None;

            // Wait for players to start listening.
            //
            Thread.Sleep(500);
            using (UdpClient client1 = new UdpClient())
            using (UdpClient client2 = new UdpClient())
            {
                client1.Connect(player1.Ip);
                client2.Connect(player2.Ip);

                // Send info about the turn order.
                //
                client1.Send(new byte[1] { 0 }, 1);
                client2.Send(new byte[1] { 1 }, 1);

                Result res;
                while (true)
                {
                    // Get first player's move info.
                    //
                    res = MakeMove(client1, client2, ref playField, Figure.Cross);
                    if (res != Result.None) break;

                    // Get second player's move info.
                    //
                    res = MakeMove(client2, client1, ref playField, Figure.Circle);
                    if (res != Result.None) break;
                }

                // The game is over. Send info to clients.
                //
                client1.Send(new byte[1] { 0 }, 1);
                client2.Send(new byte[1] { 0 }, 1);

                UpdateStats(player1, player2, res);

                // Send info about match results.
                //
                if (res == Result.Draw)
                {
                    client1.Send(new byte[1] { 2 }, 1);
                    client2.Send(new byte[1] { 2 }, 1);
                }
                else if (res == Result.Crosses)
                {
                    client1.Send(new byte[1] { 1 }, 1);
                    client2.Send(new byte[1] { 0 }, 1);
                } 
                else if (res == Result.Circles)
                {
                    client1.Send(new byte[1] { 0 }, 1);
                    client2.Send(new byte[1] { 1 }, 1);
                }
            }
            Interlocked.Add(ref currentUsers, -2);
        }

        /// <summary>
        /// Process player's move.
        /// </summary>
        /// <param name="player1">A player that makes a move.</param>
        /// <param name="player2">A player that waits for a move.</param>
        /// <param name="playField">A game's field.</param>
        /// <param name="figure">First player's figure, e.g. cross or circle.</param>
        /// <returns>Move's result.</returns>
        private static Result MakeMove(UdpClient player1, UdpClient player2,
                                       ref Figure[] playField, Figure figure)
        {
            var lastContactedIp = new IPEndPoint(IPAddress.Any, 8800);

            player1.Send(new byte[1] { 1 }, 1);
            player2.Send(new byte[1] { 1 }, 1);

            // Get first player's move info.
            //
            byte[] moveInfo = player1.Receive(ref lastContactedIp);
            player2.Send(new byte[1] { moveInfo[0] }, 1);
            playField[moveInfo[0]] = figure;

            return CheckTheField(playField);
        }

        /// <summary>
        /// Check playfield's state.
        /// </summary>
        /// <param name="playField">A game's field.</param>
        /// <returns>Current result, e.g. draw, win of circles, win of crosses or none.</returns>
        private static Result CheckTheField(Figure[] playField)
        {
            // Check horizontally.
            //
            for (int i = 0; i < playField.Length; i += 3)
            {
                if (playField[i] != Figure.None
                    && playField[i] == playField[i + 1]
                    && playField[i] == playField[i + 2])
                    return playField[i] == Figure.Cross ? Result.Crosses : Result.Circles;
            }

            // Check vertically.
            //
            for (int i = 0; i < playField.Length / 3; ++i)
            {
                if (playField[i] != Figure.None
                    && playField[i] == playField[i + 3]
                    && playField[i] == playField[i + 6])
                    return playField[i] == Figure.Cross ? Result.Crosses : Result.Circles;
            }

            // Check diagonals.
            //
            if (playField[0] != Figure.None
                    && playField[0] == playField[4]
                    && playField[0] == playField[8])
                return playField[0] == Figure.Cross ? Result.Crosses : Result.Circles;
            if (playField[2] != Figure.None
                    && playField[2] == playField[4]
                    && playField[2] == playField[6])
                return playField[2] == Figure.Cross ? Result.Crosses : Result.Circles;

            // Check for a draw. E.g. all the nodes are filled.
            //
            foreach (Figure field in playField) if (field == Figure.None) return Result.None;

            return Result.Draw;
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Player player1 = AcceptPlayer();
                Player player2 = AcceptPlayer();

                Task.Run(() => PlayTheGame(player1, player2));
            }
        }
    }
}

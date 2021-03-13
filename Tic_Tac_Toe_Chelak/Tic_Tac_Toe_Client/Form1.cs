using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tic_Tac_Toe_Client
{
    public partial class ticTacToe : Form
    {
        /// <summary>
        /// Starting port. We will use it as a baseline.
        /// </summary>
        private static int startingPort = 8800;
        /// <summary>
        /// IP of the server.
        /// </summary>
        private IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 8800);
        /// <summary>
        /// An event to get info about moves in async game.
        /// </summary>
        private AutoResetEvent moveMadeEvent = new AutoResetEvent(false);
        /// <summary>
        /// A game's field.
        /// </summary>
        private PictureBox[] playField;
        /// <summary>
        /// Image of the circle.
        /// </summary>
        private Image circle => Properties.Resources.circle;
        /// <summary>
        /// Image of the cross.
        /// </summary>
        private Image cross => Properties.Resources.cross;

        /// <summary>
        /// ID of the client. We need it to identify a unique port.
        /// </summary>
        private int clientId;
        /// <summary>
        /// Client of this user.
        /// </summary>
        private UdpClient client;
        /// <summary>
        /// Tile's number in which a move was performed.
        /// </summary>
        private byte moveIndex;

        public ticTacToe()
        {
            InitializeComponent();
            playField = fieldGrpBox.Controls.OfType<PictureBox>().Reverse().ToArray();

            // Give a unique port.
            //
            string processName = Process.GetCurrentProcess().ProcessName;
            clientId = Process.GetProcesses().Count(p => p.ProcessName == processName);
        }

        /// <summary>
        /// After click on the connect button.
        /// </summary>
        private void ConnectButtonClick(object sender, EventArgs e)
        {
            // We need a player's name to continue.
            //
            if (nameBox.Text == string.Empty)
            {
                MessageBox.Show("Please enter your name.\nChoose wisely.");
                return;
            }

            // Send player's name and get stats.
            //
            byte[] answer;
            using (client = new UdpClient(startingPort + clientId))
            {
                byte[] data = Encoding.ASCII.GetBytes(nameBox.Text);
                client.Connect(endPoint);
                client.Send(data, data.Length);
                answer = client.Receive(ref endPoint);
            }

            if (answer[0] == 1)
            {
                StatusLbl.Text = "Waiting for an opponent";
                connectButton.Enabled = false;

                winTxtStat.Text  = answer[1].ToString();
                loseTxtStat.Text = answer[2].ToString();
                drawTxtStat.Text = answer[3].ToString();

                Task.Run(() => Play());
            }
            else if (answer[0] == 0) StatusLbl.Text = "Server is busy, try again later";
        }

        /// <summary>
        /// Play the game.
        /// </summary>
        private void Play()
        {
            IPEndPoint hostIp = null;
            Image mySymbol, opponentSymbol;

            // Create a UDP connection and bind it to the local port.
            //
            using (client = new UdpClient(startingPort + clientId))
            {
                byte[] data = client.Receive(ref hostIp);
                client.Connect(hostIp);
                mySymbol = data[0] == 0 ? cross : circle;
                symbolTxt.Text = data[0] == 0 ? "cross" : "circle";
                opponentSymbol = data[0] == 0 ? circle : cross;

                bool myTurn = data[0] == 0;

                // 1 - continue playing, 0 - stop.
                //
                while (client.Receive(ref hostIp)[0] == 1)
                {
                    if (myTurn)
                    {
                        // Use invoke to change win forms objects.
                        //
                        Invoke(new MethodInvoker(() =>
                        {
                            fieldGrpBox.Enabled = true;
                            StatusLbl.Text = "Your turn";
                        }));
                        moveMadeEvent.WaitOne();

                        Invoke(new MethodInvoker(() => playField[moveIndex].Image = mySymbol));
                        client.Send(new byte[1] { moveIndex }, 1);
                        myTurn = false;
                    }
                    else
                    {
                        Invoke(new MethodInvoker(() =>
                        {
                            fieldGrpBox.Enabled = false;
                            StatusLbl.Text = "Opponent's turn";
                        }));
                        moveIndex = client.Receive(ref hostIp)[0];

                        Invoke(new MethodInvoker(() => playField[moveIndex].Image = opponentSymbol));
                        myTurn = true;
                    }
                }

                // End the game.
                //
                data = client.Receive(ref hostIp);
                if (data[0] == 2)
                {
                    MessageBox.Show("It's a draw!");
                    drawTxtStat.Text = (Convert.ToInt32(drawTxtStat.Text) + 1).ToString();
                }
                else if (data[0] == 1)
                {
                    MessageBox.Show("Decisive victory!");
                    winTxtStat.Text = (Convert.ToInt32(winTxtStat.Text) + 1).ToString();
                }
                else if (data[0] == 0)
                {
                    MessageBox.Show("Better luck next time!");
                    loseTxtStat.Text = (Convert.ToInt32(loseTxtStat.Text) + 1).ToString();
                }
                Invoke(new MethodInvoker(() =>
                {
                    foreach (PictureBox field in playField) field.Image = null;
                    fieldGrpBox.Enabled = false;
                    connectButton.Enabled = true;
                    StatusLbl.Text = "Disconnected";
                    symbolTxt.Text = "none";
                }));
            }
        }

        /// <summary>
        /// After clicking on the field.
        /// </summary>
        private void FieldClick(object sender, EventArgs e)
        {
            PictureBox field = (PictureBox)sender;
            if (field.Image != null) return;

            moveIndex = byte.Parse(field.Name.Remove(0, 5));
            moveMadeEvent.Set();
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Commons.Music.Midi;

namespace WootingMidi
{
    public partial class UserControl1 : UserControl
    {
        public UserControl1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        { }
        private IMidiPortDetails[] _midiOutDetails;//{ 65, 50, 66, 51, 67, 68, 53, 69, 54, 70, 55, 71, 34, 19, 35, 20, 36, 37, 22, 38, 23, 39, 24, 40 };
        private readonly List<Keys> _keyIndex = new List<Keys> { Keys.D1, Keys.None, Keys.D2, Keys.None, Keys.D3, Keys.D4, Keys.None, Keys.D5, Keys.None, Keys.D6, Keys.None, Keys.D7, Keys.D8, Keys.None, Keys.D9, Keys.None, Keys.D0, Keys.Q, Keys.None, Keys.W, Keys.None, Keys.E, Keys.None, Keys.R, Keys.T, Keys.None, Keys.Y, Keys.None, Keys.U, Keys.I, Keys.None, Keys.O, Keys.None, Keys.P, Keys.None, Keys.A, Keys.S, Keys.None, Keys.D, Keys.None, Keys.F, Keys.G, Keys.None, Keys.H, Keys.None, Keys.J, Keys.None, Keys.K, Keys.L, Keys.None, Keys.Z, Keys.None, Keys.X, Keys.C, Keys.None, Keys.V, Keys.None, Keys.B, Keys.None, Keys.N, Keys.M, Keys.None };
        private readonly double[] _keyPress = new double[64];
        private string _lowOctKeys = "Q_W_ER_T_Y_U";
        private string _highOctKeys = "QWERTYUIOP[]G";
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xaml;
using System.Reflection;
using System.Text.RegularExpressions;
using Commons.Music.Midi;
using WootingAnalogSDKNET;

namespace WootingMidi
{
    public class CcKeyPress
    {
        private double _press;
        public string KeyCode { get; set; }

        public double Press
        {
            get => _press;
            set
            {
                if (value > 0) Active = true;
                _press = value;
            }
        }

        public bool Active { get; set; }
    }

    public class Settings
    {
        public static Settings Last;

        public int Octave1 { get; set; } = 0;
        public int Octave2 { get; set; } = 1;
        public int Octave3 { get; set; } = 0;
        public int Octave4 { get; set; } = 1;
        public int Extras { get; set; } = 0;
        public string MidiDevice { get; set; } = "";
        public int Channel { get; set; } = 0;
        public bool SendNote { get; set; } = true;
        public bool SendAt { get; set; } = true;
        public bool SendCc { get; set; } = true;

        public Settings()
        {
            Last = this;
        }

        public static string Path()
        {
            return System.IO.Path.Combine(Environment.CurrentDirectory, "settings.xaml");
        }

        public void Save()
        {
            XamlServices.Save(Path(), this);
        }

        public static Settings Load()
        {
            try
            {
                return Last = XamlServices.Load(Path()) as Settings ?? new Settings();
            }
            catch (Exception e)
            {
                return new Settings();
            }
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public const byte NOKEY = 255;
        public const int WOOTING_ONE_VID = 0x03EB;
        public const int WOOTING_ONE_PID = 0xFF02;
        public const int WOOTING_ONE_ANALOG_USAGE_PAGE = 0x1338;

        private Settings _settings = new Settings();
        private bool _loaded = false;

        public static readonly DependencyProperty ChannelNumberProperty =
            DependencyProperty.Register("ChannelNumber", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        public int ChannelNumber
        {
            get => (int)GetValue(ChannelNumberProperty);
            set => SetValue(ChannelNumberProperty, value);
        }

        public static readonly DependencyProperty LowOctaveNumProperty =
            DependencyProperty.Register("LowOctaveNum", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        public int LowOctaveNum
        {
            get => (int)GetValue(LowOctaveNumProperty);
            set => SetValue(LowOctaveNumProperty, value);
        }

        public static readonly DependencyProperty HiOctaveNumProperty =
            DependencyProperty.Register("HiOctaveNum", typeof(int), typeof(MainWindow), new PropertyMetadata(1));

        public int HiOctaveNum
        {
            get => (int)GetValue(HiOctaveNumProperty);
            set => SetValue(HiOctaveNumProperty, value);
        }

        public static readonly DependencyProperty HiOctaveNumProperty1 =
           DependencyProperty.Register("HiOctaveNum1", typeof(int), typeof(MainWindow), new PropertyMetadata(2));

        public int HiOctaveNum1
        {
            get => (int)GetValue(HiOctaveNumProperty1);
            set => SetValue(HiOctaveNumProperty1, value);
        }

        public static readonly DependencyProperty HiOctaveNumProperty2 =
            DependencyProperty.Register("HiOctaveNum2", typeof(int), typeof(MainWindow), new PropertyMetadata(3));

        public int HiOctaveNum2
        {
            get => (int)GetValue(HiOctaveNumProperty2);
            set => SetValue(HiOctaveNumProperty2, value);
        }

        public static readonly DependencyProperty ExtrasProperty =
            DependencyProperty.Register("Extras", typeof(int), typeof(MainWindow), new PropertyMetadata(4));

        public int Extra
        {
            get => (int)GetValue(ExtrasProperty);
            set => SetValue(ExtrasProperty, value);
        }

        public static readonly DependencyProperty SendNoteProperty =
            DependencyProperty.Register("SendNote", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendNote
        {
            get => (bool)GetValue(SendNoteProperty);
            set => SetValue(SendNoteProperty, value);
        }

        public static readonly DependencyProperty SendAtProperty =
            DependencyProperty.Register("SendAt", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendAt
        {
            get => (bool)GetValue(SendAtProperty);
            set => SetValue(SendAtProperty, value);
        }

        public static readonly DependencyProperty SendCcProperty =
            DependencyProperty.Register("SendCc", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool SendCc
        {
            get => (bool)GetValue(SendCcProperty);
            set => SetValue(SendCcProperty, value);
        }

        public ObservableDictionary<int, CcKeyPress> CcPresses { get; } = new ObservableDictionary<int, CcKeyPress>();
        public ObservableCollection<CcKeyPress> CcPressesList { get; } = new ObservableCollection<CcKeyPress>();

        internal class DefaultMidiModuleDatabase : MidiModuleDatabase
        {
            private static readonly Assembly ass = typeof(DefaultMidiModuleDatabase).GetTypeInfo().Assembly;

            public IList<MidiModuleDefinition> Modules { get; set; }

            public static Stream GetResource(string name)
            {
                return ass.GetManifestResourceStream(ass.GetManifestResourceNames().FirstOrDefault((string m) => m.EndsWith(name, StringComparison.OrdinalIgnoreCase)));
            }

            public DefaultMidiModuleDatabase()
            {
                Modules = new List<MidiModuleDefinition>();
                string[] source = new StreamReader(GetResource("midi-module-catalog.txt")).ReadToEnd().Split(new char[1] {
                '\n'
            });
                foreach (string item in from s in source
                                        select s.Trim())
                {
                    if (item.Length > 0)
                        Modules.Add(MidiModuleDefinition.Load(GetResource(item)));
                }
            }
            public override IEnumerable<MidiModuleDefinition> All()
            {
                return Modules;
            }
            public override MidiModuleDefinition Resolve(string moduleName)
            {
                if (moduleName == null)
                    return null;
                string name = ResolvePossibleAlias(moduleName);
                return Modules.FirstOrDefault((MidiModuleDefinition m) => m.Name == name) ?? Modules.FirstOrDefault((MidiModuleDefinition m) => (m.Match != null && new Regex(m.Match).IsMatch(name)) || name.Contains(m.Name));
            }
            public string ResolvePossibleAlias(string name)
            {
                if (name == "Microsoft GS Wavetable Synth")
                    return "Microsoft GS Wavetable SW Synth";
                return name;
            }
            public interface IMidiPortDetails
            {
                string Id { get; }
                string Manufacturer { get; }
                string Name { get; }
                string Version { get; }
            }

            public interface IMidiAccess2 : IMidiAccess
            {
                MidiAccessExtensionManager ExtensionManager { get; }
            }
            public class MidiAccessExtensionManager
            {
                public virtual bool Supports<T>() where T : class
                {
                    return GetInstance<T>() != null;
                }

                public virtual T GetInstance<T>() where T : class
                {
                    return null;
                }
            }
        }


        private bool shiftPressed = false;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LowOctave.FillKeys(_lowOctKeys);
            HighOctave.FillKeys(_highOctKeys);

            _settings = Settings.Load();

            ChannelNumber = _settings.Channel;
            LowOctaveNum = _settings.Octave1;
            HiOctaveNum = _settings.Octave2;
            HiOctaveNum1 = _settings.Octave3;
            HiOctaveNum2 = _settings.Octave4;
            Extra = _settings.Extras;
            SendNote = _settings.SendNote;
            SendAt = _settings.SendAt;
            SendCc = _settings.SendCc;

            UpdateMidi();
            (int noDevices, WootingAnalogResult res) = WootingAnalogSDK.Initialise();
            if (res != WootingAnalogResult.Ok)
            {
                System.Windows.MessageBox.Show("Could not initialize sdk, Error: " + res.ToString());
                Environment.Exit(0);
            }
            WootingAnalogSDK.SetKeycodeMode(KeycodeType.VirtualKey);
            _loaded = true;

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    try
                    {
                        (List<(short, float)> buffer, WootingAnalogResult result) = WootingAnalogSDK.ReadFullBuffer(50);

                        //if (buffer.Count == 0) continue;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            bool tshiftPressed = false;
                            foreach (var cc in CcPresses.Values)
                            {
                                cc.Press = 0.0;
                            }
                            for (int i = 0; i < 24; i++)
                            {
                                _keyPress[i] = 0;
                            }

                            if (result == WootingAnalogResult.Ok)
                            {
                                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));

                                foreach ((short key, float value) in buffer)
                                {
                                    Keys vKey = (Keys)key;

                                    if (vKey == Keys.LShiftKey || vKey == Keys.RShiftKey)
                                    {
                                        tshiftPressed = true;
                                    }
                                    else if (CcPresses.ContainsKey(key))
                                    {
                                        CcPresses[key].KeyCode = vKey.ToString();
                                        CcPresses[key].Press = value * 100.0;
                                    }
                                    else
                                    {
                                        CcPresses.Add(key, new CcKeyPress()
                                        {
                                            KeyCode = vKey.ToString(),
                                            Press = value * 100.0
                                        });
                                    }

                                    if (_keyIndex.Contains(vKey))
                                    {
                                        var ki = _keyIndex.IndexOf(vKey);
                                        _keyPress[ki] = value;
                                    }
                                }

                                ProcessNotes();

                                CcPresses.OnCollectionChanged();
                                CcPressesList.Clear();
                                foreach (var press in CcPresses.Values)
                                {
                                    if (press.Press > 0.0)
                                        CcPressesList.Add(press);
                                }

                                var looffs = 12 + LowOctaveNum * 12;
                                var hioffs = 12 + HiOctaveNum * 12;
                                var hioffs1 = 12 + HiOctaveNum1 * 12;
                                var hioffs2 = 12 + HiOctaveNum2 * 12;
                                var extrakeys = 12 + Extra * 12;

                                LowOctave.UpdateBars(_notes, looffs);
                                HighOctave.UpdateBars(_notes, hioffs);
                            }
                            else
                            {
                                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                            }

                            shiftPressed = tshiftPressed;
                        }));
                        if (result != WootingAnalogResult.Ok)
                            Thread.Sleep(500);
                    }
                    catch (Exception e)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                        }));
                        throw e;
                    }
                    Thread.Sleep(2);
                }
            });
        }

        private IMidiPortDetails[] _midiOutDetails;//{ 65, 50, 66, 51, 67, 68, 53, 69, 54, 70, 55, 71, 34, 19, 35, 20, 36, 37, 22, 38, 23, 39, 24, 40 };
        private readonly List<Keys> _keyIndex = new List<Keys> { Keys.D1, Keys.Modifiers, Keys.D2, Keys.None, Keys.D3, Keys.D4, Keys.None, Keys.D5, Keys.None, Keys.D6, Keys.None, Keys.D7, Keys.D8, Keys.None, Keys.D9, Keys.None, Keys.D0, Keys.Q, Keys.None, Keys.W, Keys.None, Keys.E, Keys.None, Keys.R, Keys.T, Keys.None, Keys.Y, Keys.None, Keys.U, Keys.I, Keys.None, Keys.O, Keys.None, Keys.P, Keys.None, Keys.A, Keys.S, Keys.None, Keys.D, Keys.None, Keys.F, Keys.G, Keys.None, Keys.H, Keys.None, Keys.J, Keys.None, Keys.K, Keys.L, Keys.None, Keys.Z, Keys.None, Keys.X, Keys.C, Keys.None, Keys.V, Keys.None, Keys.B, Keys.None, Keys.N, Keys.M, Keys.None, Keys.Oemcomma, Keys.OemPeriod, Keys.ShiftKey, Keys.OemQuestion, Keys.Space };
        private readonly double[] _keyPress = new double[96];
        private string _lowOctKeys = "Q_W_ER_T_Y_U";
        private string _highOctKeys = "QWERTYUIOP[]G";

        private readonly NoteKey[] _notes = new NoteKey[128];
        private readonly byte[] _rawMidiMessageBuf = new byte[3];

        public void Button_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((System.Windows.Forms.Control.ModifierKeys & Keys.LShiftKey) == Keys.Shift)
            {
                System.Windows.Forms.MessageBox.Show("Pressed " + Keys.Shift);
            }
        }

        private void ProcessNotes()
        {
            if (_midiDevice == null) return;

            var extra = shiftPressed ? 1 : 0;

            var looffs = 12 + (LowOctaveNum + extra) * 12;
            var hioffs = 12 + (HiOctaveNum + extra) * 12;
            var hioffs1 = 12 + (HiOctaveNum + extra) * 12;
            var hioffs2 = 12 + (HiOctaveNum + extra) * 12;
            var extrakeys = 12 + (Extra + extra) * 12;
            for (int i = 0; i < 128; i++)
            {
                if (_notes[i] == null) _notes[i] = new NoteKey() { NoteId = (uint)i };
                RawMidiMessage res;
                if (i >= looffs && i < looffs + 64)
                {
                    var ii = i - looffs;
                    res = _notes[i].Submit(_keyPress[ii]);
                }
                else if (i >= hioffs && i < hioffs + 12)
                {
                    var ii = i - hioffs;
                    res = _notes[i].Submit(_keyPress[ii + 64]);
                }
                else
                {
                    res = _notes[i].Submit(0.0);
                }

                if (res.Valid)
                {
                    res.Bytes(_rawMidiMessageBuf, 0);
                    _midiDevice.Send(_rawMidiMessageBuf, 0, 3, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
                }
            }

            foreach (var ccc in CcPresses)
            {
                var cc = MidiCc.Generate(ChannelNumber, ccc.Key, ccc.Value.Press / 100.0);
                if (!cc.Valid || !ccc.Value.Active) continue;
                cc.Bytes(_rawMidiMessageBuf, 0);
                _midiDevice.Send(_rawMidiMessageBuf, 0, 3, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
                ccc.Value.Active = false;
            }

            if (SendAt)
            {
                var cat = MidiChannelAt.Generate(ChannelNumber, _notes.Max(cp => cp.Pressure));
                cat.Bytes(_rawMidiMessageBuf, 0);
                _midiDevice.Send(_rawMidiMessageBuf, 0, 2, 0 /*(long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds*/);
            }
        }

        private void UpdateMidi()
        {
            _midiOutDetails = MidiAccessManager.Default.Outputs.ToArray();
            MidiSelect.Items.Clear();
            foreach (var midiOutDetail in _midiOutDetails)
            {
                MidiSelect.Items.Add(midiOutDetail.Name);
            }
            MidiSelect.SelectedItem = _settings.MidiDevice;
        }

        /*private void UpdateHid()
        {
            if (_hidStream != null)
            {
                _hidStream.Close();
                _hidStream.Dispose();
            }

            var devices = DeviceList.Local.GetHidDevices(WOOTING_ONE_VID, WOOTING_ONE_PID);
            var success = false;
            foreach (var candidate in devices)
            {
                try
                {
                    if (candidate.GetMaxInputReportLength() == 33)
                    {
                        success = true;
                        _hidDevice = candidate;
                        break;
                    }
                }
                catch (Exception e)
                { }
            }
            if (success)
            {
                _hidStream = _hidDevice.Open();
                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
            }
            else
            {
                HidStatus.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                _hidDevice = null;
            }
        }*/

        private IMidiOutput _midiDevice;

        private void OnMidiSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_midiDevice != null)
            {
                var closetask = _midiDevice.CloseAsync();
                closetask.Wait();
            }
            var deviceinfo = _midiOutDetails[MidiSelect.SelectedIndex];
            var opentask = MidiAccessManager.Default.OpenOutputAsync(deviceinfo.Id);
            opentask.Wait();
            _midiDevice = opentask.Result;
            _settings.MidiDevice = deviceinfo.Name;
            _settings.Save();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (_loaded &&
                (e.Property == LowOctaveNumProperty ||
                 e.Property == HiOctaveNumProperty ||
                 e.Property == ChannelNumberProperty ||
                 e.Property == SendNoteProperty ||
                 e.Property == SendAtProperty ||
                 e.Property == SendCcProperty)
            )
            {
                _settings.Channel = ChannelNumber;
                _settings.Octave1 = LowOctaveNum;
                _settings.Octave2 = HiOctaveNum;
                _settings.Extras = Extra;
                _settings.SendNote = SendNote;
                _settings.SendAt = SendAt;
                _settings.SendCc = SendCc;

                foreach (var note in _notes)
                {
                    note.Channel = (uint)ChannelNumber;
                }
                _settings.Save();
            }
            base.OnPropertyChanged(e);
        }

        private void OnListKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void LowOctDecr_Click(object sender, RoutedEventArgs e)
        {
            LowOctaveNum = Math.Max(0, LowOctaveNum - 1);
        }

        private void HighOctDecr_Click(object sender, RoutedEventArgs e)
        {
            HiOctaveNum = Math.Max(0, HiOctaveNum - 1);
        }

        private void LowOctIncr_Click(object sender, RoutedEventArgs e)
        {
            LowOctaveNum = Math.Min(9, LowOctaveNum + 1);
        }

        private void HighOctIncr_Click(object sender, RoutedEventArgs e)
        {
            HiOctaveNum = Math.Min(9, HiOctaveNum + 1);
        }

        public virtual bool Shift { get; }
    }
}

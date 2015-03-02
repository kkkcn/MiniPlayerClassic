﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Resources;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MiniPlayerClassic
{

    public partial class MainFrom : Form
    {
        #region DLL Import And Windows Message Managing(一些非托管实现的功能的封装)

        [DllImport("user32.dll")]
        private static extern int RegisterHotKey(IntPtr hwnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern int UnregisterHotKey(IntPtr hwnd, int id);

        private const int WM_HOTKEY = 0x312; //窗口消息-热键
        private const int WM_CREATE = 0x1; //窗口消息-创建
        private const int WM_DESTROY = 0x2; //窗口消息-销毁

        private const int VK_MEDIA_PLAYPAUSE = 179;
        private const int VK_MEDIA_NEXT = 176;
        private const int VK_MEDIA_PREV = 177;
        private const int VK_MEDIA_MUSIC = 181;

        private const int VK_VOICE_ONOFF = 173;
        private const int VK_VOICE_UP = 175;
        private const int VK_VOICE_DOWN = 174;

        private const int MOD_NONE = 0;
        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="hotKey_id">指派用于生成消息的热键ID</param>
        /// <param name="fsModifiers">组合键</param>
        /// <param name="vk">生成消息的热键</param>
        private bool RegKey(IntPtr hwnd, int hotKey_id, int fsModifiers, int vk)
        {
            if (RegisterHotKey(hwnd, hotKey_id, fsModifiers, vk) == 0) 
                return false; 
            else 
                return true; 
        }

        /// <summary>
        /// 注销热键
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="hotKey_id">原用于生成消息所指派的热键ID</param>
        private void UnRegKey(IntPtr hwnd, int hotKey_id)
        {
            UnregisterHotKey(hwnd, hotKey_id);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case WM_HOTKEY: //窗口消息-热键
                    switch (m.WParam.ToInt32())
                    {
                        case WM_HOTKEY:
                            btnPlay_Click(this, null);
                            break;
                        case WM_HOTKEY + 1:
                            btnNext_Click(this,null);
                            break;
                        case WM_HOTKEY + 2:
                            btnPrev_Click(this, null);
                            break;
                        default:
                            break;
                    }
                    break;
                case WM_CREATE: //窗口消息-创建
                    RegKey(Handle, WM_HOTKEY + 0, 0, VK_MEDIA_PLAYPAUSE); //注册热键
                    RegKey(Handle, WM_HOTKEY + 1, 0, VK_MEDIA_NEXT);
                    RegKey(Handle, WM_HOTKEY + 2, 0, VK_MEDIA_PREV);
                    break;
                case WM_DESTROY: //窗口消息-销毁
                    UnRegKey(Handle, WM_HOTKEY + 0); //销毁热键
                    UnRegKey(Handle, WM_HOTKEY + 1);
                    UnRegKey(Handle, WM_HOTKEY + 2);
                    break;
                default:
                    break;
            }
        }

        #endregion

        bool _progressbar_draw = true;
        bool _volumebar_draw = true;

        public IPlayer MainPlayer;
        public BassNET_Player BassNET_Player; //播放器对象
        public c_ProgressBar cProgressBar; //进度条对象
        public c_VolumeBar cVolumeBar; //音量条对象

        public string LabelText; //标签文字
        private Graphics pb_g_enter;//画布
        private Graphics pb_g_enter2;

        List<PlayList> PlayLists;//用于管理多个播放列表

        private int def_height;//默认尺寸
        private int min_height;
        private bool is_Minisize;//界面是否在迷你模式


        private int newlists = 0;//新建列表名字的计数器，用于计算新建了多少列表方面命名

        enum playbackHeadState { ByIndex, Single, Single_Cycling, List_Cycling, Shuffle }
        private playbackHeadState playbackhead_state;
        private PlayListItem playbackhead;
        private bool playback = false;
        private bool buttonaction = false;

        private Un4seen.Bass.BASSTimer ScuptrumTimer;

        //一些东西的初始化
        public MainFrom(string[] args)
        {
            InitializeComponent();

            LabelText = "暂无播放任务";
            def_height = this.Height;
            min_height = this.Height - tb_Lists.Height;
            is_Minisize = true;

            pb_g_enter = pb_Progress.CreateGraphics(); //初始化进度条们的画布
            pb_g_enter2 = pb_Volume.CreateGraphics();

            BassNET_Player = new BassNET_Player(this.Handle); //初始化播放器对象
            MainPlayer = BassNET_Player;
            MainPlayer.TrackStateChanged += MainPlayer_StateChange;  //注册播放器改变播放状态的事件的响应函数
            MainPlayer.TrackFileChanged += MainPlayer_FileChange; //注册播放器改变文件的事件的响应函数

            BassNET_Player.WaveFormFinished += MainPlayer_WaveFormFinished;

            PlayLists = new List<PlayList>(32);

            cProgressBar = new c_ProgressBar(pb_Progress.Width, pb_Progress.Height); //初始化进度条
            cProgressBar.ChangeTitle(LabelText);
            cProgressBar.pb_maxvalue = 10;
            cProgressBar.pb_value = 0;

            cVolumeBar = new c_VolumeBar(pb_Volume.Width,pb_Volume.Height); //初始化音量条
            cVolumeBar.ChangeLabel("音量");
            cVolumeBar.pb_maxvalue = 100;
            cVolumeBar.pb_value = 100;

            to_Minisize(false);//迷你模式
            tb_Lists.TabPages.Clear();
            refreshInterface();
            playbackhead_state = playbackHeadState.Single;

            load_file_list(args);
            ScuptrumTimer = new Un4seen.Bass.BASSTimer(17);
            ScuptrumTimer.Tick += ScuptrumTimer_Tick;
            ScuptrumTimer.Start();
        }

        void MainPlayer_WaveFormFinished(object sender, EventArgs e)
        {
            cProgressBar.UpdateWaveForm(BassNET_Player.waveform);
        }

        /// <summary>
        /// 从一个字符串列表里读取文件
        /// </summary>
        /// <param name="args">储存文件路径的字符串列表</param>
        public void load_file_list(string[] args)
        {
            if (args.Length != 0)
            {
                if (args.Length == 1)
                {
                    if (System.IO.Path.GetExtension(args[0]) == ".spl")
                    {
                        PlayLists.Add(new PlayList(args[0]));
                        tb_Lists.TabPages.Add(System.IO.Path.GetFileNameWithoutExtension(args[0]));
                        RefreshPlayList();
                        refreshInterface();
                    }
                    else
                    {
                        if (MainPlayer.LoadFile(args[0]))
                            MainPlayer.Play();
                    }
                }

                if (args.Length > 1)
                {
                    List<string> temp1 = new List<string>();
                    List<string> temp2 = new List<string>();
                    foreach (string filename in args)
                    {
                        if (System.IO.Path.GetExtension(filename) == ".spl")
                            temp1.Add(filename);
                        else
                            temp2.Add(filename);
                    }
                    if (temp2.Count != 0 && tb_Lists.TabCount == 0) tmNewList_Click(this, null);
                    foreach (string filenames in temp2)
                    {
                        PlayLists[tb_Lists.SelectedIndex].Add(new PlayListItem(filenames, ""));
                    }
                    foreach (string filename in temp1)
                    {
                        PlayLists.Add(new PlayList(filename));
                        tb_Lists.TabPages.Add(System.IO.Path.GetFileNameWithoutExtension(filename));
                        RefreshPlayList();
                    }
                    refreshInterface();
                }
            }
        }

        //init
        private void MainFrom_Load(object sender, EventArgs e)
        {
        /* If you want to close the splash of Bass.Net you need to regist at 
         * www.un4seen.com and input the registration code.
         * (At the initialization of Player too.
         */ 
        //BassNet.Registration("your_email","your_code");
            
        }
        //Stop Button
        private void btnStop_Click(object sender, EventArgs e)
        {
            MainPlayer.Stop();
            buttonaction = true;
        }
        //Play/Pause button
        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (MainPlayer.TrackState != TrackStates.Playing)
                MainPlayer.Play();
            else
                MainPlayer.Pause();
            buttonaction = true;
        }

        private void playbackactions()
        {/*
            switch (playbackhead_state)
            {
                case playbackHeadState.ByIndex:
                    playbackhead = ;
                    if (playbackhead != null) 
                    {
                        if (MainPlayer.LoadFile(playbackhead.Value.FileAddress))
                            MainPlayer.Play();
                    }
                    else { playback = false; }
                    break;

                case playbackHeadState.List_Cycling:
                    if (playbackhead.Next == null)
                    {
                        if (playbackhead.List.First == null)
                        {
                            playback = false;
                            break;
                        }
                        playbackhead = playbackhead.List.First;
                    }
                    else { playbackhead = playbackhead.Next; }
                    if (MainPlayer.LoadFile(playbackhead.Value.FileAddress)) MainPlayer.Play();
                    break;

                case playbackHeadState.Single:
                    break;

                case playbackHeadState.Single_Cycling:
                    MainPlayer.Play();
                    break;
            }//*/
        }
//--------Events Checker--------------------------------------------------------------------------
        void MainPlayer_StateChange(object sender, TrackStateChange e) //响应播放状态改变的消息的函数
        {
            if (e.Message != TrackStates.Playing)
                btnPlay.Image = Properties.Resources.play;
            else
                btnPlay.Image = Properties.Resources.pause;

            if (playback && !buttonaction && MainPlayer.TrackState != TrackStates.Playing)
            {
                if (PlayLists.Count == 0)
                    playback = false;
                else
                    playbackactions();
            }
            buttonaction = false;
            System.GC.Collect();
        }

        void MainPlayer_FileChange(object sender, TrackFileChange e)
        { 
            if(e.Message != "")
            {
                LabelText = System.IO.Path.GetFileName(MainPlayer.FilePath);
                cProgressBar.ChangeTitle(LabelText);
                cProgressBar.pb_maxvalue = (int)(MainPlayer.TrackLength * 1000);
                BassNET_Player.GetWaveForm(cProgressBar.width, cProgressBar.height);
            }
            System.GC.Collect();
        }
//------Window interface change-------------------------------------------------------------------
        public void to_Minisize(Boolean animate)//迷你尺寸
        {

            this.Height = min_height;
            is_Minisize = true;
        }

        public void to_NormalSize(Boolean animate)//正常尺寸
        {

            this.Height = def_height;
            is_Minisize = false;
        }

        public void refreshInterface()//刷新界面元素的设置
        {
            if (tb_Lists.TabCount == 0)
            {
                this.Text = "MiniPlayer";
                tbtnRemove.Enabled = false;
                tbtnPlayMode.Enabled = false;
                if (!is_Minisize) to_Minisize(true);
                tbtnModeChange.Image = Properties.Resources.arrow_down;
                tbtnModeChange.Enabled = false;
                tbtnModeChange.ToolTipText = "切换界面模式\n（请先新建列表）";
                tmAddList.Enabled = false;
                playback = false;
            }
            else
            {
                if (PlayLists[tb_Lists.SelectedIndex].FilePath != "")
                    tb_Lists.SelectedTab.Text = System.IO.Path.GetFileNameWithoutExtension(PlayLists[tb_Lists.SelectedIndex].FilePath);

                this.Text = "MiniPlayer - " + tb_Lists.SelectedTab.Text;
                listBox1.Parent = tb_Lists.SelectedTab;
                tbtnPlayMode.Enabled = true;
                tbtnRemove.Enabled = true;
                if (is_Minisize) to_NormalSize(true);
                tbtnModeChange.Image = Properties.Resources.arrow_up;
                tbtnModeChange.Enabled = true;
                tbtnModeChange.ToolTipText = "切换界面模式";
                tmAddList.Enabled = true;
                playback = true;
            }

        }
//------------------------------------------------------------------------------------------------

        #region About Drawing the ProgressBar //这里因为都是些很易懂的过程就懒得注释了
        private void pb_Progress_MouseDown(object sender, MouseEventArgs e) 
        {
            _progressbar_draw = false;
            buttonaction = false;
            int temp;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                temp = (int)((float)cProgressBar.pb_maxvalue * ((float)e.X / (float)cProgressBar.width));
                cProgressBar.pb_value = temp;
                cProgressBar.DrawBar(pb_g_enter);
            }
        }

        private void pb_Progress_MouseMove(object sender, MouseEventArgs e)
        {
            int temp;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                temp = (int)((float)cProgressBar.pb_maxvalue * ((float)e.X / (float)cProgressBar.width));
                cProgressBar.pb_value = temp;
                cProgressBar.DrawBar(pb_g_enter);
            }
        }

        private void pb_Progress_MouseUp(object sender, MouseEventArgs e)
        {
            _progressbar_draw = true;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            { MainPlayer.TrackPosition = (double)cProgressBar.pb_value / 1000; }
        }
        #endregion

        #region Bar's Timer

        void ScuptrumTimer_Tick(object sender, EventArgs e)
        {
            double temp;
            temp = MainPlayer.TrackPosition;
            if (temp == -1) { temp = 0; }

            if(_progressbar_draw)
            {
                cProgressBar.pb_value = (int)(temp * 1000);
                cProgressBar.DrawBar(pb_g_enter);
            }

            if(_volumebar_draw)
            {
                cVolumeBar.DrawBar(pb_g_enter2);
                int left = 0, right = 0;
                BassNET_Player.GetLevel(ref left, ref right);
                cVolumeBar.tellitlevel(left, right);
                BassNET_Player.getData(ref cVolumeBar.fft_data);
            }

        }

        private void tmrPGBars_Tick(object sender, EventArgs e)
        {
            double temp;
            temp = MainPlayer.TrackPosition;
            if (temp == -1) { temp = 0; }
            cProgressBar.pb_value = (int)(temp * 1000);
            cProgressBar.DrawBar(pb_g_enter);

            cVolumeBar.DrawBar(pb_g_enter2);
            int left = 0, right = 0;
            BassNET_Player.GetLevel(ref left, ref right);
            cVolumeBar.tellitlevel(left, right);
            BassNET_Player.getData(ref cVolumeBar.fft_data);
        }

        private void tmrVBar_Tick(object sender, EventArgs e)
        {
            
        }

        #endregion

        #region VolumeBar
        private void pb_Volume_MouseDown(object sender, MouseEventArgs e)
        {
            //tmrVBar.Enabled = false;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                cVolumeBar.pb_value = (int)((float)cVolumeBar.pb_maxvalue * ((float)e.X/(float)pb_Volume.Width));
                MainPlayer.Volume = (float)cVolumeBar.pb_value / (float)cVolumeBar.pb_maxvalue;
                //cVolumeBar.DrawBar(pb_g_enter2);
            }
        }

        private void pb_Volume_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                cVolumeBar.pb_value = (int)((float)cVolumeBar.pb_maxvalue * ((float)e.X / (float)pb_Volume.Width));
                MainPlayer.Volume = (float)cVolumeBar.pb_value / (float)cVolumeBar.pb_maxvalue;
                //cVolumeBar.DrawBar(pb_g_enter2);
            }
        }

        private void pb_Volume_MouseUp(object sender, MouseEventArgs e)
        {
            //tmrVBar.Enabled = true;
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                cVolumeBar.pb_value = (int)((float)cVolumeBar.pb_maxvalue * ((float)e.X / (float)pb_Volume.Width));
                MainPlayer.Volume = (float)cVolumeBar.pb_value / (float)cVolumeBar.pb_maxvalue;
                //cVolumeBar.DrawBar(pb_g_enter2);
            }
        }
        #endregion

        private void tbtnAdd_ButtonClick(object sender, EventArgs e) //“添加文件”按钮
        {
            OpenFileDialog dlg1 = new OpenFileDialog(); //创建一个文件打开窗口对象
            dlg1.Filter = "Supported files|" + MainPlayer.SupportStream + ";*.spl";
            dlg1.Filter += "|Simple List File|*.spl";
            dlg1.Filter += "|All Files|*.*";
            
            dlg1.Multiselect = true; //允许文件打开窗口多选
            dlg1.ShowDialog(); //显示这个窗口

            if (dlg1.FileNames.Length <= 0) return; //如果没有选择文件就退出函数
            else load_file_list(dlg1.FileNames);

            System.GC.Collect();
        }

        private void tbtnRemove_ButtonClick(object sender, EventArgs e) //“删除选项”按钮
        {
            int i;

            if (listBox1.SelectedItems.Count < 1) { return; }//如果没有选中项目就返回

            for (i = listBox1.SelectedItems.Count - 1;i >= 0; i--)
            {
                listBox1.Items.Remove(listBox1.SelectedItems[i]);
                PlayLists[tb_Lists.SelectedIndex].RemoveAt(i);
            }
            
            RefreshPlayList();
        }

        private void RefreshPlayList()//刷新播放列表过程
        {
            listBox1.Items.Clear();//清空列表控件
            if (PlayLists.Count != 0)
            {
                for (int i = 0; i < PlayLists[tb_Lists.SelectedIndex].Count; i++)
                {
                    listBox1.Items.Add(System.IO.Path.GetFileName(PlayLists[tb_Lists.SelectedIndex][i].FileAddress));
                }
            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            playbackhead = PlayLists[tb_Lists.SelectedIndex][listBox1.Items.IndexOf(listBox1.SelectedItems[0])]; //把对象从链表中读取出来
            if (MainPlayer.LoadFile(playbackhead.FileAddress)) { MainPlayer.Play(); } //载入文件
        }

        private void tmEmptyList_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0) { return; }//如果列表没有选中项就返回
            if (MessageBox.Show("要清空列表？\n此操作不可恢复哦。",
                "清空列表？",MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No) { return; };

            PlayLists[tb_Lists.SelectedIndex].Clear();
            listBox1.Items.Clear();
        }

        private void tmCloseList_Click(object sender, EventArgs e)
        {
            if (PlayLists[tb_Lists.SelectedIndex].OperationCount != 0)
            { 
                switch(MessageBox.Show("列表已修改，保存？", "列表", MessageBoxButtons.YesNoCancel))
                {
                    case System.Windows.Forms.DialogResult.Cancel:
                        return;

                    case System.Windows.Forms.DialogResult.Yes:
                        tmSaveList_Click(sender, e);
                        break;
                }
            }

            PlayLists.RemoveAt(tb_Lists.SelectedIndex);
            RefreshPlayList();
            tb_Lists.TabPages.Remove(tb_Lists.SelectedTab);
            refreshInterface();
        }

        private void tmNewList_Click(object sender, EventArgs e)
        {
            PlayLists.Add(new PlayList());//创建一个播放列表

            tb_Lists.TabPages.Add("*未命名列表" + newlists++.ToString());
            if (tb_Lists.TabCount == 1)
            {
                listBox1.Parent = tb_Lists.SelectedTab;
            }
            refreshInterface();
        }

        private void tmDeleteListFile_Click(object sender, EventArgs e)//删除列表操作
        {
            if (MessageBox.Show("删除列表文件吗？\n此操作不可恢复哦！", "删除列表", 
                MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
            return;

            if(PlayLists[tb_Lists.SelectedIndex].FilePath != "")
                if(System.IO.File.Exists(PlayLists[tb_Lists.SelectedIndex].FilePath))
                    System.IO.File.Delete(PlayLists[tb_Lists.SelectedIndex].FilePath);

            PlayLists.RemoveAt(tb_Lists.SelectedIndex);
            tb_Lists.TabPages.Remove(tb_Lists.SelectedTab);
            RefreshPlayList();

        }

        private void tbtnList_Click(object sender, EventArgs e)
        {
            tbtnList.ShowDropDown();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            listBox1.Parent = tb_Lists.SelectedTab;
            RefreshPlayList();
            refreshInterface();
        }

        private void tbtnModeChange_Click(object sender, EventArgs e)
        {
            if (is_Minisize)
            {
                tbtnModeChange.Image = Properties.Resources.arrow_up;
                to_NormalSize(true);
            }
            else
            {
                tbtnModeChange.Image = Properties.Resources.arrow_down;
                to_Minisize(true);
            }  
        }

        private void tmOpenList_Click(object sender, EventArgs e)
        {

        }

        private void tmAddList_Click(object sender, EventArgs e)
        {
            if(PlayLists.Count != 0)
            {
                OpenFileDialog dlg1 = new OpenFileDialog();
                dlg1.Filter = "Simple List File|*.spl";
                dlg1.ShowDialog();
                string tmp1 = dlg1.FileName;
                if(tmp1 != "")
                {
                    PlayLists[tb_Lists.SelectedIndex].AddFromFile(tmp1);
                    RefreshPlayList();
                }
            }
        }

        private void tmSaveList_Click(object sender, EventArgs e)
        {
            if(PlayLists[tb_Lists.SelectedIndex].FilePath == "")
            {
                SaveFileDialog dlg1 = new SaveFileDialog();
                dlg1.Filter = "Simple List File|*.spl";
                dlg1.ShowDialog();
                string temp1 = dlg1.FileName;
                if (dlg1.FileName != "")
                {
                    if (!PlayLists[tb_Lists.SelectedIndex].SaveToFile(dlg1.FileName))
                    {
                        MessageBox.Show("存储列表时发生错误。", "列表");
                    }
                }
            }
            else
            {
                if (!PlayLists[tb_Lists.SelectedIndex].SaveToFile(PlayLists[tb_Lists.SelectedIndex].FilePath))
                {
                    MessageBox.Show("存储列表时发生错误。", "列表");
                }
            }
            
            refreshInterface();
        }

        private void tmSaveAs_Click(object sender, EventArgs e)
        {
            SaveFileDialog dlg1 = new SaveFileDialog();
            dlg1.Filter = "Simple List File|*.spl";
            dlg1.ShowDialog();
            string temp1 = dlg1.FileName;
            if (dlg1.FileName != "")
            {
                if (!PlayLists[tb_Lists.SelectedIndex].SaveToFile(dlg1.FileName))
                {
                    MessageBox.Show("存储列表时发生错误。", "列表");
                }
            }
            refreshInterface();
        }

        private void MainFrom_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(PlayLists.Count != 0)
            {
                for (int i = 0; i < PlayLists.Count; i++)
                {
                    if(PlayLists[i].OperationCount != 0)
                    {
                        if (MessageBox.Show("有列表内容曾经更改。\n是否退出？", "正要退出", 
                            MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
                        {
                            e.Cancel = true;
                            break;
                        }
                    }
                }
            }

            ScuptrumTimer.Stop();
            ScuptrumTimer.Dispose();
        }

        private void tmPlaytheList_Click(object sender, EventArgs e)
        {
            playbackhead_state = playbackHeadState.ByIndex;
            tbtnPlayMode.Text = tmPlaytheList.Text;
            tbtnPlayMode.Image = Properties.Resources.single_list;
        }

        private void tmListRepeat_Click(object sender, EventArgs e)
        {
            playbackhead_state = playbackHeadState.List_Cycling;
            tbtnPlayMode.Text = tmListRepeat.Text;
            tbtnPlayMode.Image = Properties.Resources.repeat_list;
        }

        private void tmSingleRepeat_Click(object sender, EventArgs e)
        {
            playbackhead_state = playbackHeadState.Single;
            tbtnPlayMode.Text = tmSingle.Text;
            tbtnPlayMode.Image = Properties.Resources.single;
        }

        private void tmSingle_Click(object sender, EventArgs e)
        {
            playbackhead_state = playbackHeadState.Single_Cycling;
            tbtnPlayMode.Text = tmSingleRepeat.Text;
            tbtnPlayMode.Image = Properties.Resources.repeat_single;
        }

        private void btnNext_Click(object sender, EventArgs e)
        {/*
            if (playbackhead.Next == null) return;
            playbackhead = playbackhead.Next;
            if (playbackhead != null)
            {
                buttonaction = true;
                if (MainPlayer.LoadFile(playbackhead.Value.FileAddress)) { MainPlayer.Play(); }
            }//*/
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {/*
            if (playbackhead.Previous == null) return;
            playbackhead = playbackhead.Previous;
            if (playbackhead != null)
            {
                buttonaction = true;
                if (MainPlayer.LoadFile(playbackhead.Value.FileAddress)) { MainPlayer.Play(); }
            }//*/
        }

        private void tbtnPlayMode_ButtonClick(object sender, EventArgs e)
        {
            if (tbtnPlayMode.Text == tmPlaytheList.Text)
                tmListRepeat_Click(this, null);
            else if (tbtnPlayMode.Text == tmListRepeat.Text)
                tmSingleRepeat_Click(this, null);
            else if (tbtnPlayMode.Text == tmSingleRepeat.Text)
                tmSingle_Click(this, null);
            else if (tbtnPlayMode.Text == tmSingle.Text)
                tmSuffle_Click(this, null);
            else if (tbtnPlayMode.Text == tmShuffle.Text)
                tmPlaytheList_Click(this, null);

        }

        private void tmSuffle_Click(object sender, EventArgs e)
        {
            playbackhead_state = playbackHeadState.Shuffle;
            tbtnPlayMode.Text = tmShuffle.Text;
            tbtnPlayMode.Image = Properties.Resources.shuffle;
        }
    }
}

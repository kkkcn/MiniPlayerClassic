﻿using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace MiniPlayerClassic
{
    public class c_ProgressBar
    {
        //常量
        const int s_font = 10;
        const int thumb_text_offset = 0;//文本的偏移值
        const int label_text_interval = 50;
        const int label_text_movestep = 2;
        //画布
        private Bitmap buffer, canvas;
        private Bitmap waveform = null;

        public Graphics draw;
        public Graphics bitmap_enter;
        //画笔
        private Pen pen;
        //笔刷
        SolidBrush brush;
        //字体
        public Font TextFont { get { return font; } set { font = value;} }
        Font font;
        //矩形尺寸
        Rectangle rect_pb, rect_fore;
        //变量

        private StringBuilder title, subtitle;

        public int pb_value, pb_maxvalue;

        private int x_label1 = 0, x_label2 = 0;//文本的x坐标
        //尺寸
        public int height, width;
        private int s_h_middle = 0;//中间分界条的位置
        private int pb_f_long = 0;//储存进度条的实际绘图长度

        Point point_title, point_subtitle;

        Size size_title;
        Size size_subtitle;

        bool rollflage = false;//上方文本的滚动标志

        Color clBackGround = Color.LightGray,
              clFore = Color.FromArgb(150, Color.Yellow),
              clText = Color.Black,
              clFrame = Color.LightSlateGray,
              clThumb = Color.Yellow;

        public c_ProgressBar(int w,int h)
        {
            height = h - 1;
            width = w - 1;
            x_label1 = 0;
            //画布
            buffer = new Bitmap(w, h);
            canvas = new Bitmap(w, h);
            //把Graphic链接到画布以便画图
            draw = Graphics.FromImage(canvas);
            bitmap_enter = Graphics.FromImage(buffer);
            //笔刷
            pen = new Pen(clFrame);
            //画刷
            brush = new SolidBrush(clBackGround);
            //矩形
            rect_pb = new Rectangle(0, 0, width, height);
            rect_fore = new Rectangle(0, 0, 0, height);
            //字体
            font = new Font("MS YaHei UI",s_font);

            point_title = new Point(0, 0);
            point_subtitle = new Point(0, 0);

            title = new StringBuilder();
            subtitle = new StringBuilder();

            size_title = new Size(0, 0);
            size_subtitle = new Size(0, 0);
        }

        public void UpdateWaveForm(Bitmap bit)
        {
            if (bit == null) { waveform = null; return; }
            waveform = bit;
        }

        public void ChangeTitle(string s)//改变标题文字
        {
            title.Clear();
            title.Append(s);
            size_title = TextRenderer.MeasureText(title.ToString(), font);
        }

        private bool _title_too_width()
        {
            if (size_title.Width > (width - 2))
                rollflage = true;
            else
                rollflage = false;

            return rollflage;
        }

        private void subtitle_style1()
        {
            const int ms_per_hour = 1000 * 60 * 60;
            const int ms_per_minute = 1000 * 60;

            int left_hour = pb_value / ms_per_hour;
            bool show_hour = left_hour > 0;
            int all_hour = pb_maxvalue / ms_per_hour;

            int min,sec,min2,sec2;
            subtitle.Clear();
            min = pb_value % ms_per_hour / ms_per_minute;
            sec = pb_value % ms_per_minute / 1000;
            min2 = pb_maxvalue % ms_per_hour / ms_per_minute;
            sec2 = pb_maxvalue % ms_per_minute / 1000;

            if(show_hour)
                subtitle.AppendFormat("{0:D2}:{1:D2}:{2:D2}|{3:D2}:{4:D2}:{5:D2}",left_hour,min,sec,all_hour,min2,sec2);
            else
                subtitle.AppendFormat("{0:D2}:{1:D2}|{2:D2}:{3:D2}",min, sec, min2, sec2);

        }

        public void DrawBar(Graphics e)//画图函数
        {
            
            //进度条进度的规定
            if (pb_value >= pb_maxvalue) { pb_value = pb_maxvalue; }
            if (pb_value <= 0) { pb_value = 0; }
            s_h_middle = height / 2;
            //实际绘图区域的计算
            pb_f_long = (int)((float)width * ((float)pb_value / (float)pb_maxvalue));

            rect_pb.Width = width;
            rect_pb.Height = height;
            rect_fore.Width = pb_f_long;

            //获取文本的长度
            size_subtitle = TextRenderer.MeasureText(subtitle.ToString(), font);

            //设置文本的坐标
            point_title.X = 0; point_title.Y = 0;
            point_subtitle.X = 0; point_subtitle.Y = 0;
            //文本滚动的设定
            if (_title_too_width())//如果文本长度越界
                x_label1 = x_label1 - label_text_movestep;//滚动
            else 
                x_label1 = (width - size_title.Width) / 2;
            if (x_label1 <= (-size_title.Width - label_text_interval)) { x_label1 = 0; }
            //下方标签文字的位置计算
            subtitle_style1();
            byte temp1 = (byte)(size_subtitle.Width / 2 - thumb_text_offset);
            if (pb_f_long < temp1) { x_label2 = 0; }
            if (pb_f_long >= temp1) { x_label2 = pb_f_long - temp1; }
            if ((pb_f_long + temp1) >= width) { x_label2 = width - temp1*2; }

            point_title.X = x_label1;
            point_title.Y = (s_h_middle + 4 - size_title.Height) / 2;
            point_subtitle.X = x_label2;
            point_subtitle.Y = s_h_middle + (s_h_middle + 4 - size_subtitle.Height) / 2;

            brush.Color = clBackGround;
            bitmap_enter.FillRectangle(brush, rect_pb);
            if (waveform != null) bitmap_enter.DrawImage(waveform, 1, 2);
            brush.Color = clFore;
            bitmap_enter.FillRectangle(brush, rect_fore);


            pen.Color = clThumb;
            bitmap_enter.DrawLine(pen, rect_fore.Width, 0, rect_fore.Width, height);
            if (pb_f_long > 0) { bitmap_enter.DrawLine(pen, pb_f_long - 1, 0, pb_f_long - 1, height); }

            //labels
            brush.Color = clText;
            if (rollflage)
            {
                bitmap_enter.DrawString(title.ToString(), font, brush, point_title);
                point_title.X = point_title.X + size_title.Width + label_text_interval ;
                bitmap_enter.DrawString(title.ToString(), font, brush, point_title);
            } 
            else 
            {
                bitmap_enter.DrawString(title.ToString(), font, brush, point_title);
            }

            bitmap_enter.DrawString(subtitle.ToString(), font, brush, point_subtitle);


            pen.Color = clFrame;
            bitmap_enter.DrawRectangle(pen, rect_pb);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            bitmap_enter.DrawLine(pen, 0, s_h_middle, width, s_h_middle);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            //Draw on the graphic "e"
            e.DrawImage(buffer,0,0);
        }
    }
}

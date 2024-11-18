using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileHasherWPF.Model;

namespace FileHasherWPF.View
{
    public partial class MainWindow : Window
    {
        // 指定校验类型
        private Hasher.HashAlgos hashAlgo;
        // 校验文本模式
        private bool isTextMode;
        // 文本框第一次点击时清空内容
        private bool firstClick = true;

        // 获取环境换行符
        private static readonly string newline = Environment.NewLine;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            Topmost = false;
#endif
        }

        #region 文件的控制逻辑
        // 当前任务列表与文件读取进度
        List<FileHasher> hashList = new List<FileHasher>();
        long totalFileBytes = 0L;
        long currentFileBytes = 0L;
        bool isProcessing = false; // 文件大小有可能为0，不宜用于判断任务状态，虽然巧妙但容易出错

        // 处理打开多个文件的逻辑控制
        // 异步调用哈希算法，异步输出结果，避免死锁
        // 尽量使用TAP模式，避免EAP、APM模式
        // 此处void用作事件处理，其它情况异步应避免使用void，改用Task
        private void HashFiles(string[] files)
        {
            if ((files == null) || (files.Length <= 0)) return; // 判断文件，不支持目录
            FirstClear();
            // 添加到处理队列
            foreach (string f in files)
            {
                var task = new FileHasher(hashAlgo, f);
                totalFileBytes += task.FileLength;
                hashList.Add(task);
                GoHash(task);
            }
            SetGUIBusy();
        }
        // 异步处理文件
        async void GoHash(FileHasher task)
        {
#if DEBUG
            var timer = new System.Diagnostics.Stopwatch(); timer.Start();
#endif
            await task.StartHashFile();
            AddResult(task);
#if DEBUG
            timer.Stop(); textBox_Stream.AppendText("耗时：" + timer.Elapsed.TotalMilliseconds + "ms" + newline + newline);
#endif
        }
        // 输出结果
        private void AddResult(FileHasher result)
        {
            string str = result.HashResult;
            if ((str != Hasher.STATUS.FILE_ERROR) &&
                (str != Hasher.STATUS.HASH_INCOMPL))
            { textBox_HashCode.Text = str; }
            // 是否显示完整的路径
            bool isFullPath = checkBox_IsFullPath.IsChecked.Value;
            textBox_Stream.AppendText((isFullPath ? result.FilePath : result.FileName) + newline
                + str + newline + newline);
        }

        // 停止任务
        private void Button_Stop_Click(object sender, RoutedEventArgs e)
        {
            foreach (var hash in hashList)
            {
                hash.Stop();
            }
            SetGUIIdle(); //此句冗余，仅为了GUI更快响应
        }

        // 任务开始与停止时更新GUI
        // 另外：异步线程一般无法直接往UI线程写入，且应避免从UI线程读
        // 另外：使用Dispatcher来防止阻塞UI是个坏习惯
        private void SetGUIBusy()
        {
            if (!isProcessing)
            {
                isProcessing = true;
                button_Stop.IsEnabled = true;
                checkBox_IsText.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;
                UpdateProgressBar(); // 更新进度条
            }
        }
        private void SetGUIIdle()
        {
            if (isProcessing)
            {
                hashList.Clear();
                totalFileBytes = 0L;
                isProcessing = false;
                button_Stop.IsEnabled = false;
                checkBox_IsText.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
                // 滚动到最后一行
                textBox_Stream.Focus();
                textBox_Stream.CaretIndex = textBox_Stream.Text.Length; // 插入光标到末尾
                textBox_Stream.ScrollToEnd();
            }
        }
        // 异步，基于所有任务（处理队列）的进度条更新
        // 另外：数据绑定是基于事件的，每一次属性的变更传递事件到控件，而不是由控件本身来主动、定时访问。
        // 既然文件哈希的过程是确定的，那么此处从简设计，使用固定的更新频率来显示进度条
        async void UpdateProgressBar()
        {
            do
            {
                progressBar.Maximum = totalFileBytes;
                currentFileBytes = 0L;
                foreach (var hash in hashList)
                {
                    currentFileBytes += hash.CurrentBytesPosition;
                }
                progressBar.Value = currentFileBytes;
                await Task.Delay(100); // 0.1秒间隔
            }
            while (currentFileBytes < totalFileBytes);
            SetGUIIdle();
        }
        #endregion

        #region 文本框的逻辑
        // 文本框被聚焦
        private void TextBox_Stream_GotFocus(object sender, RoutedEventArgs e)
        {
            FirstClear();
        }
        // 文本框的首次清空
        private void FirstClear()
        {
            if (firstClick)
            {
                textBox_Stream.Clear();
                firstClick = false;
            }
        }
        // 文本框变动
        private void TextBox_Stream_TextChanged(object sender, TextChangedEventArgs e)
        {
            HashTextBoxText();
        }
        private void HashTextBoxText()
        {
            if (!firstClick && isTextMode)
            {
                textBox_HashCode.Text = new StringHasher(hashAlgo, textBox_Stream.Text).HashResult;
            }
        }
        #endregion

        #region 拖放的逻辑
        /*
            WPF中，TextBox组件无法直接在DragEnter事件中接受文件的拖放，而是显示为“禁止”的鼠标指针，
            这是WPF的特性，是优点，TextBox理应只接受String。
            但当不需要这种特性时，使用【PreviewDragEnter】事件响应即可完美解决，简洁优雅。
            WinForm不用考虑这个特性，但却要手动处理逻辑分层的问题。
            WinForm使用的是Direct Event，而WPF则有Bubbling Event与Tunneling Event，Preview开头的属于后者
            https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/routed-events-overview
            在研究过程中，曾用过一个丑陋的workaround：
            textBox_Stream.IsHitTestVisible = false
            这样实际上拖放时是到其下层的窗体，但也使得TextBox不能鼠标操作，变成只能看不能点的组件。
        */
        // 拖放文件到窗口
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!isTextMode && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                dropOverlay.Visibility = Visibility.Visible;
            }
        }
        private void OnFilesDrop(object sender, DragEventArgs e)
        {
            if (!isTextMode && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                dropOverlay.Visibility = Visibility.Hidden;
                HashFiles(files);
            }
        }
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            if (!isTextMode)
            {
                dropOverlay.Visibility = Visibility.Hidden;
            }
        }
        #endregion

        #region 各按钮的点击
        // 打开文件对话框
        private void Button_GetHash_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                HashFiles(openFileDialog.FileNames);
            }
        }

        // 复制到剪贴板
        private void Button_Copy_Click(object sender, RoutedEventArgs e)
        {
            string text = textBox_HashCode.Text;
            if (text != string.Empty)
                Clipboard.SetText(text);
        }
        // 从剪贴板粘贴
        private void Button_Paste_Click(object sender, RoutedEventArgs e)
        {
            string text = Clipboard.GetText();
            if (text != null && text != string.Empty)
            {
                // 去除无关符号
                text = text.Replace(" ", string.Empty);
                text = text.Replace("-", string.Empty);
                text = text.Replace(":", string.Empty);
                textBox_HashCodeForCompare.Text = text;
            }
        }

        // 对比两个文本框的内容
        private void Button_CompareHash_Click(object sender, RoutedEventArgs e)
        {
            string a = textBox_HashCode.Text;
            string b = textBox_HashCodeForCompare.Text;
            if ((a != string.Empty) && (b != string.Empty))
            {
                if (a.Equals(b, StringComparison.CurrentCultureIgnoreCase))
                { MessageBox.Show(Hasher.STATUS.HASH_EQUAL, Hasher.STATUS.SUCCESS); }
                else
                { MessageBox.Show(Hasher.STATUS.HASH_UNEQUAL, Hasher.STATUS.CAUTION, MessageBoxButton.OK, MessageBoxImage.Exclamation); }
            }
        }

        // 清空文本框
        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            // FirstClear()被包含在其它操作中，因此不再多写
            textBox_Stream.Clear();
            textBox_HashCodeForCompare.Clear();
            textBox_HashCode.Clear();
        }
        #endregion

        #region 单选框的事件逻辑
        private void RadioButton_MD5_Checked(object sender, RoutedEventArgs e)
        {
            textBox_HashCodeForCompare.MaxLength = 32;
            hashAlgo = Hasher.HashAlgos.MD5;
            HashTextBoxText();
        }
        private void RadioButton_SHA1_Checked(object sender, RoutedEventArgs e)
        {
            textBox_HashCodeForCompare.MaxLength = 40;
            hashAlgo = Hasher.HashAlgos.SHA1;
            HashTextBoxText();
        }
        private void RadioButton_SHA256_Checked(object sender, RoutedEventArgs e)
        {
            textBox_HashCodeForCompare.MaxLength = 64;
            hashAlgo = Hasher.HashAlgos.SHA256;
            HashTextBoxText();
        }
        private void RadioButton_SHA512_Checked(object sender, RoutedEventArgs e)
        {
            textBox_HashCodeForCompare.MaxLength = 128;
            hashAlgo = Hasher.HashAlgos.SHA512;
            HashTextBoxText();
        }
        #endregion

        #region 复选框的事件逻辑
        private void CheckBox_IsText_Checked(object sender, RoutedEventArgs e)
        {
            isTextMode = true;
            textBlock_Info.Text = Application.Current.MainWindow.FindResource("STR_STRINGS").ToString();
            textBox_Stream.IsReadOnly = false;
            FirstClear();
            textBox_Stream.Focus();
            HashTextBoxText();
        }
        private void CheckBox_IsText_Unchecked(object sender, RoutedEventArgs e)
        {
            isTextMode = false;
            textBlock_Info.Text = Application.Current.MainWindow.FindResource("STR_OUTPUT").ToString();
            textBox_Stream.IsReadOnly = true;
        }
        #endregion
    }
}

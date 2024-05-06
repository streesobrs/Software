using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Software.其他界面
{
    /// <summary>
    /// PageHashDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PageHashDialog : Page
    {
        public PageHashDialog(string file, string md5, string sha1,string sha256,string sha384,string sha512)
        {
            InitializeComponent();
            FileTextBox.Text = $"File: {file}";
            MD5TextBox.Text = $"MD5: {md5}";
            SHA1TextBox.Text = $"SHA1: {sha1}";
            SHA256TextBox.Text = $"SHA256: {sha256}";
            SHA384TextBox.Text = $"SHA384: {sha384}";
            SHA512TextBox.Text = $"SHA512: {sha512}";
        }
    }
}

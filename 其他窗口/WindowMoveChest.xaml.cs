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
using System.Windows.Shapes;

namespace Software.其他窗口
{
    /// <summary>
    /// PageMoveChest.xaml 的交互逻辑
    /// </summary>
    public partial class WindowMoveChest : Window
    {
        public WindowMoveChest()
        {
            InitializeComponent();
        }

        private void PageMoveChest_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                // 在这里处理向上移动的逻辑
            }
            else if (e.Key == Key.Down)
            {
                // 在这里处理向下移动的逻辑
            }
            else if (e.Key == Key.Left)
            {
                // 在这里处理向左移动的逻辑
            }
            else if (e.Key == Key.Right)
            {
                // 在这里处理向右移动的逻辑
            }
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Up);
            keyEvent.RoutedEvent = Keyboard.KeyDownEvent;
            InputManager.Current.ProcessInput(keyEvent);
        }

        private void Left_Click(object sender, RoutedEventArgs e)
        {
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Left);
            keyEvent.RoutedEvent = Keyboard.KeyDownEvent;
            InputManager.Current.ProcessInput(keyEvent);
        }

        private void Right_Click(object sender, RoutedEventArgs e)
        {
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Right);
            keyEvent.RoutedEvent = Keyboard.KeyDownEvent;
            InputManager.Current.ProcessInput(keyEvent);
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Down);
            keyEvent.RoutedEvent = Keyboard.KeyDownEvent;
            InputManager.Current.ProcessInput(keyEvent);
        }
    }
}

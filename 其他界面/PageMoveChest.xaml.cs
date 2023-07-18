using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Software.其他界面
{
    /// <summary>
    /// PageMoveChest.xaml 的交互逻辑
    /// </summary>
    public partial class PageMoveChest : Page
    {
        public PageMoveChest()
        {
            InitializeComponent();
            Keyboard.Focus(this); // 设置焦点到当前页面

            Loaded += PageMoveChest_Loaded;  // 注册Loaded事件
        }

        private void PageMoveChest_Loaded(object sender, RoutedEventArgs e)
        {
            KeyDown += PageMoveChest_KeyDown;  // 注册KeyDown事件
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

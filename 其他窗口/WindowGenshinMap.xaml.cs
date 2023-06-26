using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Software.其他窗口
{
    /// <summary>
    /// WindowGenshinMap.xaml 的交互逻辑
    /// </summary>
    public partial class WindowGenshinMap : Window
    {

        public WindowGenshinMap()
        {
            InitializeComponent();
        }

        private Point lastMousePosition;

        private void canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                lastMousePosition = e.GetPosition(canvas);
                canvas.CaptureMouse();
            }
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && canvas.IsMouseCaptured)
            {
                Point newMousePosition = e.GetPosition(canvas);
                double deltaX = newMousePosition.X - lastMousePosition.X;
                double deltaY = newMousePosition.Y - lastMousePosition.Y;
                Canvas.SetLeft(image, Canvas.GetLeft(image) + deltaX);
                Canvas.SetTop(image, Canvas.GetTop(image) + deltaY);
                lastMousePosition = newMousePosition;
            }
        }

        private void canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                canvas.ReleaseMouseCapture();
            }
        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //Point mousePos = e.GetPosition(image);
            //double zoom = e.Delta > 0 ? 1.1 : 0.9; // 放大或缩小因子
            //scaleTransform.CenterX = mousePos.X / image.ActualWidth;
            //scaleTransform.CenterY = mousePos.Y / image.ActualHeight;
            //scaleTransform.ScaleX *= zoom;
            //scaleTransform.ScaleY *= zoom;
        }

        private void Button_Teyvat(object sender, RoutedEventArgs e)
        {
            image.Source = new BitmapImage(new Uri("pack://SiteOfOrigin:,,,/resources/image/Teyvat.jpg"));
            Canvas.SetLeft(image, -8781);
            Canvas.SetTop(image, -6024);
        }

        private void Button_TheChasm(object sender, RoutedEventArgs e)
        {
            image.Source = new BitmapImage(new Uri("pack://SiteOfOrigin:,,,/resources/image/TheChasm.jpg"));
            Canvas.SetLeft(image, -284);
            Canvas.SetTop(image, -638);
        }

        private void Button_Enkanomiya(object sender, RoutedEventArgs e)
        {
            image.Source = new BitmapImage(new Uri("pack://SiteOfOrigin:,,,/resources/image/Enkanomiya.jpg"));
            Canvas.SetLeft(image, -1544);
            Canvas.SetTop(image, -1354);
        }
    }
}

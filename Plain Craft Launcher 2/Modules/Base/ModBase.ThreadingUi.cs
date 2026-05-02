using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;
using PCL.Core.Utils.OS;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace PCL
{
    public static partial class ModBase
    {
        private static int Uuid = 1;
        private static object UuidLock;

        /// <summary>
        ///     获取一个全程序内不会重复的数字（伪 Uuid）。
        /// </summary>
        public static int GetUuid()
        {
            if (UuidLock is null)
                UuidLock = new object();
            lock (UuidLock)
            {
                Uuid += 1;
                return Uuid;
            }
        }
        /// <summary>
        ///     确保在 UI 线程中执行代码。
        ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
        ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
        /// </summary>
        public static Output RunInUiWait<Output>(Func<Output> Action)
        {
            if (RunInUi()) return Action();

            return System.Windows.Application.Current.Dispatcher.Invoke(Action);
        }

        /// <summary>
        ///     确保在 UI 线程中执行代码。
        ///     如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
        ///     为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
        /// </summary>
        public static void RunInUiWait(Action Action)
        {
            if (System.Windows.Application.Current is null)
                return;
            if (RunInUi())
                Action();
            else
                System.Windows.Application.Current.Dispatcher.Invoke(Action);
        }

        /// <summary>
        ///     确保在 UI 线程中执行代码，代码按触发顺序执行。
        ///     如果当前并非 UI 线程，也不阻断当前线程的执行。
        /// </summary>
        public static void RunInUi(Action Action, bool ForceWaitUntilLoaded = false)
        {
            if (System.Windows.Application.Current is null)
                return;
            if (RunInUi())
                Action();
            else
                System.Windows.Application.Current.Dispatcher.InvokeAsync(Action,
                    ForceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
        }
        // 边距改变
        /// <summary>
        ///     相对增减控件的左边距。
        /// </summary>
        public static void DeltaLeft(FrameworkElement control, double newValue)
        {
            // 安全性检查
            DebugAssert(!double.IsNaN(newValue));
            DebugAssert(!double.IsInfinity(newValue));

            if (control is Window)
                // 窗口改变
                ((Window)control).Left += newValue;
            else
                // 根据 HorizontalAlignment 改变数值
                switch (control.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                    case HorizontalAlignment.Stretch:
                    {
                        control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                    case HorizontalAlignment.Right:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                            control.Margin.Right - newValue, control.Margin.Bottom);
                        break;
                    }

                    default:
                    {
                        DebugAssert(false);
                        break;
                    }
                }
        }

        /// <summary>
        ///     设置控件的左边距。（仅针对置左控件）
        /// </summary>
        public static void SetLeft(FrameworkElement control, double newValue)
        {
            DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
            control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
        }

        /// <summary>
        ///     相对增减控件的上边距。
        /// </summary>
        public static void DeltaTop(FrameworkElement control, double newValue)
        {
            // 安全性检查
            DebugAssert(!double.IsNaN(newValue));
            DebugAssert(!double.IsInfinity(newValue));

            if (control is Window)
                // 窗口改变
                ((Window)control).Top += newValue;
            else
                // 根据 VerticalAlignment 改变数值
                switch (control.VerticalAlignment)
                {
                    case VerticalAlignment.Top:
                    {
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                    case VerticalAlignment.Bottom:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                            control.Margin.Bottom - newValue);
                        break;
                    }

                    default:
                    {
                        DebugAssert(false);
                        break;
                    }
                }

            // If Double.IsNaN(newValue) OrElse Double.IsInfinity(newValue) Then Return '安全性检查
            // Select Case control.VerticalAlignment
            // Case VerticalAlignment.Top, VerticalAlignment.Stretch, VerticalAlignment.Center
            // control.Margin = New Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom)
            // Case VerticalAlignment.Bottom
            // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, -newValue)
            // 'control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, CType(control.Parent, Object).ActualHeight - control.ActualHeight - newValue)
            // End Select
        }

        /// <summary>
        ///     设置控件的顶边距。（仅针对置上控件）
        /// </summary>
        public static void SetTop(FrameworkElement control, double newValue)
        {
            DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
            control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
        }
        /// <summary>
        ///     将 UI 内容固定为图片并进行 Clear。
        /// </summary>
        public static void ControlFreeze(Panel UI)
        {
            UI.Background = ControlBrush(UI);
            UI.Children.Clear();
        }

        /// <summary>
        ///     将 UI 内容固定为图片并进行 Clear。
        /// </summary>
        public static void ControlFreeze(Border UI)
        {
            UI.Background = ControlBrush(UI);
            UI.Child = null;
        }
        private static readonly int UiThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        ///     当前线程是否为主线程。
        /// </summary>
        public static bool RunInUi()
        {
            return Thread.CurrentThread.ManagedThreadId == UiThreadId;
        }
    }
}

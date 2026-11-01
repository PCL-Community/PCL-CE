using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.UI.Controls;

namespace PCL;

internal static partial class TextStyles
{
    public class MinecraftFormatter
    {
        private const string RandomChars =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!@#$%^&*()_+-=[]{}|;:,.<>?/~";

        private static readonly Random Random = new();

        /// <summary>
        /// Minecraft 文本格式化代码，用于显示不同颜色的文本
        /// </summary>
        /// <param name="text">要格式化的文本</param>
        /// <param name="lab">控件</param>
        public static void SetColorfulTextLab(string text, TextBlock? lab, bool isDarkMode = true)
        {
            if (lab is null)
            {
                Common.Log("[Style] SetColorfulTextLab: lab is null");
                return;
            }

            lab.Inlines.Clear();

            var hasItalicProperty = false;
            var hasDeleteLineProperty = false;
            var hasStrickThroughProperty = false;
            var hasBlodProperty = false;
            var isRandomText = false;

            var color = isDarkMode ? "#FFFFFF" : "#888888";
            var isColorCode = false;
            var curRun = new TimerRun();
            lab.Inlines.Add(curRun);

            var randomTextRuns = new List<TimerRun>();

            foreach (var c in text)
            {
                if (c.ToString() == "§")
                {
                    isColorCode = true;
                    continue;
                }

                if (isColorCode)
                {
                    var prevColor = color;
                    if (!MotdRenderer.TryGetColorFromCode(c.ToString(), isDarkMode, out color))
                    {
                        color = prevColor;
                        switch (c)
                        {
                            case 'k':
                            case 'K':
                            {
                                isRandomText = true;
                                if (!string.IsNullOrEmpty(curRun.Text))
                                {
                                    curRun = new TimerRun();
                                    lab.Inlines.Add(curRun);
                                }

                                curRun.AutoStart = true;
                                randomTextRuns.Add(curRun);
                                break;
                            }
                            case 'l':
                            {
                                hasBlodProperty = true;
                                break;
                            }
                            case 'o':
                            {
                                hasItalicProperty = true;
                                break;
                            }
                            case 'n':
                            {
                                hasStrickThroughProperty = true;
                                break;
                            }
                            case 'm':
                            {
                                hasDeleteLineProperty = true;
                                break;
                            }
                            case 'r':
                            {
                                color = isDarkMode ? "#FFFFFF" : "#888888";
                                hasBlodProperty = false;
                                hasItalicProperty = false;
                                hasStrickThroughProperty = false;
                                hasDeleteLineProperty = false;
                                isRandomText = false;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(curRun.Text) &&
                        c.ToString() != "k" &&
                        c.ToString() != "K")
                    {
                        curRun = new TimerRun();
                        lab.Inlines.Add(curRun);
                    }

                    curRun.Foreground = new SolidColorBrush(new Common.MyColor(color!));
                    curRun.FontWeight = hasBlodProperty ? FontWeights.Bold : FontWeights.Normal;
                    curRun.FontStyle = hasItalicProperty ? FontStyles.Italic : FontStyles.Normal;
                    curRun.TextDecorations = hasStrickThroughProperty
                        ? TextDecorations.Strikethrough
                        : null;
                    curRun.TextDecorations = hasDeleteLineProperty
                        ? TextDecorations.Underline
                        : null;
                }
                else if (isRandomText)
                {
                    curRun.Text += RandomChars[Random.Next(RandomChars.Length)].ToString();
                }
                else
                {
                    curRun.Text += c.ToString();
                }

                if (isColorCode)
                    isColorCode = false;
            }

            if (randomTextRuns.Count <= 0) return;
            foreach (var run in randomTextRuns)
            {
                run.UpdateInterval = TimeSpan.FromMilliseconds(20d);
                run.TimerTick += sender =>
                {
                    if (string.IsNullOrEmpty(sender.Text)) return;
                    var sb = new StringBuilder();
                    for (int i = 0, loopTo = sender.Text.Length - 1; i <= loopTo; i++)
                        sb.Append(RandomChars[Random.Next(RandomChars.Length)]);
                    sender.Text = sb.ToString();
                };
            }
        }
    }
}

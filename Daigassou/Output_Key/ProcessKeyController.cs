using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Daigassou.Properties;
using Newtonsoft.Json;

namespace DaigassouDX.Controller
{
    public class ProcessKeyController
    {
        private const int WmKeydown = 0x0100;
        private const int WmKeyup = 0x0101;

        public static readonly Dictionary<int, int> _initkeymap = new Dictionary<int, int>
        {
            {48, 73},
            {49, 56},
            {50, 79},
            {51, 57},
            {52, 80},
            {53, 219},
            {54, 48},
            {55, 221},
            {56, 189},
            {57, 220},
            {58, 187},
            {59, 222},
            {60, 81},
            {61, 50},
            {62, 87},
            {63, 51},
            {64, 69},
            {65, 82},
            {66, 53},
            {67, 84},
            {68, 54},
            {69, 89},
            {70, 55},
            {71, 85},
            {72, 90},
            {73, 83},
            {74, 88},
            {75, 68},
            {76, 67},
            {77, 86},
            {78, 71},
            {79, 66},
            {80, 72},
            {81, 78},
            {82, 74},
            {83, 77},
            {84, 191},
            {108,49},
            {109,52},
            {110,188},
            {111,190},
            {112,65},
            
            

        };
        public static  Dictionary<int, int> _keymap = new Dictionary<int, int>
        {
            {48, 73},
            {49, 56},
            {50, 79},
            {51, 57},
            {52, 80},
            {53, 219},
            {54, 48},
            {55, 221},
            {56, 189},
            {57, 220},
            {58, 187},
            {59, 222},
            {60, 81},
            {61, 50},
            {62, 87},
            {63, 51},
            {64, 69},
            {65, 82},
            {66, 53},
            {67, 84},
            {68, 54},
            {69, 89},
            {70, 55},
            {71, 85},
            {72, 90},
            {73, 83},
            {74, 88},
            {75, 68},
            {76, 67},
            {77, 86},
            {78, 71},
            {79, 66},
            {80, 72},
            {81, 78},
            {82, 74},
            {83, 77},
            {84, 191}
        };
        public static char GetKeyChar(Keys k)
        {
            var nonVirtualKey = MapVirtualKey((uint)k, 2);
            var mappedChar = Convert.ToChar(nonVirtualKey);
            return mappedChar;
        }
        public static void SaveKeyConfig(Dictionary<int, int> keyBinding)
        {
            _keymap = keyBinding;
            var jsonText= JsonConvert.SerializeObject(keyBinding);
            Settings.Default.KeyBinding = jsonText;
            Settings.Default.Save();
        }
        public static void LoadKeyConfig()
        {
            if (Settings.Default.KeyBinding!="")
            {
                try
                {
                    var jsonObject = (Dictionary<int, int>)JsonConvert.DeserializeObject(Settings.Default.KeyBinding, typeof(Dictionary<int, int>));
                    _keymap = jsonObject;
				}
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    _keymap = _initkeymap;
                }
                
            }
            else
            {
                _keymap = _initkeymap;
            }
        }

        public static void ResetKeyConfig()
        {
            _keymap = _initkeymap;
            SaveKeyConfig(_keymap);
        }
        private readonly object keyLock = new object();

        public Process Process;

        [DllImport("user32.dll", EntryPoint = "PostMessage", CallingConvention = CallingConvention.Winapi)]
        public static extern bool PostMessage(IntPtr hwnd, uint msg, uint wParam, uint lParam);

        [DllImport("User32.dll")]
        public static extern void keybd_event(Keys bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private static ProcessKeyController controller;
        public static ProcessKeyController GetInstance()
        {
            if (controller==null)
            {
                controller = new ProcessKeyController();
            }

            return controller;
        }
        internal void ReleaseAllKey()
        {
            foreach (var item in _keymap) ReleaseKeyBoardByPitch(item.Key);
        }
		/// <summary>
		/// 88键映射到37键范围，得到48~84的数字，出错则为0。在使用isUsingGuitarKey的时候还可能输出108~113的数字
		/// </summary>
		/// <param name="pitch"></param>
		/// <returns></returns>
		public static int PitchExchange(int pitch)
		{
			switch (Settings.Default.Use88)
			{
				default:
				case 0: break;
				case 1:
					{
						var v = pitch - 24;
						if (v < 0) return 0;
						else if (v < 24) pitch = 48 + v % 12;//将C1~B2映射到C3~B3。
						else if (v > 60) pitch = 72 + v % 12;//C6~C8映射到C5~C6。
						/*
						1	2	3	4	5	6	7	8
						3	3	3	4	5	6	6	6 ←这样映射，可以避免音高差太多的问题
						*/
						break;
					}
				case 2:
					{
						if (pitch < 24) return 0;
						else if (pitch < 48) pitch += 24;//将C1~B2映射到C3~B4好了
						else if (pitch <= 84 + 24) pitch -= 24;//84是C6……C#6~C8映射到C#4~C6，超过C8的不知道是什么键
						else return 0;
						/*
						1	2	3	4	5	6	7	8
						3	4	3	4	5	6	5	6 ←这样映射，可以避免同时弹2、3跨八度跟没跨一样的问题。
						*/
						break;
					}
					/*
	接入88键的键盘，为37键之外的按键重新映射到37键上。然后要注意当重新映射后的键也在被按下时，应当只响应一次。
		映射如果只是把低音区域映射到C3~C4，只是把高音区域映射到C5~C6可能不太好。37键的范围是C3~C6。将C1~B2映射到C3~B4好了，C1以下的A0、bB0、B0丢掉，谁会弹这些阿。最高音则是C8，C#6~C8映射到C#4~C6好了？
						*/
			}
			if(Settings.Default.isUsingGuitarKey)
				if (pitch < 108 || pitch > 113) return 0;
			return pitch;
		}
		/// <summary>
		/// 接受输入48~84，超范围则进行一次映射。
		/// </summary>
		/// <param name="pitch"></param>
        public void PressKeyBoardByPitch(int pitch)
        {
			if (pitch < 48 || pitch > 84)
			{
				pitch = PitchExchange(pitch);
				if (pitch == 0) return;// 0;
			}
            KeyDownBoardByKey((Keys) _keymap[pitch]);
			//return pitch;
        }
		/// <summary>
		/// 接受输入48~84，超范围则进行一次映射。
		/// </summary>
		/// <param name="pitch"></param>
		public void ReleaseKeyBoardByPitch(int pitch)
        {
			//if ((pitch >= 48 && pitch <= 84) || (pitch >= 108 && pitch <= 113 && Settings.Default.isUsingGuitarKey))
			if (pitch < 48 || pitch > 84)
			{
				pitch = PitchExchange(pitch);
				if (pitch == 0) return;
			}
			KeyUpBoardByKey((Keys) _keymap[pitch]);
			//return pitch;
        }

        public void KeyDownBoardByKey(Keys viKeys)
        {
            if (Settings.Default.isBackgroundKey)
            {
                if (Process != null && Process.MainWindowHandle != IntPtr.Zero)
                    PostMessage(Process.MainWindowHandle, WmKeydown, (uint) viKeys, 0);
            }
            else
            {
                keybd_event(viKeys, (byte) MapVirtualKey((uint) viKeys, 0U), 0, 0);
            }
        }

        public void KeyUpBoardByKey(Keys viKeys)
        {
            if (Settings.Default.isBackgroundKey)
            {
                if (Process != null && Process.MainWindowHandle != IntPtr.Zero)
                    PostMessage(Process.MainWindowHandle, WmKeyup, (uint) viKeys, 0);
            }
            else
            {
                keybd_event(viKeys, (byte) MapVirtualKey((uint) viKeys, 0U), 2, 0);
            }
        }

        /// <summary>
        ///     Press a key after [startOffset]ms and keep [duration]ms to release.
        /// </summary>
        /// <param name="viKeys">Key need to be press</param>
        /// <param name="startOffset">Start delay, ms</param>
        /// <param name="duration">duration </param>
        public void KeyPressBoardByKey(Keys viKeys, uint startOffset, uint duration)
        {
            if (Process != null && Process.MainWindowHandle != IntPtr.Zero)
            {
                var t = Task.Run(delegate
                {
                    Debug.WriteLine("start waiting"+DateTime.Now.ToFileTimeUtc());
                    Thread.Sleep((int)startOffset);
                    PostMessage(Process.MainWindowHandle, WmKeydown, (uint) viKeys, 0);
                    Debug.WriteLine("Keypress" + DateTime.Now.ToFileTimeUtc());
                    Thread.Sleep((int)duration);
                    PostMessage(Process.MainWindowHandle, WmKeyup, (uint) viKeys, 0);
                    Debug.WriteLine("Keyup" + DateTime.Now.ToFileTimeUtc());
                });
            }
        }
    }
}
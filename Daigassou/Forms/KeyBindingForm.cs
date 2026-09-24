using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Daigassou.Properties;
using DaigassouDX.Controller;
using Sunny.UI;

namespace Daigassou.Forms
{
	public partial class KeyBindingForm : UIForm
	{
		private readonly UITextBox[] keyBoxes;
		private Dictionary<int, int> keyConfig;

		public KeyBindingForm()
		{
			InitializeComponent();
			UIStyles.InitColorful(Color.FromArgb(255, 141, 155), Color.White);
			keyBoxes = new UITextBox[42]
			{
				uiTextBox1,
				uiTextBox2,
				uiTextBox3,
				uiTextBox4,
				uiTextBox5,
				uiTextBox6,
				uiTextBox7,
				uiTextBox8,
				uiTextBox9,
				uiTextBox10,
				uiTextBox11,
				uiTextBox12,
				uiTextBox13,
				uiTextBox14,
				uiTextBox15,
				uiTextBox16,
				uiTextBox17,
				uiTextBox18,
				uiTextBox19,
				uiTextBox20,
				uiTextBox21,
				uiTextBox22,
				uiTextBox23,
				uiTextBox24,
				uiTextBox25,
				uiTextBox26,
				uiTextBox27,
				uiTextBox28,
				uiTextBox29,
				uiTextBox30,
				uiTextBox31,
				uiTextBox32,
				uiTextBox33,
				uiTextBox34,
				uiTextBox35,
				uiTextBox36,
				uiTextBox37,
				uiTextBox38,
				uiTextBox39,
				uiTextBox40,
				uiTextBox41,
				uiTextBox42
			};
			loadKeyconfig();
		}

		private void textBox_KeyDown(object sender, KeyEventArgs e)
		{
			var tmpBox = (UITextBox)sender;
			var index = Array.IndexOf(keyBoxes, tmpBox);
			if (tmpBox == null) throw new ArgumentNullException(nameof(tmpBox));
			tmpBox.Text = ProcessKeyController.GetKeyChar(e.KeyCode).ToString();

			if (index < 37)
				keyConfig[index + 48] = (int)e.KeyCode;
			else
				keyConfig[index - 37 + 108] = (int)e.KeyCode;
			if (index + 1 < keyBoxes.Length) keyBoxes[index + 1].Focus();//焦点跳到下一个。
		}

		private void TextBox_KeyPress(object sender, KeyPressEventArgs e)
		{
		}

		private void loadKeyconfig()
		{
			ProcessKeyController.LoadKeyConfig();
			foreach (var keypair in ProcessKeyController._keymap)
				if (keypair.Key < 85)
					keyBoxes[keypair.Key - 48].Text = ProcessKeyController.GetKeyChar((Keys)keypair.Value).ToString();
				else
					keyBoxes[keypair.Key - 108 + 37].Text =
						ProcessKeyController.GetKeyChar((Keys)keypair.Value).ToString();

			keyConfig = ProcessKeyController._keymap;
		}

		private void btnConfirm_Click(object sender, EventArgs e)
		{
			ProcessKeyController.SaveKeyConfig(keyConfig);
			Close();
		}

		private void btnReset_Click(object sender, EventArgs e)
		{
			ProcessKeyController.ResetKeyConfig();
			foreach (var uiTextBox in keyBoxes) uiTextBox.Text = "";
			loadKeyconfig();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void KeyBindingForm_FormClosing(object sender, FormClosingEventArgs e)
		{
		}

		//配置文件解密部分来源： https://github.com/EmperorArthur/FFXIV_Settings/tree/master
		/// <summary>
		/// 对字节数组进行逐字节 XOR 解密
		/// </summary>
		static byte[] Xor(byte[] data, byte xorValue)
		{
			byte[] output = new byte[data.Length];
			for (int i = 0; i < data.Length; i++)
			{
				output[i] = (byte)(data[i] ^ xorValue);
			}
			return output;
		}
		static string ReadSection(BinaryReader br, byte xorValue = 0x73)
		{
			// 读取并解密 3 字节头部
			byte[] headerBytes = br.ReadBytes(3);
			byte[] header = Xor(headerBytes, xorValue);
			var type = header[0];
			// 小端无符号 16 位整数 (Python 的 'H' 格式)
			ushort size = BitConverter.ToUInt16(header, 1);
			
			byte[] dataBytes = br.ReadBytes(size-1);// 读取数据并解密，去掉最后一个 null 字节
			byte[] decrypted = Xor(dataBytes, xorValue);
			return Encoding.UTF8.GetString(decrypted);
		}
		class KeyBind
		{
			/// <summary>
			/// 快捷键功能描述
			/// </summary>
			public string Function;
			/// <summary>
			/// 主快捷键
			/// </summary>
			public Keys Pri;
			/// <summary>
			/// 主快捷键的控制按键（Ctrl、Shift、Alt之类） 但是目前不知道对照关系
			/// </summary>
			public Keys Pri_m;
			/// <summary>
			/// 次快捷键
			/// </summary>
			public Keys Sub;
			/// <summary>
			/// 次快捷键的控制按键（Ctrl、Shift、Alt之类） 但是目前不知道对照关系
			/// </summary>
			public Keys Sub_m;
			Keys ModifyKey(int i)
			{
				Keys k = 0;
				return k;
			}
			public KeyBind(string func,string basestring)
			{
				Function = func;
				var bss = basestring.Split(',');
				var keys1 = bss[0].Split('.');
				var keys2 = bss[1].Split('.');
				Pri = (Keys)int.Parse(keys1[0],System.Globalization.NumberStyles.HexNumber);
				Pri_m = ModifyKey(int.Parse(keys1[1], System.Globalization.NumberStyles.HexNumber));
				Sub = (Keys)int.Parse(keys2[0], System.Globalization.NumberStyles.HexNumber);
				Sub_m=ModifyKey(int.Parse(keys2[1], System.Globalization.NumberStyles.HexNumber));
			}
			public override string ToString()
			{
				return $"{Function} {Pri}";
			}
		}
		void GetKeyBind(string KeyBindFilePath)
		{
			List<KeyBind> KeyBinds = new List<KeyBind>();
			using (FileStream fs = new FileStream(KeyBindFilePath, FileMode.Open))
			using (BinaryReader br = new BinaryReader(fs))
			{
				br.ReadInt32();
				var maxsize = br.ReadInt32();
				var thissize = br.ReadInt32();//12
				if (thissize > maxsize)
				{
					throw new Exception("不合理");
				}
				br.ReadInt32();//保留
				br.ReadByte();//头部结束符
				int cursor = 0;
				var now = fs.Position;
				while (fs.Position < thissize + now)
				{
					try
					{
						var r = ReadSection(br);
						var r2 = ReadSection(br);
						KeyBinds.Add(new KeyBind(r, r2));
						/*
						keybind={}
						keybind['command'] =    read_section(in_file,0x73)['data']
						key_string =            read_section(in_file,0x73)['data']
						keys = key_string.split(',')
						keybind['key1'] = keys[0].split('.')[0]
						keybind['key1_modifier'] = keys[0].split('.')[1]
						keybind['key2'] = keys[1].split('.')[0]
						keybind['key2_modifier'] = keys[1].split('.')[1]
						*/
					}
					catch (Exception ex)
					{
						//break;
					}
				}
			}
			string[] OP = { "C", "C_SHARP", "D", "D_SHARP", "E", "F", "F_SHARP", "G", "G_SHARP", "A", "A_SHARP", "B" };
			KeyBinds = KeyBinds.FindAll(x => x.Function.StartsWith("PERFORMANCE_MODE"));
			int Setindex = 0;
			for (int i = 3; i < 6; i++)
			{
				for (int j = 0; j < OP.Length; j++)
				{
					var op = OP[j].Insert(1, i.ToString());
					var k = KeyBinds.Find(x => x.Function.EndsWith($"_{op}"));
					if (k != null)
					{
						keyBoxes[Setindex].Text=k.Pri.ToString();
						keyConfig[Setindex + 48] = (int)k.Pri;
						Setindex++;
					}
				}
			}
			//另外的几个吉他的对应哪几个Function啊……
			
			//if (index < 37)
			//	keyConfig[index + 48] = (int)e.KeyCode;
			//else
			//	keyConfig[index - 37 + 108] = (int)e.KeyCode;

		}

		private void SelectDAT_Click(object sender, EventArgs e)
		{
			var processList = Utils.Utils.GetProcesses();
			string dir = "";//候选KEYBIND.DAT路径
			if (processList.Count == 0)
			{
				MessageBox.Show(this, "未运行游戏，请手动选择配置文件");
			}
			else 
			{
				dir=Path.GetPathRoot(processList[0].MainModule.FileName);
				dir = Path.Combine(dir, "My Games\\FINAL FANTASY XIV - A Realm Reborn\\");
				DateTime dt=new DateTime(2000,1,1); 

				var dirs=Directory.GetDirectories(dir);
				foreach(var d in dirs)
				{
					if (Path.GetDirectoryName(d).StartsWith("FFXIV_CHR"))
					{
						string s= Path.Combine(d, "KEYBIND.DAT");
						FileInfo fi = new FileInfo(s);
						if (dt < fi.LastWriteTime)
						{
							dt = fi.LastWriteTime;
							dir = s;
						}
					}
				}
			}
			//F:\最终幻想XIV\game\My Games\FINAL FANTASY XIV - A Realm Reborn\FFXIV_CHR0040000BE021520C
			OpenFileDialog of = new OpenFileDialog()
			{
				Filter = "|*.DAT",
				Title= "请选择KEYBIND.DAT文件。常见于最终幻想XIV\\game\\My Games\\FINAL FANTASY XIV - A Realm Reborn\\FFXIV_CHRxxx\\"
			};
			if (!string.IsNullOrEmpty(dir))
			{
				of.InitialDirectory = Path.GetPathRoot(dir);
				of.FileName = dir;
			}
			of.ShowDialog();
			if (of.FileName.Length == 0) return;
			GetKeyBind(of.FileName);
			MessageBox.Show(this, "完成");
		}
	}
}
/*
可以读取游戏目录下的KEYBIND.DAT来获取按键配置

PERFORMANCE_MODE_OCTAVE_HIGHER 10.00 B3.00
PERFORMANCE_MODE_OCTAVE_LOWER 11.00 B0.00
PERFORMANCE_MODE_SHARP 00.00 B2.00
PERFORMANCE_MODE_FLAT 00.00 AF.00
PERFORMANCE_MODE_C4 51.00 A9.00
PERFORMANCE_MODE_C4_SHARP 32.00 00.00
PERFORMANCE_MODE_D4 57.00 A7.00
PERFORMANCE_MODE_D4_SHARP 33.00 00.00
PERFORMANCE_MODE_E4 45.00 AA.00
PERFORMANCE_MODE_F4 52.00 A8.00
PERFORMANCE_MODE_F4_SHARP 35.00 00.00
PERFORMANCE_MODE_G4 54.00 AD.00
PERFORMANCE_MODE_G4_SHARP 36.00 00.00
PERFORMANCE_MODE_A4 59.00 AB.00
PERFORMANCE_MODE_A4_SHARP 37.00 00.00
PERFORMANCE_MODE_B4 55.00 AE.00
PERFORMANCE_MODE_C5 49.00 AC.00
PERFORMANCE_MODE_TONE_NEXT 00.00 00.00
PERFORMANCE_MODE_TONE_PREV 00.00 00.00
PERFORMANCE_MODE_TONE0 00.00 00.00
PERFORMANCE_MODE_TONE1 00.00 00.00
PERFORMANCE_MODE_TONE2 00.00 00.00
PERFORMANCE_MODE_TONE3 00.00 00.00
PERFORMANCE_MODE_TONE4 00.00 00.00
PERFORMANCE_MODE_EX_OCTAVE_HIGHER 10.00 B3.00
PERFORMANCE_MODE_EX_OCTAVE_LOWER 11.00 B0.00
PERFORMANCE_MODE_EX_SHARP 00.00 B2.00
PERFORMANCE_MODE_EX_FLAT 00.00 AF.00
PERFORMANCE_MODE_EX_C3 51.00 00.00
PERFORMANCE_MODE_EX_C3_SHARP 32.00 00.00
PERFORMANCE_MODE_EX_D3 57.00 00.00
PERFORMANCE_MODE_EX_D3_SHARP 33.00 00.00
PERFORMANCE_MODE_EX_E3 45.00 00.00
PERFORMANCE_MODE_EX_F3 52.00 00.00
PERFORMANCE_MODE_EX_F3_SHARP 35.00 00.00
PERFORMANCE_MODE_EX_G3 54.00 00.00
PERFORMANCE_MODE_EX_G3_SHARP 36.00 00.00
PERFORMANCE_MODE_EX_A3 59.00 00.00
PERFORMANCE_MODE_EX_A3_SHARP 37.00 00.00
PERFORMANCE_MODE_EX_B3 55.00 00.00
PERFORMANCE_MODE_EX_C4 49.00 A9.00
PERFORMANCE_MODE_EX_C4_SHARP 39.00 00.00
PERFORMANCE_MODE_EX_D4 4F.00 A7.00
PERFORMANCE_MODE_EX_D4_SHARP 30.00 00.00
PERFORMANCE_MODE_EX_E4 50.00 AA.00
PERFORMANCE_MODE_EX_F4 89.00 A8.00
PERFORMANCE_MODE_EX_F4_SHARP 82.00 00.00
PERFORMANCE_MODE_EX_G4 8B.00 AD.00
PERFORMANCE_MODE_EX_G4_SHARP 41.00 00.00
PERFORMANCE_MODE_EX_A4 5A.00 AB.00
PERFORMANCE_MODE_EX_A4_SHARP 53.00 00.00
PERFORMANCE_MODE_EX_B4 58.00 AE.00
PERFORMANCE_MODE_EX_C5 43.00 AC.00
PERFORMANCE_MODE_EX_C5_SHARP 46.00 00.00
PERFORMANCE_MODE_EX_D5 56.00 00.00
PERFORMANCE_MODE_EX_D5_SHARP 47.00 00.00
PERFORMANCE_MODE_EX_E5 42.00 00.00
PERFORMANCE_MODE_EX_F5 4E.00 00.00
PERFORMANCE_MODE_EX_F5_SHARP 4A.00 00.00
PERFORMANCE_MODE_EX_G5 4D.00 00.00
PERFORMANCE_MODE_EX_G5_SHARP 4B.00 00.00
PERFORMANCE_MODE_EX_A5 83.00 00.00
PERFORMANCE_MODE_EX_A5_SHARP 4C.00 00.00
PERFORMANCE_MODE_EX_B5 85.00 00.00
PERFORMANCE_MODE_EX_C6 86.00 00.00
PERFORMANCE_MODE_EX_TONE_NEXT 00.00 00.00
PERFORMANCE_MODE_EX_TONE_PREV 00.00 00.00
PERFORMANCE_MODE_EX_TONE0 00.00 00.00
PERFORMANCE_MODE_EX_TONE1 00.00 00.00
PERFORMANCE_MODE_EX_TONE2 00.00 00.00
PERFORMANCE_MODE_EX_TONE3 00.00 00.00
PERFORMANCE_MODE_EX_TONE4 00.00 00.00
*/
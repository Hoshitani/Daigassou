using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Daigassou.Input_Midi;
using Sunny.UI;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Daigassou
{
    public partial class AboutForm : Form
    {
        
        public AboutForm()
        {
            InitializeComponent();
        }
		CancellationTokenSource cts;
		private async void AboutForm_Load(object sender, EventArgs e)
        {
            lblVersion.Text = "Ver " + Assembly.GetExecutingAssembly().GetName().Version;
			cts = new CancellationTokenSource();
			//Do(cts.Token);
		}
		//async Task Do(CancellationToken token)
		//{
		//	int i = 80;
		//	while (!token.IsCancellationRequested&&i>=0)
		//	{
		//		try
		//		{
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().PressKeyBoardByPitch(48);
		//			await Task.Delay(i, token);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().PressKeyBoardByPitch(55);
		//			await Task.Delay(i, token);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().PressKeyBoardByPitch(60);
		//			await Task.Delay(i, token);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().PressKeyBoardByPitch(67);
		//			await Task.Delay(i, token);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().PressKeyBoardByPitch(72);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(48);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(55);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(67);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(60);
		//			DaigassouDX.Controller.ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(72);
		//			await Task.Delay(500, token);
		//			Debug.WriteLine($"在{i}ms下的琶音");
		//			i -= 2;
		//			await Task.Delay(500, token);

		//		}
		//		catch (Exception)
		//		{
		//			break;
		//		}
		//	}

		//}
		async Task Do(CancellationToken token)
		{
			var ins = DaigassouDX.Controller.ProcessKeyController.GetInstance();
			int gap = 60;
			while (!token.IsCancellationRequested && gap >= 0)
			{
				try
				{
					for (int i = 0; i < 20; i++)
					{
						ins.PressKeyBoardByPitch(48 + i);
						await Task.Delay(gap, token);
						ins.ReleaseKeyBoardByPitch(48 + i);
					}
					Debug.WriteLine($"在{gap}ms下");
					await Task.Delay(1000, token);
					gap--;
				}
				catch (Exception)
				{
					break;
				}
			}
		}
		private void AboutForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			cts.Cancel();
		}
		//测试1秒钟的按键次数
	}
}
//检查是不是真的50ms一个按键，如果是的话，就要在开始演奏前设定bpm了。如果同时按键数量*50超过一拍的时长，就要拉长随后输入的等待间隔。在连续数秒无输入的时候重置。
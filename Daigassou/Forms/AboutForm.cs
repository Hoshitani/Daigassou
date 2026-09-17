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
			//cts = new CancellationTokenSource();
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
		private void AboutForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			//cts.Cancel();
		}
	}
}

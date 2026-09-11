using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Daigassou.Properties;
using Daigassou.Utils;
using DaigassouDX.Controller;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;
using Sunny.UI;
using System.Diagnostics;
using System.Linq;

namespace Daigassou.Input_Midi
{
    public static class KeyboardUtilities
    {
        private static InputDevice wetMidiKeyboard;
        private static readonly object NoteOnlock = new object();
        private static readonly object NoteOfflock = new object();
        private static readonly object noteLock = new object();
        //private static readonly Queue<NoteEvent> noteQueue = new Queue<NoteEvent>();
        private static CancellationTokenSource cts = new CancellationTokenSource();
        public static int offset;
        public static event EventHandler<MidiEventReceivedEventArgs> eventHandler;
		// public static KeyController kc;
		/// <summary>
		/// 哪些键被按下了。要存88键，对于1351的长按，松开第一个1的时候不能取消第二个1
		/// </summary>
		static bool[] Pressing = new bool[88];
		/// <summary>
		/// 37键 每个实际按下的键都有一个映射。需要记录每个键是被Pressing的哪个触发的。
		/// </summary>
		static int[] Map = new int[37];
        public static DevicesConnector virtualConnector(int[] indexs)
        {
            IOutputDevice[] outputDevices = new IOutputDevice[indexs.Length];
            
            for (int i = 0; i < indexs.Length; i++)
            {
                var tmpDevice = OutputDevice.GetByIndex(indexs[i]);
                outputDevices[i] = tmpDevice;

            }

            var dv = new DevicesConnector(wetMidiKeyboard, outputDevices);

            return dv;
        }
        public static bool Connect(int Index)
        {
            wetMidiKeyboard = InputDevice.GetByIndex(Index);
            {
                try
                {
                    wetMidiKeyboard.EventReceived += MidiKeyboard_EventReceived;
                    wetMidiKeyboard.SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff;
                    wetMidiKeyboard.StartEventsListening();
                    cts = new CancellationTokenSource();

                    Task.Run(() =>
                    {
                        
                        NoteProcess(cts.Token);
                    }, cts.Token);
                    return true;
                }
                catch (Exception e)
                {
                    UIMessageTip.ShowError($"连接错误 \r\n {e.Message}");
                    return false;
                }
            }
        }
		public static IdleTriggeredBatcher<NoteEvent> batcher =new IdleTriggeredBatcher<NoteEvent>();
		/// <summary>
		/// 接收到键盘事件
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private static void MidiKeyboard_EventReceived(object sender, MidiEventReceivedEventArgs e)
        {
          
            eventHandler?.Invoke(sender, e);

            switch (e.Event)
            {
                case NoteOnEvent @event:
					Debug.WriteLine($"按下\t{@event.NoteNumber}\t{@event.Velocity}");
					if (@event.Velocity < Settings.Default.IgnoreVol) break;//响度低的忽略
					batcher.OnEvent(@event);
					//noteQueue.Enqueue(@event);
                    break;
                case NoteOffEvent @event:
					Debug.WriteLine($"抬起\t{@event.NoteNumber}");//有时候会漏信息，估计和midi数据传输的线有关？
					batcher.OnEvent(@event);//不敢加入队列就是怕同一个音再被演奏一次的时候，出现一个键被按下，又要按一遍的情况。要不……每次按下之前先松开一遍？
					//KeyOff[@event.NoteNumber - 24] = true;
					//noteQueue.Enqueue(@event);
					break;
            }
        }

        public static void Disconnect()
        {
            if (wetMidiKeyboard == null) return;
            if (wetMidiKeyboard.IsListeningForEvents)
                try
                {
                    wetMidiKeyboard.StopEventsListening();
                    wetMidiKeyboard.EventReceived -= MidiKeyboard_EventReceived;
                    wetMidiKeyboard.Dispose();
                    cts.Cancel();
                }
                catch (Exception e)
                {
                    UIMessageTip.ShowError($"断开错误 \r\n {e.Message}");
                    
                }

            eventHandler = null;
        }

        public static List<string> GetKeyboardList()
        {
            var ret = new List<string>();
            var index = 0;
            foreach (var device in InputDevice.GetAll())
            {
                ret.Add(device.Name+"|"+index++);
            }

            return ret;
        }
        public static List<string> GetOutputDeviceList()
        {
            var ret = new List<string>();
            var index = 0;
            foreach (var device in OutputDevice.GetAll())
            {
                ret.Add(device.Name + "|" + index++);
            }

            return ret;
        }
		/// <summary>
		/// 异步方法执行了一个同步函数啊这是。
		/// </summary>
		/// <param name="token"></param>
        public static void NoteProcess(CancellationToken token)
        {
            var minimumInterval = (int) Settings.Default.MinEventMs;
            while (!token.IsCancellationRequested)
            {
                //NoteEvent nextKey;
                lock (noteLock)
                {
					//if (noteQueue.Count <= 0)
					//{
					//    Thread.Sleep(1); continue;
					//}//等待被挪到batcher.Dequeue()里了。
					Queue<NoteEvent> queue = new Queue<NoteEvent>();
					var batch = batcher.Dequeue().OrderBy(x=>x.NoteNumber).ToList();
					var Release = batch.FindAll(x => x.Velocity == 0);
					batch = batch.FindAll(x => x.Velocity > 0).ToList();
					var Left=batch.FindAll(x => x.NoteNumber>batch[0].NoteNumber&& x.NoteNumber <= batch[0].NoteNumber + 12);
					var Right = batch.FindAll(x => x.NoteNumber > batch[0].NoteNumber + 12);
					if(batch.Count>0)queue.Enqueue(batch.First());//先弹最低音
					foreach (var r in Right) queue.Enqueue(r);//再弹右手
					foreach (var l in Left) queue.Enqueue(l);//再弹其余的左手
					//如果同时演奏高音区域和低音区域的话，高音区域很可能是主旋律。应该优先弹高音区域再弹低音区域？
					//问题出在这个“很可能”。如何判定？
					//其实可以猜测手的位置，最高音-8度和最低音+8度就是一只手（因人而异，要加个选项吗😓）能跨越的最大范围，从而区分出来左右手是哪些键。优先弹最低音，高音区，其余的低音？
					foreach (var l in Release) queue.Enqueue(l);//再处理放开

					bool[] array = new bool[37];//还要注意一个问题：如果queue里有两个键映射到了37键的同一个键，那么应当去掉一个。
					//Debug.WriteLine("\r\nBatch————");
					foreach (var nextKey in queue)
					{
						int number = nextKey.NoteNumber;
						Debug.Write("\r\n"+number);
						var npitch = ProcessKeyController.PitchExchange(number + offset);//实际要按的键是npitch,npitch-48在0~37
						if (npitch == 0) continue;
						switch (nextKey)
						{
							case NoteOnEvent keyon:
								if (npitch - 48 < 37)//npitch是102……
								{
									if (array[npitch - 48])
									{
										Debug.Write("\t同音\r\n");
										continue;//在本次打包的队列里已经存在了，不应再按下一次。
									}
									array[npitch - 48] = true;
								}
								NoteOn(number,npitch);
								Thread.Sleep(minimumInterval);
								//有时候会收到莫名其妙的信号，明明没有按那个键。
								break;
							case NoteOffEvent keyoff:
								NoteOff(number + offset, npitch);
								//Thread.Sleep(minimumInterval);//等按键抬起会影响之后的输入？不等了吧。
								break;
						}
						Debug.WriteLine("");
					}
					//MidiKeyboard_EventReceived需要以一个间隔（通过设置调整）来打包数据，将打包期间的按键拆解成琶音逐个输出。处理完一个包后就立刻处理下一个包，这也许可以解决琶音被拆散到两个包的情况。
					//会导致响应不及时吗？应该以第一个输入作为打包起点，一段时间没有输入就停止打包，回到等待第一个输入的状态。
				}
            }
        }

        public static void NoteOn(int OriginNote,int Note_37)
        {
			lock (NoteOnlock)
			{
				if (Note_37 <48||Note_37>84) return;
				if (Map[Note_37 - 48] > 0)//对应的37键被按下了，先抬起它
				{
					Debug.Write("\t已被按下");
					NoteOff(Map[Note_37 - 48],Note_37);//拿着当时按下的键 值去抬就好了。
					Thread.Sleep((int)Settings.Default.MinEventMs);
				}
				//if (Pressing[pitch - 24]) NoteOff(pitch - 24);//如果这个键被按下，就先抬起再按。 1351
				ProcessKeyController.GetInstance().PressKeyBoardByPitch(Note_37);
				if(OriginNote+offset-24<Pressing.Length)Pressing[OriginNote+offset - 24] = true;
				Map[Note_37 - 48] = OriginNote;
			}
        }

        public static void NoteOff(int OriginNote, int Note_37 = 0)
        {
			lock (NoteOfflock)
			{
				if (OriginNote + offset - 24 < Pressing.Length)
				{
					if (!Pressing[OriginNote - 24])
					{
						Debug.Write("\t已被松开");//可能是按键力度太低被忽略
						return;//如果已经没在按了就不处理，免得程序感到疑惑，为什么一个按键抬起了两次。
					}
					if (OriginNote + offset - 24 < Pressing.Length)
					{
						Debug.Write("\t松开");
						Pressing[OriginNote - 24] = false;
					}
				}
				ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(Note_37);
				if (Note_37 >= 48) Map[Note_37 - 48] = 0;
			}
		}
    }
}
//同时按下两个映射到同一个键的键时，需要取消其中一个
//按顺序长按两个映射到同一个键的键时，按下第二个键要取消第一个键，这样在松开第一个键的时候，真实的键才不会被松开。
//每个键都要有一个指针，指向88键的某个键。当它被按下，且自己指向某个键，就应该取消那个键！
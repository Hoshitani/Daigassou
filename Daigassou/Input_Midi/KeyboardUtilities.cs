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
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using System.Threading.Channels;
namespace Daigassou.Input_Midi
{
    public static class KeyboardUtilities
    {
        private static InputDevice wetMidiKeyboard;
        private static CancellationTokenSource cts = new CancellationTokenSource();
        public static int offset;
        public static event EventHandler<MidiEventReceivedEventArgs> eventHandler;
		/// <summary>
		/// 被按下的物理键。要存88键，对于1351的长按，松开第一个1的时候不能取消第二个1
		/// </summary>
		static bool[] Pressing = new bool[88];
		/// <summary>
		/// 37键 每个实际按下的键都有一个映射。需要记录每个键是被Pressing的哪个触发的，数值0~87。
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
					Task.Run(async () =>
					{
						await QueueDeal(cts.Token);
					});
					return true;
                }
                catch (Exception e)
                {
                    UIMessageTip.ShowError($"连接错误 \r\n {e.Message}");
                    return false;
                }
            }
        }
		//其实也没必要打包，检查一个键和下一个键的时间间距，判断要不要一块儿处理就好了。用Queue存起来还是比较好的
		class NEvent
		{
			///// <summary>
			///// 距离上一个事件的时长
			///// </summary>
			//public long gap;
			/// <summary>
			/// 收到事件的时间
			/// </summary>
			public DateTime dt;
			/// <summary>
			/// 正常范围是-3~84。加过offset减过24 C1是0，C3是24
			/// </summary>
			public int number;
			/// <summary>
			/// 映射到37键的值，范围0~36。负值需要跳过
			/// </summary>
			public int MapNumber
			{
				get
				{
					var pitch = number;
					if (pitch < 0) return -1;
					switch (Settings.Default.Use88)
					{
						default:
						case 0: break;
						case 1:
							{
								if (pitch < 24) pitch = pitch % 12;//将C1~B2映射到C3~B3。
								else if (pitch > 60) pitch = 24 + pitch % 12;//C6~C8映射到C5~C6。
								else pitch -= 24;
								/*
								1	2	3	4	5	6	7	8
								3	3	3	4	5	6	6	6 ←这样映射，可以避免音高差太多的问题
								*/
								break;
							}
						case 2:
							{
								if (pitch < 24) ;// pitch += 24;//将C1~B2映射到C3~B4好了 C3就是0，所以不用处理
								else if (pitch <= 84) pitch -= 48;//84是C6……C#6~C8映射到C#4~C6，超过C8的不知道是什么键
								else return -1;
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
					if (Settings.Default.isUsingGuitarKey)
						if (pitch < 108-24 || pitch > 113-24) return -1;
					return pitch;
				}
			}
			readonly static string[] symbols = ["C", "C#", "D", "bE", "E", "F", "F#", "G", "G#", "A", "bB", "B"];
			public string Symbol
			{
				get
				{
					var n = number;
					var h = 0;
					if (n > 0) h = (int)Math.Ceiling((n+1) / 12f);
					else n = n + symbols.Length;
					return $"{symbols[n%12]}{h}";
				}
			}
			public int Velocity;
			public bool Off { get { return Velocity == 0; } }
			MidiEventType eventtype;
			public byte Channel;
			public NEvent(NoteEvent e)
			{
				dt = DateTime.Now;
				eventtype = e.EventType;
				number = e.NoteNumber+offset-24;
				Velocity = e.Velocity;
				Channel = e.Channel;//一般都是保持为0的……但如果想利用上88键做多乐器的话就不一样了，这需要认真的给88键绑按键，不是映射能解决的问题。
			}
			public override string ToString()
			{
				return $"{Symbol} {Velocity}";
			}
		}
		static Channel<NEvent> Queue = Channel.CreateUnbounded<NEvent>();
		static ConcurrentQueue<NEvent> Dealed = new ConcurrentQueue<NEvent>();
		/*
			using (var inputDevice = InputDevice.GetByName("Input MIDI device"))
			{
				var recording = new Recording(Melanchall.DryWetMidi.Interaction.TempoMap.Default, inputDevice);

				inputDevice.StartEventsListening();
				recording.Start();
				// ...

				recording.Stop();

				var recordedFile = recording.ToFile();
				recording.Dispose();
				recordedFile.Write("Recorded data.mid");
			}
			//可以把Midi信号存成文件
		*/
		static async Task QueueDeal(CancellationToken ct)
		{
			/*
			设计思路：
			一个后台线程，不断尝试处理队列中的消息。队列消息由MidiKeyboard_EventReceived按先后顺序放入。
			
			输入的数据如果立刻输出，不等待任何延迟是最理想的。但人的输入是有时间间隔的，输出也需要有至少10ms的间隔。（关于这个间隔可以测一下，看看最低支持多少间隔。如果稳定60fps，一帧是16ms）

			程序运行开始，如果同时输入5个按键，程序会用数毫秒【处理时间D1】从队列中取出这几个键（因为他们的gap小于一个值），累加他们的gap，得到这些键实际键入的总时间间距【输入时间I】。根据左右手判定制定输出顺序，去除重复按键【处理时间D2】，进行输出，耗时<=Settings.Default.MinChordMs*数量【输出时间O】。原程序在输出后需要等待一个固定时间Settings.Default.MinEventMs【等待时间W】，然后进入下一轮判断。感觉如果要按实际松键时间来处理的话，W就没必要等了，因为人很难在一个短间隔里同时完成按键和松键吧。有的程序会忽略过短的输入（待验证）
			如果在D1+I+D2+O的时间中，有了新的输入，那么输入需要等到这一轮处理结束才能开始处理。
			
			对于80bpm的十六分音符，间隔是0.125s。只要程序处理时间低于这个就不会造成延迟。
			如果输入间隔刚好卡在Settings.Default.DispartMs之下，又连续有了很多很多按键，可能会导致处理这一瞬间用的时间太长。这一批之后虽然下一个按键的gap不长，但处理到它的时候可能已经过了很长时间了（之前多个按键的gap和输出时间）。为了让听感不那么差，这里应该多等待一会儿吧？也就是把后边的整体输入都往后推，对外展示出相对比较平稳的节奏。
			

			*/
			List<NEvent> Package = new List<NEvent>();
			FileStream fs=new FileStream("D:/MidiLog.txt", FileMode.Append);
			try
			{
				while (await Queue.Reader.WaitToReadAsync(ct))
				{
					if (!Queue.Reader.TryRead(out NEvent a))
						continue;//到底什么情况会上边Read到了下边没Read到啊
					Package.Add(a);
					DateTime now = DateTime.Now;
					CancellationTokenSource cts = new CancellationTokenSource();//一秒后失效 在一秒之内的输出都排队进去
					while (!ct.IsCancellationRequested)
					{
						if (Queue.Reader.TryPeek(out NEvent next))
						{
							cts.CancelAfter(Settings.Default.DispartMs);
							if ((next.dt - a.dt).TotalMilliseconds < Settings.Default.DispartMs)//连续的小于都拼起来？
							{
								Package.Add(next);
								Queue.Reader.TryRead(out _);
							}
							else break;
						}
						else
						{
							//if (!cts.IsCancellationRequested)
							//{
							//	try
							//	{
							//		await Task.Delay(Settings.Default.DispartMs / 10,cts.Token);//等10ms看看……这会导致总等待时间不固定？建议改为打包间隔

							//	}
							//	catch (TaskCanceledException)
							//	{
							//		break;
							//	}
							//}
							//else 
								break;//没有下一个东西了
						}
					}
					//try
					//{
					//	await Task.Delay(-1, cts.Token);//这样能稳定Settings.Default.DispartMs后输出
					//}
					//catch (TaskCanceledException)
					//{
					//}//不太好，感觉很卡
					List<Task> lt = new List<Task>();
					NoteProcess(Package, ct);//不等了
					StringBuilder sb = new StringBuilder();
					foreach (var n in Package)
					{
						Dealed.Enqueue(n);
						sb.Append(n.ToString() + "\t");
					}
					Debug.WriteLine(sb.ToString());
					var s = Encoding.UTF8.GetBytes($"{now:HH:mm:ss:fff}\t{sb}\r\n");
					lt.Add(fs.WriteAsync(s, 0, s.Length));
					//什么时候输出？输出后再等延时？另一个线程输出？输出后延时会导致处理变慢吧。
					Package.Clear();
					await Task.WhenAll(lt);//将写入文件和操作按键同时处理
				}

			}
			catch (Exception)
			{


			}
			fs.Close();
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
					//Debug.WriteLine($"按下\t{@event.NoteNumber}\t{@event.Velocity}");
					Queue.Writer.WriteAsync(new NEvent(@event));
					//batcher.OnEvent(@event);
					//noteQueue.Enqueue(@event);
                    break;
                case NoteOffEvent @event:
					//Debug.WriteLine($"抬起\t{@event.NoteNumber}");//有时候会漏信息，估计和midi数据传输的线有关？
					//batcher.OnEvent(@event);//不敢加入队列就是怕同一个音再被演奏一次的时候，出现一个键被按下，又要按一遍的情况。要不……每次按下之前先松开一遍？
					Queue.Writer.WriteAsync(new NEvent(@event));
					//Queue.Enqueue(new NEvent(@event));
					//KeyOff[@event.NoteNumber - 24] = true;
					//noteQueue.Enqueue(@event);
					break;
				default:
					break;//还有什么事件类型呢？
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
		//DateTime //输出需要的时间。当处理上一批输入时，下一批输入到了，应当立刻终止上一批的输入？保证时效性？
		/// <summary>
		/// 用于输出按键，判断力度，去重（映射到同一个键的）
		/// </summary>
		static async Task NoteProcess(List<NEvent> Package, CancellationToken token)
		{
			if (Package.Count == 0) return;
			int minimumInterval = (int)Settings.Default.MinEventMs;
			var batch = Package.OrderBy(x => x.number).ToList();
			var Release = batch.FindAll(x => x.Velocity == 0);
			batch = batch.FindAll(x => x.Velocity > Settings.Default.IgnoreVol).ToList();

			var Left = batch.FindAll(x => x.number > batch[0].number && x.number <= batch[0].number + 12);//从最低音开始的一个八度
			var Right = batch.FindAll(x => x.number > batch[0].number + 12);//超过最低音一个八度的音
			List<NEvent> queue = new List<NEvent>();
			//if (batch.Count > 0) queue.Add(batch.First());//先弹最低音
			//queue.AddRange(Right);//再弹右手
			//queue.AddRange(Left);//再弹其余的左手
			queue.AddRange(batch);//还有必要分解吗？感觉从左到右也挺好的，低音先行
			queue.AddRange(Release);//再处理放开

			bool[] array = new bool[37];//还要注意一个问题：如果queue里有两个键映射到了37键的同一个键，那么应当去掉其中一个。用这个数组记录本次要按下那些键进行去重

			foreach (var nextKey in queue)
			{
				if (nextKey.MapNumber < 0) continue;
				if (nextKey.Velocity > 0)
				{
					if (nextKey.MapNumber < 37)//去掉吉他的那些
					{
						if (array[nextKey.MapNumber]) continue;//在本次打包的队列里已经存在了，不应再按下一次。
						array[nextKey.MapNumber] = true;
					}

					if (Map[nextKey.MapNumber] > 0)//对应的37键在这一批之前就被按下了，先抬起它
					{
						Debug.WriteLine($"{nextKey.MapNumber}已被{Map[nextKey.MapNumber]}按下，先抬起");
						NoteOff(nextKey.MapNumber);//要抬这个键，自己用Map找对应哪个物理键
						await Task.Delay(minimumInterval);//不能是0
					}
					ProcessKeyController.GetInstance().PressKeyBoardByPitch(nextKey.MapNumber + 48);
					var t = nextKey.number;
					if (t >= 0 && t < Pressing.Length) Pressing[t + 3] = true;//因为是-3开头的。
					Map[nextKey.MapNumber] = nextKey.number + 3;
					await Task.Delay(minimumInterval);
				}
				else
				{
					NoteOff(nextKey.MapNumber);
				}
			}
		}

		public static void NoteOff(int Note_37 = 0)
		{
			int OriginNote = Map[Note_37];
			if (OriginNote < Pressing.Length&&OriginNote>=0)
			{
				if (!Pressing[OriginNote])
				{
					Debug.WriteLine($"{OriginNote}已被松开");//可能是按键力度太低被忽略
					return;//如果已经没在按了就不处理，免得程序感到疑惑，为什么一个按键抬起了两次。
				}
				Debug.WriteLine($"{OriginNote}松开"); 
				Pressing[OriginNote] = false;
				Map[Note_37] = 0;
			}
			ProcessKeyController.GetInstance().ReleaseKeyBoardByPitch(Note_37+48);
		}
    }
}
//同时按下两个映射到同一个键的键时，需要取消其中一个
//按顺序长按两个映射到同一个键的键时，按下第二个键要取消第一个键，这样在松开第一个键的时候，真实的键才不会被松开。
//每个键都要有一个指针，指向88键的某个键。当它被按下，且自己指向某个键，就应该取消那个键！
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Daigassou
{
	//Kimi的代码：
	public class IdleTriggeredBatcher<T>
	{
		private static TimeSpan _windowSize;
		private readonly ConcurrentQueue<T[]> _outputQueue = new();
		private static Timer _windowTimer;

		// 状态机：0=空闲等待首个事件, 1=窗口已激活正在累积
		private int _isWindowActive = 0;

		// 当前窗口的累积缓冲区（仅窗口线程访问，无需锁）
		private readonly ConcurrentQueue<T> _currentBuffer = new();
		//private readonly object _bufferLock = new(); // 保护 List 的读写交错
		//把锁拆了，会提高响应效果吗？

		// 可选：处理完成回调或手动取出
		public event Action<T[]>? BatchReady;

		public IdleTriggeredBatcher()
		{
			_windowSize = new TimeSpan(0, 0, 0, 0, 50);
			// 定时器初始不启动（Infinite），由首个事件启动
			_windowTimer = new Timer(OnWindowTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		}
		public void SetWindowSize(int windowSize)
		{
			_windowSize = new TimeSpan(0, 0, 0, 0, windowSize);
			_windowTimer.Dispose();
			_windowTimer = new Timer(OnWindowTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		}

		/// <summary>
		/// 由事件线程调用，线程安全
		/// </summary>
		public void OnEvent(T data)
		{
			// 尝试从空闲状态切换到激活状态（原子操作）
			if (Interlocked.CompareExchange(ref _isWindowActive, 1, 0) == 0)
			{
				// 成功抢到"首个事件"资格，启动窗口定时器
				// 注意：此时立即启动定时器，确保窗口从首个事件开始计时
				_windowTimer.Change(_windowSize, Timeout.InfiniteTimeSpan);
			}

			// 累积数据（窗口激活期间，定时器回调与事件回调可能并发，需加锁保护 List）
			//lock (_bufferLock)
			{
				_currentBuffer.Enqueue(data);
			}
		}

		private void OnWindowTick(object? state)
		{
			T[] batch;
			bool hasData;

			//lock (_bufferLock)
			{
				hasData = _currentBuffer.Count > 0;
				if (hasData)
				{
					batch = _currentBuffer.ToArray();
					for(int i=0;i<batch.Length;i++) _currentBuffer.TryDequeue(out _);
				}
				else
				{
					batch = Array.Empty<T>();
				}
			}

			if (hasData)
			{
				// 有数据：打包入队，立即开启下一个窗口
				_outputQueue.Enqueue(batch);
				BatchReady?.Invoke(batch); // 或者直接由消费线程处理 _outputQueue

				// 连续窗口：立即重置定时器，开始下一个时间窗口
				_windowTimer.Change(_windowSize, Timeout.InfiniteTimeSpan);
			}
			else
			{
				// 窗口完全空白：回到空闲状态，等待下一个首个事件
				// 注意：必须确保在设置状态前，没有新事件刚好写入 buffer 却被遗漏
				// 由于 OnEvent 先检查状态再写入，而这里先清空 buffer 再改状态，存在竞态：
				// 如果 OnEvent 在 hasData=false 之后、状态改 0 之前写入，会丢失

				// 更安全的做法：先改状态，再检查一次 buffer（双检查）
				Interlocked.Exchange(ref _isWindowActive, 0);

				//lock (_bufferLock)
				{
					// 如果改状态后恰好有新数据进来（OnEvent 已把状态改为 1 并写入数据）
					// 但由于 OnEvent 是先改状态再写数据，这里改回 0 后，新事件会再次启动定时器
					// 所以只需确保没有"状态为 0 但 buffer 有数据"的情况
					if (_currentBuffer.Count > 0)
					{
						// 极端情况：改状态的瞬间有新事件进入且已写入数据
						// 此时该事件会启动定时器，我们只需把状态改回 1
						Interlocked.Exchange(ref _isWindowActive, 1);
						_windowTimer.Change(_windowSize, Timeout.InfiniteTimeSpan);
					}
				}
			}
		}

		/// <summary>
		/// 供消费线程调用：阻塞直到有数据包
		/// </summary>
		public T[] Dequeue(CancellationToken ct = default)
		{
			while (!ct.IsCancellationRequested)
			{
				if (_outputQueue.TryDequeue(out var batch))
					return batch;
				Thread.Sleep(10); // 或使用 ManualResetEventSlim 优化
			}
			throw new OperationCanceledException();
		}

		public void Dispose() => _windowTimer.Dispose();
	}
}
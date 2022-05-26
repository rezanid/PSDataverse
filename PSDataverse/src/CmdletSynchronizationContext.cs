using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DataverseModule
{
    public class CmdletSynchronizationContext : SynchronizationContext, IDisposable
    {
        private BlockingCollection<MarshalItem> workItems;
        private static CmdletSynchronizationContext currentAsyncCmdletContext;

        private CmdletSynchronizationContext(int boundedCapacity)
        {
            this.workItems = new BlockingCollection<MarshalItem>(boundedCapacity);
        }

        public static void Async(Func<Task> handler, int boundedCapacity)
        {
            var previousContext = SynchronizationContext.Current;

            try
            {
                using (var synchronizationContext = new CmdletSynchronizationContext(boundedCapacity))
                {
                    SetSynchronizationContext(synchronizationContext);
                    currentAsyncCmdletContext = synchronizationContext;

                    var task = handler();
                    if (task == null)
                    {
                        return;
                    }

                    var waitable = task.ContinueWith(t => synchronizationContext.Complete(), scheduler: TaskScheduler.Default);

                    synchronizationContext.ProcessQueue();

                    waitable.GetAwaiter().GetResult();
                }
            }
            finally
            {
                SetSynchronizationContext(previousContext);
                currentAsyncCmdletContext = previousContext as CmdletSynchronizationContext;
            }
        }

        internal static void PostItem(MarshalItem item)
        {
            currentAsyncCmdletContext.Post(item);
        }

        public void Dispose()
        {
            if (this.workItems != null)
            {
                this.workItems.Dispose();
                this.workItems = null;
            }
        }

        private void EnsureNotDisposed()
        {
            if (this.workItems == null)
            {
                throw new ObjectDisposedException(nameof(CmdletSynchronizationContext));
            }
        }

        private void Complete()
        {
            EnsureNotDisposed();

            this.workItems.CompleteAdding();
        }

        private void ProcessQueue()
        {
            MarshalItem workItem;
            while (this.workItems.TryTake(out workItem, Timeout.Infinite))
            {
                workItem.Invoke();
            }
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Post(new MarshalItemAction<object>(s => callback(s), state));
        }

        private void Post(MarshalItem item)
        {
            EnsureNotDisposed();

            this.workItems.Add(item);
        }
    }

    #region items
    internal abstract class MarshalItem
    {
        internal abstract void Invoke();
    }

    abstract class MarshalItemFuncBase<TRet> : MarshalItem
    {
        private TRet retVal;
        private readonly Task<TRet> retValTask;

        protected MarshalItemFuncBase()
        {
            this.retValTask = new Task<TRet>(() => this.retVal);
        }

        internal sealed override void Invoke()
        {
            this.retVal = this.InvokeFunc();
            this.retValTask.Start();
        }

        internal TRet WaitForResult()
        {
            this.retValTask.Wait();
            return this.retValTask.Result;
        }

        internal abstract TRet InvokeFunc();
    }
    class MarshalItemAction<T> : MarshalItem
    {
        private readonly Action<T> action;
        private readonly T arg1;

        internal MarshalItemAction(Action<T> action, T arg1)
        {
            this.action = action;
            this.arg1 = arg1;
        }

        internal override void Invoke()
        {
            this.action(this.arg1);
        }
    }
    class MarshalItemAction<T1, T2> : MarshalItem
    {
        private readonly Action<T1, T2> action;
        private readonly T1 arg1;
        private readonly T2 arg2;

        internal MarshalItemAction(Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            this.action = action;
            this.arg1 = arg1;
            this.arg2 = arg2;
        }

        internal override void Invoke()
        {
            this.action(this.arg1, this.arg2);
        }
    }
    class MarshalItemFunc<TRet> : MarshalItemFuncBase<TRet>
    {
        private readonly Func<TRet> func;

        internal MarshalItemFunc(Func<TRet> func)
        {
            this.func = func;
        }

        internal override TRet InvokeFunc()
        {
            return this.func();
        }
    }
    class MarshalItemFunc<T1, TRet> : MarshalItemFuncBase<TRet>
    {
        private readonly Func<T1, TRet> func;
        private readonly T1 arg1;

        internal MarshalItemFunc(Func<T1, TRet> func, T1 arg1)
        {
            this.func = func;
            this.arg1 = arg1;
        }

        internal override TRet InvokeFunc()
        {
            return this.func(this.arg1);
        }
    }
    class MarshalItemFunc<T1, T2, TRet> : MarshalItemFuncBase<TRet>
    {
        private readonly Func<T1, T2, TRet> func;
        private readonly T1 arg1;
        private readonly T2 arg2;

        internal MarshalItemFunc(Func<T1, T2, TRet> func, T1 arg1, T2 arg2)
        {
            this.func = func;
            this.arg1 = arg1;
            this.arg2 = arg2;
        }

        internal override TRet InvokeFunc()
        {
            return this.func(this.arg1, this.arg2);
        }
    }
    class MarshalItemFunc<T1, T2, T3, TRet> : MarshalItemFuncBase<TRet>
    {
        private readonly Func<T1, T2, T3, TRet> func;
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;

        internal MarshalItemFunc(Func<T1, T2, T3, TRet> func, T1 arg1, T2 arg2, T3 arg3)
        {
            this.func = func;
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
        }

        internal override TRet InvokeFunc()
        {
            return this.func(this.arg1, this.arg2, this.arg3);
        }
    }
    class MarshalItemFuncOut<T1, T2, T3, TRet, TOut> : MarshalItem
    {
        private readonly FuncOut func;
        private readonly T1 arg1;
        private readonly T2 arg2;
        private readonly T3 arg3;

        internal delegate TRet FuncOut(T1 t1, T2 t2, T3 t3, out TOut tout);

        private TRet retVal;
        private TOut outVal;
        private readonly Task<TRet> retValTask;

        internal MarshalItemFuncOut(FuncOut func, T1 arg1, T2 arg2, T3 arg3)
        {
            this.func = func;
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.retValTask = new Task<TRet>(() => this.retVal);
        }

        internal override void Invoke()
        {
            this.retVal = this.func(this.arg1, this.arg2, this.arg3, out this.outVal);
            this.retValTask.Start();
        }

        internal TRet WaitForResult(out TOut val)
        {
            this.retValTask.Wait();
            val = this.outVal;
            return this.retValTask.Result;
        }
    }
    class MarshalItemFuncRef<T1, T2, TRet, TRef1, TRef2> : MarshalItem
    {
        internal delegate TRet FuncRef(T1 t1, T2 t2, ref TRef1 tref1, ref TRef2 tref2);

        private readonly Task<TRet> retValTask;
        private readonly FuncRef func;
        private readonly T1 arg1;
        private readonly T2 arg2;
        private TRef1 arg3;
        private TRef2 arg4;
        private TRet retVal;

        internal MarshalItemFuncRef(FuncRef func, T1 arg1, T2 arg2, TRef1 arg3, TRef2 arg4)
        {
            this.func = func;
            this.arg1 = arg1;
            this.arg2 = arg2;
            this.arg3 = arg3;
            this.arg4 = arg4;
            this.retValTask = new Task<TRet>(() => this.retVal);
        }

        internal override void Invoke()
        {
            this.retVal = this.func(this.arg1, this.arg2, ref this.arg3, ref this.arg4);
            this.retValTask.Start();
        }

        // ReSharper disable RedundantAssignment
        internal TRet WaitForResult(ref TRef1 ref1, ref TRef2 ref2)
        {
            this.retValTask.Wait();
            ref1 = this.arg3;
            ref2 = this.arg4;
            return this.retValTask.Result;
        }
        // ReSharper restore RedundantAssignment
    }
    #endregion items
}

﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PowerArgs.Cli.Physics
{
    public class ObjectiveOptions
    {
        public Func<Task> Main { get; set; }
        public List<Action<Objective>> Watches { get; set; }
        public Action<AggregateException> OnException { get; set; }
        public Action<string> OnAbort { get; set; }
        public Event<string> Log { get; set; }
    }

    public class AbortObjectiveException : Exception
    {
        public AbortObjectiveException(string message) : base(message) { }
    }

    public class Objective : IDelayProvider
    {
        private InterjectableProcess Focus { get; set; }
        private ObjectiveOptions options;
        public Objective(ObjectiveOptions options)
        {
            this.options = options;
        }

        public Task DelayAsync(double ms) => Focus.DelayAsync(ms);
        public Task DelayAsync(TimeSpan timeout) => DelayAsync(timeout.TotalMilliseconds);
        public Task DelayAsync(Event ev, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) => Focus.DelayAsync(ev, timeout, evalFrequency);
        public Task DelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) => Focus.DelayAsync(condition, timeout, evalFrequency);
        public Task<bool> TryDelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) => Focus.TryDelayAsync(condition, timeout, evalFrequency);
        public Task YieldAsync() => Focus.YieldAsync();
        public void Interject(Func<Task> work) => Focus.Interject(work);

        public void Evaluate()
        {
            GetFocused();
            if (Focus != null && Focus.HasStarted == false)
            {
                Focus.Start();
            }

            if (options.Watches != null)
            {
                foreach (var watcher in options.Watches)
                {
                    watcher.Invoke(this);
                }
            }
        }

        private void GetFocused()
        {
            if (Focus == null)
            {
                Focus = new InterjectableProcess(options.Main);
                options.Log?.Fire("Starting main objective");
            }
            else if(Focus.IsComplete == false)
            {
                // already focused
            }
            else if(Focus.Exception != null && Focus.Exception.InnerExceptions.Count == 1 && Focus.Exception.InnerException is AbortObjectiveException)
            {
                options.OnAbort(Focus.Exception.InnerException.Message);
                options.Log?.Fire("Refocusing after "+ Focus.Exception.InnerException.Message);
                Focus = new InterjectableProcess(options.Main);
            }
            else if (Focus.Exception != null && options.OnException != null)
            {
                options.OnException.Invoke(Focus.Exception);
                options.Log?.Fire("Refocusing after handled exception");
                Focus = new InterjectableProcess(options.Main);
            }
            else if(Focus.Exception != null)
            {
                throw new AggregateException(Focus.Exception);
            }
            else
            {
                Focus = new InterjectableProcess(options.Main);
                options.Log?.Fire("Objective met, refocusing on main objective");
            }
        }
 
        private class InterjectableProcess
        {
            private Task task;
            private Func<Task> mainProcess;
            private Queue<Func<Task>> interjections = new Queue<Func<Task>>();

            public bool HasStarted => task != null;
            public bool IsComplete => task == null ? false : task.IsCompleted;
            public AggregateException Exception => task == null ? null : task.Exception;

            public InterjectableProcess(Func<Task> mainProcess)
            {
                this.mainProcess = mainProcess;
            }

            public void Interject(Func<Task> work)
            {
                lock (interjections)
                {
                    interjections.Enqueue(work);
                }
            }

            public void Start()
            {
                task = mainProcess();
            }

            public async Task YieldAsync()
            {
                await DrainInterjections();
                await Time.CurrentTime.YieldAsync();
            }

            public async Task DelayAsync(double ms)
            {
                await DrainInterjections();
                await Time.CurrentTime.DelayAsync(ms);
            }

            public async Task DelayAsync(Event ev, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
            {
                var fired = false;

                ev.SubscribeOnce(() =>
                {
                    fired = true;
                });

                await DelayAsync(() => fired, timeout, evalFrequency);
            }

            public async Task DelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
            {
                if (await TryDelayAsync(condition, timeout, evalFrequency) == false)
                {
                    throw new TimeoutException("Timed out awaiting delay condition");
                }
            }

            public async Task<bool> TryDelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
            {
                var startTime = Time.CurrentTime.Now;
                var governor = evalFrequency.HasValue ? new RateGovernor(evalFrequency.Value, lastFireTime: startTime) : null;
                while (true)
                {
                    if (governor != null && governor.ShouldFire(Time.CurrentTime.Now) == false)
                    {
                        await DrainInterjections();
                        await Task.Yield();
                    }
                    else if (condition())
                    {
                        return true;
                    }
                    else if (timeout.HasValue && Time.CurrentTime.Now - startTime >= timeout.Value)
                    {
                        return false;
                    }
                    else
                    {
                        await DrainInterjections();
                        await Task.Yield();
                    }
                }
            }

            private async Task DrainInterjections()
            {
                List<Func<Task>> interjectionsBeforeYield = null;
                lock (interjections)
                {
                    while (interjections.Count > 0)
                    {
                        interjectionsBeforeYield = interjectionsBeforeYield ?? new List<Func<Task>>();
                        interjectionsBeforeYield.Add(interjections.Dequeue());
                    }
                }

                if (interjectionsBeforeYield != null)
                {
                    foreach (var interjection in interjectionsBeforeYield)
                    {
                        await interjection();
                    }
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TraceRouting.Trace
{
    public abstract class ObservableRoute
    {
        protected readonly List<IObserver<HopEvent>> Observers;

        protected ObservableRoute()
        {
            Observers = new List<IObserver<HopEvent>>();
        }

        public IDisposable Subscribe(IObserver<HopEvent> observer)
        {
            if (!Observers.Contains(observer))
                Observers.Add(observer);
            return new HopEventUnsubscriber(Observers, observer);
        }

        public virtual void EmitEvent(HopEvent @event)
        {
            foreach (var observer in Observers)
            {
                // Create new tasks for the events otherwise they could block execution
                Task.Run(() => observer.OnNext(@event));
            }
        }

        public virtual void EmitCompleted()
        {
            foreach (var observer in Observers)
            {
                // Create new tasks for the events otherwise they could block execution
                Task.Run(() => observer.OnCompleted());
            }
        }

        public virtual void EmitError(Exception exception)
        {
            foreach (var observer in Observers)
            {
                // Create new tasks for the events otherwise they could block execution
                Task.Run(() => observer.OnError(exception));
            }
        }

        public abstract void Execute();

        private class HopEventUnsubscriber : IDisposable
        {
            private readonly List<IObserver<HopEvent>> _observers;
            private readonly IObserver<HopEvent> _observer;

            public HopEventUnsubscriber(List<IObserver<HopEvent>> observers,
                IObserver<HopEvent> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }
    }
}
